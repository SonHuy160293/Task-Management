using DevFlow.AspNetCore.HttpClient.Observability;
using Microsoft.AspNetCore.Builder;
using Serilog;
using Serilog.Events;

namespace DevFlow.AspNetCore.DependencyInjection;

public static class SerilogExtensions
{
    public static WebApplicationBuilder AddConfiguredSerilog(this WebApplicationBuilder builder)
    {
        builder.Host.UseSerilog(
            (context, services, loggerConfiguration) =>
            {
                loggerConfiguration
                    .ReadFrom.Configuration(context.Configuration)
                    .ReadFrom.Services(services)
                    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
                    .Enrich.FromLogContext()
                    .Enrich.WithMachineName()
                    .Enrich.WithThreadId()
                    .Enrich.With(new HttpClientLogPrivacy())
                    .Filter.With(new HttpClientLogPrivacy());
            },
            writeToProviders: true);

        return builder;
    }
}
