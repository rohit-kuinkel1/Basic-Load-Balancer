using System.Diagnostics;
using System.Net.Http;
using Microsoft.AspNetCore.Http;
using SimpleServer.Interfaces;

namespace SimpleServer.Middleware;

public class RequestMetricsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IMetricsService _metricsService;

    public RequestMetricsMiddleware(RequestDelegate next, IMetricsService metricsService)
    {
        _next = next;
        _metricsService = metricsService;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            _metricsService.RecordRequest(stopwatch.ElapsedMilliseconds);
        }
    }
}