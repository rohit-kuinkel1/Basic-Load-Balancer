namespace LoadBalancer
{
    public class CircuitBreakerConfig
    {
        public int FailureThreshold { get; set; } = 5;
        public TimeSpan ResetTimeout { get; set; } = TimeSpan.FromSeconds( 30 );
        public int HalfOpenMaxAttempts { get; set; } = 3;

        public static CircuitBreakerConfig Factory( int failureThreshold = 5, int resetTimeoutInSec = 30, int maxHalfOpenAttempts = 3 )
        {
            return new CircuitBreakerConfig
            {
                FailureThreshold = failureThreshold,
                ResetTimeout = TimeSpan.FromSeconds( resetTimeoutInSec ),
                HalfOpenMaxAttempts = maxHalfOpenAttempts
            };
        }
    }
}
