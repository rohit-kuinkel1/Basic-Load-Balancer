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

                //simulate incoming requests
                Log.Info( "Load Balancer started with auto-scaling enabled. Press Ctrl+C to exit." );

                var dummyRequest = new HttpRequestMessage( HttpMethod.Get, "http://localhost" );

                while( true )
                {
                    var wasRequestHandled = await loadBalancer.HandleRequestAsync( dummyRequest );
                    if( wasRequestHandled )
                    {
                        Log.Info( $"Request: OK" );
                    }
                    else
                    {
                        Log.Fatal( $"Request: Failed" );
                    }

                    //simulate varying load by randomizing request intervals
                    var randomDelay = Random.Shared.Next( 50, 2000 );
                    await Task.Delay( randomDelay );
                }
            }
            catch( Exception ex ) when( ex is LoadBalancerException )
            {
                Log.Error( "An error occurred", ex );
                Environment.Exit( 1 );
            }
        }
    }
}