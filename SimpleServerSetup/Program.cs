using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using SimpleServer.Interfaces;
using SimpleServer.Middleware;
using System.Diagnostics;
using System.Linq;
using LoadBalancer.Logger;
using Microsoft.Extensions.Logging;
using LoadBalancer;
namespace SimpleServer
{
    public class Program
    {
        public static void Main( string[] args )
        {
            try
            {
                Task.Delay( 3000 );
                Log.AddSink(
                       LogSinks.ConsoleAndFile,
                       Path.Combine(
                           Environment.GetFolderPath( Environment.SpecialFolder.Desktop ),
                           "LoadBalancerLogs"
                       )
                );
                Log.SetMinimumLevel( LoadBalancer.Logger.LogLevel.TRC );

                if( args.Length > 0 )
                {
                    if( args[0].ToLowerInvariant() == "kill" && args.Length > 1 )
                    {
                        KillProcessOnPort( args[1] );
                        return;
                    }
                    else if( args[0].ToLowerInvariant() == "start" )
                    {
                        StartServer( args.Length > 1 ? args[1] : "5001" );
                        return;
                    }
                }

                Console.WriteLine( "Usage: \n- To start the server: start [port(int)] \n- To kill process on port: kill [port(int)]" );
            }
            catch( Exception ex )
            {
                Log.Error( $"An exception occured", ex );
                Console.ReadLine();
            }
        }

        private static void StartServer( string port )
        {
            var builder = WebApplication.CreateBuilder();

            builder.Services.AddSingleton<IMetricsService, MetricsService>();

            var app = builder.Build();
            app.UseMiddleware<RequestMetricsMiddleware>();

            app.MapGet( "/health", async ( IMetricsService metricsService ) =>
            {
                await Task.Delay( metricsService.SimulateLatency() );
                return Results.Ok( "Healthy" );
            } );

            app.MapGet( "/api", async ( IMetricsService metricsService ) =>
            {
                await Task.Delay( metricsService.SimulateLatency() );
                return Results.Ok(
                    new
                    {
                        message = $"Response from server on port {port}"
                    }
                );
            } );

            app.Urls.Add( $"http://localhost:{port}" );

            Console.WriteLine( $"Starting server on port {port}..." );
            app.Run();
        }

        private static void KillProcessOnPort( string port )
        {
            try
            {
                var processId = GetProcessIdUsingPort( port );
                if( processId == -1 )
                {
                    Log.Warn( $"No process with PID {processId} found that is occupying port {port}." );
                    return;
                }

                Log.Debug( $"Found process with PID {processId} occupying port {port}" );
                KillProcess( processId );            
            }
            catch( Exception ex )
            {
                Log.Error( $"Error killing process: {ex.Message}" );
            }
        }

        private static int GetProcessIdUsingPort( string port )
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "netstat",
                    Arguments = "-ano",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                Log.Debug( $"Trying to get PID for the process occupying port {port}" );

                using( var process = Process.Start( startInfo ) )
                {
                    if( process == null )
                    {
                        Log.Error( "Failed to start process for netstat." );
                        return -1;
                    }

                    using( var reader = process.StandardOutput )
                    {
                        var output = reader.ReadToEnd();
                        Log.Debug( $"Netstat raw output:\n{output}" );

                        if( string.IsNullOrEmpty( output ) )
                        {
                            Log.Warn( "Netstat output was empty." );
                            return -1;
                        }

                        var lines = output.Split( new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries )
                                          .Where( line => line.Contains( $":{port}" ) );

                        foreach( var line in lines )
                        {
                            Log.Debug( $"{line}" );
                            var parts = line.Split( new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries );
                            if( parts.Length > 4 && int.TryParse( parts[^1], out var processId ) )
                            {
                                return processId;
                            }
                        }
                    }
                }
            }
            catch( Exception ex )
            {
                Log.Error( $"Error in GetProcessIdUsingPort: {ex.Message}" );
            }

            return -1;
        }


        private static void KillProcess( int processId )
        {
            Log.Debug( $"Killing Process with PID: {processId}" );
            var startInfo = new ProcessStartInfo
            {
                FileName = "taskkill",
                Arguments = $"/PID {processId} /F",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using( var process = Process.Start( startInfo ) )
            {
                process?.WaitForExit();
                try
                {
                    Process.GetProcessById( processId );
                    Log.Error( $"Process with PID {processId} still exists..." );
                }
                catch( ArgumentException )
                {
                    Log.Debug( $"Successfully killed process with PID {processId}." );
                }              
            }
        }
    }
}
