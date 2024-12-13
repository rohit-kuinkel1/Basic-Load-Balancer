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
                Log.SetMinimumLevel(LogLevel.Trace);
                Log.AddSink(
                    LogSinks.ConsoleAndFile,
                    Path.Combine(
                        Environment.GetFolderPath( Environment.SpecialFolder.Desktop ),
                        "LoadBalancerLogs"
                    )
                );

                //we will make sure of singelton later on with builder services
                var loadBalancer = new LoadBalancer(
                                        loadBalancingStrategy: new RoundRobinStrategy(),
                                        httpClient: new HttpClient(),
                                        enabledAutoScaling: true,
                                        autoScalingConfig: AutoScalingConfig.Factory(),
                                        healthCheckInterval:TimeSpan.FromSeconds(10),
                                        minHealthThreshold: 90
                );

                List<(int DurationInSeconds, int RequestsPerSecond)> TrafficPatterns = new()
                {
                    (20, 4),
                    (5, 1000), 
                    (9, 4000),    
                    (5, 20000),                
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