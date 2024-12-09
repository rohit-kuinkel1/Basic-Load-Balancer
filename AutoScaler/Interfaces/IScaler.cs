namespace ServerManager
{
    public interface IScaler
    {
        Task SpawnServerAsync();
        Task RemoveServerAsync( IServer server );
        Task MonitorAndScaleAsync();
        Task<SystemMetrics> GetCurrentMetricsAsync();
        bool ShouldScaleUp();
        bool CanScaleDown();
    }
}