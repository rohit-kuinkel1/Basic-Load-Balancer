namespace LoadBalancer.Interfaces
{
    public interface ILoadBalancingStrategy
    {
        IServer SelectServer( IEnumerable<IServer> servers );
    }
}