using System.Collections.Concurrent;

namespace LoadBalancer.Logger
{
    internal class LogQueue : IDisposable
    {
        private readonly BlockingCollection<LogEntry> _queue;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly Task _processingTask;
        private readonly IEnumerable<ILogger> _loggers;
        private readonly ILogger _errorLogger;

        public LogQueue(IEnumerable<ILogger> loggers, ILogger? errorLogger = null)
        {
            _queue = new BlockingCollection<LogEntry>(new ConcurrentQueue<LogEntry>(), boundedCapacity: 1000);
            _cancellationTokenSource = new CancellationTokenSource();
            _loggers = loggers ?? throw new ArgumentNullException(nameof(loggers));
            _errorLogger = errorLogger ?? new ConsoleLogger(LogLevel.TRC);

            _processingTask = Task.Factory.StartNew(
                ProcessLogs,
                _cancellationTokenSource.Token,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }

        public void Enqueue(LogEntry entry)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            try
            {
                if (!_queue.TryAdd(entry, TimeSpan.FromSeconds(1)))
                {
                    _errorLogger?.Write(LogLevel.WRN, "Log queue is full. Dropping this log entry.");
                }
            }
            catch (InvalidOperationException)
            {
                //queue is completed, ignore new entries silently till we have place
            }
        }

        private void ProcessLogs()
        {
            try
            {
                foreach (var entry in _queue.GetConsumingEnumerable(_cancellationTokenSource.Token))
                {
                    try
                    {
                        foreach (var logger in _loggers)
                        {
                            try
                            {
                                if (logger.ShouldLog(entry.Level))
                                {
                                    logger.Write(entry.Level, entry.Message, entry.Exception);
                                }
                            }
                            catch (Exception loggerEx)
                            {
                                //log individual logger errors without stopping the entire processing
                                _errorLogger?.Write(
                                    LogLevel.ERR,
                                    $"Error in logger {logger.GetType().Name}: {loggerEx.Message}",
                                    loggerEx);
                            }
                        }
                    }
                    catch (Exception processingEx)
                    {
                        _errorLogger?.Write(LogLevel.ERR, "Error processing log entry", processingEx);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                //normal shutdown
            }
            catch (Exception ex)
            {
                _errorLogger?.Write(LogLevel.FTL, "Unexpected error in log processing", ex);
            }
        }

        public async Task FlushAsync(TimeSpan? timeout = null)
        {
            timeout ??= TimeSpan.FromSeconds(5);

            _queue.CompleteAdding();

            try
            {
                await Task.WhenAny(
                    _processingTask,
                    Task.Delay(timeout.Value)
                );

                //force flush all loggers even if processing isn't complete
                foreach (var logger in _loggers)
                {
                    logger.Flush();
                }
            }
            catch (Exception ex)
            {
                _errorLogger?.Write(LogLevel.ERR, "Error during log queue flush", ex);
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _cancellationTokenSource.Cancel();

                try
                {
                    FlushAsync().GetAwaiter().GetResult();
                }
                catch
                {
                }

                _queue.Dispose();
                _cancellationTokenSource.Dispose();

                foreach (var logger in _loggers)
                {
                    logger.Dispose();
                }
            }
        }
    }
}