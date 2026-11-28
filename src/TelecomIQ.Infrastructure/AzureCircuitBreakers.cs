using System.Collections.Concurrent;
using System.Net;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;

namespace TelecomIQ.Infrastructure;

internal static class AzureCircuitBreakers
{
    private static readonly ConcurrentDictionary<string, ResiliencePipeline<HttpResponseMessage>> Pipelines = new();

    internal static ResiliencePipeline<HttpResponseMessage> For(Uri endpoint, ILogger logger)
    {
        var path = endpoint.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var key = endpoint.GetLeftPart(UriPartial.Authority);
        // Isolate chat and embedding deployments; search and extraction use separate hosts.
        if (path.Length >= 3 && path[0] == "openai" && path[1] == "deployments")
            key += "/openai/deployments/" + path[2];

        return Pipelines.GetOrAdd(key, dependency =>
            new ResiliencePipelineBuilder<HttpResponseMessage>()
                .AddCircuitBreaker(new CircuitBreakerStrategyOptions<HttpResponseMessage>
                {
                    FailureRatio = 0.5,
                    MinimumThroughput = 3,
                    SamplingDuration = TimeSpan.FromMinutes(3),
                    BreakDuration = TimeSpan.FromMinutes(1),
                    ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                        .Handle<HttpRequestException>(e => e.StatusCode is null)
                        .Handle<OperationCanceledException>(e => e.InnerException is TimeoutException)
                        .HandleResult(r => r.StatusCode == HttpStatusCode.TooManyRequests ||
                            r.StatusCode == HttpStatusCode.RequestTimeout || (int)r.StatusCode >= 500),
                    OnOpened = args =>
                    {
                        logger.LogWarning("Azure circuit opened for {Dependency} for {Duration}", dependency, args.BreakDuration);
                        return ValueTask.CompletedTask;
                    },
                    OnHalfOpened = args =>
                    {
                        logger.LogInformation("Azure circuit half-open for {Dependency}", dependency);
                        return ValueTask.CompletedTask;
                    },
                    OnClosed = args =>
                    {
                        logger.LogInformation("Azure circuit closed for {Dependency}", dependency);
                        return ValueTask.CompletedTask;
                    }
                }).Build());
    }
}
