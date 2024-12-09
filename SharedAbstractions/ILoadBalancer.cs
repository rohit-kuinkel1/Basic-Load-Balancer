namespace LoadBalancer
{
    public interface ILoadBalancer
    {
        Task<bool> SendRequestAsync();
        void AddServer( IServer server );
        void RemoveServer( IServer server );
        Task PerformHealthChecksAsync();
    }
}