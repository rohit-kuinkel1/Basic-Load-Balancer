using LoadBalancer.Logger;
using LoadBalancer.Interfaces;

namespace LoadBalancer
{
    public class Program
    {
        public static async Task Main( string[] args )
        {
            try
            {
                Log.AddSink( LogSinks.ConsoleAndFile, Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.Desktop ), "LoadBalancerLogs" ) );

                var loadBalancer = new LoadBalancer(
                    new RoundRobinStrategy(),
                    new HttpClient()
                );

                var circuitBreakerConfig = CircuitBreakerConfig.Factory();

                //demo servers
                loadBalancer.AddServer( new Server( "localhost", 5001, circuitBreakerConfig ) );
                loadBalancer.AddServer( new Server( "localhost", 5002, circuitBreakerConfig ) );
                loadBalancer.AddServer( new Server( "localhost", 5003, circuitBreakerConfig ) );

                _ = Task.Run( async () =>
                {
                    while( true )
                    {
                        await loadBalancer.PerformHealthChecksAsync();
                        await Task.Delay( TimeSpan.FromSeconds( 10 ) );
                    }
                } );

                //simulate incoming requests
                Log.Info( "Load Balancer started. Press Ctrl+C to exit." );
                while( true )
                {
                    if( await loadBalancer.SendRequestAsync() )
                    {
                        Log.Info( $"Request: OK" );
                    }
                    else
                    {
                        Log.Fatal( $"Request: Failed" );
                    }

                    await Task.Delay( 1000 ); //simulate request interval
                }
            }
            catch( Exception ex )
            {
                Log.Error( "An error occurred", ex );
                Environment.Exit( 1 );
            }
        }
    }
}