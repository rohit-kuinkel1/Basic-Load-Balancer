namespace LoadBalancer
{
    public class AutoScalingConfig
    {
        public int MinServers { get; set; } = 2;
        public int MaxServers { get; set; } = 10;
        public int MaxRequestThresholdForScaleUp { get; set; } = 100;
        public int MinRequestThresholdForScaleDown { get; set; } = 20;
        public TimeSpan ScaleCheckIntervalSec { get; set; } = TimeSpan.FromSeconds( 30 );

        public static AutoScalingConfig Factory(
            int minServers = 2,
            int maxServers = 10,
            int requestThresholdForScaleUp = 100,
            int requestThresholdForScaleDown = 20,
            int scaleCheckIntervalSec = 30
        )
        {
            return new AutoScalingConfig()
            {
                MinServers = minServers,
                MaxServers = maxServers,
                MaxRequestThresholdForScaleUp = requestThresholdForScaleUp,
                MinRequestThresholdForScaleDown = requestThresholdForScaleDown,
                ScaleCheckIntervalSec = TimeSpan.FromSeconds( scaleCheckIntervalSec )
            };
        }
    }
}