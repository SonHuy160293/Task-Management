using System.Text.Json;
using DevFlow.EntityFrameworkCore.Reliability;
using DevFlow.Identity.Domain.Models;
using DevFlow.Identity.Infrastructure.Messaging;
using DevFlow.Identity.Infrastructure.Persistence.Core;
using DevFlow.Identity.Infrastructure.Persistence.Outbox;
using DevFlow.Messaging.Outbox;
using DevFlow.Notification.Application.Features.EmailDelivery.Commands.AcceptEmailDelivery;
using DevFlow.Notification.Contracts;
using DevFlow.Notification.Infrastructure.Email.Providers;
using DevFlow.Notification.Infrastructure.Email.Workers;
using DevFlow.SharedKernel.Abstractions.Shared;
using DevFlow.SharedKernel.Cqrs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DevFlow.Notification.Tests;
public sealed class PersistenceIntegrationTests
{
    internal static EmailDeliveryRequestedV1 Request(DeliveryFixture f)
    {
        var now = DateTimeOffset.UtcNow;
        return VerificationEmailEnvelopeFactory.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), null, "alice@example.test", "Alice", "en", new string ('a', 43), now, now.AddHours(24), "v1", f.Key.ExportSubjectPublicKeyInfoPem());
    }

    [DeliveryDatabaseFact]
    public async Task UserAndOutboxCommitTogetherAndRollbackTogether()
    {
        await using var fixture = await DeliveryFixture.CreateAsync();
        var request = Request(fixture);
        await using var db = fixture.Identity();
        var uow = new IdentityUnitOfWork(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() => uow.ExecuteInTransactionAsync<bool>(async ct =>
        {
            db.Users.Add(User.Create("rollback@example.test", "Rollback"));
            await new IdentityEmailOutbox(db).StageAsync(request, ct);
            throw new InvalidOperationException("Test rollback.");
        }));
        Assert.Empty(await db.Users.ToListAsync());
        Assert.Empty(await db.Set<OutboxMessage>().ToListAsync());
        await uow.ExecuteInTransactionAsync(async ct =>
        {
            db.Users.Add(User.Create("commit@example.test", "Commit"));
            await new IdentityEmailOutbox(db).StageAsync(request, ct);
            return true;
        });
        Assert.Single(await db.Users.ToListAsync());
        Assert.Single(await db.Set<OutboxMessage>().ToListAsync());
        Assert.DoesNotContain("alice@example.test", (await db.Set<OutboxMessage>().SingleAsync()).PayloadJson);
    }

    [DeliveryDatabaseFact]
    public async Task ConcurrentPublishersRespectFencingAndMissingReceiptRequeues()
    {
        await using var fixture = await DeliveryFixture.CreateAsync();
        var request = Request(fixture);
        await using (var seed = fixture.Identity())
        {
            await new IdentityUnitOfWork(seed).ExecuteInTransactionAsync(async ct =>
            {
                await new IdentityEmailOutbox(seed).StageAsync(request, ct);
                return true;
            });
        }

        await using var a = fixture.Identity();
        await using var b = fixture.Identity();
        var storeA = new IdentityOutboxDispatchStore(a);
        var storeB = new IdentityOutboxDispatchStore(b);
        var claims = await Task.WhenAll(storeA.ClaimAsync(default), storeB.ClaimAsync(default));
        Assert.Equal(1, claims.Sum(x => x.Count));
        var first = claims.SelectMany(x => x).Single();
        await using (var update = fixture.Identity())
        {
            await update.Database.ExecuteSqlInterpolatedAsync($"UPDATE reliability.\"OutboxMessages\" SET \"LeaseUntil\" = {DateTimeOffset.UtcNow.AddMinutes(-1)} WHERE \"MessageId\" = {request.MessageId}");
        }

        var recovered = (await storeB.ClaimAsync(default)).Single();
        Assert.Equal(first.MessageId, recovered.MessageId);
        Assert.Equal(first.PayloadJson, recovered.PayloadJson);
        await storeA.PublishedAsync(first.MessageId, first.LeaseToken!.Value, default);
        await using (var verify = fixture.Identity())
        {
            Assert.Equal(OutboxStatus.Processing, (await verify.Set<OutboxMessage>().SingleAsync()).Status);
        }

        await storeB.PublishedAsync(recovered.MessageId, recovered.LeaseToken!.Value, default);
        await using (var update = fixture.Identity())
        {
            await update.Database.ExecuteSqlInterpolatedAsync($"UPDATE reliability.\"OutboxMessages\" SET \"PublishedAt\" = {DateTimeOffset.UtcNow.AddMinutes(-6)} WHERE \"MessageId\" = {request.MessageId}");
        }

        var replay = (await storeA.ClaimAsync(default)).Single();
        Assert.Equal(request.MessageId, replay.MessageId);
        Assert.Equal(first.PayloadJson, replay.PayloadJson);
        await using var audit = fixture.Identity();
        Assert.Single(await audit.Set<OutboxRecovery>().ToListAsync());
    }

    [DeliveryDatabaseFact]
    public async Task DuplicateConsumptionCreatesOneJobAndMismatchedPayloadRollsBack()
    {
        await using var fixture = await DeliveryFixture.CreateAsync();
        await using var services = fixture.Services();
        var request = Request(fixture);
        await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ =>
        {
            await using var scope = services.CreateAsyncScope();
            var result = await scope.ServiceProvider.GetRequiredService<ISender>().SendAsync(new AcceptEmailDeliveryCommand(request), default);
            Assert.True(result.IsSuccess);
        }));
        await using var db = fixture.Notification();
        Assert.Single(await db.EmailDeliveries.ToListAsync());
        Assert.Single(await db.Set<InboxMessage>().ToListAsync());
        Assert.Equal(8, await db.Set<OutboxMessage>().CountAsync());
        await using var badScope = services.CreateAsyncScope();
        await Assert.ThrowsAnyAsync<Exception>(() => badScope.ServiceProvider.GetRequiredService<ISender>().SendAsync(new AcceptEmailDeliveryCommand(request with { Payload = request.Payload with { Ciphertext = request.Payload.Ciphertext + "bad" } }), default));
        Assert.Equal(8, await db.Set<OutboxMessage>().CountAsync());
    }

    [DeliveryDatabaseFact]
    public async Task ProviderTimeoutReusesFrozenRequestAndIdempotencyAndRecordsAcceptance()
    {
        await using var fixture = await DeliveryFixture.CreateAsync();
        var provider = new TestProvider();
        await using var services = fixture.Services(s => s.AddSingleton<IEmailProvider>(provider));
        var request = Request(fixture);
        await using (var scope = services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ISender>().SendAsync(new AcceptEmailDeliveryCommand(request), default);
        }

        var worker = new EmailDeliveryWorker(services.GetRequiredService<IServiceScopeFactory>(), NullLogger<EmailDeliveryWorker>.Instance);
        await worker.RunOnceAsync(default);
        await using (var db = fixture.Notification())
        {
            var job = await db.EmailDeliveries.SingleAsync();
            Assert.Equal("Pending", job.Status);
            Assert.NotNull(job.FirstAttemptAt);
            Assert.NotNull(job.FrozenRequestJson);
            Assert.DoesNotContain("alice@example.test", job.FrozenRequestJson);
            await db.Database.ExecuteSqlRawAsync("UPDATE notification.\"EmailDeliveries\" SET \"NextAttemptAt\" = now(); UPDATE notification.\"EmailDeliveryControls\" SET \"NextRequestAt\" = now()");
        }

        await worker.RunOnceAsync(default);
        Assert.Equal(2, provider.Requests.Count);
        Assert.Equal(provider.Requests[0], provider.Requests[1]);
        await using var verify = fixture.Notification();
        var accepted = await verify.EmailDeliveries.SingleAsync();
        Assert.Equal("ProviderAccepted", accepted.Status);
        Assert.Equal("{}", accepted.RequestJson);
        Assert.Null(accepted.FrozenRequestJson);
        Assert.Equal(2, await verify.EmailDeliveryAttempts.CountAsync());
    }

    [DeliveryDatabaseFact]
    public async Task ExpiredJobIsRecordedButNeverSentAndOldKeyIsNotRequiredForDuplicate()
    {
        await using var fixture = await DeliveryFixture.CreateAsync();
        var provider = new TestProvider();
        await using var services = fixture.Services(s =>
        {
            s.AddSingleton<IEmailProvider>(provider);
            s.Configure<DevFlow.Notification.Infrastructure.Email.EmailDeliveryOptions>(options => options.ActiveKeyId = "v2");
        });
        using var rotated = System.Security.Cryptography.RSA.Create(3072);
        File.WriteAllText(Path.Combine(fixture.Keys, "v2.private.pem"), rotated.ExportPkcs8PrivateKeyPem());
        File.WriteAllText(Path.Combine(fixture.Keys, "v2.public.pem"), rotated.ExportSubjectPublicKeyInfoPem());
        var created = DateTimeOffset.UtcNow.AddDays(-2);
        var request = Request(fixture)with
        {
            CreatedAt = created,
            ExpiresAt = created.AddDays(1)
        };
        await using (var scope = services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ISender>().SendAsync(new AcceptEmailDeliveryCommand(request), default);
        }

        File.Delete(Path.Combine(fixture.Keys, "v1.private.pem"));
        await using (var scope = services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ISender>().SendAsync(new AcceptEmailDeliveryCommand(request), default);
        }

        var worker = new EmailDeliveryWorker(services.GetRequiredService<IServiceScopeFactory>(), NullLogger<EmailDeliveryWorker>.Instance);
        await worker.RunOnceAsync(default);
        Assert.Empty(provider.Requests);
        await using var db = fixture.Notification();
        Assert.Equal("Expired", (await db.EmailDeliveries.SingleAsync()).Status);
    }

    [DeliveryDatabaseFact]
    public async Task RetryReservesNewUtcDayBudgetAndBlocksFurtherSubmissionsAtLimit()
    {
        await using var fixture = await DeliveryFixture.CreateAsync();
        var provider = new TestProvider();
        await using var services = fixture.Services(s =>
        {
            s.AddSingleton<IEmailProvider>(provider);
            s.Configure<DevFlow.Notification.Infrastructure.Email.EmailDeliveryOptions>(options => options.DailyLimit = 1);
        });
        await using (var scope = services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ISender>().SendAsync(new AcceptEmailDeliveryCommand(Request(fixture)), default);
        }

        var worker = new EmailDeliveryWorker(services.GetRequiredService<IServiceScopeFactory>(), NullLogger<EmailDeliveryWorker>.Instance);
        await worker.RunOnceAsync(default);
        await using (var db = fixture.Notification())
        {
            await db.Database.ExecuteSqlRawAsync("UPDATE notification.\"EmailDeliveries\" SET \"NextAttemptAt\" = now(); UPDATE notification.\"EmailDeliveryControls\" SET \"NextRequestAt\" = now(), \"Day\" = '2000-01-01'");
        }

        await worker.RunOnceAsync(default);
        await using (var db = fixture.Notification())
        {
            Assert.Equal(1, (await db.EmailDeliveryControls.SingleAsync()).DailyReservations);
            await db.Database.ExecuteSqlRawAsync("UPDATE notification.\"EmailDeliveryControls\" SET \"NextRequestAt\" = now()");
        }

        await using (var scope = services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ISender>().SendAsync(new AcceptEmailDeliveryCommand(Request(fixture)), default);
        }

        await worker.RunOnceAsync(default);
        Assert.Equal(2, provider.Requests.Count);
        await using var verify = fixture.Notification();
        Assert.Single(await verify.EmailDeliveries.Where(x => x.Status == "Pending").ToListAsync());
    }

    private sealed class TestProvider : IEmailProvider
    {
        public List<string> Requests { get; } = [];

        public Task<ProviderResult> SendAsync(Guid id, string json, CancellationToken ct)
        {
            Requests.Add(id.ToString() + json);
            if (Requests.Count == 1)
            {
                throw new OperationCanceledException();
            }

            return Task.FromResult(new ProviderResult("Accepted", "test-provider-id"));
        }
    }
}
