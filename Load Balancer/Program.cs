using LoadBalancer.Exceptions;
using LoadBalancer.Logger;

namespace LoadBalancer
{
    public class Program
    {
        public static async Task Main( string[] args )
        {
            try
            {
                Log.AddSink(
                    LogSinks.ConsoleAndFile,
                    Path.Combine(
                        Environment.GetFolderPath( Environment.SpecialFolder.Desktop ),
                        "LoadBalancerLogs"
                    )
                );

                var autoScalingConfig = AutoScalingConfig.Factory();

                var loadBalancer = new LoadBalancer(
                                        loadBalancingStrategy: new RoundRobinStrategy(),
                                        httpClient: new HttpClient(),
                                        enabledAutoScaling: true,
                                        autoScalingConfig: autoScalingConfig,
                                        minHealthThreshold: 90
                );

                var dummyRequest = new HttpRequestMessage( HttpMethod.Get, "http://localhost" );

                List<(int DurationInSeconds, int RequestsPerSecond)> TrafficPatterns = new()
                {
                    (20, 1000), // High load: 1000 req/sec for 30 seconds
                    (9, 40),    // Low load: 40 req/sec for 9 seconds
                    (15, 200),  // Moderate load: 200 req/sec for 15 seconds
                    (20, 500),  // Burst: 500 req/sec for 20 seconds
                };

                foreach( var pattern in TrafficPatterns )
                {
                    await SimulateTraffic( loadBalancer, pattern.RequestsPerSecond, pattern.DurationInSeconds );
                }
            }
            catch( Exception ex ) when( ex is LoadBalancerException )
            {
                Log.Error( "An error occurred", ex );
                Environment.Exit( 1 );
            }
        }

        private static async Task SimulateTraffic( LoadBalancer loadBalancer, int requestsPerSecond, int durationInSeconds )
        {
            Log.Info( $"Simulating traffic: {requestsPerSecond} requests/second for {durationInSeconds} seconds." );

            var tasks = new List<Task>();
            var endTime = DateTime.UtcNow.AddSeconds( durationInSeconds );

            while( DateTime.UtcNow < endTime )
            {
                for( int i = 0; i < requestsPerSecond; i++ )
                {
                    tasks.Add( Task.Run( async () =>
                    {
                        var dummyRequest = new HttpRequestMessage( HttpMethod.Get, "http://localhost" );
                        var wasRequestHandled = await loadBalancer.HandleRequestAsync( dummyRequest );
                        if( wasRequestHandled )
                        {
                            Log.Info( "Request: OK" );
                        }
                        else
                        {
                            Log.Fatal( "Request: Failed" );
                        }
                    } ) );
                }

                await Task.Delay( 1000 ); //pause for 1 sec to maintain the RPS
            }

            await Task.WhenAll( tasks );
            Log.Info( $"Finished traffic simulation: {requestsPerSecond} requests/second." );
        }
    }
}