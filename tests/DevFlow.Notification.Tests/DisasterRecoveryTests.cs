using System.Diagnostics;
using DevFlow.EntityFrameworkCore.Reliability;
using DevFlow.Identity.Infrastructure.Messaging;
using DevFlow.Identity.Infrastructure.Persistence.Core;
using DevFlow.Identity.Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace DevFlow.Notification.Tests;
public sealed class DisasterRecoveryTests
{
    [DeliveryRestoreFact]
    public async Task RealPgDumpRestorePreservesPendingOutboxForRecovery()
    {
        string? container = Environment.GetEnvironmentVariable("DEVFLOW_DELIVERY_TEST_POSTGRES_CONTAINER");
        ArgumentNullException.ThrowIfNull(container);
        if (!container.StartsWith("devflow-delivery-tests-", StringComparison.Ordinal) || !container.EndsWith("-postgres", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Restore testing requires a disposable test container.");
        }

        await using var fixture = await DeliveryFixture.CreateAsync();
        var request = PersistenceIntegrationTests.Request(fixture);
        await using (var db = fixture.Identity())
        {
            await new IdentityUnitOfWork(db).ExecuteInTransactionAsync(async ct =>
            {
                await new IdentityEmailOutbox(db).StageAsync(request, ct);
                return true;
            });
        }

        var source = new NpgsqlConnectionStringBuilder(fixture.IdentityConnection);
        string clone = "devflow_mail_test_restore_" + Guid.NewGuid().ToString("N");
        string archive = "/tmp/" + clone + ".dump";
        var adminString = Environment.GetEnvironmentVariable("DEVFLOW_DELIVERY_TEST_POSTGRES")!;
        await using var admin = new NpgsqlConnection(adminString);
        await admin.OpenAsync();
        try
        {
            await DockerAsync(["exec", container, "pg_dump", "-U", "devflow_test", "-Fc", "-f", archive, "-d", source.Database!]);
            await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{clone}\"", admin))
            {
                await create.ExecuteNonQueryAsync();
            }

            await DockerAsync(["exec", container, "pg_restore", "-U", "devflow_test", "--exit-on-error", "-d", clone, archive]);
            source.Database = clone;
            await using var restored = new DevFlow.Identity.Infrastructure.Persistence.DbContexts.IdentityDbContext(new DbContextOptionsBuilder<DevFlow.Identity.Infrastructure.Persistence.DbContexts.IdentityDbContext>().UseNpgsql(source.ConnectionString).Options);
            var claim = (await new IdentityOutboxDispatchStore(restored).ClaimAsync(default)).Single();
            Assert.Equal(request.MessageId, claim.MessageId);
            Assert.DoesNotContain("alice@example.test", claim.PayloadJson);
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{clone}\" WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
            await DockerAsync(["exec", container, "rm", "-f", archive]);
        }
    }

    internal static async Task DockerAsync(string[] arguments)
    {
        await DockerOutputAsync(arguments);
    }

    internal static async Task<string> DockerOutputAsync(string[] arguments)
    {
        var start = new ProcessStartInfo("docker")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)!;
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        await process.WaitForExitAsync(deadline.Token);
        string result = await output;
        await error;
        Assert.True(process.ExitCode == 0, "Docker test operation failed; no secrets are included in diagnostics.");
        return result;
    }
}
