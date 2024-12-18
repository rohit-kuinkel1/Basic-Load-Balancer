using System.Collections.Concurrent;
using LoadBalancer.Logger;

namespace LoadBalancer.RequestCache
{
    public class CachedRequestsManager
    {
        private readonly ConcurrentQueue<CachedRequest> _failedRequests = new();
        private readonly Func<HttpRequestMessage, Task<bool>> _handleRequest;
        private readonly Timer _retryTimer;
        private const int RetryIntervalMs = 5000;
        private bool _isProcessing;

        public CachedRequestsManager( Func<HttpRequestMessage, Task<bool>> handleRequest )
        {
            _handleRequest = handleRequest;
            _retryTimer = new Timer( ProcessFailedRequests, null, RetryIntervalMs, RetryIntervalMs );
        }

        public void CacheFailedRequest( HttpRequestMessage request )
        {
            var cachedRequest = new CachedRequest( request );
            _failedRequests.Enqueue( cachedRequest );
            Log.Info( $"Request cached. Queue size: {_failedRequests.Count}" );
        }

        private async void ProcessFailedRequests( object? __ )
        {
            if( _isProcessing || _failedRequests.IsEmpty )
            {
                return;
            }

            _isProcessing = true;

            try
            {
                while( _failedRequests.TryPeek( out var cachedRequest ) )
                {
                    var wasRequestHandled = await _handleRequest( cachedRequest.Request );

                    if( wasRequestHandled )
                    {
                        _failedRequests.TryDequeue( out _ );
                        Log.Info( $"Cached request processed successfully. Remaining: {_failedRequests.Count}" );
                    }
                    else
                    {
                        //if current request fails, stop processing and wait for next retry interval
                        break;
                    }
                }
            }
            catch( Exception ex )
            {
                Log.Error( "Error processing cached requests", ex );
            }
            finally
            {
                _isProcessing = false;
            }
        }

        public async Task DisposeAsync()
        {
            await _retryTimer.DisposeAsync();
        }
    }
}