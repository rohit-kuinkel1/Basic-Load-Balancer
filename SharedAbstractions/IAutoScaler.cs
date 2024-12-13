namespace LoadBalancer.Interfaces
{
    public interface IAutoScaler
    {
        void Initialize();
        void TrackRequest(DateTime timestamp);
        Task MonitorAndScaleAsync();
    }
}