namespace LoadBalancer.Interfaces
{
    public interface ILoadBalancer
    {
        Task<bool> SendRequestAsync();
        Task PerformHealthChecksAsync();
    }
}