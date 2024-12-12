using LoadBalancer.Interfaces;

namespace LoadBalancer
{
    public class RoundRobinStrategy : ILoadBalancingStrategy
    {
        private int _currentIndex = 0;
        private readonly object _lock = new object();

        public IServer SelectServer( IEnumerable<IServer> servers )
        {
            if( servers == null || !servers.Any() )
            {
                return default;
            }

            var availableServers = servers.Where( s => s.IsServerHealthy ).ToList();
            if( !availableServers.Any() )
            {
                return default;
            }

            lock( _lock )
            {
                if( _currentIndex >= availableServers.Count )
                {
                    _currentIndex = 0;
                }

                var selectedServer = availableServers[_currentIndex];
                _currentIndex++;
                return selectedServer;
            }
        }
    }
}
