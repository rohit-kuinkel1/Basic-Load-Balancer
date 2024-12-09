namespace ServerSetup
{
    public interface IMetricsService
    {
        void RecordRequest( long durationMs );
        int SimulateLatency();
        double GetAverageResponseTime();
    }
}