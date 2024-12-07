namespace Load_Balancer
{
    public class CircuitBreakerConfig
    {
        public int FailureThreshold { get; set; } = 5;
        public TimeSpan ResetTimeout { get; set; } = TimeSpan.FromSeconds( 30 );
        public int HalfOpenMaxAttempts { get; set; } = 3;
    }
}
