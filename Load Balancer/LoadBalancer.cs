using System.Collections.Concurrent;

namespace LoadBalancer
{
    /// <summary>
    /// Represents a load balancer managing a pool of servers.
    /// </summary>
    public class LoadBalancer : ILoadBalancer
    {
        private ConcurrentBag<IServer> _servers = new();
        private readonly HttpClient _httpClient = new();
        private static readonly Random _random = new();

        /// <summary>
        /// Adds a new server to the load balancer.
        /// </summary>
        public void AddServer( IServer server )
        {
            _servers.Add( server );
            Console.WriteLine( $"Server added: {server.ServerAddress}:{server.ServerPort}" );

        }

        /// <summary>
        /// Removes a server from the load balancer after allowing active connections to finish.
        /// </summary>
        public void RemoveServer( IServer server )
        {
            if( server is Server server2 )
            {
                server2.EnableDrainMode();
                Task.Run( async () =>
                {
                    while( server2.ActiveConnections > 0 )
                    {
                        await Task.Delay( 50 );
                    }

                    _servers = new ConcurrentBag<IServer>( _servers.Where( s => s != server ) );
                    Console.WriteLine( $"Server removed from pool: {server.ServerAddress}:{server.ServerPort}" );
                } );
            }
        }

        /// <summary>
        /// Selects a server using weighted health.
        /// </summary>
        private IServer GetNextServerWeightedHealth()
        {
            var healthyServers = _servers.Where( s => s.IsServerHealthy ).ToList();
            if( !healthyServers.Any() )
            {
                return default;
            }

            var cumulativeWeights = new List<double>();
            double totalWeight = 0;

            foreach( var server in healthyServers )
            {
                totalWeight += server.ServerHealth;
                cumulativeWeights.Add( totalWeight );
            }

            var randomValue = _random.NextDouble() * totalWeight;
            for( int i = 0; i < cumulativeWeights.Count; i++ )
            {
                if( randomValue <= cumulativeWeights[i] )
                {
                    return healthyServers[i];
                }
            }

            return healthyServers.Last();
        }

        /// <summary>
        /// Sends a request to a server selected by the load balancer.
        /// </summary>
        public async Task<bool> SendRequestAsync()
        {
            var server = GetNextServerWeightedHealth() as Server;
            if( server == null )
            {
                return false;
            }

            try
            {
                Interlocked.Increment( ref server._activeConnections );

                var timeout = TimeSpan.FromMilliseconds( Math.Max( 500, server.AverageResponseTimeMs * 2 ) );
                using var cts = new CancellationTokenSource( timeout );
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                var response = await _httpClient.GetAsync( $"http://{server.ServerAddress}:{server.ServerPort}/api", cts.Token );
                stopwatch.Stop();

                server.RecordRequest( response.IsSuccessStatusCode, stopwatch.ElapsedMilliseconds );

                return response.IsSuccessStatusCode;

            }
            catch
            {
                server.RecordRequest( false, 0 );
                return false;
            }
            finally
            {
                Interlocked.Decrement( ref server._activeConnections );
            }
        }


        /// <summary>
        /// Performs health checks on all servers.
        /// </summary>
        public async Task PerformHealthChecksAsync()
        {
            foreach( var iserver in _servers )
            {
                var server = iserver as Server;
                if( server == null )
                {
                    continue;
                }

                try
                {
                    var timeout = TimeSpan.FromMilliseconds( Math.Max( 500, server.AverageResponseTimeMs * 2 ) );
                    using var cts = new CancellationTokenSource( timeout );

                    var response = await _httpClient.GetAsync( $"http://{server.ServerAddress}:{server.ServerPort}/health", cts.Token );
                    server.UpdateHealthStatus( response.IsSuccessStatusCode );
                }
                catch
                {
                    server.UpdateHealthStatus( false );
                }
            }
        }
    }
}