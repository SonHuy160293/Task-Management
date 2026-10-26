using DevFlow.EntityFrameworkCore.Interceptors;
using DevFlow.Identity.Application.Abstractions.Repositories;
using DevFlow.Identity.Infrastructure.Persistence.Core;
using DevFlow.Identity.Infrastructure.Persistence.DbContexts;
using DevFlow.Identity.Infrastructure.Persistence.Repositories;
using DevFlow.SharedKernel.Abstractions.Persistence;
using DevFlow.SharedKernel.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
namespace DevFlow.Identity.Infrastructure.DepedencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        => services
            .AddPersistence(configuration)
            .AddRepositories()
            .AddUnitOfWork()
            .AddIdentitySecurity(configuration)
            .AddInfrastructureCqrs();

    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = GetApplicationConnectionString(configuration);

        services.AddScoped<AuditSaveChangesInterceptor>();
        services.AddSingleton<SqlConnectionFactory>();

        services.AddDbContext<IdentityDbContext>((serviceProvider, options) =>
            options.UseNpgsql(connectionString)
                .AddInterceptors(serviceProvider.GetRequiredService<AuditSaveChangesInterceptor>()));

        return services;
    }

    public static IServiceCollection AddRepositories(this IServiceCollection services)
        => services
            .AddScoped<IUserRepository, UserRepository>()
            .AddScoped<DevFlow.Identity.Application.Abstractions.Messaging.IEmailDeliveryReceiptStore, DevFlow.Identity.Infrastructure.Messaging.EmailDeliveryReceiptStore>()
            .AddScoped<IOneTimeTokenRepository, OneTimeTokenRepository>()
            .AddScoped<IPasswordCredentialRepository, PasswordCredentialRepository>();

    public static IServiceCollection AddUnitOfWork(this IServiceCollection services)
        => services.AddScoped<IUnitOfWork, IdentityUnitOfWork>();


    public static IServiceCollection AddInfrastructureCqrs(this IServiceCollection services)
        => services
            .AddSelfMadeCqrs(typeof(ServiceCollectionExtensions).Assembly);

    private static string GetApplicationConnectionString(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("ApplicationDb");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Connection string 'ApplicationDb' is missing.");
        }

        if (configuration["Database:PasswordFile"] is { Length: > 0 } passwordFile)
        {
            var parsed = new Npgsql.NpgsqlConnectionStringBuilder(connectionString)
            {
                Password = File.ReadAllText(passwordFile).Trim(),
            };
            return parsed.ConnectionString;
        }
        return connectionString;
    }
}
