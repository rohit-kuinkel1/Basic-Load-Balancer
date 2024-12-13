using LoadBalancer.Interfaces;
using LoadBalancer.Logger;

namespace LoadBalancer
{
    public class Server : IServer
    {
        public string ServerAddress { get; }
        public int ServerPort { get; }

        /// <summary>
        /// start off with 100 health for each server
        /// </summary>
        public double ServerHealth { get; set; } = 100.0;

        /// <summary>
        /// default average response time for a server
        /// </summary>
        public double AverageResponseTimeMs { get; private set; } = 20;

        /// <summary>
        /// denotes the safe max capacity for this current instance of <see cref="Server"/>
        /// </summary>
        public double MaxCapacityInPercentage { get; }

        /// <summary>
        /// denotes the currently in use capacity of <see cref="Server"/>
        /// </summary>
        public double CurrentLoadInPercentage { get; private set; }

        private int _activeConnections;
        public int ActiveConnections
        {
            get => _activeConnections;
            private set => _activeConnections = value;
        }

        public bool IsServerHealthy => CircuitBreaker.State == CircuitState.Closed ||
                                     CircuitBreaker.State == CircuitState.HalfOpen;

        private int _requestCount;
        private int _failedRequests;
        private long _totalResponseTime;
        private int _consecutiveFailures;

        public CircuitBreaker CircuitBreaker { get; }

        public Server( string address, int port, CircuitBreakerConfig breakerConfig, double maxCapacity = 80 )
        {
            ServerAddress = address;
            ServerPort = port;
            CircuitBreaker = new CircuitBreaker( breakerConfig );
            MaxCapacityInPercentage = maxCapacity;
        }

        public bool CanHandleRequest( int requestCount )
        {
            return CurrentLoadInPercentage + requestCount <= ( MaxCapacityInPercentage * 0.9 ); //only use up to 90% of capacity
        }

        public void AddLoad( int requestCount )
        {
            Interlocked.Add( ref _activeConnections, requestCount );
            CurrentLoadInPercentage += requestCount;
        }

        public void RemoveLoad( int requestCount )
        {
            Interlocked.Add( ref _activeConnections, -requestCount );
            CurrentLoadInPercentage = Math.Max( 0, CurrentLoadInPercentage - requestCount );
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
                CircuitBreaker.RecordFailure();
            }
            else
            {
                CircuitBreaker.RecordSuccess();
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

                // Ensure Circuit Breaker is marked as healthy
                CircuitBreaker.RecordSuccess();
            }
            else
            {
                _consecutiveFailures++;
                CircuitBreaker.RecordFailure();

                if( _consecutiveFailures >= 3 )
                {
                    //prevent further requests from being sent
                    CircuitBreaker.State = CircuitState.Open;

                    if( _consecutiveFailures >= 5 )
                    {
                        Log.Fatal( $"Server {ServerAddress}:{ServerPort} has failed too many times." );
                    }
                }
            }
        }


        /// <summary>
        /// Enables drain mode to gracefully shut down the server.
        /// Marks the <see cref="CircuitBreaker"/> as failed when drained
        /// </summary>
        public void EnableDrainMode()
        {
            ServerHealth = 0;
            CircuitBreaker.RecordFailure();
        }
    }
}
