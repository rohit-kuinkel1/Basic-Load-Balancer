using System.Collections.Concurrent;
using LoadBalancer.Interfaces;
using LoadBalancer.Logger;

namespace LoadBalancer
{
    public class LoadBalancer : ILoadBalancer
    {
        private readonly ConcurrentDictionary<IServer, bool> _servers = new();
        private readonly ILoadBalancingStrategy _loadBalancingStrategy;
        private readonly HealthCheckService _healthCheckService;
        private readonly RequestHandler _requestHandler;
        private readonly IAutoScaler? _autoScaler;
        private readonly System.Timers.Timer _healthCheckTimer;
        private readonly int _minHealthThreshold;

        public LoadBalancer(
            ILoadBalancingStrategy? loadBalancingStrategy,
            HttpClient? httpClient,
            bool enabledAutoScaling = false,
            AutoScalingConfig? autoScalingConfig = null,
            TimeSpan healthCheckInterval = default,
            int minHealthThreshold = 70
        )
        {
            _loadBalancingStrategy = loadBalancingStrategy ?? new RoundRobinStrategy();
            _healthCheckService = new HealthCheckService( httpClient ?? new HttpClient() );
            _requestHandler = new RequestHandler( httpClient ?? new HttpClient() );
            _minHealthThreshold = minHealthThreshold;

            //initialize AutoScaler if auto scaling was enabled
            if( enabledAutoScaling )
            {
                _autoScaler = new AutoScaler(
                    autoScalingConfig ?? AutoScalingConfig.Factory(),
                    () => new Server(
                        "localhost",
                        PortUtils.FindAvailablePort(),
                        CircuitBreakerConfig.Factory(),
                        maxCapacityInPercentage: 50,
                        totalConnections: 100 ),
                    server => _servers.TryAdd( server, true ),
                    RemoveUnhealthyServer,
                    () => _servers.Count );

                _autoScaler.Initialize();
                Log.Info( "Load Balancer started with auto-scaling enabled. Press Ctrl+C to exit." );
            }
            else
            {
                Log.Warn( "Load Balancer started without auto-scaling enabled. Press Ctrl+C to exit." );
            }

            _healthCheckTimer = new System.Timers.Timer
            {
                Interval = healthCheckInterval == default ? TimeSpan.FromSeconds( 10 ).TotalMilliseconds : healthCheckInterval.TotalMilliseconds,
                AutoReset = true
            };

            Log.Debug( $"Will perform health checks for the servers every {TimeSpan.FromSeconds( _healthCheckTimer.Interval / 1000 )} seconds" );
            _healthCheckTimer.Elapsed += async ( _, _ ) => await PerformHealthChecksAsync();
            _healthCheckTimer.Start();

            //simulate health decrease
            StartHealthDecrementTask( timeInSec: 1, decreaseAmount: 10 );
        }

        public async Task<bool> HandleRequestAsync( HttpRequestMessage request )
        {
            _autoScaler?.TrackRequest( DateTime.UtcNow );
            return await SendRequestAsync();
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
            Log.Debug( $"Performing health checks for all the servers" );
            var tasks = _servers.Keys.Select( async server =>
            {
                await _healthCheckService.PerformHealthCheckAsync( server );

                if( !server.IsServerHealthy && server is Server s && s.CircuitBreaker.State == CircuitState.Open )
                {
                    RemoveUnhealthyServer();
                }
                else
                {
                    Log.Debug( $"Server {server.ServerAddress}:{server.ServerPort} is currently healthy with health {server.ServerHealth}" );
                }
            } );

            await Task.WhenAll( tasks );
        }

        private void RemoveUnhealthyServer()
        {
            var serverToRemove = _servers.Keys
                .OfType<Server>()
                .Where( server => server.ServerHealth < 80 )
                .OrderBy( server => server.ServerHealth )
                .FirstOrDefault();

            if( serverToRemove is null )
            {
                Log.Warn( "No unhealthy server found to remove." );
                return;
            }

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

                PortUtils.ReleasePort( serverToRemove.ServerPort );
                Log.Warn( $"Server removed from pool with port release: {serverToRemove.ServerAddress}:{serverToRemove.ServerPort}" );
            } );
        }

        public void StopHealthChecks()
        {
            _healthCheckTimer.Stop();
            _healthCheckTimer.Dispose();
        }
    }
}
