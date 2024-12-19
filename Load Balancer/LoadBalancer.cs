using System.Collections.Concurrent;
using LoadBalancer.Interfaces;
using LoadBalancer.Logger;
using LoadBalancer.RequestCache;

namespace LoadBalancer
{
    public class LoadBalancer : ILoadBalancer
    {
        private readonly ConcurrentDictionary<IServer, bool> _servers = new();
        private readonly ILoadBalancingStrategy _loadBalancingStrategy;
        private readonly HealthCheckService _healthCheckService;
        private readonly RequestHandler _requestHandler;
        private readonly IAutoScaler? _autoScaler;
        private readonly CachedRequestsManager? _cachedRequestsManager;
        private System.Timers.Timer? _healthCheckTimer;
        private readonly int _minHealthThreshold;

        public LoadBalancer(
        ILoadBalancingStrategy? loadBalancingStrategy,
        HttpClient? httpClient,
        bool enabledAutoScaling = false,
        AutoScalingConfig? autoScalingConfig = null,
        TimeSpan healthCheckInterval = default,
        int minHealthThreshold = 70,
        CachedRequestsManager? cachedRequestsManager = null
    )
        {
            _loadBalancingStrategy = loadBalancingStrategy ?? new RoundRobinStrategy();
            _healthCheckService = new HealthCheckService( httpClient ?? new HttpClient() );
            _requestHandler = new RequestHandler( httpClient ?? new HttpClient() );
            _cachedRequestsManager = cachedRequestsManager ?? new CachedRequestsManager( HandleRequestAsync );
            _minHealthThreshold = minHealthThreshold;

            if( enabledAutoScaling )
            {
                _autoScaler = new AutoScaler(
                    autoScalingConfig ?? AutoScalingConfig.Factory(),
                    () => new Server(
                            Server.DefaultAddress,
                            PortUtils.FindAvailablePort(),
                            CircuitBreakerConfig.Factory(),
                            80,
                            100 ),
                    server => _servers.TryAdd( server, true ),
                    RemoveServer,
                    () => _servers.Count );
            }
        }

        public void Initialize( TimeSpan healthCheckInterval )
        {
            _autoScaler?.Initialize();
            Log.Info( $"Load Balancer initialized with auto-scaling enabled. Current active servers: {_servers.Count}" );

            _healthCheckTimer = new System.Timers.Timer
            {
                Interval = healthCheckInterval == default ? TimeSpan.FromSeconds( 10 ).TotalMilliseconds : healthCheckInterval.TotalMilliseconds,
                AutoReset = true
            };

            Log.Debug( $"Will perform health checks for the servers every {_healthCheckTimer.Interval / 1000} seconds" );
            _healthCheckTimer.Elapsed += async ( _, _ ) => await PerformHealthChecksAsync();
            _healthCheckTimer.Start();

            StartHealthDecrementTask( timeInSec: 10, decreaseAmount: 3 );
        }

        public async Task<bool> HandleRequestAsync( HttpRequestMessage request )
        {

            if( _servers.Keys.Any( s => s.ServerHealth > _minHealthThreshold ) && _cachedRequestsManager.HasCachedRequests())
            {
                await ProcessCachedRequestsImmediately();
            }

            _autoScaler?.TrackRequest( DateTime.UtcNow );
            var success = await SendRequestAsync();
            if( success )
            {
                Log.Info( "Response: OK" );
            }
            else
            {
                _cachedRequestsManager?.CacheFailedRequest( request );
                Log.Error( "Response: Failed" );
            }

            return success;
        }

        private async Task ProcessCachedRequestsImmediately()
        {
            const int maxBatchSize = 5;
            var processedCount = 0;

            while( processedCount < maxBatchSize )
            {
                var success = await _cachedRequestsManager.ProcessNextRequest();
                if( !success )
                {
                    break;
                }
                processedCount++;
            }
        }

