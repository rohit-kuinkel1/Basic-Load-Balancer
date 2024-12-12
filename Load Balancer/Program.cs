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
                Log.AddSink( LogSinks.ConsoleAndFile,
                    Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.Desktop ), "LoadBalancerLogs" ) );

                var autoScalingConfig = new AutoScalingConfig
                {
                    MinServers = 2,
                    MaxServers = 5,
                    RequestThresholdForScaleUp = 50,
                    RequestThresholdForScaleDown = 10,
                    ScaleCheckIntervalSec = TimeSpan.FromSeconds( 30 )
                };

                var loadBalancer = new LoadBalancer(
                    new RoundRobinStrategy(),
                    new HttpClient(),
                    autoScalingConfig
                );

                _ = Task.Run( async () =>
                {
                    while( true )
                    {
                        await loadBalancer.PerformHealthChecksAsync();
                        await Task.Delay( TimeSpan.FromSeconds( 10 ) );
                    }
                } );

                //simulate incoming requests
                Log.Info( "Load Balancer started with auto-scaling enabled. Press Ctrl+C to exit." );

                var dummyRequest = new HttpRequestMessage( HttpMethod.Get, "http://localhost" );

                while( true )
                {
                    if( await loadBalancer.HandleRequestAsync( dummyRequest ) )
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
            catch( Exception ex ) when (ex is LoadBalancerException)
            {
                Log.Error( "An error occurred", ex );
                Environment.Exit( 1 );
            }
        }
    }
}