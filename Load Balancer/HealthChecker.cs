namespace LoadBalancer
{
    public class HealthCheckService
    {
        private readonly HttpClient _httpClient;

        public HealthCheckService( HttpClient httpClient )
        {
            _httpClient = httpClient ?? throw new ArgumentNullException( nameof( httpClient ) );
        }

        public async Task PerformHealthCheckAsync( IServer iserver )
        {
            if( iserver is not Server server )
            {
                return;
            }

            try
            {
                var timeout = TimeSpan.FromMilliseconds( Math.Max( 500, server.AverageResponseTimeMs * 2 ) );
                using var cts = new CancellationTokenSource( timeout );

                var response = await _httpClient.GetAsync(
                    $"http://{server.ServerAddress}:{server.ServerPort}/health",
                    cts.Token
                );

                var isHealthy = response.IsSuccessStatusCode;
                server.UpdateHealthStatus( isHealthy );

                if( isHealthy )
                {
                    server.CircuitBreaker.RecordSuccess();
                }
                else
                {
                    server.CircuitBreaker.RecordFailure();
                }
            }
            catch
            {
                //in case of an exception, mark the server as unhealthy and record a failure
                server.UpdateHealthStatus( false );
                server.CircuitBreaker.RecordFailure();
            }
        }

    }
}
