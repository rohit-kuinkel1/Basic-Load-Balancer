using LoadBalancer.Interfaces;
using LoadBalancer.Logger;

namespace LoadBalancer
{
    public class Server : IServer
    {

        public const string DefaultAddress = "localhost";
        public string ServerAddress { get; }
        public int ServerPort { get; }

        /// <summary>
        /// Start off with 100 health for each server
        /// </summary>
        public double ServerHealth { get; set; } = 100.0;

        /// <summary>
        /// Default average response time for a server
        /// </summary>
        public double AverageResponseTimeMs { get; private set; } = 20;

        /// <summary>
        /// Denotes the safe max capacity for this current instance of <see cref="Server"/>
        /// </summary>
        public double MaxCapacityInPercentage { get; }

        /// <summary>
        /// Denotes the currently in use capacity of <see cref="Server"/>
        /// </summary>
        public double CurrentLoadInPercentage
        {
            get
            {
                return (double)_activeConnections / MaxConcurrentConnections * 100;
            }
        }

        internal int _activeConnections;
        public int ActiveConnections
        {
            get => _activeConnections;
            private set => _activeConnections = value;
        }

        /// <summary>
        /// The maximum number of concurrent connections allowed for this server
        /// </summary>
        public int MaxConcurrentConnections { get; }

        /// <summary>
        /// Server is marked as healthy as long as the <see cref="CircuitState"/> for the <see cref="CircuitBreaker"/> for this instance is 
        /// either <see cref="CircuitState.Closed"/> or <see cref="CircuitState.HalfOpen"/>.
        /// If the <see cref="CircuitState"/> is <see cref="CircuitState.Open"/> then the server is deemed to be unhealthy
        /// </summary>
        public bool IsServerHealthy => CircuitBreaker.State == CircuitState.Closed || CircuitBreaker.State == CircuitState.HalfOpen;

        private int _requestCount;
        private int _failedRequests;
        private long _totalResponseTime;
        private int _consecutiveFailures;

        public CircuitBreaker CircuitBreaker { get; }

        public Server( string address, int port, CircuitBreakerConfig breakerConfig, double maxCapacityInPercentage = 80, int totalConnections = 1000 )
        {
            ServerAddress = address;
            ServerPort = port;
            CircuitBreaker = new CircuitBreaker( breakerConfig );
            MaxCapacityInPercentage = maxCapacityInPercentage;
            MaxConcurrentConnections = (int)( totalConnections * ( maxCapacityInPercentage / 100 ) );
        }

        public bool CanHandleRequest( int requestCount )
        {
            return CircuitBreaker.AllowRequest() && ActiveConnections + requestCount <= MaxConcurrentConnections;
        }

        public void AddLoad( int requestCount )
        {
            Interlocked.Add( ref _activeConnections, requestCount );
        }

        public void RemoveLoad( int requestCount )
        {
            Interlocked.Add( ref _activeConnections, -requestCount );
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
                //ServerHealth = Math.Min( 100.0, ServerHealth + 10 );
                _consecutiveFailures = 0;

                CircuitBreaker.RecordSuccess();
            }
            else
            {
                _consecutiveFailures++;
                CircuitBreaker.RecordFailure();

                if( _consecutiveFailures >= 3 )
                {
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
            Log.Debug( $"Drain Mode enabled for {this.ServerAddress}:{this.ServerPort}" );
            ServerHealth = 0;
            CircuitBreaker.RecordFailure();
        }
    }
}
