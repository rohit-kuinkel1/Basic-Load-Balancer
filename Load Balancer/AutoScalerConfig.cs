namespace LoadBalancer
{
    public class AutoScalingConfig
    {
        public int MinServers { get; set; } = 2;
        public int MaxServers { get; set; } = 40;
        public int NumberOfMaxRequestForScaleUp { get; set; } = 100;
        public int NumberOfMinRequestForScaleDown { get; set; } = 20;
        public TimeSpan ScaleCheckIntervalMs { get; set; } = TimeSpan.FromMilliseconds( 50 );

        public static AutoScalingConfig Factory(
            int minServers = 2,
            int maxServers = 10,
            int requestThresholdForScaleUp = 100,
            int requestThresholdForScaleDown = 20,
            int scaleCheckIntervalMs = 30
        )
        {
            return new AutoScalingConfig()
            {
                MinServers = minServers,
                MaxServers = maxServers,
                NumberOfMaxRequestForScaleUp = requestThresholdForScaleUp,
                NumberOfMinRequestForScaleDown = requestThresholdForScaleDown,
                ScaleCheckIntervalMs = TimeSpan.FromMilliseconds( scaleCheckIntervalMs )
            };
        }
    }
}