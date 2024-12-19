using LoadBalancer.Exceptions;
using System.Collections.Concurrent;

namespace LoadBalancer.Logger
{
    public static class Log
    {
        private static readonly ConcurrentDictionary<LogSinks, ILogger> _sinks = new();
        private static readonly object _lock = new();
        private static LogLevel _minimumLevel;
        private static LogQueue? _logQueue;

        static Log()
        {
            TryAddSink(LogSinks.Console, new ConsoleLogger(_minimumLevel));
            InitializeLogQueue();
        }

        private static void InitializeLogQueue()
        {
            lock (_lock)
            {
                _logQueue?.Dispose();
                _logQueue = new LogQueue(_sinks.Values);
            }
        }

        public static void SetMinimumLevel(LogLevel level)
        {
            _minimumLevel = level;
        }

        public static void AddSink(LogSinks sink, string? targetDirectory = null)
        {
            lock (_lock)
            {
                switch (sink)
                {
                    case LogSinks.Console:
                        TryAddSink(sink, new ConsoleLogger(_minimumLevel));
                        break;

                    case LogSinks.File:
                        if (!string.IsNullOrWhiteSpace(targetDirectory))
                        {
                            TryAddSink(sink, new FileLogger(targetDirectory, _minimumLevel));
                            break;
                        }
                        throw new LoadBalancerException($"Target must be specified for file logging: {targetDirectory} ", typeof(ArgumentException), "LB-DIR-001");

                    case LogSinks.ConsoleAndFile:
                        if (!string.IsNullOrWhiteSpace(targetDirectory))
                        {
                            TryAddSink(LogSinks.Console, new ConsoleLogger(_minimumLevel));
                            TryAddSink(LogSinks.File, new FileLogger(targetDirectory, _minimumLevel));
                            break;
                        }
                        throw new LoadBalancerException($"Target must be specified for file logging: {targetDirectory} ", typeof(ArgumentException), "LB-DIR-001");

                    default:
                        throw new LoadBalancerException($"Unsupported log sink type: {sink}", typeof(NotSupportedException), "LB-LOGIC-001");
                }

                InitializeLogQueue();
            }
        }

        private static void TryAddSink(LogSinks sink, ILogger target)
        {
            _sinks.TryAdd(sink, target);
        }

        public static void TryRemoveSink(LogSinks sink)
        {
            if (_sinks.TryRemove(sink, out ILogger? target))
            {
                target?.Dispose();
                InitializeLogQueue();
            }
        }

        private static void Write(LogLevel level, string message, Exception? exception = null)
        {
            if (level < _minimumLevel || _logQueue == null)
            {
                return;
            }

            _logQueue.Enqueue(new LogEntry(level, message, exception));
        }

        public static void Trace(string message) => Write(LogLevel.TRC, message);
        public static void Debug(string message) => Write(LogLevel.DBG, message);
        public static void Info(string message) => Write(LogLevel.INF, message);
        public static void Warn(string message, Exception? ex = null) => Write(LogLevel.WRN, message, ex);
        public static void Error(string message, Exception? ex = null) => Write(LogLevel.ERR, message, ex);
        public static void Fatal(string message, Exception? ex = null) => Write(LogLevel.FTL, message, ex);

        public static void FlushLogger()
        {
            _logQueue?.FlushAsync();
        }

        public static void Kill()
        {
            lock (_lock)
            {
                _logQueue?.Dispose();
                _logQueue = null;

                foreach (var sink in _sinks.Values)
                {
                    sink.Dispose();
                }
                _sinks.Clear();
            }
        }

        public static void ShutDown()
        {
            Kill();
        }
    }
}