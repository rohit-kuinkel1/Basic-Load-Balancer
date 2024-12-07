namespace LoadBalancer
{
    public interface ILoadBalancingStrategy
    {
        IServer SelectServer( IEnumerable<IServer> servers );
    }
}