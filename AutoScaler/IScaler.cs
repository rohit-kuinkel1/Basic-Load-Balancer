using System.Threading.Tasks;
using LoadBalancer;

namespace AutoScaler
{
    public interface IScaler
    {
        Task SpawnServerAsync();
        Task RemoveServerAsync( IServer server );
        Task MonitorAndScaleAsync();
        Task<SystemMetrics> GetCurrentMetricsAsync();
        bool CanScaleUp();
        bool CanScaleDown();
    }
}