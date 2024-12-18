namespace LoadBalancer.Interfaces
{
    public interface IAutoScaler
    {
        void Initialize();
        void TrackRequest(DateTime timestamp);
        Task MonitorAndScaleAsync();

        //spawn server should not really be publicly available so omit it from here
        public void KillServer(int port);
    }
}