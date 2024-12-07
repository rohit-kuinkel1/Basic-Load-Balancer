namespace SimpleServer.Services;

public interface IMetricsService
{
    void RecordRequest(long durationMs);
    int SimulateLatency();
    double GetAverageResponseTime();
}