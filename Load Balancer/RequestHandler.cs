
using System.Diagnostics;

namespace LoadBalancer
{
    public class RequestHandler
    {
        private readonly HttpClient _httpClient;

        public RequestHandler( HttpClient httpClient )
        {
            _httpClient = httpClient ?? throw new ArgumentNullException( nameof( httpClient ) );
        }

        public async Task<bool> SendRequestAsync( IServer server )
        {
            if( server is not Server concreteServer )
            {
                return false;
            }

            try
            {
                Interlocked.Increment( ref concreteServer._activeConnections );

                var timeout = TimeSpan.FromMilliseconds( Math.Max( 500, concreteServer.AverageResponseTimeMs * 2 ) );
                using var cts = new CancellationTokenSource( timeout );
                var stopwatch = Stopwatch.StartNew();

                var response = await _httpClient.GetAsync(
                    $"http://{server.ServerAddress}:{server.ServerPort}/api",
                    cts.Token
                );
                stopwatch.Stop();

                concreteServer.RecordRequest( response.IsSuccessStatusCode, stopwatch.ElapsedMilliseconds );

                return response.IsSuccessStatusCode;
            }
            catch
            {
                concreteServer.RecordRequest( false, 0 );
                return false;
            }
            finally
            {
                Interlocked.Decrement( ref concreteServer._activeConnections );
            }
        }
    }
}
