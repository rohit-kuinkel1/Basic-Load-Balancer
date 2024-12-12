namespace LoadBalancer
{
    public class AutoScalingConfig
    {
        public int MinServers { get; set; } = 2;
        public int MaxServers { get; set; } = 10;
        public int RequestThresholdForScaleUp { get; set; } = 100;
        public int RequestThresholdForScaleDown { get; set; } = 20;
        public TimeSpan ScaleCheckInterval { get; set; } = TimeSpan.FromSeconds( 30 );

        public static AutoScalingConfig Default() => new AutoScalingConfig();
    }
}