        public async Task<bool> SendRequestAsync()
        {
            var availableServers = _servers.Keys
                .Where( server => server.CanHandleRequest( 1 ) )
                .ToList();

            var server = _loadBalancingStrategy.SelectServer( availableServers );
            if( server == null )
            {
                return false;
            }

            return await _requestHandler.SendRequestAsync( server );
        }

        private void StartHealthDecrementTask( double timeInSec = 10, double decreaseAmount = 5 )
        {
            Task.Run( async () =>
            {
                while( true )
                {
                    foreach( var server in _servers.Keys )
                    {
                        lock( server )
                        {
                            server.ServerHealth = Math.Max( 0, server.ServerHealth - decreaseAmount );
                        }
                    }
                    //wait timeInSec seconds before decaying the health for all the servers again
                    await Task.Delay( TimeSpan.FromSeconds( timeInSec ) );
                }
            } );
        }

        public async Task PerformHealthChecksAsync()
        {
            var tasks = _servers.Keys.Select( async server =>
            {
                await _healthCheckService.PerformHealthCheckAsync( server );

                if( !server.IsServerHealthy && server is Server s && s.CircuitBreaker.State == CircuitState.Open )
                {
                    RemoveServer();
                }
                else
                {
                    Log.Debug( $"Server {server.ServerAddress}:{server.ServerPort} is currently healthy with health {server.ServerHealth}" );
                }
            } );

            await Task.WhenAll( tasks );
        }

        private void RemoveServer( bool forceRemoval = false )
        {
            IServer? strm = null;

            if( forceRemoval )
            {
                strm = _servers.Keys
                    .OfType<IServer>()
                    .OrderBy( server => server.ServerHealth )
                    .FirstOrDefault();
            }
            else
            {
                strm = _servers.Keys
                    .OfType<IServer>()
                    .Where( server => server.ServerHealth < 80 )
                    .OrderBy( server => server.ServerHealth )
                    .FirstOrDefault();
            }

            if( strm is Server serverToRemove )
            {

                Log.Info( $"Initiating removal for unhealthy server: {serverToRemove.ServerAddress}:{serverToRemove.ServerPort}. Circuit status: {serverToRemove.CircuitBreaker.State}" );
                serverToRemove.EnableDrainMode();

                Task.Run( async () =>
                {
                    Log.Debug( $"Waiting for connections to the unhealthy server {serverToRemove.ServerAddress}:{serverToRemove.ServerPort} to die off before backlogging it." +
                              $" Current active connections to it: {serverToRemove.ActiveConnections}" );
                    while( serverToRemove.ActiveConnections > 0 )
                    {
                        await Task.Delay( 10 );
                    }

                    var filteredServers = _servers.Keys.Where( srv => srv != serverToRemove ).ToList();
                    _servers.Clear();

                    foreach( var srv in filteredServers )
                    {
                        _servers.TryAdd( srv, true );
                    }

                    _autoScaler?.KillServer( serverToRemove.ServerPort );
                    PortUtils.ReleasePort( serverToRemove.ServerPort );

                    Log.Warn( $"Server removed from pool with port release: {serverToRemove.ServerAddress}:{serverToRemove.ServerPort}" );
                } );
            }
            return;
        }

        public void StopHealthChecks()
        {
            _healthCheckTimer?.Stop();
            _healthCheckTimer?.Dispose();
        }

        public async Task DestroyAsync()
        {
            Log.Warn( $"Killing all the instantiated servers before exit. Count: {_servers.Count}" );

            var killServerTasks = new List<Task>();
            foreach( var server in _servers.Keys )
            {
                if( _autoScaler != null )
                {
                    killServerTasks.Add( Task.Run( () => _autoScaler.KillServer( server.ServerPort ) ) );
                }
            }

            await Task.WhenAll( killServerTasks );

            _servers.Clear();
            Log.Warn( $"Cleanup complete. Count: {_servers.Count}" );
        }

    }
}
