using System.Collections.Concurrent;
using LoadBalancer.Logger;

namespace LoadBalancer.RequestCache
{
    public class CachedRequestsManager
    {
        private readonly ConcurrentQueue<CachedRequest> _failedRequests = new(); //FIFO
        private readonly Func<HttpRequestMessage, Task<bool>> _handleRequest;
        private readonly Timer _retryTimer;
        private const int RetryIntervalMs = 5000;
        private bool _isProcessing;

        public CachedRequestsManager( Func<HttpRequestMessage, Task<bool>> handleRequest )
        {
            _handleRequest = handleRequest;
           // _retryTimer = new Timer( ProcessFailedRequests, null, RetryIntervalMs, RetryIntervalMs );
        }

        public void CacheFailedRequest( HttpRequestMessage request )
        {
            var cachedRequest = new CachedRequest( request );
            _failedRequests.Enqueue( cachedRequest );
            Log.Info( $"Request cached. Queue size: {_failedRequests.Count}" );
        }

        public CachedRequest? GetNextCachedRequest()
        {
            _failedRequests.TryDequeue( out CachedRequest? request );
            Log.Debug( $"Dequeued cached request from {request?.Timestamp}" );
            return request;
        }

        public bool HasCachedRequests()
        {
            return _failedRequests.Count != 0;
        }

        public int GetCachedRequestCount()
        {
            return _failedRequests.Count;
        }

        //public async Task<bool> ProcessNextRequest()
        //{
        //    if( _failedRequests.IsEmpty || _isProcessing )
        //    {
        //        return false;
        //    }

        //    try
        //    {
        //        if( _failedRequests.TryPeek( out CachedRequest? cachedRequest ) )
        //        {
        //            var wasRequestHandled = await _handleRequest( cachedRequest.Request );
        //            if( wasRequestHandled )
        //            {
        //                _failedRequests.TryDequeue( out _ );
        //                Log.Info( $"Cached request from {cachedRequest.Timestamp} processed successfully. Remaining: {_failedRequests.Count}" );
        //                return true;
        //            }
        //        }
        //    }
        //    catch( Exception ex )
        //    {
        //        Log.Error( "Error processing cached request", ex );
        //    }

        //    return false;
        //}

        //private async void ProcessFailedRequests( object? __ )
        //{
        //    if( _isProcessing || _failedRequests.IsEmpty )
        //    {
        //        return;
        //    }

        //    _isProcessing = true;

        //    try
        //    {
        //        while( _failedRequests.TryPeek( out var cachedRequest ) )
        //        {
        //            var wasRequestHandled = await _handleRequest( cachedRequest.Request );

        //            if( wasRequestHandled )
        //            {
        //                _failedRequests.TryDequeue( out _ );
        //                Log.Info( $"Cached request from {cachedRequest.Timestamp} processed successfully. Remaining: {_failedRequests.Count}" );
        //            }
        //            else
        //            {
        //                //if current request fails, stop processing and wait for next retry interval
        //                break;
        //            }
        //        }
        //    }
        //    catch( Exception ex )
        //    {
        //        Log.Error( "Error processing cached requests", ex );
        //    }
        //    finally
        //    {
        //        _isProcessing = false;
        //    }
        //}

        public async Task DisposeAsync()
        {
            await _retryTimer.DisposeAsync();
        }
    }
}