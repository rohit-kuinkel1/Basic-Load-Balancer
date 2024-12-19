using System.Collections.Concurrent;
using System.Diagnostics;
using LoadBalancer.Exceptions;
using LoadBalancer.Interfaces;
using LoadBalancer.Logger;

namespace LoadBalancer
{
    public enum ScaleDownTendency
    {
        //values represent seconds in the past from the present, so Aggressive = -5 representing DateTime.Now - 5sec
        Aggressive = -5,
        Normal = -30,
        Lenient = -60,
    }

    public class AutoScaler : IAutoScaler
    {
        private readonly AutoScalingConfig _config;
        private readonly ConcurrentDictionary<DateTime, int> _requestMetrics = new();
        private readonly object _scalingLock = new object();
        private readonly Func<Server> _serverFactory;
        private readonly Action<IServer> _addServerCallback;
        private readonly Action<bool> _removeServerCallback;
        private readonly Func<int> _getCurrentServerCount;

        public AutoScaler(
            AutoScalingConfig config,
            Func<Server> serverFactory,
            Action<IServer> addServerCallback,
            Action<bool> removeServerCallback,
            Func<int> getCurrentServerCount )
        {
            _config = config ?? throw new ArgumentNullException( nameof( config ) );
            _serverFactory = serverFactory ?? throw new ArgumentNullException( nameof( serverFactory ) );
            _addServerCallback = addServerCallback ?? throw new ArgumentNullException( nameof( addServerCallback ) );
            _removeServerCallback = removeServerCallback ?? throw new ArgumentNullException( nameof( removeServerCallback ) );
            _getCurrentServerCount = getCurrentServerCount ?? throw new ArgumentNullException( nameof( getCurrentServerCount ) );
        }

        public int GetRequestCountForTimeWindow( int seconds )
        {
            var currentTime = DateTime.UtcNow;
            var windowStartTime = currentTime.AddSeconds( -seconds );

            var recentRequests = _requestMetrics
                .Where( kvp => kvp.Key > windowStartTime )
                .Sum( kvp => kvp.Value );

            return recentRequests;
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
                    await Task.Delay( _config.ScaleCheckIntervalMs );
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

                //makes sure that the scaling down isnt too aggressive, if the time is for example -5 seconds, then the scale down is too aggressive
                var recentRequests = _requestMetrics
                    .Where( kvp => kvp.Key > currentTime.AddSeconds( (double)ScaleDownTendency.Normal ) )
                    .Sum( kvp => kvp.Value );

                CleanupOldMetrics();
                EvaluateAndScale( recentRequests );
            }
            catch( Exception ex ) when( ex is LoadBalancerException )
            {
                Log.Error( "Error in auto-scaling monitoring", ex );
            }
        }

        private void CleanupOldMetrics( DateTime? currentTime = null )
        {
            var currentTime1 = currentTime ?? DateTime.UtcNow;

            var oldMetrics = _requestMetrics.Keys.Where( k => k < currentTime1.AddMinutes( -5 ) );
            foreach( var key in oldMetrics )
            {
                _requestMetrics.TryRemove( key, out _ );
            }
        }

        private void EvaluateAndScale( int recentRequestsCount )
        {
            lock( _scalingLock )
            {
                var currentServerCount = _getCurrentServerCount();
                var isScalingUpNecessary = ShouldScaleUp( recentRequestsCount, currentServerCount );
                if( isScalingUpNecessary )
                {
                    SpawnNewServer();
                    Log.Info( $"Scaling up: Added new server. Total servers: {currentServerCount + 1}" );
                }
                else if( ShouldScaleDown( recentRequestsCount, currentServerCount ) )
                {
                    _removeServerCallback( true );
                    Log.Info( $"Scaling down: Removed a server. Total servers: {currentServerCount - 1}" );
                }
            }
        }

        /// <summary>
        /// only scale up if we are getting more requests than the max threshold authorized for 
        /// scale up in <see cref="AutoScalingConfig"/> and if the current server count is
        /// less than that allowed in the <see cref="AutoScalingConfig"/>.
        /// </summary>
        /// <param name="recentRequests"></param>
        /// <param name="currentServerCount"></param>
        /// <returns></returns>
        private bool ShouldScaleUp( int recentRequests, int currentServerCount )
            => recentRequests > _config.NumberOfTotalMaxRequestForScaleUp &&
               currentServerCount < _config.MaxServers;

        /// <summary>
        /// same logic as above but mirrored
        /// </summary>
        /// <param name="recentRequests"></param>
        /// <param name="currentServerCount"></param>
        /// <returns></returns>
        private bool ShouldScaleDown( int recentRequests, int currentServerCount )
            => recentRequests < _config.NumberOfTotalMinRequestForScaleDown &&
               currentServerCount > _config.MinServers;

        private void SpawnNewServer( int? preferredPort = null )
        {
            if( _getCurrentServerCount() >= _config.MaxServers )
            {
                Log.Fatal( $"Cannot instantiate more servers since {nameof( _config )} allows " +
                    $"max {_config.MaxServers} servers and {_getCurrentServerCount()} servers are already up." );
                return;
            }

            var port = preferredPort ?? PortUtils.FindAvailablePort();
            //hard coded for now , remove it later
            var scriptPath = @"D:\git\Basic-Load-Balancer\SimpleServerSetup\simpleserversetup.ps1";

            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-File \"{scriptPath}\" start {port}",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                process.Start();

                Log.Info( $"Spawing up a new server instance localhost:{port}" );
                var server = new Server( "localhost", port, CircuitBreakerConfig.Factory() );

                //register this new server with the load balancer
                _addServerCallback( server );
            }
            catch( Exception ex ) when( ex is TimeoutException or LoadBalancerException )
            {
                Log.Error( $"Failed to spawn a new server on port {port}, releasing this port: {ex.Message}" );
                PortUtils.ReleasePort( port );
            }
        }

        public void KillServer( int port )
        {
            var scriptPath = @"D:\git\Basic-Load-Balancer\SimpleServerSetup\simpleserversetup.ps1";

            try
            {
                using( var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-File \"{scriptPath}\" kill {port}",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                } )
                {
                    process.Start();
                    process.WaitForExit();
                    if( process.ExitCode != 0 )
                    {
                        Log.Warn( $"Process to kill server on port {port} exited with code {process.ExitCode}" );
                    }
                }
            }
            catch( Exception ex )
            {
                Log.Error( $"Failed to kill server on port {port}: {ex.Message}" );
            }
        }


    }
}