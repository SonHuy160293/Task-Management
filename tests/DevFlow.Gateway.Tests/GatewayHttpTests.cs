using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using DevFlow.AspNetCore.DependencyInjection;
using DevFlow.AspNetCore.Middleware;
using DevFlow.Gateway;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog.Core;
using Serilog.Events;
using Xunit;

namespace DevFlow.Gateway.Tests;

public sealed class GatewayHttpTests
{
    private const string Registration = "/identity/api/v1/auth/register";
    private const string Webhook = "/notification/api/v1/webhooks/email/resend";

    [Theory]
    [InlineData(Registration, "/api/v1/auth/register")]
    [InlineData(Webhook, "/api/v1/webhooks/email/resend")]
    public async Task RoutesPreserveRequestBytesHeadersAndQuery(string publicPath, string internalPath)
    {
        await using var rig = await Rig.StartAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, publicPath + "?value=a%2Fb&value=c");
        request.Content = new StringContent("{ \"signed\": \"body\\nbytes\" }", Encoding.UTF8, "application/json");
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer unchanged.token.value");
        request.Headers.Add("svix-signature", "v1,signed-value");
        using var response = await rig.Client.SendAsync(request);
        var body = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(internalPath, body["path"]!.GetValue<string>());
        Assert.Equal("?value=a%2Fb&value=c", body["query"]!.GetValue<string>());
        Assert.Equal("{ \"signed\": \"body\\nbytes\" }", body["body"]!.GetValue<string>());
        Assert.Equal("Bearer unchanged.token.value", body["authorization"]!.GetValue<string>());
        Assert.Equal("v1,signed-value", body["signature"]!.GetValue<string>());
        Assert.Equal(response.Headers.GetValues("X-Correlation-ID").Single(), body["correlation"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("/identity/api/v1/integrations/users/123")]
    [InlineData("/identity/swagger/index.html")]
    [InlineData("/notification/health/ready")]
    [InlineData("/api/v1/auth/register")]
    [InlineData("/tasks/api/v1/tasks")]
    public async Task UnpublishedPathsNeverReachBackend(string path)
    {
        await using var rig = await Rig.StartAsync();
        using var response = await rig.Client.PostAsync(path, new StringContent("{}"));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(0, rig.BackendCalls);
    }

    [Fact]
    public async Task WrongMethodReturns405()
    {
        await using var rig = await Rig.StartAsync();
        using var response = await rig.Client.GetAsync(Registration);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        Assert.Contains("POST", response.Content.Headers.Allow);
    }

    [Theory]
    [InlineData(202)]
    [InlineData(204)]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(429)]
    [InlineData(500)]
    [InlineData(503)]
    public async Task BackendResponsesArePreserved(int status)
    {
        await using var rig = await Rig.StartAsync();
        rig.Respond = async context =>
        {
            context.Response.StatusCode = status;
            context.Response.Headers.WWWAuthenticate = "Bearer realm=backend";
            context.Response.Headers.RetryAfter = "17";
            if (status != 204)
            {
                context.Response.ContentType = "application/custom+json";
                await context.Response.WriteAsync("{\"backend\":true}");
            }
        };
        using var response = await rig.Client.PostAsync(Registration, new StringContent("{}"));
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal("17", response.Headers.GetValues("Retry-After").Single());
        Assert.Equal("Bearer realm=backend", response.Headers.GetValues("WWW-Authenticate").Single());
        Assert.Equal(status == 204 ? null : "application/custom+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(status == 204 ? "" : "{\"backend\":true}", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("malformed")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("11111111-1111-1111-1111-111111111111,22222222-2222-2222-2222-222222222222")]
    [InlineData("{11111111-1111-1111-1111-111111111111}")]
    public async Task CorrelationIsNormalizedAndPropagated(string? supplied)
    {
        await using var rig = await Rig.StartAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, Registration) { Content = new StringContent("{}") };
        if (supplied is not null)
        {
            request.Headers.TryAddWithoutValidation("X-Correlation-ID", supplied);
        }

        using var response = await rig.Client.SendAsync(request);
        string id = response.Headers.GetValues("X-Correlation-ID").Single();
        Assert.True(Guid.TryParseExact(id, "D", out var parsed));
        Assert.NotEqual(Guid.Empty, parsed);
        Assert.Equal(id, (await response.Content.ReadFromJsonAsync<JsonObject>())!["correlation"]!.GetValue<string>());
        if (supplied?.StartsWith('{') == true)
        {
            Assert.Equal(Guid.Parse(supplied).ToString("D"), id);
        }
    }

    [Theory]
    [InlineData("http://localhost:3000", true)]
    [InlineData("https://unapproved.example", false)]
    public async Task PreflightIsHandledLocally(string origin, bool allowed)
    {
        await using var rig = await Rig.StartAsync();
        using var request = new HttpRequestMessage(HttpMethod.Options, Registration);
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "authorization,x-correlation-id,traceparent");
        using var response = await rig.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(allowed, response.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.Equal(0, rig.BackendCalls);
    }

    [Fact]
    public async Task GatewayOwnsCorsOnBackendAndGatewayErrors()
    {
        await using var rig = await Rig.StartAsync();
        rig.Respond = context =>
        {
            context.Response.Headers.AccessControlAllowOrigin = "https://wrong.example";
            context.Response.Headers.AccessControlAllowCredentials = "true";
            context.Response.StatusCode = 400;
            return context.Response.WriteAsync("backend");
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, Registration) { Content = new StringContent("{}") };
        request.Headers.Add("Origin", "http://localhost:3000");
        using var response = await rig.Client.SendAsync(request);
        Assert.Equal("http://localhost:3000", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
        Assert.Equal("backend", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task GatewayFailureKeepsCorsAndCorrelation()
    {
        await using var rig = await Rig.StartAsync(config => config["ReverseProxy"]!["Clusters"]!["identity"]!["Destinations"]!["identity-api"]!["Address"] = "http://127.0.0.1:1/");
        using var request = new HttpRequestMessage(HttpMethod.Post, Registration) { Content = new StringContent("{}") };
        request.Headers.Add("Origin", "http://localhost:3000");
        using var response = await rig.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal("http://localhost:3000", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        string correlation = response.Headers.GetValues("X-Correlation-ID").Single();
        var body = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal(correlation, body["error"]!["traceId"]!.GetValue<string>());
    }

    [Fact]
    public async Task W3cTraceIdSurvivesWithChildSpanAndTrustedForwarding()
    {
        await using var rig = await Rig.StartAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, Registration) { Content = new StringContent("{}") };
        const string traceId = "12345678901234567890123456789012";
        request.Headers.Add("traceparent", "00-" + traceId + "-1234567890123456-01");
        request.Headers.Add("X-Forwarded-For", "203.0.113.19");
        request.Headers.Add("X-Forwarded-Proto", "https");
        using var response = await rig.Client.SendAsync(request);
        var body = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal(traceId, body["traceId"]!.GetValue<string>());
        Assert.Contains(traceId, body["traceparent"]!.GetValue<string>());
        Assert.DoesNotContain("-1234567890123456-", body["traceparent"]!.GetValue<string>());
        Assert.Equal("203.0.113.19", body["remoteIp"]!.GetValue<string>());
        Assert.Equal("https", body["scheme"]!.GetValue<string>());
    }

    [Fact]
    public async Task UntrustedForwardingCannotSpoofClientOrScheme()
    {
        await using var rig = await Rig.StartAsync(overrides: new() { ["ForwardedHeaders:KnownProxies:0"] = "192.0.2.1" });
        using var request = new HttpRequestMessage(HttpMethod.Post, Registration) { Content = new StringContent("{}") };
        request.Headers.Add("X-Forwarded-For", "203.0.113.19");
        request.Headers.Add("X-Forwarded-Proto", "https");
        using var response = await rig.Client.SendAsync(request);
        var body = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal("127.0.0.1", body["remoteIp"]!.GetValue<string>());
        Assert.Equal("http", body["scheme"]!.GetValue<string>());
    }

    [Theory]
    [InlineData(Registration, 16385, false)]
    [InlineData(Registration, 16385, true)]
    [InlineData(Webhook, 65537, false)]
    [InlineData(Webhook, 65537, true)]
    public async Task OversizedBodiesAreRejected(string route, int size, bool chunked)
    {
        await using var rig = await Rig.StartAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, route);
        request.Content = chunked ? new ChunkedContent(size) : new ByteArrayContent(new byte[size]);
        using var response = await rig.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [Fact]
    public async Task ConnectionFailureDoesNotBreakReadinessOrOtherRoute()
    {
        await using var rig = await Rig.StartAsync(config => config["ReverseProxy"]!["Clusters"]!["identity"]!["Destinations"]!["identity-api"]!["Address"] = "http://127.0.0.1:1/");
        using var failed = await rig.Client.PostAsync(Registration, new StringContent("{}"));
        Assert.Equal(HttpStatusCode.BadGateway, failed.StatusCode);
        Assert.Contains("Gateway.ForwardingFailed", await failed.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, (await rig.Client.GetAsync("/health/ready")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await rig.Client.PostAsync(Webhook, new StringContent("{}"))).StatusCode);
    }

    [Fact]
    public async Task EmptyDestinationSetReturns503()
    {
        await using var rig = await Rig.StartAsync(config => config["ReverseProxy"]!["Clusters"]!["identity"]!["Destinations"] = new JsonObject());
        using var response = await rig.Client.PostAsync(Registration, new StringContent("{}"));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("Gateway.Unavailable", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task RedirectAndEmptyErrorRemainUnmodified()
    {
        await using var rig = await Rig.StartAsync();
        rig.Respond = context =>
        {
            context.Response.StatusCode = 307;
            context.Response.Headers.Location = "https://example.test/redirect";
            return Task.CompletedTask;
        };
        using var redirect = await rig.Client.PostAsync(Registration, new StringContent("{}"));
        Assert.Equal(HttpStatusCode.TemporaryRedirect, redirect.StatusCode);
        Assert.Equal("https://example.test/redirect", redirect.Headers.Location!.AbsoluteUri);
        Assert.Equal(1, rig.BackendCalls);
        rig.Respond = context => { context.Response.StatusCode = 400; return Task.CompletedTask; };
        using var empty = await rig.Client.PostAsync(Registration, new StringContent("{}"));
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.Empty(await empty.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task ClientCancellationAbortsDownstream()
    {
        await using var rig = await Rig.StartAsync();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        rig.Respond = async context =>
        {
            started.TrySetResult();
            try { await Task.Delay(TimeSpan.FromSeconds(20), context.RequestAborted); }
            catch (OperationCanceledException) { cancelled.TrySetResult(); }
        };
        using var cancellation = new CancellationTokenSource();
        Task<HttpResponseMessage> request = rig.Client.PostAsync(Registration, new StringContent("{}"), cancellation.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task InterruptedResponseIsAbortedWithoutAnAppendedEnvelope()
    {
        await using var rig = await Rig.StartAsync();
        rig.Respond = async context =>
        {
            context.Response.ContentLength = 1000;
            await context.Response.WriteAsync("partial-backend-response");
            await context.Response.Body.FlushAsync();
            await Task.Delay(100);
            context.Abort();
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, Registration) { Content = new StringContent("{}") };
        using var response = await rig.Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await Assert.ThrowsAnyAsync<HttpRequestException>(() => response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task FailureBeforeBodyDoesNotReuseBackendEncodingOrLength()
    {
        await using var rig = await Rig.StartAsync();
        rig.Respond = async context =>
        {
            context.Response.Headers.ContentEncoding = "gzip";
            context.Response.ContentLength = 1000;
            await context.Response.StartAsync();
            await Task.Delay(100);
            context.Abort();
        };
        using var response = await rig.Client.PostAsync(Registration, new StringContent("{}"));
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Empty(response.Content.Headers.ContentEncoding);
        Assert.Contains("Gateway.ForwardingFailed", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("AllowedHosts", "*")]
    [InlineData("ForwardedHeaders:KnownProxies:0", "")]
    [InlineData("Cors:AllowedOrigins:0", "http://frontend.example.test")]
    [InlineData("Cors:AllowedOrigins:0", "https://frontend.example.test/")]
    [InlineData("Cors:AllowedOrigins:0", "https://user:password@frontend.example.test")]
    public void ProductionRejectsUnsafeIngressSettings(string key, string value)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AllowedHosts"] = "api.example.test", ["ForwardedHeaders:KnownProxies:0"] = "127.0.0.1",
            ["Cors:AllowedOrigins:0"] = "https://frontend.example.test",
        });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { [key] = value });
        Assert.Throws<InvalidOperationException>(() => GatewayApplication.Build(builder));
    }

    [Fact]
    public async Task InvalidStartupConfigurationFails()
    {
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => Rig.StartAsync(config => config["ReverseProxy"]!["Routes"]!["identity-register"]!["ClusterId"] = "missing"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DeadlineCancelsBackendAndReturns504(bool routeDeadline)
    {
        await using var rig = await Rig.StartAsync(config =>
        {
            if (routeDeadline) { config["ReverseProxy"]!["Routes"]!["identity-register"]!["Timeout"] = "00:00:00.2"; }
            else { config["ReverseProxy"]!["Clusters"]!["identity"]!["HttpRequest"]!["ActivityTimeout"] = "00:00:00.2"; }
        });
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        rig.Respond = async context =>
        {
            try { await Task.Delay(TimeSpan.FromSeconds(10), context.RequestAborted); }
            catch (OperationCanceledException) { cancelled.TrySetResult(); }
        };
        using var response = await rig.Client.PostAsync(Registration, new StringContent("{}"));
        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
        Assert.Contains("Gateway.Timeout", await response.Content.ReadAsStringAsync());
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ReloadAcceptsValidFileAndRetainsLastGoodOnMalformedAndInvalidFiles()
    {
        await using var rig = await Rig.StartAsync();
        var config = JsonNode.Parse(await File.ReadAllTextAsync(rig.RoutesFile))!;
        config["ReverseProxy"]!["Routes"]!["identity-register"]!["Transforms"]![0] = new JsonObject { ["PathSet"] = "/reloaded" };
        await File.WriteAllTextAsync(rig.RoutesFile, config.ToJsonString());
        await rig.WaitForPathAsync("/reloaded");
        await File.WriteAllTextAsync(rig.RoutesFile, "{invalid-json");
        await rig.WaitForRejectionAsync(1);
        await rig.WaitForPathAsync("/reloaded");
        int rejected = rig.Logs.Events.Count(entry => entry.MessageTemplate.Text == "Gateway configuration rejected");
        config["ReverseProxy"]!["Routes"]!["identity-register"]!["ClusterId"] = "missing";
        await File.WriteAllTextAsync(rig.RoutesFile, config.ToJsonString());
        await rig.WaitForRejectionAsync(rejected + 1);
        await rig.WaitForPathAsync("/reloaded");
    }

    [Fact]
    public async Task LogsContainOperationalFieldsWithoutSensitiveData()
    {
        await using var rig = await Rig.StartAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, Registration + "?secret=private-query") { Content = new StringContent("private-body") };
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer private-token");
        request.Headers.Add("svix-signature", "private-signature");
        using var response = await rig.Client.SendAsync(request);
        var completion = Assert.Single(rig.Logs.Events.Where(entry => entry.MessageTemplate.Text.StartsWith("Gateway {", StringComparison.Ordinal)));
        foreach (string field in new[] { "RouteId", "ClusterId", "DestinationId", "StatusCode", "ElapsedMilliseconds", "CorrelationId", "TraceId", "SpanId" })
        {
            Assert.True(completion.Properties.ContainsKey(field), field);
        }

        string logs = string.Join('\n', rig.Logs.Events.Select(entry => entry.RenderMessage() + string.Join(',', entry.Properties)));
        Assert.DoesNotContain("private-", logs);
        Assert.DoesNotContain("127.0.0.1", logs);
        Assert.All(rig.Logs.Events, entry => Assert.Null(entry.Exception));
    }

    private sealed class ChunkedContent(int size) : HttpContent
    {
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync(new byte[size]).AsTask();
    }

    private sealed class CaptureSink : ILogEventSink
    {
        public ConcurrentQueue<LogEvent> Events { get; } = new();
        public void Emit(LogEvent logEvent) => Events.Enqueue(logEvent);
    }

    private sealed class Rig : IAsyncDisposable
    {
        private WebApplication _gateway = null!;
        private WebApplication _backend = null!;
        private readonly ActivityListener _listener = new() { ShouldListenTo = _ => true, Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded };
        private int _calls;
        public int BackendCalls => _calls;
        public string RoutesFile { get; private set; } = string.Empty;
        public HttpClient Client { get; private set; } = null!;
        public CaptureSink Logs { get; } = new();
        public Func<HttpContext, Task>? Respond { get; set; }

        public static async Task<Rig> StartAsync(Action<JsonNode>? configure = null, Dictionary<string, string?>? overrides = null)
        {
            var rig = new Rig();
            ActivitySource.AddActivityListener(rig._listener);
            var backendBuilder = WebApplication.CreateBuilder();
            backendBuilder.Logging.ClearProviders();
            backendBuilder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
            backendBuilder.Services.AddTrustedForwarding(backendBuilder.Configuration);
            rig._backend = backendBuilder.Build();
            rig._backend.UseForwardedHeaders();
            rig._backend.UseCorrelationId();
            rig._backend.Run(async context =>
            {
                Interlocked.Increment(ref rig._calls);
                if (rig.Respond is { } respond) { await respond(context); return; }
                using var reader = new StreamReader(context.Request.Body);
                string body = await reader.ReadToEndAsync(context.RequestAborted);
                await context.Response.WriteAsJsonAsync(new
                {
                    path = context.Request.Path.Value, query = context.Request.QueryString.Value, body,
                    authorization = context.Request.Headers.Authorization.ToString(), signature = context.Request.Headers["svix-signature"].ToString(),
                    correlation = context.TraceIdentifier, traceId = Activity.Current?.TraceId.ToString(), traceparent = context.Request.Headers["traceparent"].ToString(),
                    remoteIp = context.Connection.RemoteIpAddress?.ToString(), scheme = context.Request.Scheme,
                });
            });
            await rig._backend.StartAsync();
            string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/Gateway/DevFlow.Gateway"));
            JsonNode config = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(root, "proxy.json")))!;
            foreach (var cluster in config["ReverseProxy"]!["Clusters"]!.AsObject())
            {
                foreach (var destination in cluster.Value!["Destinations"]!.AsObject()) { destination.Value!["Address"] = Address(rig._backend); }
            }

            configure?.Invoke(config);
            rig.RoutesFile = Path.Combine(Path.GetTempPath(), "devflow-gateway-" + Guid.NewGuid().ToString("N") + ".json");
            await File.WriteAllTextAsync(rig.RoutesFile, config.ToJsonString());
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = root, EnvironmentName = "Development", ApplicationName = typeof(GatewayApplication).Assembly.FullName });
            builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Gateway:RoutesFile"] = rig.RoutesFile, ["ForwardedHeaders:KnownProxies:0"] = "127.0.0.1" });
            if (overrides is not null) { builder.Configuration.AddInMemoryCollection(overrides); }
            builder.Services.AddSingleton<ILogEventSink>(rig.Logs);
            try
            {
                rig._gateway = GatewayApplication.Build(builder);
                await rig._gateway.StartAsync();
            }
            catch
            {
                await rig.DisposeAsync();
                throw;
            }
            rig.Client = new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false }) { BaseAddress = new Uri(Address(rig._gateway)), Timeout = TimeSpan.FromSeconds(10) };
            return rig;
        }

        public async Task WaitForPathAsync(string expected)
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            while (true)
            {
                using var response = await Client.PostAsync(Registration, new StringContent("{}"), deadline.Token);
                var body = (await response.Content.ReadFromJsonAsync<JsonObject>(deadline.Token))!;
                if (body["path"]?.GetValue<string>() == expected) { return; }
                await Task.Delay(50, deadline.Token);
            }
        }

        public async Task WaitForRejectionAsync(int minimum)
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            while (Logs.Events.Count(entry => entry.MessageTemplate.Text == "Gateway configuration rejected") < minimum) { await Task.Delay(50, deadline.Token); }
        }

        public async ValueTask DisposeAsync()
        {
            Client?.Dispose();
            if (_gateway is not null) { await _gateway.DisposeAsync(); }
            await _backend.DisposeAsync();
            _listener.Dispose();
            File.Delete(RoutesFile);
        }

        private static string Address(WebApplication app) => app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single() + "/";
    }
}
