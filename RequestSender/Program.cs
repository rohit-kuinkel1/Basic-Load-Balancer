using LoadBalancer.Exceptions;
using LoadBalancer.Logger;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace LoadBalancer
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            try
            {
                Log.SetMinimumLevel(LogLevel.Trace);
                Log.AddSink(
                    LogSinks.ConsoleAndFile,
                    Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                        "LoadBalancerLogs"
                    )
                );

                var serviceProvider = Program.BuildServiceProvider();
                var loadBalancer = serviceProvider.GetRequiredService<LoadBalancer>();

                List<(int DurationInSeconds, int RequestsPerSecond)> TrafficPatterns = new()
                {
                    (10, 1),
                    (5, 1000),
                    (9, 40000),
                    (5, 20000),
                };

                foreach (var pattern in TrafficPatterns)
                {
                    await SimulateTraffic(loadBalancer, pattern.RequestsPerSecond, pattern.DurationInSeconds);
                }
            }
            catch (Exception ex) when (ex is LoadBalancerException)
            {
                Log.Error("An error occurred", ex);
                Environment.Exit(1);
            }
        }

        private static ServiceProvider BuildServiceProvider()
        {
            var services = new ServiceCollection();

            services.AddSingleton<LoadBalancer>(provider =>
            {
                return new LoadBalancer(
                    loadBalancingStrategy: new RoundRobinStrategy(),
                    httpClient: new HttpClient(),
                    enabledAutoScaling: true,
                    autoScalingConfig: AutoScalingConfig.Factory(),
                    healthCheckInterval: TimeSpan.FromSeconds(10),
                    minHealthThreshold: 90
                );
            });

            return services.BuildServiceProvider();
        }

        private static async Task SimulateTraffic(LoadBalancer loadBalancer, int requestsPerSecond, int durationInSeconds)
        {
            Log.Info($"Simulating traffic: {requestsPerSecond} requests/second for {durationInSeconds} seconds.");

            var tasks = new List<Task>();
            var endTime = DateTime.UtcNow.AddSeconds(durationInSeconds);

            while (DateTime.UtcNow < endTime)
            {
                for (int i = 0; i < requestsPerSecond; i++)
                {
                    tasks.Add(Task.Run(async () =>
                    {
                        var dummyRequest = new HttpRequestMessage(HttpMethod.Get, "http://localhost");
                        var wasRequestHandled = await loadBalancer.HandleRequestAsync(dummyRequest);
                        if (wasRequestHandled)
                        {
                            Log.Info("Request: OK");
                        }
                        else
                        {
                            Log.Fatal("Request: Failed");
                        }
                    }));
                }

                await Task.Delay(2000);
            }

            await Task.WhenAll(tasks);
            Log.Info($"Finished traffic simulation: {requestsPerSecond} requests/second.");
        }
    }
}
