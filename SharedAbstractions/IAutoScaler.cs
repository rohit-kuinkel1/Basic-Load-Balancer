namespace LoadBalancer.Interfaces
{
    public interface IAutoScaler
    {
        void Initialize();
        void TrackRequest(DateTime timestamp);
        Task MonitorAndScaleAsync();

        //spawn server should not really be publicly available so omit it from here
        public void KillServer(int port);

        public void SpawnServers(int count, bool bypassRestrictions); //dont like this, will cahnge in future
    }
}