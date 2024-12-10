namespace LoadBalancer.Logging
{
    public interface ILogger : IDisposable
    {
        void Log(LogLevel level, string message, Exception? exception = null);
        bool ShouldLog(LogLevel level);
        void Flush();
    }
}