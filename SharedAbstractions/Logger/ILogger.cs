namespace LoadBalancer.Logger
{
    public interface ILogger : IDisposable
    {
        void Write(LogLevel level, string message, Exception? exception = null);
        bool ShouldLog(LogLevel level);
        void Flush();
    }
}