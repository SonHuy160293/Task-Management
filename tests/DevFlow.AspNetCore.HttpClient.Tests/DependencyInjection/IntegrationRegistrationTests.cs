using System.Net;
using DevFlow.AspNetCore.HttpClient.Abstractions;
using DevFlow.AspNetCore.HttpClient.DependencyInjection;
using DevFlow.AspNetCore.HttpClient.Options;
using DevFlow.SharedKernel.Integrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DevFlow.AspNetCore.HttpClient.Tests.DependencyInjection;

public sealed class IntegrationRegistrationTests
{
    [Fact]
    public void RegistrationRejectsMissingRequiredConfiguration()
    {
        IConfiguration configuration = CreateConfiguration(apiKey: null);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIntegrationHttpClient<ITestClient, TestClient>("Identity", configuration.GetSection("Integrations:Identity"));
        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptionsMonitor<IntegrationClientOptions>>().Get("Identity"));
    }

    [Fact]
    public async Task RegisteredClientUsesBaseUrlAndDestinationHeaders()
    {
        IConfiguration configuration = CreateConfiguration("identity-secret");
        var handler = new SequenceHandler([HttpStatusCode.OK]);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ICorrelationIdAccessor>(new CorrelationAccessor(Guid.Parse("11111111-1111-1111-1111-111111111111")));
        services.AddIntegrationHttpClient<ITestClient, TestClient>("Identity", configuration.GetSection("Integrations:Identity"))
            .ConfigurePrimaryHttpMessageHandler(() => handler);
        using ServiceProvider provider = services.BuildServiceProvider();

        ITestClient client = provider.GetRequiredService<ITestClient>();
        IntegrationResult<string> response = await client.SendAsync(HttpMethod.Get);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new Uri("https://identity.test/resource"), handler.LastRequestUri);
        Assert.Equal("identity-secret", handler.LastApiKey);
    }

    [Fact]
    public async Task RegisteredClientRetriesGetButDoesNotRetryPostOrUnauthorized()
    {
        Assert.Equal(3, await CountAttemptsAsync(HttpMethod.Get, [
            HttpStatusCode.InternalServerError,
            HttpStatusCode.InternalServerError,
            HttpStatusCode.OK]));
        Assert.Equal(1, await CountAttemptsAsync(HttpMethod.Post, [
            HttpStatusCode.InternalServerError,
            HttpStatusCode.OK]));
        Assert.Equal(1, await CountAttemptsAsync(HttpMethod.Get, [
            HttpStatusCode.Unauthorized,
            HttpStatusCode.OK]));
    }

    [Fact]
    public async Task RegisteredClientEnforcesAttemptTimeoutWithoutRetryingPost()
    {
        IConfiguration configuration = CreateConfiguration(
            "secret",
            retryAttempts: 2,
            attemptTimeout: TimeSpan.FromMilliseconds(20));
        var handler = new DelayedHandler();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIntegrationHttpClient<ITestClient, TestClient>("Identity", configuration.GetSection("Integrations:Identity"))
            .ConfigurePrimaryHttpMessageHandler(() => handler);
        using ServiceProvider provider = services.BuildServiceProvider();

        IntegrationResult<string> result = await provider.GetRequiredService<ITestClient>().SendAsync(HttpMethod.Post);
        Assert.Equal(IntegrationFailureCategory.Timeout, result.Error?.Category);
        Assert.Equal(1, handler.Attempts);
    }

    [Fact]
    public async Task RegisteredClientOpensCircuitAndFailsFast()
    {
        IConfiguration configuration = CreateConfiguration(
            "secret",
            retryAttempts: 1,
            attemptTimeout: TimeSpan.FromMilliseconds(20),
            minimumThroughput: 4,
            samplingDuration: TimeSpan.FromMilliseconds(500),
            breakDuration: TimeSpan.FromMilliseconds(500));
        var handler = new SequenceHandler([
            HttpStatusCode.InternalServerError,
            HttpStatusCode.InternalServerError,
            HttpStatusCode.InternalServerError,
            HttpStatusCode.InternalServerError,
            HttpStatusCode.OK]);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIntegrationHttpClient<ITestClient, TestClient>("Identity", configuration.GetSection("Integrations:Identity"))
            .ConfigurePrimaryHttpMessageHandler(() => handler);
        using ServiceProvider provider = services.BuildServiceProvider();
        ITestClient client = provider.GetRequiredService<ITestClient>();

        IntegrationResult<string> first = await client.SendAsync(HttpMethod.Get);
        IntegrationResult<string> second = await client.SendAsync(HttpMethod.Get);
        IntegrationResult<string> result = await client.SendAsync(HttpMethod.Get);
        Assert.Equal(IntegrationFailureCategory.CircuitOpen, result.Error?.Category);

        Assert.Equal(4, handler.Attempts);
    }

    private static async Task<int> CountAttemptsAsync(HttpMethod method, IEnumerable<HttpStatusCode> statuses)
    {
        IConfiguration configuration = CreateConfiguration("secret");
        var handler = new SequenceHandler(statuses);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIntegrationHttpClient<ITestClient, TestClient>("Identity", configuration.GetSection("Integrations:Identity"))
            .ConfigurePrimaryHttpMessageHandler(() => handler);
        using ServiceProvider provider = services.BuildServiceProvider();

        IntegrationResult<string> response = await provider.GetRequiredService<ITestClient>().SendAsync(method);
        return handler.Attempts;
    }

    private static IConfiguration CreateConfiguration(
        string? apiKey,
        int retryAttempts = 2,
        TimeSpan? attemptTimeout = null,
        int minimumThroughput = 20,
        TimeSpan? samplingDuration = null,
        TimeSpan? breakDuration = null)
    {
        var values = new Dictionary<string, string?>
        {
            ["Integrations:Identity:BaseUrl"] = "https://identity.test/",
            ["Integrations:Identity:ApiKeyHeaderName"] = "X-Service-Key",
            ["Integrations:Identity:ApiKey"] = apiKey,
            ["Integrations:Identity:Resilience:TotalRequestTimeout:Timeout"] = "00:00:05",
            ["Integrations:Identity:Resilience:AttemptTimeout:Timeout"] = (attemptTimeout ?? TimeSpan.FromSeconds(1)).ToString("c"),
            ["Integrations:Identity:Resilience:Retry:MaxRetryAttempts"] = retryAttempts.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["Integrations:Identity:Resilience:Retry:Delay"] = "00:00:00.001",
            ["Integrations:Identity:Resilience:Retry:BackoffType"] = "Constant",
            ["Integrations:Identity:Resilience:Retry:UseJitter"] = "false",
            ["Integrations:Identity:Resilience:CircuitBreaker:FailureRatio"] = "0.5",
            ["Integrations:Identity:Resilience:CircuitBreaker:MinimumThroughput"] = minimumThroughput.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["Integrations:Identity:Resilience:CircuitBreaker:SamplingDuration"] = (samplingDuration ?? TimeSpan.FromSeconds(30)).ToString("c"),
            ["Integrations:Identity:Resilience:CircuitBreaker:BreakDuration"] = (breakDuration ?? TimeSpan.FromSeconds(15)).ToString("c"),
        };

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    public interface ITestClient
    {
        Task<IntegrationResult<string>> SendAsync(HttpMethod method);
    }

    public sealed class TestClient(IIntegrationHttpClient<ITestClient> client) : ITestClient
    {
        public Task<IntegrationResult<string>> SendAsync(HttpMethod method)
            => method == HttpMethod.Post
                ? client.PostAsync<object, string>("resource", new object())
                : client.GetAsync<string>("resource");
    }

    private sealed class CorrelationAccessor(Guid correlationId) : ICorrelationIdAccessor
    {
        public Guid? GetCorrelationId() => correlationId;
    }

    private sealed class SequenceHandler(IEnumerable<HttpStatusCode> statuses) : HttpMessageHandler
    {
        private readonly Queue<HttpStatusCode> _statuses = new(statuses);

        public int Attempts { get; private set; }

        public Uri? LastRequestUri { get; private set; }

        public string? LastApiKey { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Attempts++;
            LastRequestUri = request.RequestUri;
            LastApiKey = request.Headers.TryGetValues("X-Service-Key", out IEnumerable<string>? values)
                ? values.Single()
                : null;
            HttpStatusCode status = _statuses.Count > 0 ? _statuses.Dequeue() : HttpStatusCode.OK;
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent("{\"success\":true,\"data\":\"ok\",\"error\":null}"),
            });
        }
    }

    private sealed class DelayedHandler : HttpMessageHandler
    {
        public int Attempts { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Attempts++;
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}
