namespace LoadBalancer
{
    public class CircuitBreakerConfig
    {
        public int FailureThreshold { get; set; } = 5;
        public TimeSpan ResetTimeoutSec { get; set; } = TimeSpan.FromSeconds( 30 );
        public int HalfOpenMaxAttempts { get; set; } = 3;

        public static CircuitBreakerConfig CBCFactory(
            int failureThreshold = 5, 
            int resetTimeoutSec =  30, 
            int halfOpenMaxAttempts = 3)
        {
            return new CircuitBreakerConfig
            {
                FailureThreshold =  failureThreshold,
                ResetTimeoutSec = TimeSpan.FromSeconds( resetTimeoutSec ),
                HalfOpenMaxAttempts = halfOpenMaxAttempts
            };
        }
    }
}
