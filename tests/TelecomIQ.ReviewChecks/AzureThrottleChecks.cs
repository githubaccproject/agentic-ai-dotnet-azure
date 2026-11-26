using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Azure.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using TelecomIQ.Domain;
using TelecomIQ.Infrastructure;

internal static class AzureThrottleChecks
{
    public static async Task Run()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Azure:OpenAI"] = "https://openai.example",
            ["Azure:EmbeddingDeployment"] = "embedding",
            ["Azure:Search"] = "https://search.example"
        }).Build();

        var fixture = new StubAzureHttp(
            Throttled("0", retryAfterSeconds: 90), Json("""{"data":[{"embedding":[1]}]}"""),
            Throttled(retryAfterSeconds: 0), Json("""{"value":[]}"""));
        using var http = new HttpClient(fixture);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var credential = new StubCredential();
        var azure = new AzureRest(http, credential, config, NullLogger<AzureRest>.Instance);
        var ct = timeout.Token;
        Require((await azure.Embed("sample", ct)).SequenceEqual([1f]), "An embedding should recover from a 429.");
        Require((await azure.Retrieve([new PolicyReference(Guid.NewGuid(), 1)], "sample", [1], ct)).Length == 0,
            "Policy search should recover from a 429 using the Search scope.");
        Require(fixture.Requests.Count == 4 && fixture.Requests[0] == fixture.Requests[1]
            && fixture.Requests[2] == fixture.Requests[3], "Each retry must resend the same URL and JSON.");
        Require(credential.Scopes.SequenceEqual([
            "https://cognitiveservices.azure.com/.default", "https://cognitiveservices.azure.com/.default",
            "https://search.azure.com/.default", "https://search.azure.com/.default"]), "Azure scopes must match the target service.");

        await ExpectFailure(config, [Throttled("0"), Throttled("0"), Throttled("0"), Throttled("0")],
            HttpStatusCode.TooManyRequests, 4);
        await ExpectFailure(config, [Throttled(retryAfterSeconds: 180)], HttpStatusCode.TooManyRequests, 1);
        await ExpectFailure(config, [new HttpResponseMessage(HttpStatusCode.Forbidden)], HttpStatusCode.Forbidden, 1);

        var delayed = new StubAzureHttp(Throttled(retryAfterSeconds: 60));
        using var delayedHttp = new HttpClient(delayed);
        using var cancel = new CancellationTokenSource();
        var delayedAzure = new AzureRest(delayedHttp, credential, config, NullLogger<AzureRest>.Instance);
        var pending = delayedAzure.Embed("sample", cancel.Token);
        await delayed.Sent.Task.WaitAsync(ct);
        cancel.Cancel();
        try
        {
            await pending.WaitAsync(ct);
            throw new InvalidOperationException("Cancellation should stop retry waiting.");
        }
        catch (OperationCanceledException) when (cancel.IsCancellationRequested && !ct.IsCancellationRequested) { }
        Require(delayed.Requests.Count == 1, "Cancellation must prevent sending a retry.");
    }

    private static async Task ExpectFailure(IConfiguration config, HttpResponseMessage[] responses,
        HttpStatusCode status, int attempts)
    {
        var fixture = new StubAzureHttp(responses);
        using var http = new HttpClient(fixture);
        var azure = new AzureRest(http, new StubCredential(), config, NullLogger<AzureRest>.Instance);
        try
        {
            await azure.Embed("sample", CancellationToken.None);
            throw new InvalidOperationException("An HttpRequestException was expected.");
        }
        catch (HttpRequestException e) when (e.StatusCode == status)
        {
            Require(e.Message.Contains("/embeddings", StringComparison.Ordinal), "The error should identify the failing operation.");
            Require(fixture.Requests.Count == attempts, "The request must stop at the expected retry budget.");
        }
    }

    private static HttpResponseMessage Throttled(string? milliseconds = null, int? retryAfterSeconds = null)
    {
        var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        if (milliseconds is not null) response.Headers.Add("retry-after-ms", milliseconds);
        if (retryAfterSeconds is int seconds) response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(seconds));
        return response;
    }

    private static HttpResponseMessage Json(string content) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(content, Encoding.UTF8, "application/json")
    };

    private static void Require(bool condition, string error)
    {
        if (!condition) throw new InvalidOperationException(error);
    }

    private sealed class StubAzureHttp(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        public List<(Uri?, string)> Requests { get; } = [];
        public TaskCompletionSource Sent { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var response = responses[Requests.Count];
            Requests.Add((request.RequestUri, await request.Content!.ReadAsStringAsync(ct)));
            Sent.TrySetResult();
            return response;
        }
    }

    private sealed class StubCredential : TokenCredential
    {
        public List<string> Scopes { get; } = [];

        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            Scopes.Add(requestContext.Scopes.Single());
            return new AccessToken("fixture-token", DateTimeOffset.UtcNow.AddHours(1));
        }

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            ValueTask.FromResult(GetToken(requestContext, cancellationToken));
    }
}
