using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace LoadBalancer
{
    public class Program
    {
        public static async Task Main( string[] args )
        {
            try
            {
                using var httpClient = new HttpClient();
                var roundRobinStrategy = new RoundRobinStrategy();
                var loadBalancer = new LoadBalancer( roundRobinStrategy, httpClient );

                //demo servers
                loadBalancer.AddServer( new Server( "localhost", 5001 ) );
                loadBalancer.AddServer( new Server( "localhost", 5002 ) );
                loadBalancer.AddServer( new Server( "localhost", 5003 ) );

                _ = Task.Run( async () =>
                {
                    while( true )
                    {
                        await loadBalancer.PerformHealthChecksAsync();
                        await Task.Delay( TimeSpan.FromSeconds( 10 ) ); 
                    }
                } );

                //simulate incoming requests
                Console.WriteLine( "Load Balancer started. Press Ctrl+C to exit." );
                while( true )
                {
                    var result = await loadBalancer.SendRequestAsync();
                    Console.WriteLine( $"Request result: {( result ? "Success" : "Failed" )}" );
                    await Task.Delay( 1000 ); //simulate request interval
                }
            }
            catch( Exception ex )
            {
                Console.WriteLine( $"An error occurred: {ex.Message}" );
                Environment.Exit( 1 );
            }
        }
    }
}