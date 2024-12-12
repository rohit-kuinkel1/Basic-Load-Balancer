using SimpleServer.Interfaces;

namespace SimpleServer
{
    public class MetricsService : IMetricsService
    {
        private readonly Queue<long> _responseTimes = new();
        private readonly object _lock = new();
        private readonly Random _random = new();
        private const int MAX_SAMPLES = 100;

        public void RecordRequest( long durationMs )
        {
            lock( _lock )
            {
                _responseTimes.Enqueue( durationMs );
                if( _responseTimes.Count > MAX_SAMPLES )
                {
                    _responseTimes.Dequeue();
                }
            }
        }

        public int SimulateLatency()
        {
            //simulate varying response times between 50-200ms
            return _random.Next( 50, 200 );
        }

        public double GetAverageResponseTime()
        {
            lock( _lock )
            {
                return _responseTimes.Any()
                    ? _responseTimes.Average()
                    : 100; //default average if no requests yet
            }
        }
    }
}