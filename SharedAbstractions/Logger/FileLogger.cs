using LoadBalancer.Exceptions;
using System.Text;

namespace LoadBalancer.Logging
{
    public class FileLogger : ILogger
    {
        private readonly string _targetDirectoryPath;
        private readonly LogLevel _minLevel;
        private readonly object _lock = new object();
        private StreamWriter? _writer;
        private DateTime _currentFileDate;

        public FileLogger(string targetDirectory, LogLevel minLevel = LogLevel.Info)
        {
            try
            {
                _targetDirectoryPath = Path.GetFullPath(targetDirectory);

                if (!Directory.Exists(_targetDirectoryPath))
                {
                    CheckDirectoryWritePermissions(_targetDirectoryPath);
                    Directory.CreateDirectory(_targetDirectoryPath);
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException)
            {
                throw new LoadBalancerException(
                    $"Insufficient permissions to create log directory at {targetDirectory}",
                    ex.GetType().Name,
                    ex);
            }

            _targetDirectoryPath = targetDirectory;
            _minLevel = minLevel;
            InitializeWriter();
        }

        /// <summary>
        /// Checks if the provided path <paramref name="targetDirectory"/> is accessible by the 
        /// application.
        /// </summary>
        /// <param name="targetDirectory"></param>
        /// <exception cref="UnauthorizedAccessException"></exception>
        private void CheckDirectoryWritePermissions(string targetDirectory)
        {
            try
            {
                string testFile = Path.Combine(targetDirectory, Path.GetRandomFileName());
                using (FileStream fs = File.Create(testFile, 1, FileOptions.DeleteOnClose)) { }
            }
            catch (Exception)
            {
                throw new UnauthorizedAccessException($"No write permissions for directory: {targetDirectory}");
            }
        }

        private void InitializeWriter()
        {
            lock (_lock)
            {
                _currentFileDate = DateTime.UtcNow;
                string fileName = $"LoadBalancer__{_currentFileDate:dd_MM_yy__HH_mm_ss}.log";
                string logFileFullPath = Path.Combine(_targetDirectoryPath, fileName);

                _writer = new StreamWriter(logFileFullPath, true, Encoding.UTF8) { AutoFlush = true };
            }
        }

        public bool ShouldLog(LogLevel level)
        {
            return level >= _minLevel;
        }
        public void Log(LogLevel level, string message, Exception? exception = null)
        {
            if (!ShouldLog(level))
            {
                return;
            }

            lock (_lock)
            {
                string logEntry = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.ffff}]  [{level}]    {message}";
                _writer?.WriteLine(logEntry);

                if (exception != null)
                {
                    _writer?.WriteLine($"EXC: {exception}");
                }
            }
        }

        public void Flush()
        {
            lock (_lock)
            {
                _writer?.Flush();
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                //flush all the logs out first before disposing
                _writer?.Flush();
                _writer?.Dispose();
            }
        }
    }
}