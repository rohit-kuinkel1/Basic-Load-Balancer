using System.Collections.Generic;
using System.Threading.Tasks;

namespace LoadBalancer
{
    public interface ILoadBalancer
    {
        Task<bool> SendRequestAsync( IServer server );
        IServer SelectServer( IEnumerable<IServer> servers );
        Task PerformHealthCheckAsync( IServer server );
    }
}