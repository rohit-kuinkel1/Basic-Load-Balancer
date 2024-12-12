using System.Collections.Concurrent;
using System.Net.Http;
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
        private readonly IAutoScaler _autoScaler;

        public LoadBalancer(
            ILoadBalancingStrategy loadBalancingStrategy,
            HttpClient httpClient,
            AutoScalingConfig? autoScalingConfig = null )
        {
            _loadBalancingStrategy = loadBalancingStrategy ?? throw new ArgumentNullException( nameof( loadBalancingStrategy ) );
            _healthCheckService = new HealthCheckService( httpClient );
            _requestHandler = new RequestHandler( httpClient );

            // Initialize AutoScaler with internal server management methods
            _autoScaler = new AutoScaler(
                autoScalingConfig ?? AutoScalingConfig.Factory(),
                () => new Server( "localhost", PortUtils.FindAvailablePort(), CircuitBreakerConfig.Factory() ),
                server => _servers.Add( server ),
                server => RemoveUnhealthyServer( server ),
                () => _servers.Count
            );

            _autoScaler.Initialize();
        }

        public async Task<bool> HandleRequestAsync( HttpRequestMessage request )
        {
            _autoScaler.TrackRequest( DateTime.UtcNow );
            return await SendRequestAsync();
        }

        public async Task<bool> SendRequestAsync()
        {
            var server = _loadBalancingStrategy.SelectServer( _servers );
            if( server == null )
            {
                return false;
            }

            return await _requestHandler.SendRequestAsync( server );
        }

        public async Task PerformHealthChecksAsync()
        {
            var tasks = _servers.Select( async server =>
            {
                await _healthCheckService.PerformHealthCheckAsync( server );

                if( !server.IsServerHealthy && server is Server s && s.CircuitBreaker.State == CircuitState.Open )
                {
                    RemoveUnhealthyServer( server );
                }
            } );

            await Task.WhenAll( tasks );
        }

        private void RemoveUnhealthyServer( IServer iserver )
        {
            if( iserver is Server server )
            {
                Log.Info( $"Initiating removal for unhealthy server: {server.ServerAddress}:{server.ServerPort}" );
                server.EnableDrainMode();

                Task.Run( async () =>
                {
                    while( server.ActiveConnections > 0 )
                    {
                        await Task.Delay( 50 );
                    }

                    var filteredServers = _servers.Where( srv => srv != server ).ToList();
                    var newBag = new ConcurrentBag<IServer>( filteredServers );

                    foreach( var srv in newBag )
                    {
                        _servers.Add( srv );
                    }

                    PortUtils.ReleasePort( server.ServerPort );
                    Log.Warn( $"Server removed from pool: {server.ServerAddress}:{server.ServerPort}" );
                } );
            }
        }

    }
}