using System.Collections.Concurrent;

namespace LoadBalancer
{
    public class LoadBalancer : ILoadBalancer
    {
        private ConcurrentBag<IServer> _servers = new();
        private readonly ILoadBalancingStrategy _loadBalancingStrategy;
        private readonly HealthCheckService _healthCheckService;
        private readonly RequestHandler _requestHandler;

        public LoadBalancer( ILoadBalancingStrategy loadBalancingStrategy, HttpClient httpClient )
        {
            _loadBalancingStrategy = loadBalancingStrategy ?? throw new ArgumentNullException( nameof( loadBalancingStrategy ) );
            _healthCheckService = new HealthCheckService( httpClient );
            _requestHandler = new RequestHandler( httpClient );
        }

        public void AddServer( IServer server )
        {
            _servers.Add( server );
            Console.WriteLine( $"Server added: {server.ServerAddress}:{server.ServerPort}" );
        }

        public void RemoveServer( IServer iserver )
        {
            if( iserver is Server server )
            {
                Console.WriteLine( $"Initiating removal for server: {server.ServerAddress}:{server.ServerPort}" );
                server.EnableDrainMode();
                Task.Run( async () =>
                {
                    while( server.ActiveConnections > 0 )
                    {
                        await Task.Delay( 50 );
                    }

                    _servers = new ConcurrentBag<IServer>( _servers.Where( s => s != server ) );
                    Console.WriteLine( $"Server removed from pool: {server.ServerAddress}:{server.ServerPort}" );
                } );
            }
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
            var tasks = _servers.Select( async iserver =>
            {
                await _healthCheckService.PerformHealthCheckAsync( iserver );

                if(
                    iserver is Server server
                    && !server.IsServerHealthy
                    && server.CircuitBreaker.State == Load_Balancer.CircuitState.Open
                )
                {
                    RemoveServer( server );
                }
            } );

            await Task.WhenAll( tasks );
        }

    }
}