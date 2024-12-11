namespace LoadBalancer.Logger
{
    /// <summary>
    /// <see cref="ConsoleLogger"/> redirects the output to the <see cref="LogSinks.Console"/> for the tool.
    /// </summary>
    public class ConsoleLogger : ILogger
    {
        private readonly object _lock = new object();
        private readonly LogLevel _minLevel;

        public ConsoleLogger(LogLevel minLevel = LogLevel.Info)
        {
            _minLevel = minLevel;
        }

        public bool ShouldLog(LogLevel level)
        {
            return level >= _minLevel;
        }

        public void Write(LogLevel level, string message, Exception? exception = null)
        {
            if (!ShouldLog(level))
            {
                return;
            }

            lock (_lock)
            {
                var prevColorPair = GetCurrentConsoleColors();
                var newColorPair = GetConsoleColors(level);

                SetConsoleColors(newColorPair);
                Console.WriteLine($"[{DateTime.UtcNow:dd.MM.yyyy HH:mm:ss.fffffff}] [{level}] {message} ");
                SetConsoleColors(prevColorPair);

                if (exception != null)
                {
                    SetConsoleColors(Tuple.Create(ConsoleColor.White, ConsoleColor.Red));
                    Console.Write("\x1b[1m\x1b[3m");
                    Console.WriteLine(exception.ToString());
                    Console.Write("\x1b[0m");
                    SetConsoleColors(prevColorPair);
                }
            }
        }

        /// <summary>
        /// <see cref="GetConsoleColors(LogLevel)"/> returns a <see cref="Tuple{T1, T2}"/> of <see cref="ConsoleColor"/>
        /// where the item at 0 represents the <see cref="Console.ForegroundColor"/> and the item at index 1 represents
        /// the <see cref="Console.BackgroundColor"/>
        /// </summary>
        /// <param name="level"></param>
        /// <returns></returns>
        private Tuple<ConsoleColor, ConsoleColor> GetConsoleColors(LogLevel level)
        {
            return level switch
            {
                LogLevel.Trace => Tuple.Create(ConsoleColor.Gray, ConsoleColor.Black),
                LogLevel.Debug => Tuple.Create(ConsoleColor.DarkGray, ConsoleColor.Black),
                LogLevel.Info => Tuple.Create(ConsoleColor.Green, ConsoleColor.Black),
                LogLevel.Warn => Tuple.Create(ConsoleColor.Yellow, ConsoleColor.Black),
                LogLevel.Error => Tuple.Create(ConsoleColor.Red, ConsoleColor.Black),
                LogLevel.Fatal => Tuple.Create(ConsoleColor.White, ConsoleColor.DarkRed),
                _ => Tuple.Create(ConsoleColor.White, ConsoleColor.Black)
            };
        }

        private void SetConsoleColors(Tuple<ConsoleColor, ConsoleColor> colorPairs)
        {
            Console.ForegroundColor= colorPairs.Item1;
            Console.BackgroundColor = colorPairs.Item2;
        }

        private Tuple<ConsoleColor, ConsoleColor> GetCurrentConsoleColors()
        {
            return Tuple.Create(Console.ForegroundColor, Console.BackgroundColor);
        }

        public void Flush()
        {
            lock (_lock)
            {
                try
                {
                    Console.Out.Flush();
                    Console.Error.Flush();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Flush failed: {ex.Message}");
                }
            }
        }

        public void Dispose()
        {

        }
    }
}