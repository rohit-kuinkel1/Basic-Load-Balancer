using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using SimpleServer.Interfaces;
using SimpleServer.Middleware;
using System.Diagnostics;
using System.Linq;
using LoadBalancer.Logger;
namespace SimpleServer
{
    public class Program
    {
        public static void Main( string[] args )
        {
            Log.AddSink(LogSinks.Console);
            Log.SetMinimumLevel( LogLevel.TRC );

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

            Console.WriteLine( "Usage: \n- To start the server: start [port] \n- To kill process on port: kill [port]" );
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
                Log.Debug( $"Found process with PID {processId} occupying port {port}" );
                if( processId == -1 )
                {
                    Log.Warn( $"No process is running on port {port}." );
                    return;
                }

                KillProcess( processId );
            }
            catch( Exception ex )
            {
                Log.Error( $"Error killing process: {ex.Message}" );
            }
        }

        private static int GetProcessIdUsingPort( string port )
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "netstat",
                Arguments = $"-ano | findstr :{port}",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using( var process = Process.Start( startInfo ) )
            {
                using( var reader = process?.StandardOutput )
                {
                    var output = reader?.ReadToEnd();
                    if( string.IsNullOrEmpty( output ) )
                    {
                        return -1;
                    }

                    var lines = output.Split( new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries );
                    var pid = lines.FirstOrDefault()?.Split( new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries ).Last();
                    return int.TryParse( pid, out var processId ) ? processId : -1;
                }
            }
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
