namespace LoadBalancer
{
    public class WeightedHealthStrategy : ILoadBalancingStrategy
    {
        private readonly Random _random = new Random();

        public IServer SelectServer( IEnumerable<IServer> servers )
        {
            if( servers == null || !servers.Any() )
            {
                return default;
            }

            var healthyServers = servers.Where( s => s.IsServerHealthy ).ToList();
            if( !healthyServers.Any() )
            {
                return default;
            }

            // Calculate cumulative weights based on health scores
            var cumulativeWeights = new List<double>();
            double totalWeight = 0;

            foreach( var server in healthyServers )
            {
                totalWeight += server.ServerHealth;
                cumulativeWeights.Add( totalWeight );
            }

            //select a server using weighted random selection
            var randomValue = _random.NextDouble() * totalWeight;

            for( int i = 0; i < cumulativeWeights.Count; i++ )
            {
                if( randomValue <= cumulativeWeights[i] )
                {
                    return healthyServers[i];
                }
            }

            //fallback to last server (should rarely happen due to floating-point precision)
            return healthyServers.Last();
        }
    }
}
