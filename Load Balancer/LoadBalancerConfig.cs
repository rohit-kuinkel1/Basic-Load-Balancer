public static class LoadBalancerConfig
{
    public static int HealthCheckIntervalMs { get; set; } = 300000;
    public static int RequestTimeoutMs { get; set; } = 500;
    public static int MaxConsecutiveFailures { get; set; } = 3;
    public static double HealthScoreIncrement { get; set; } = 5.0;
    public static double EwmaAlpha { get; set; } = 0.2;
}
