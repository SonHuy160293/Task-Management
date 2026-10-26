using DevFlow.AspNetCore.DependencyInjection;
using DevFlow.AspNetCore.Middleware;
using DevFlow.Observability;
using Microsoft.AspNetCore.Http.Timeouts;
using Serilog.Core;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Transforms;

namespace DevFlow.Gateway;

public static class GatewayApplication
{
    public static WebApplication Build(WebApplicationBuilder builder)
    {
        ValidateProductionSettings(builder);
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Limits.MaxRequestBodySize = 65536;
            options.AddServerHeader = false;
        });
        builder.Services.AddSingleton<ILogEventFilter, GatewayLogPrivacy>();
        builder.Logging.ClearProviders();
        builder.AddConfiguredSerilog();
        builder.AddDevFlowObservability();
        builder.Services.AddConfiguredCors(builder.Configuration);
        builder.Services.AddTrustedForwarding(builder.Configuration);
        builder.Services.AddRequestTimeouts(options => options.DefaultPolicy = new RequestTimeoutPolicy
        {
            Timeout = TimeSpan.FromSeconds(30),
            WriteTimeoutResponse = context => GatewayErrors.WriteAsync(context, 504, "Gateway.Timeout"),
        });
        builder.Services.AddReverseProxy()
            .ConfigureHttpClient((_, handler) =>
            {
                handler.ConnectTimeout = TimeSpan.FromSeconds(3);
                handler.AllowAutoRedirect = false;
            })
            .AddTransforms(context => context.AddResponseTransform(transform =>
            {
                if (transform.ProxyResponse is { } response)
                {
                    foreach (string header in response.Headers.Select(pair => pair.Key).Where(name => name.StartsWith("Access-Control-", StringComparison.OrdinalIgnoreCase)).ToArray())
                    {
                        response.Headers.Remove(header);
                        transform.HttpContext.Response.Headers.Remove(header);
                    }
                }

                return ValueTask.CompletedTask;
            }));
        builder.Services.AddSingleton<IProxyConfigProvider, GatewayConfigProvider>();
        builder.Services.AddHealthChecks();

        var app = builder.Build();
        app.UseForwardedHeaders();
        app.UseCorrelationId();
        app.UseMiddleware<GatewayRequestMiddleware>();
        app.UseRouting();
        app.UseCors();
        app.UseRequestTimeouts();
        app.Use(async (context, next) =>
        {
            try
            {
                await next(context);
            }
            finally
            {
                if (context.Features.Get<IHttpRequestTimeoutFeature>()?.RequestTimeoutToken.IsCancellationRequested == true)
                {
                    context.Items["GatewayTimedOut"] = true;
                }
            }
        });
        app.MapHealthChecks("/health/live");

        // Resolving the provider during endpoint initialization validates the initial snapshot.
        app.MapGet("/health/ready", (IProxyConfigProvider configuration) =>
            configuration.GetConfig().Routes.Count > 0 ? Results.Ok(new { status = "Healthy" }) : Results.StatusCode(503));
        app.MapReverseProxy();
        return app;
    }

    private static void ValidateProductionSettings(WebApplicationBuilder builder)
    {
        if (builder.Environment.IsDevelopment())
        {
            return;
        }

        string[] proxies = builder.Configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? [];
        string hosts = builder.Configuration["AllowedHosts"] ?? string.Empty;
        string[] origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        if (proxies.Length == 0 || proxies.Any(proxy => !System.Net.IPAddress.TryParse(proxy, out _))
            || hosts.Length == 0 || hosts.Contains('*', StringComparison.Ordinal)
            || origins.Length == 0 || origins.Any(origin => !Uri.TryCreate(origin, UriKind.Absolute, out var uri)
                || uri.Scheme != Uri.UriSchemeHttps || uri.GetLeftPart(UriPartial.Authority) != origin
                || !string.IsNullOrEmpty(uri.UserInfo)))
        {
            throw new InvalidOperationException("Production requires explicit ingress proxy IPs, allowed hosts, and exact HTTPS frontend origins.");
        }
    }
}
