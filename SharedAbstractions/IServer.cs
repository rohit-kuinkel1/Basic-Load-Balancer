namespace LoadBalancer.Interfaces
{
    public interface IServer
    {
        string ServerAddress { get; }
        int ServerPort { get; }
        bool IsServerHealthy { get; }
        double ServerHealth { get; set; }
        void RecordRequest(bool success, long responseTimeMs);
        void UpdateHealthStatus(bool isHealthy);
    }
}