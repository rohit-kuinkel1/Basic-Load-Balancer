namespace LoadBalancer
{
    public class HealthCheckService
    {
        private readonly HttpClient _httpClient;

        public HealthCheckService( HttpClient httpClient )
        {
            _httpClient = httpClient ?? throw new ArgumentNullException( nameof( httpClient ) );
        }

        public async Task PerformHealthChecksAsync( IServer server )
        {
            if( server is not Server concreteServer )
            {
                return;
            }

            try
            {
                var timeout = TimeSpan.FromMilliseconds( Math.Max( 500, concreteServer.AverageResponseTimeMs * 2 ) );
                using var cts = new CancellationTokenSource( timeout );

                var response = await _httpClient.GetAsync(
                    $"http://{server.ServerAddress}:{server.ServerPort}/health",
                    cts.Token
                );
                concreteServer.UpdateHealthStatus( response.IsSuccessStatusCode );
            }
            catch
            {
                concreteServer.UpdateHealthStatus( false );
            }
        }

    }
}
