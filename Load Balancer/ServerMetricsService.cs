using System.Collections.Concurrent;
using LoadBalancer.Interfaces;

namespace LoadBalancer
{
    public class ServerMetricsService
    {
        private readonly ConcurrentDictionary<IServer, List<ServerMetric>> _serverMetricsHistory;
        private const int METRICS_HISTORY_LIMIT = 5;

        public ServerMetricsService()
        {
            _serverMetricsHistory = new ConcurrentDictionary<IServer, List<ServerMetric>>();
        }

        public void TrackServerLoad( IServer server, double load )
        {
            var metric = new ServerMetric
            {
                Timestamp = DateTime.UtcNow,
                Load = load
            };

            _serverMetricsHistory.AddOrUpdate(
                server,
                new List<ServerMetric> { metric },
                ( _, history ) =>
                {
                    history.Add( metric );
                    if( history.Count > METRICS_HISTORY_LIMIT )
                    {
                        history.RemoveAt( 0 );
                    }
                    return history;
                }
            );
        }

        public double GetCurrentAverageLoad()
        {
            if( !_serverMetricsHistory.Any() )
                return 0;

            return _serverMetricsHistory
                .Select( kvp => kvp.Value.LastOrDefault()?.Load ?? 0 )
                .Average();
        }

        public Dictionary<IServer, double> GetHistoricalAverageLoads()
        {
            return _serverMetricsHistory.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Select( m => m.Load ).Average()
            );
        }

        public void RemoveServerMetrics( IServer server )
        {
            _serverMetricsHistory.TryRemove( server, out _ );
        }

        public void CleanupOldMetrics( TimeSpan threshold )
        {
            var cutoffTime = DateTime.UtcNow - threshold;
            foreach( var server in _serverMetricsHistory.Keys )
            {
                _serverMetricsHistory.AddOrUpdate(
                    server,
                    new List<ServerMetric>(),
                    ( _, metrics ) =>
                    {
                        metrics.RemoveAll( m => m.Timestamp < cutoffTime );
                        return metrics;
                    }
                );
            }
        }
    }

    public class ServerMetric
    {
        public DateTime Timestamp { get; set; }
        public double Load { get; set; }
    }
}