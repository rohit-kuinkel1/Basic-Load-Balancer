namespace LoadBalancer.Logger
{
    public enum LogLevel
    {
        TRC = 0,
        DBG = 1,
        INF = 2,
        WRN = 3,
        ERR = 4,
        FTL = 5
    }

    public enum LogSinks
    {
        Console = 0,
        File = 1,
        ConsoleAndFile = 2,
    }
}