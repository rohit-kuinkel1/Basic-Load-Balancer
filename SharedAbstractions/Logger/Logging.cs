using LoadBalancer.Exceptions;
using System.Collections.Concurrent;

namespace LoadBalancer.Logging
{
    public static class Logging
    {
        private static readonly ConcurrentDictionary<LogSinks, ILogger> _sinks = new();
        private static readonly object _lock = new();
        private static LogLevel _minimumLevel = LogLevel.Info;

        static Logging()
        {
            //add a default destination to the Console
            TryAddSink(LogSinks.Console, new ConsoleLogger(_minimumLevel));
        }

        public static void SetMinimumLevel(LogLevel level)
        {
            _minimumLevel = level;
        }

        private static void AddSink(LogSinks sink, string? targetDirectory = null)
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
                    throw new LoadBalancerException($"Target must be specified for file logging: {targetDirectory} ", typeof(ArgumentException));

                case LogSinks.ConsoleAndFile:
                    if (!string.IsNullOrWhiteSpace(targetDirectory))
                    {
                        TryAddSink(LogSinks.Console, new ConsoleLogger(_minimumLevel));
                        TryAddSink(LogSinks.File, new FileLogger(targetDirectory, _minimumLevel));
                        break;
                    }
                    throw new LoadBalancerException($"Target must be specified for file logging: {targetDirectory} ", typeof(ArgumentException));

                default:
                    throw new LoadBalancerException($"Unsupported log sink type: {sink}", typeof(NotSupportedException));
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
            }
        }

        private static void Log(LogLevel level, string message, Exception? exception = null)
        {
            if (level < _minimumLevel)
            {
                return;
            }

            foreach (var target in _sinks.Values)
            {
                if (target.ShouldLog(level))
                {
                    target.Log(level, message, exception);
                }
            }
        }

        public static void Trace(string message) => Log(LogLevel.Trace, message);
        public static void Debug(string message) => Log(LogLevel.Debug, message);
        public static void Info(string message) => Log(LogLevel.Info, message);
        public static void Warn(string message, Exception? ex = null) => Log(LogLevel.Warn, message, ex);
        public static void Error(string message, Exception? ex = null) => Log(LogLevel.Error, message, ex);
        public static void Fatal(string message, Exception? ex = null) => Log(LogLevel.Fatal, message, ex);

        public static void FlushLogger()
        {
            foreach (var sink in _sinks.Values)
            {
                sink.Flush();
            }
        }

        public static void Kill()
        {
            foreach (var sink in _sinks.Values)
            {
                sink.Dispose();
            }
            _sinks.Clear();
        }

        public static void ShutDown()
        {
            Kill();
        }
    }
}