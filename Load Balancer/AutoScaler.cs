using System.Collections.Concurrent;
using System.Diagnostics;
using LoadBalancer.Exceptions;
using LoadBalancer.Interfaces;
using LoadBalancer.Logger;

namespace LoadBalancer
{
    public class AutoScaler : IAutoScaler
    {
        private readonly AutoScalingConfig _config;
        private readonly ConcurrentDictionary<DateTime, int> _requestMetrics = new();
        private readonly object _scalingLock = new object();
        private readonly Func<Server> _serverFactory;
        private readonly Action<IServer> _addServerCallback;
        private readonly Action _removeServerCallback;
        private readonly Func<int> _getCurrentServerCount;
        private readonly ConcurrentDictionary<IServer, DateTime> _serverLoadHistory = new();
        private const double MAX_SERVER_CAPACITY_THRESHOLD = 0.9; // 90% capacity threshold
        private const int SCALE_DOWN_CHECK_MINUTES = 5; //check server load for last 5 minutes before scaling down
        private readonly ConcurrentBag<IServer> _servers;

        public AutoScaler(
            AutoScalingConfig config,
            Func<Server> serverFactory,
            Action<IServer> addServerCallback,
            Action removeServerCallback,
            Func<int> getCurrentServerCount,
            ConcurrentBag<IServer> servers )
        {
            _config = config ?? throw new ArgumentNullException( nameof( config ) );
            _serverFactory = serverFactory ?? throw new ArgumentNullException( nameof( serverFactory ) );
            _addServerCallback = addServerCallback ?? throw new ArgumentNullException( nameof( addServerCallback ) );
            _removeServerCallback = removeServerCallback ?? throw new ArgumentNullException( nameof( removeServerCallback ) );
            _getCurrentServerCount = getCurrentServerCount ?? throw new ArgumentNullException( nameof( getCurrentServerCount ) );
            _servers = servers ?? throw new ArgumentNullException( nameof( servers ) );
        }

        public void Initialize()
        {
            for( int i = 0; i < _config.MinServers; i++ )
            {
                SpawnNewServer();
            }

            StartMonitoringTask();
        }

        private void StartMonitoringTask()
        {
            Task.Run( async () =>
            {
                while( true )
                {
                    await MonitorAndScaleAsync();
                    await Task.Delay( _config.ScaleCheckIntervalSec );
                }
            } );
        }

        public void ScaleUp()
        {
            lock( _scalingLock )
            {
                var currentServerCount = _getCurrentServerCount();
                if( currentServerCount < _config.MaxServers )
                {
                    SpawnNewServer();
                    Log.Info( $"Emergency scale-up triggered. Total servers: {currentServerCount + 1}" );
                }
            }
        }

        public void TrackRequest( DateTime timestamp )
        {
            _requestMetrics.AddOrUpdate(
                timestamp,
                1,
                ( _, count ) => count + 1
            );
        }

        public async Task MonitorAndScaleAsync()
        {
            try
            {
                var currentTime = DateTime.UtcNow;
                var recentRequests = _requestMetrics
                    .Where( kvp => kvp.Key > currentTime.AddSeconds( -30 ) )
                    .Sum( kvp => kvp.Value );

                CleanupOldMetrics( currentTime );
                await EvaluateAndScaleAsync( recentRequests );
            }
            catch( Exception ex ) when( ex is LoadBalancerException )
            {
                Log.Error( "Error in auto-scaling monitoring", ex );
            }
        }

        private void CleanupOldMetrics( DateTime currentTime )
        {
            var oldMetrics = _requestMetrics.Keys.Where( k => k < currentTime.AddMinutes( -5 ) );
            foreach( var key in oldMetrics )
            {
                _requestMetrics.TryRemove( key, out _ );
            }

            // Cleanup old server load history
            var oldLoadHistory = _serverLoadHistory
                .Where( kvp => kvp.Value < currentTime.AddMinutes( -SCALE_DOWN_CHECK_MINUTES ) )
                .Select( kvp => kvp.Key );

            foreach( var server in oldLoadHistory )
            {
                _serverLoadHistory.TryRemove( server, out _ );
            }
        }

        private async Task EvaluateAndScaleAsync( int recentRequests )
        {
            lock( _scalingLock )
            {
                var currentServerCount = _getCurrentServerCount();
                var serverLoads = GetServerLoads();

                if( ShouldScaleUp( serverLoads ) )
                {
                    SpawnNewServer();
                    Log.Info( $"Scaling up: Added new server due to high load. Total servers: {currentServerCount + 1}" );
                }
                else if( ShouldScaleDown( serverLoads, currentServerCount ) )
                {
                    _removeServerCallback();
                    Log.Info( $"Scaling down: Removed server. Total servers: {currentServerCount - 1}" );
                }
            }
        }

        private Dictionary<IServer, double> GetServerLoads()
        {
            var serverLoads = new Dictionary<IServer, double>();

            foreach( var server in _servers )
            {
                // Calculate load percentage based on current load and max capacity
                double loadPercentage = (double)server.CurrentLoad / server.MaxCapacity;
                serverLoads[server] = loadPercentage;
            }

            return serverLoads;
        }

        private bool ShouldScaleUp( Dictionary<IServer, double> serverLoads )
        {
            // Scale up if any server is near capacity threshold
            return serverLoads.Any( load => load.Value >= MAX_SERVER_CAPACITY_THRESHOLD ) &&
                   _getCurrentServerCount() < _config.MaxServers;
        }

        private bool ShouldScaleDown( Dictionary<IServer, double> serverLoads, int currentServerCount )
        {
            if( currentServerCount <= _config.MinServers )
                return false;

            //check if we have consistently low load across all servers
            var averageLoad = serverLoads.Average( x => x.Value );
            var timeWithLowLoad = _serverLoadHistory
                .Where( history => history.Value >= DateTime.UtcNow.AddMinutes( -SCALE_DOWN_CHECK_MINUTES ) )
                .Count();

            return averageLoad < 0.3 && // 30% capacity
                   timeWithLowLoad >= SCALE_DOWN_CHECK_MINUTES;
        }

        private void SpawnNewServer()
        {
            var port = PortUtils.FindAvailablePort();
            var scriptPath = @"D:\git\Basic-Load-Balancer\SimpleServerSetup\start_servers.ps1";
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-File \"{scriptPath}\" -Port {port}",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                process.Start();

                Log.Info( $"Spawning up a new server instance localhost:{port}" );
                var server = new Server( "localhost", port, CircuitBreakerConfig.Factory() );
                _addServerCallback( server );
            }
            catch( Exception ex ) when( ex is TimeoutException or LoadBalancerException )
            {
                Log.Error( $"Failed to spawn a new server on port {port}: {ex.Message}" );
                PortUtils.ReleasePort( port );
            }
        }
    }
}