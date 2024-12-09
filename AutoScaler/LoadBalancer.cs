using System.Collections.Concurrent;
using LoadBalancer;

namespace LoadBalancer
{
    /// <summary>
    /// <see cref="LoadBalancer"/> will primarily focus on what its name says, load balancing.
    /// The heavy lifting of the server management, spawning of new servers, deletion of those servers
    /// when the traffic goes back to normal, backlogging unhealthy servers, performing health checks etc 
    /// all is done by the ServerManager.
    /// </summary>
    public class LoadBalancer : ILoadBalancer
    {
        private readonly ILoadBalancingStrategy _loadBalancingStrategy;
        private readonly HealthCheckService _healthCheckService;
        private readonly RequestHandler _requestHandler;

        public LoadBalancer( ILoadBalancingStrategy loadBalancingStrategy, HttpClient httpClient )
        {
            _loadBalancingStrategy = loadBalancingStrategy ?? throw new ArgumentNullException( nameof( loadBalancingStrategy ) );
            _healthCheckService = new HealthCheckService( httpClient );
            _requestHandler = new RequestHandler( httpClient );
        }

        public async Task<bool> SendRequestAsync( IServer server )
        {
            if( server == null )
            {
                return false;
            }

            return await _requestHandler.SendRequestAsync( server );
        }

        public IServer SelectServer( IEnumerable<IServer> servers )
        {
            return _loadBalancingStrategy.SelectServer( servers );
        }

        public async Task PerformHealthCheckAsync( IServer server )
        {
            await _healthCheckService.PerformHealthCheckAsync( server );
        }
    }
}