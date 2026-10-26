using System.Collections.Concurrent;
using System.Net;
using DevFlow.AspNetCore.HttpClient.Abstractions;
using DevFlow.AspNetCore.HttpClient.Handlers;
using DevFlow.AspNetCore.HttpClient.Options;

namespace DevFlow.AspNetCore.HttpClient.Tests.Handlers;

public sealed class IntegrationHeadersHandlerTests
{
    [Fact]
    public async Task HandlerPropagatesCorrelationIdAndConfiguredApiKey()
    {
        Guid correlationId = Guid.NewGuid();
        var capture = new CaptureHandler();
        using var handler = new IntegrationHeadersHandler(
            CreateOptions("X-Service-Key", "destination-secret"),
            new StubCorrelationAccessor(correlationId))
        {
            InnerHandler = capture,
        };
        using var client = new System.Net.Http.HttpClient(handler);

        await client.GetAsync("https://identity.test/users");

        CapturedHeaders headers = Assert.Single(capture.Requests);
        Assert.Equal(correlationId.ToString("D"), headers.CorrelationId);
        Assert.Equal("destination-secret", headers.ApiKey);
    }

    [Fact]
    public async Task HandlerReplacesIncomingApiKeyAndGeneratesBackgroundCorrelationId()
    {
        var capture = new CaptureHandler();
        using var handler = new IntegrationHeadersHandler(
            CreateOptions("X-Service-Key", "configured-secret"),
            new StubCorrelationAccessor(null))
        {
            InnerHandler = capture,
        };
        using var client = new System.Net.Http.HttpClient(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://identity.test/users");
        request.Headers.TryAddWithoutValidation("X-Service-Key", "incoming-secret");

        await client.SendAsync(request);

        CapturedHeaders headers = Assert.Single(capture.Requests);
        Assert.True(Guid.TryParse(headers.CorrelationId, out _));
        Assert.Equal("configured-secret", headers.ApiKey);
    }

    [Fact]
    public async Task SeparateHandlersDoNotLeakCredentialsAcrossConcurrentRequests()
    {
        var firstCapture = new CaptureHandler();
        var secondCapture = new CaptureHandler();
        using var first = CreateClient("first-key", firstCapture);
        using var second = CreateClient("second-key", secondCapture);

        await Task.WhenAll(
            Enumerable.Range(0, 20).Select(index => index % 2 == 0
                ? first.GetAsync($"https://first.test/{index}")
                : second.GetAsync($"https://second.test/{index}")));

        Assert.All(firstCapture.Requests, request => Assert.Equal("first-key", request.ApiKey));
        Assert.All(secondCapture.Requests, request => Assert.Equal("second-key", request.ApiKey));
    }

    private static System.Net.Http.HttpClient CreateClient(string key, HttpMessageHandler innerHandler)
        => new(new IntegrationHeadersHandler(
            CreateOptions("X-Service-Key", key),
            new StubCorrelationAccessor(Guid.NewGuid()))
        {
            InnerHandler = innerHandler,
        });

    private static IntegrationClientOptions CreateOptions(string header, string key)
        => new() { ApiKeyHeaderName = header, ApiKey = key };

    private sealed class StubCorrelationAccessor(Guid? value) : ICorrelationIdAccessor
    {
        public Guid? GetCorrelationId() => value;
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        public ConcurrentBag<CapturedHeaders> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(new CapturedHeaders(
                request.Headers.GetValues(IntegrationHeadersHandler.CorrelationHeaderName).Single(),
                request.Headers.GetValues("X-Service-Key").Single()));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    private sealed record CapturedHeaders(string CorrelationId, string ApiKey);
}
