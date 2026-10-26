using DevFlow.AspNetCore.DependencyInjection;
using DevFlow.AspNetCore.Filters;
using DevFlow.AspNetCore.Middleware;
using DevFlow.AspNetCore.Options;
using DevFlow.AspNetCore.Security;
using DevFlow.Identity.API.Configuration;
using DevFlow.Identity.API.Health;
using DevFlow.Identity.API.Security;
using DevFlow.Identity.Application.DependencyInjection;
using DevFlow.Identity.Infrastructure;
using DevFlow.Identity.Infrastructure.DepedencyInjection;
using DevFlow.Identity.Infrastructure.Messaging;
using DevFlow.Observability;
using DevFlow.SharedKernel.Security;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serilog;
using System.Globalization;
using System.Text.Json;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
    .CreateLogger();

try
{
    Log.Information("Starting DevFlow.Identity API");

    var builder = WebApplication.CreateBuilder(args);
    builder.Configuration.AddIdentitySecretFiles();

    builder.AddConfiguredSerilog();
    builder.AddDevFlowObservability();

    builder.Services
        .AddApplication()
        .AddInfrastructure(builder.Configuration);

    builder.Services.AddIdentityVerificationEmail(builder.Configuration);

    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ICurrentUserProvider, HttpContextCurrentUserProvider>();
    builder.Services.AddControllers(options =>
    {
        options.Filters.Add<ApiResponseEnvelopeFilter>();
    });
    builder.Services.AddConfiguredCors(builder.Configuration);
    builder.Services.AddConfiguredAuthentication(builder.Configuration, builder.Environment);
    builder.Services.AddConfiguredAuthorization();
    builder.Services.AddIdentityHttpsRedirection(builder.Configuration);
    builder.Services.AddTrustedForwarding(builder.Configuration);
    //builder.Services.AddRateLimiter(options =>
    //{
    //    var permitLimit = builder.Configuration.GetValue("RateLimiting:AuthPublic:PermitLimit", 5);
    //    var windowMinutes = builder.Configuration.GetValue("RateLimiting:AuthPublic:WindowMinutes", 10);

    //    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    //    options.AddPolicy(RateLimitPolicyNames.AuthPublic, httpContext =>
    //    {
    //        var clientIp = httpContext.Connection.RemoteIpAddress?.ToString()
    //            ?? IPAddress.Loopback.ToString();
    //        var endpointKey = httpContext.Request.Path.Value ?? "unknown";
    //        var clientKey = $"{clientIp}:{endpointKey}";

    //        return RateLimitPartition.GetFixedWindowLimiter(
    //            clientKey,
    //            _ => new FixedWindowRateLimiterOptions
    //            {
    //                PermitLimit = permitLimit,
    //                Window = TimeSpan.FromMinutes(windowMinutes),
    //                QueueLimit = 0,
    //                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
    //            });
    //    });
    //});
    builder.Services.AddConfiguredApiVersioning();
    builder.Services
        .AddHealthChecks()
        .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"])
        .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);

    builder.Services.AddConfiguredSwagger();

    var app = builder.Build();

    Log.Information(
        "DevFlow.Identity API configured for {Environment} and listening on {Urls}",
        app.Environment.EnvironmentName,
        app.Configuration["ASPNETCORE_URLS"] ?? "(default)");

    app.UseForwardedHeaders();
    app.UseCorrelationId();
    app.UseGlobalExceptionHandling();
    app.UseApiStatusCodeEnvelope();
    app.UseSerilogRequestLogging();
    app.UseStaticFiles();

    app.UseConfiguredSwagger();

    var useFrontendResetPage = app.Configuration.GetValue<bool>("PasswordReset:UseFrontendResetPage");
    var enableBuiltInResetPage = app.Configuration.GetValue<bool>("PasswordReset:EnableBuiltInResetPage");

    if (enableBuiltInResetPage && !useFrontendResetPage)
    {
        app.MapGet("/reset-password", async context =>
        {
            var resetPagePath = Path.Combine(
                app.Environment.ContentRootPath,
                "wwwroot",
                "reset-password.html");

            if (!File.Exists(resetPagePath))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                await context.Response.WriteAsync("The built-in reset password page was not found.");
                return;
            }

            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.SendFileAsync(resetPagePath);
        });
    }

    if (app.Environment.IsDevelopment())
    {
        app.MapGet("/", () => Results.Redirect("/swagger"));
    }

    app.UseIdentityHttpsRedirection();

    app.UseCors(CorsOptions.PolicyName);
    app.UseIdentityRegistrationProtection();
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapHealthChecks("/health/live", new HealthCheckOptions
    {
        Predicate = registration => registration.Tags.Contains("live"),
        ResponseWriter = WriteHealthCheckResponseAsync
    });

    app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
        Predicate = registration => registration.Tags.Contains("ready"),
        ResponseWriter = WriteHealthCheckResponseAsync
    });

    app.MapControllers();

    await app.RunAsync();

    Log.Information("DevFlow.Identity API stopped cleanly");
}
catch (Exception exception)
{
    Log.Fatal(exception, "DevFlow.Identity API terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

static Task WriteHealthCheckResponseAsync(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json";

    var response = new
    {
        status = report.Status.ToString(),
        checks = report.Entries.Select(entry => new
        {
            name = entry.Key,
            status = entry.Value.Status.ToString(),
            description = entry.Value.Description,
            duration = entry.Value.Duration.TotalMilliseconds
        }),
        totalDuration = report.TotalDuration.TotalMilliseconds
    };

    return context.Response.WriteAsync(JsonSerializer.Serialize(response));
}

