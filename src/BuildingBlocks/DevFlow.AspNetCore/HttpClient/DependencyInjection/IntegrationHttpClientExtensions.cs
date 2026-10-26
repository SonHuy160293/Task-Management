using DevFlow.AspNetCore.HttpClient.Abstractions;
using DevFlow.AspNetCore.HttpClient.Clients;
using DevFlow.AspNetCore.HttpClient.Correlation;
using DevFlow.AspNetCore.HttpClient.Handlers;
using DevFlow.AspNetCore.HttpClient.Options;
using DevFlow.AspNetCore.HttpClient.Options.Resilience;
using DevFlow.AspNetCore.HttpClient.Validation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Polly;

namespace DevFlow.AspNetCore.HttpClient.DependencyInjection;

public static class IntegrationHttpClientExtensions
{
    public static IHttpClientBuilder AddIntegrationHttpClient<TClient, TImplementation>(
        this IServiceCollection services,
        string destinationName,
        IConfigurationSection configuration)
        where TClient : class
        where TImplementation : class, TClient
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationName);
        ArgumentNullException.ThrowIfNull(configuration);

        services.TryAddSingleton<ICorrelationIdAccessor, ActivityCorrelationIdAccessor>();
        services.AddSingleton<IValidateOptions<IntegrationClientOptions>>(
            new IntegrationClientOptionsValidator(destinationName));
        services.AddOptions<IntegrationClientOptions>(destinationName)
            .Bind(configuration)
            .ValidateOnStart();

        IntegrationClientOptions resilienceOptions = configuration.Get<IntegrationClientOptions>() ?? new();
        services.AddTransient<TClient, TImplementation>();
        IHttpClientBuilder builder = services.AddHttpClient<IIntegrationHttpClient<TClient>, IntegrationHttpClient<TClient>>(destinationName, (provider, client) =>
        {
            IntegrationClientOptions options = provider.GetRequiredService<IOptionsMonitor<IntegrationClientOptions>>()
                .Get(destinationName);
            client.BaseAddress = options.BaseUrl;
            client.Timeout = Timeout.InfiniteTimeSpan;
        });

        builder.AddHttpMessageHandler(provider =>
        {
            IntegrationClientOptions options = provider.GetRequiredService<IOptionsMonitor<IntegrationClientOptions>>()
                .Get(destinationName);
            return new IntegrationHeadersHandler(options, provider.GetRequiredService<ICorrelationIdAccessor>());
        });
        builder.AddHttpMessageHandler(provider => new IntegrationTelemetryHandler(
            destinationName,
            provider.GetRequiredService<ICorrelationIdAccessor>(),
            provider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<IntegrationTelemetryHandler>>()));

        // Replace host defaults so integration requests have one destination-specific pipeline.
#pragma warning disable EXTEXP0001
        builder.RemoveAllResilienceHandlers();
#pragma warning restore EXTEXP0001
        builder.AddStandardResilienceHandler(options => ConfigureResilience(options, resilienceOptions.Resilience));
        return builder;
    }

    private static void ConfigureResilience(
        HttpStandardResilienceOptions target,
        IntegrationResilienceOptions source)
    {
        target.TotalRequestTimeout.Timeout = source.TotalRequestTimeout.Timeout;
        target.AttemptTimeout.Timeout = source.AttemptTimeout.Timeout;
        target.Retry.MaxRetryAttempts = source.Retry.MaxRetryAttempts;
        target.Retry.Delay = source.Retry.Delay;
        target.Retry.BackoffType = Enum.Parse<DelayBackoffType>(source.Retry.BackoffType, ignoreCase: true);
        target.Retry.UseJitter = source.Retry.UseJitter;
        target.Retry.ShouldRetryAfterHeader = source.Retry.ShouldRetryAfterHeader;
        target.Retry.DisableForUnsafeHttpMethods();
        target.CircuitBreaker.FailureRatio = source.CircuitBreaker.FailureRatio;
        target.CircuitBreaker.MinimumThroughput = source.CircuitBreaker.MinimumThroughput;
        target.CircuitBreaker.SamplingDuration = source.CircuitBreaker.SamplingDuration;
        target.CircuitBreaker.BreakDuration = source.CircuitBreaker.BreakDuration;
    }
}
