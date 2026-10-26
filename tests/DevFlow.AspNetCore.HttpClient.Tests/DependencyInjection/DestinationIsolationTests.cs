using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using DevFlow.AspNetCore.HttpClient.Abstractions;
using DevFlow.AspNetCore.HttpClient.DependencyInjection;
using DevFlow.AspNetCore.HttpClient.Options;
using DevFlow.Identity.Contracts;
using DevFlow.SharedKernel.Integrations;
using DevFlow.Tasks.Application.Abstractions.Integrations;
using DevFlow.Tasks.Infrastructure.Integrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

namespace DevFlow.AspNetCore.HttpClient.Tests.DependencyInjection;

public sealed class DestinationIsolationTests
{
    [Fact]
    public async Task DestinationsKeepTheirTransportCredentialsAndRetrySettings()
    {
        Guid userId = Guid.NewGuid();
        var values = new Dictionary<string, string?>
        {
            ["Integrations:Identity:BaseUrl"] = "https://identity.test/",
            ["Integrations:Identity:ApiKey"] = "identity-key",
            ["Integrations:Identity:Resilience:Retry:MaxRetryAttempts"] = "1",
            ["Integrations:Organization:BaseUrl"] = "https://organization.test/",
            ["Integrations:Organization:ApiKey"] = "organization-key",
            ["Integrations:Organization:Resilience:Retry:MaxRetryAttempts"] = "2",
        };
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        // Match the existing host defaults; registration must replace their resilience pipeline.
        services.ConfigureHttpClientDefaults(builder => builder.AddStandardResilienceHandler());
        var identityHandler = new CaptureHandler(userId);
        var organizationHandler = new CaptureHandler(userId);
        services.AddIntegrationHttpClient<IIdentityIntegrationService, IdentityIntegrationService>(
            "Identity",
            configuration.GetSection("Integrations:Identity"))
            .ConfigurePrimaryHttpMessageHandler(() => identityHandler);
        services.AddIntegrationHttpClient<IOrganizationClient, OrganizationClient>(
            "Organization",
            configuration.GetSection("Integrations:Organization"))
            .ConfigurePrimaryHttpMessageHandler(() => organizationHandler);
        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<IIntegrationHttpClient<IIdentityIntegrationService>>());
        Assert.NotNull(provider.GetRequiredService<IIntegrationHttpClient<IOrganizationClient>>());
        IIdentityIntegrationService identity = provider.GetRequiredService<IIdentityIntegrationService>();
        IOrganizationClient organization = provider.GetRequiredService<IOrganizationClient>();
        await Task.WhenAll(Enumerable.Range(0, 12).Select(async _ =>
        {
            IntegrationResult<UserIntegrationDto> first = await identity.GetUserAsync(userId);
            IntegrationResult<UserIntegrationDto> second = await organization.GetAsync();
            Assert.True(first.IsSuccess);
            Assert.True(second.IsSuccess);
        }));

        Assert.Equal(12, identityHandler.Requests.Count);
        Assert.Equal(12, organizationHandler.Requests.Count);
        Assert.All(identityHandler.Requests, request =>
        {
            Assert.Equal(new Uri($"https://identity.test/api/v1/integrations/users/{userId:D}"), request.Uri);
            Assert.Equal("identity-key", request.Key);
        });
        Assert.All(organizationHandler.Requests, request =>
        {
            Assert.Equal(new Uri("https://organization.test/users"), request.Uri);
            Assert.Equal("organization-key", request.Key);
        });
        IOptionsMonitor<IntegrationClientOptions> options =
            provider.GetRequiredService<IOptionsMonitor<IntegrationClientOptions>>();
        Assert.Equal(1, options.Get("Identity").Resilience.Retry.MaxRetryAttempts);
        Assert.Equal(2, options.Get("Organization").Resilience.Retry.MaxRetryAttempts);
        IOptionsMonitor<HttpStandardResilienceOptions> pipelines =
            provider.GetRequiredService<IOptionsMonitor<HttpStandardResilienceOptions>>();
        Assert.Equal(1, pipelines.Get("Identity-standard").Retry.MaxRetryAttempts);
        Assert.Equal(2, pipelines.Get("Organization-standard").Retry.MaxRetryAttempts);
    }

    public interface IOrganizationClient
    {
        Task<IntegrationResult<UserIntegrationDto>> GetAsync();
    }

    public sealed class OrganizationClient(IIntegrationHttpClient<IOrganizationClient> client) : IOrganizationClient
    {
        public Task<IntegrationResult<UserIntegrationDto>> GetAsync()
            => client.GetAsync<UserIntegrationDto>("users");
    }

    private sealed class CaptureHandler(Guid userId) : HttpMessageHandler
    {
        public ConcurrentBag<(Uri? Uri, string Key)> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add((request.RequestUri, request.Headers.GetValues("X-Integration-Api-Key").Single()));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    Success = true,
                    Data = new UserIntegrationDto(userId, "Ada", null, "active"),
                }),
            });
        }
    }
}
