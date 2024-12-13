namespace LoadBalancer.Interfaces
{
    public interface IServer
    {
        string ServerAddress { get; }
        int ServerPort { get; }
        bool IsServerHealthy { get; }
        double ServerHealth { get; set; }
        double MaxCapacityInPercentage { get; }
        double CurrentLoadInPercentage { get; }
        bool CanHandleRequest(int requestCount);
        void AddLoad(int requestCount);
        void RemoveLoad(int requestCount);
        void RecordRequest(bool success, long responseTimeMs);
        void UpdateHealthStatus(bool isHealthy);
    }
}