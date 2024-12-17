namespace LoadBalancer.Logger
{
    internal class LogEntry
    {
        public LogLevel Level { get; }
        public string Message { get; }
        public Exception? Exception { get; }
        public DateTime Timestamp { get; }

        public LogEntry(LogLevel level, string message, Exception? exception = null)
        {
            Level = level;
            Message = message;
            Exception = exception;
            Timestamp = DateTime.UtcNow;
        }
    }
}