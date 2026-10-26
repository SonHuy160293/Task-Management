using System.Globalization;
using DevFlow.Identity.Infrastructure.DepedencyInjection;
using DevFlow.Identity.Infrastructure.Persistence.DbContexts;
using DevFlow.Observability;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
    .CreateLogger();

IHost? host = null;

try
{
    var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
    {
        Args = args,
        EnvironmentName = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environments.Production,
        ContentRootPath = AppContext.BaseDirectory,
    });
    builder.Configuration
        .AddUserSecrets<Program>(optional: true)
        .AddEnvironmentVariables()
        .AddCommandLine(args);
    builder.Logging.ClearProviders();
    builder.AddDevFlowObservability();
    builder.Services.AddPersistence(builder.Configuration);
    builder.Services.AddSerilog(
        (services, logger) => logger
            .ReadFrom.Configuration(builder.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "DevFlow.Identity.MigrationRunner"),
        writeToProviders: true);

    host = builder.Build();
    var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
    await host.StartAsync(lifetime.ApplicationStopping);

    await using var scope = host.Services.CreateAsyncScope();
    var context = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
    Log.Information("Applying Identity database migrations");
    await context.Database.MigrateAsync(lifetime.ApplicationStopping);
    Log.Information("Identity database is up to date");
}
catch (Exception exception)
{
    // Configuration/provider exceptions can contain connection strings or credentials.
    Log.Fatal("Identity migration failed ({ExceptionType})", exception.GetType().Name);
    Environment.ExitCode = 1;
}
finally
{
    if (host is not null)
    {
        try
        {
            await host.StopAsync();
        }
        finally
        {
            host.Dispose();
        }
    }

    await Log.CloseAndFlushAsync();
}
