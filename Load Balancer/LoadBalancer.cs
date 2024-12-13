using System.Collections.Concurrent;
using LoadBalancer.Interfaces;
using LoadBalancer.Logger;

namespace LoadBalancer
{
    public class LoadBalancer : ILoadBalancer
    {
        private readonly ConcurrentBag<IServer> _servers = new();
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

            //initialize AutoScaler if auto-scaling is enabled
            if( enabledAutoScaling )
            {
                _autoScaler =
                    new AutoScaler(
                        autoScalingConfig ?? AutoScalingConfig.Factory(),
                        () => new Server( "localhost", PortUtils.FindAvailablePort(), CircuitBreakerConfig.Factory(), maxCapacityInPercentage: 50, totalConnections: 100 ),
                        server => _servers.Add( server ),
                        RemoveUnhealthyServer,
                        () => _servers.Count
                    );

                _autoScaler.Initialize();
                Log.Info( "Load Balancer started with auto-scaling enabled. Press Ctrl+C to exit." );
            }
            else
            {
                Log.Warn( "Load Balancer started without auto-scaling enabled.Press Ctrl+C to exit." );
            }


            //for now we use a "dummy" health check, meaning do the health check in X time interval
            //this will be replaced later on with a smarter algorithm that will know when to do the checks
            _healthCheckTimer = new System.Timers.Timer
            {
                Interval = healthCheckInterval == default ? TimeSpan.FromSeconds( 10 ).TotalMilliseconds : healthCheckInterval.TotalMilliseconds,
                AutoReset = true
            };

            _healthCheckTimer.Elapsed += async ( _, _ ) => await PerformHealthChecksAsync();
            _healthCheckTimer.Start();

            //simulate health decrease
            StartHealthDecrementTask( 0.01 );
        }

        public async Task<bool> HandleRequestAsync( HttpRequestMessage request )
        {
            _autoScaler?.TrackRequest( DateTime.UtcNow );
            return await SendRequestAsync();
        }

        public async Task<bool> SendRequestAsync()
        {
            var availableServers = _servers
                .Where( server => server.CanHandleRequest( 1 ) )
                .ToList();

            var server = _loadBalancingStrategy.SelectServer( availableServers );
            if( server == null )
            {
                return false;
            }

            return await _requestHandler.SendRequestAsync( server );
        }



        /// <summary>
        /// dummy health decreaser for now
        /// </summary>
        private void StartHealthDecrementTask( double timeInMinutes = 10 )
        {
            Task.Run( async () =>
            {
                while( true )
                {
                    foreach( var server in _servers )
                    {
                        lock( server )
                        {
                            server.ServerHealth = Math.Max( 0, server.ServerHealth - 5 );
                        }
                    }
                    await Task.Delay( TimeSpan.FromMinutes( timeInMinutes ) );
                }
            } );
        }


        public async Task PerformHealthChecksAsync()
        {
            var tasks = _servers.Select( async server =>
            {
                await _healthCheckService.PerformHealthCheckAsync( server );

                if( !server.IsServerHealthy && server is Server s && s.CircuitBreaker.State == CircuitState.Open )
                {
                    RemoveUnhealthyServer();
                }
            } );

            await Task.WhenAll( tasks );
        }

        private void RemoveUnhealthyServer()
        {
            var serverToRemove = _servers
                .OfType<Server>()
                .Where( server => server.ServerHealth < 80 )
                .OrderBy( server => server.ServerHealth )
                .FirstOrDefault();

            if( serverToRemove is null )
            {
                Log.Warn( "No unhealthy server found to remove." );
                return;
            }

            Log.Info( $"Initiating removal for unhealthy server: {serverToRemove.ServerAddress}:{serverToRemove.ServerPort}. Circuit status:{serverToRemove.CircuitBreaker.State}" );
            serverToRemove.EnableDrainMode();

            Task.Run( async () =>
            {
                while( serverToRemove.ActiveConnections > 0 )
                {
                    await Task.Delay( 50 );
                }

                var filteredServers = _servers.Where( srv => srv != serverToRemove ).ToList();
                var newBag = new ConcurrentBag<IServer>( filteredServers );

                foreach( var srv in newBag )
                {
                    _servers.Add( srv );
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
