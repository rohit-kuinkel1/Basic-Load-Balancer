using System.Collections.Concurrent;
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
        private readonly Action<IServer> _removeServerCallback;
        private readonly Func<int> _getCurrentServerCount;
        private int _nextPort = 5001;

        public AutoScaler(
            AutoScalingConfig config,
            Func<Server> serverFactory,
            Action<IServer> addServerCallback,
            Action<IServer> removeServerCallback,
            Func<int> getCurrentServerCount )
        {
            _config = config ?? throw new NullException( nameof( config ) );
            _serverFactory = serverFactory ?? throw new NullException( nameof( serverFactory ) );
            _addServerCallback = addServerCallback ?? throw new NullException( nameof( addServerCallback ) );
            _removeServerCallback = removeServerCallback ?? throw new NullException( nameof( removeServerCallback ) );
            _getCurrentServerCount = getCurrentServerCount ?? throw new NullException( nameof( getCurrentServerCount ) );
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
                    await Task.Delay( _config.ScaleCheckInterval );
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
                    var newServer = _serverFactory();
                    _removeServerCallback( newServer );
                    Log.Info( $"Scaling down: Removed server. Total servers: {currentServerCount - 1}" );
                }
            }
        }

        private bool ShouldScaleUp( int recentRequests, int currentServerCount )
        {
            return 
                recentRequests > _config.RequestThresholdForScaleUp
                && currentServerCount < _config.MaxServers;
        }

        private bool ShouldScaleDown( int recentRequests, int currentServerCount )
        {
            return
                recentRequests < _config.RequestThresholdForScaleDown
                && currentServerCount > _config.MinServers;
        }

        private void SpawnNewServer()
        {
            //for now we just host it locally and assume that the port is always free, will be changed later where we scan for free ports first
            var newServer = new Server( "localhost", _nextPort++, CircuitBreakerConfig.Factory() );
            _addServerCallback( newServer );
        }
    }
}