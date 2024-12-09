namespace AutoScaler
{
    public class SystemMetrics
    {
        public double AverageResponseTime { get; set; }
        public int TotalActiveConnections { get; set; }
        public int ServerCount { get; set; }
        public double CpuUtilization { get; set; }
        public double MemoryUtilization { get; set; }
    }
}