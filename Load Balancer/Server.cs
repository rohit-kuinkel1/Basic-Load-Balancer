namespace LoadBalancer
{
    /// <summary>
    /// Represents a server with health and performance metrics.
    /// </summary>
    public class Server : IServer
    {
        public string ServerAddress { get; }
        public int ServerPort { get; }
        public double ServerHealth { get; private set; } = 100.0;
        public double AverageResponseTimeMs { get; private set; } = 50;

        internal int _activeConnections;
        public int ActiveConnections
        {
            get => _activeConnections;
            private set => _activeConnections = value;
        }
        public bool IsServerHealthy { get; private set; } = true;
        private int _requestCount;
        private int _failedRequests;
        private long _totalResponseTime;
        private int _consecutiveFailures;

        public Server( string address, int port )
        {
            ServerAddress = address;
            ServerPort = port;
        }

        /// <summary>
        /// Records a request to the server, updating its metrics.
        /// </summary>
        public void RecordRequest( bool success, long responseTimeMs )
        {
            Interlocked.Increment( ref _requestCount );
            Interlocked.Add( ref _totalResponseTime, responseTimeMs );

            if( !success )
            {
                Interlocked.Increment( ref _failedRequests );
            }

            lock( this )
            {
                AverageResponseTimeMs = ( 1 - LoadBalancerConfig.EwmaAlpha ) * AverageResponseTimeMs + LoadBalancerConfig.EwmaAlpha * responseTimeMs;
            }
        }

        /// <summary>
        /// Updates the server's health status based on the latest health check.
        /// </summary>
        public void UpdateHealthStatus( bool isHealthy )
        {
            if( isHealthy )
            {
                ServerHealth = Math.Min( 100.0, ServerHealth + 10 );
                _consecutiveFailures = 0;
                IsServerHealthy = true;
            }
            else
            {
                _consecutiveFailures++;
                if( _consecutiveFailures >= 3 )
                {
                    IsServerHealthy = false;
                }
            }
        }

        /// <summary>
        /// Enables drain mode to gracefully shut down the server.
        /// </summary>
        public void EnableDrainMode()
        {
            IsServerHealthy = false;
            ServerHealth = 0;
        }
    }
}
