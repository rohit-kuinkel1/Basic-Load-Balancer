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

        public AutoScaler(
            AutoScalingConfig config,
            Func<Server> serverFactory,
            Action<IServer> addServerCallback,
            Action removeServerCallback,
            Func<int> getCurrentServerCount )
        {
            _config = config ?? throw new ArgumentNullException( nameof( config ) );
            _serverFactory = serverFactory ?? throw new ArgumentNullException( nameof( serverFactory ) );
            _addServerCallback = addServerCallback ?? throw new ArgumentNullException( nameof( addServerCallback ) );
            _removeServerCallback = removeServerCallback ?? throw new ArgumentNullException( nameof( removeServerCallback ) );
            _getCurrentServerCount = getCurrentServerCount ?? throw new ArgumentNullException( nameof( getCurrentServerCount ) );
        }

        public void Initialize()
        {
            for( int i = 0; i < _config.MinServers; i++ )
            {
                SpawnNewServer();
            }

            Task.Run( async () =>
            {
                while( true )
                {
                    await MonitorAndScaleAsync();
                    await Task.Delay( _config.ScaleCheckIntervalSec );
                }
            } );
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
                EvaluateAndScale( recentRequests );
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
        }

        private void EvaluateAndScale( int recentRequests )
        {
            lock( _scalingLock )
            {
                var currentServerCount = _getCurrentServerCount();

                if( ShouldScaleUp( recentRequests, currentServerCount ) )
                {
                    SpawnNewServer();
                    Log.Info( $"Scaling up: Added new server. Total servers: {currentServerCount + 1}" );
                }
                else if( ShouldScaleDown( recentRequests, currentServerCount ) )
                {
                    _removeServerCallback();
                    Log.Info( $"Scaling down: Removed server. Total servers: {currentServerCount - 1}" );
                }
            }
        }

        private bool ShouldScaleUp( int recentRequests, int currentServerCount )
            => recentRequests > _config.MaxRequestThresholdForScaleUp &&
               currentServerCount < _config.MaxServers;

        private bool ShouldScaleDown( int recentRequests, int currentServerCount )
            => recentRequests < _config.MinRequestThresholdForScaleDown &&
               currentServerCount > _config.MinServers;

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

                Log.Info( $"Spawing up a new server instance localhost:{port}" );
                var server = new Server( "localhost", port, CircuitBreakerConfig.Factory() );
                _addServerCallback( server );
            }
            catch( Exception ex ) when( ex is TimeoutException or LoadBalancerException )
            {
                {
                    Log.Error( $"Failed to spawn a new server on port {port}: {ex.Message}" );
                    PortUtils.ReleasePort( port );
                }
            }
        }
    }
}