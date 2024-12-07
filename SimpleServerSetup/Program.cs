using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using SimpleServer.Middleware;
using SimpleServer.Services;


namespace SimpleServer
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddSingleton<IMetricsService, MetricsService>();

            var app = builder.Build();
            app.UseMiddleware<RequestMetricsMiddleware>();
            app.MapGet("/health", async (IMetricsService metricsService) =>
            {
                await Task.Delay(metricsService.SimulateLatency());
                return Results.Ok("Healthy");
            });

            app.MapGet("/api", async (IMetricsService metricsService) =>
            {
                await Task.Delay(metricsService.SimulateLatency());
                return Results.Ok(new { message = $"Response from server on port {builder.Configuration["urls"]}" });
            });

            var port = args.Length > 0 ? args[0] : "5001";
            app.Urls.Add($"http://localhost:{port}");

            app.Run();
        }
    }
}
