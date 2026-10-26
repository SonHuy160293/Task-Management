using System.Security.Cryptography;
using DevFlow.Identity.Infrastructure.Persistence.DbContexts;
using DevFlow.Notification.Application;
using DevFlow.Notification.Infrastructure;
using DevFlow.Notification.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace DevFlow.Notification.Tests;
public sealed class DeliveryDatabaseFactAttribute : FactAttribute
{
    public DeliveryDatabaseFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DEVFLOW_DELIVERY_TEST_POSTGRES")))
        {
            Skip = "Set DEVFLOW_DELIVERY_TEST_POSTGRES to a disposable PostgreSQL 18 server with database-create privileges.";
        }
    }
}

public sealed class DeliveryBrokerFactAttribute : FactAttribute
{
    public DeliveryBrokerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DEVFLOW_DELIVERY_TEST_POSTGRES")) || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DEVFLOW_DELIVERY_TEST_RABBIT_PORT")))
        {
            Skip = "Set disposable PostgreSQL and RabbitMQ integration test endpoints.";
        }
    }
}

public sealed class DeliveryRestoreFactAttribute : FactAttribute
{
    public DeliveryRestoreFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DEVFLOW_DELIVERY_TEST_POSTGRES")) || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DEVFLOW_DELIVERY_TEST_POSTGRES_CONTAINER")))
        {
            Skip = "Run scripts/Test-EmailDelivery.ps1 to enable actual pg_dump/pg_restore verification.";
        }
    }
}

public sealed class DeliveryRestartFactAttribute : FactAttribute
{
    public DeliveryRestartFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DEVFLOW_DELIVERY_TEST_POSTGRES")) || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DEVFLOW_DELIVERY_TEST_RABBIT_PORT")) || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DEVFLOW_DELIVERY_TEST_RABBIT_CONTAINER")))
        {
            Skip = "Run scripts/Test-EmailDelivery.ps1 to enable actual broker restart verification.";
        }
    }
}

internal sealed class DeliveryFixture : IAsyncDisposable
{
    private string _identityName = "devflow_mail_test_i_" + Guid.NewGuid().ToString("N");
    private string _notificationName = "devflow_mail_test_n_" + Guid.NewGuid().ToString("N");
    public string IdentityConnection { get; private set; } = "";
    public string NotificationConnection { get; private set; } = "";
    public string Keys { get; } = Path.Combine(Path.GetTempPath(), "devflow-mail-test-" + Guid.NewGuid().ToString("N"));
    public RSA Key { get; } = RSA.Create(3072);

    public static async Task<DeliveryFixture> CreateAsync()
    {
        var fixture = new DeliveryFixture();
        try
        {
            await fixture.InitializeAsync();
            return fixture;
        }
        catch
        {
            await fixture.DisposeAsync();
            throw;
        }
    }

    private async Task InitializeAsync()
    {
        var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("DEVFLOW_DELIVERY_TEST_POSTGRES"));
        await using var admin = new NpgsqlConnection(connection.ConnectionString);
        await admin.OpenAsync();
        foreach (string name in new[]
        {
            _identityName,
            _notificationName
        }

        )
        {
            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", admin);
            await create.ExecuteNonQueryAsync();
        }

        connection.Database = _identityName;
        IdentityConnection = connection.ConnectionString;
        connection.Database = _notificationName;
        NotificationConnection = connection.ConnectionString;
        await using var identity = Identity();
        await identity.Database.MigrateAsync();
        await using var notification = Notification();
        await notification.Database.MigrateAsync();
        Directory.CreateDirectory(Keys);
        await File.WriteAllTextAsync(Path.Combine(Keys, "v1.private.pem"), Key.ExportPkcs8PrivateKeyPem());
        await File.WriteAllTextAsync(Path.Combine(Keys, "v1.public.pem"), Key.ExportSubjectPublicKeyInfoPem());
        await File.WriteAllTextAsync(Path.Combine(Keys, "recipient-hmac.secret"), Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
    }

    public IdentityDbContext Identity() => new(new DbContextOptionsBuilder<IdentityDbContext>().UseNpgsql(IdentityConnection).Options);
    public NotificationDbContext Notification() => new(new DbContextOptionsBuilder<NotificationDbContext>().UseNpgsql(NotificationConnection).Options);
    public ServiceProvider Services(Action<IServiceCollection>? configure = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:NotificationDb"] = NotificationConnection, ["Notification:EmailDelivery:Mode"] = "Mailpit", ["Notification:EmailDelivery:Development"] = "true", ["Notification:EmailDelivery:KeyDirectory"] = Keys, ["Notification:EmailDelivery:ActiveKeyId"] = "v1", ["Notification:EmailDelivery:RecipientHashKeyFile"] = Path.Combine(Keys, "recipient-hmac.secret"), ["Notification:EmailDelivery:Sender"] = "DevFlow <auth@example.test>", ["Notification:EmailDelivery:FrontendOrigin"] = "https://app.example.test", }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddNotificationApplication().AddNotificationPersistence(configuration).AddNotificationEmail(configuration);
        configure?.Invoke(services);
        return services.BuildServiceProvider();
    }

    public async ValueTask DisposeAsync()
    {
        Key.Dispose();
        if (Directory.Exists(Keys))
        {
            foreach (var file in Directory.GetFiles(Keys))
            {
                File.Delete(file);
            }

            Directory.Delete(Keys);
        }

        string? adminString = Environment.GetEnvironmentVariable("DEVFLOW_DELIVERY_TEST_POSTGRES");
        if (adminString is null)
        {
            return;
        }

        NpgsqlConnection.ClearAllPools();
        await using var admin = new NpgsqlConnection(adminString);
        await admin.OpenAsync();
        foreach (string name in new[]
        {
            _identityName,
            _notificationName
        }

        )
        {
            if (!name.StartsWith("devflow_mail_test_", StringComparison.Ordinal) || name.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '_'))
            {
                throw new InvalidOperationException("Unsafe test database cleanup target.");
            }

            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
