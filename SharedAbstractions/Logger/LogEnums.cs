namespace LoadBalancer.Logging
{
    public enum LogLevel
    {
        Trace = 0,
        Debug = 1,
        Info = 2,
        Warn = 3,
        Error = 4,
        Fatal = 5
    }

    public enum LogDestination
    {
        Console = 0,
        File = 1,
        ConsoleAndFile = 2,
    }
}