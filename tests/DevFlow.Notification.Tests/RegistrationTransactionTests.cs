using System.Collections.Concurrent;
using DevFlow.EntityFrameworkCore.Reliability;
using DevFlow.Identity.Application.Abstractions.Messaging;
using DevFlow.Identity.Application.Features.Auth.Commands.Registration;
using DevFlow.Identity.Infrastructure.Messaging;
using DevFlow.Identity.Infrastructure.Persistence.Core;
using DevFlow.Identity.Infrastructure.Persistence.DbContexts;
using DevFlow.Identity.Infrastructure.Persistence.Repositories;
using DevFlow.Identity.Infrastructure.Persistence.Outbox;
using DevFlow.Messaging.Outbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using DevFlow.Identity.Infrastructure.Security.Options;
using DevFlow.Identity.Infrastructure.Security.Passwords;
using DevFlow.Identity.Infrastructure.Security.Tokens;
using DevFlow.Notification.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace DevFlow.Notification.Tests;

public sealed class RegistrationTransactionTests
{
    [DeliveryDatabaseFact]
    public async Task ConcurrentRegistrationsCommitOneCompleteAccountAndEncryptedMessage()
    {
        await using var fixture = await DeliveryFixture.CreateAsync();
        using var hasher = new Argon2PasswordHasher(Options.Create(new Argon2Options { MemoryKiB = 19456, Iterations = 2 }));
        var factory = new VerificationEmailRequestFactory("v1", fixture.Key.ExportSubjectPublicKeyInfoPem());
        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(async i =>
        {
            await using var db = fixture.Identity();
            var command = new RegisterAccountCommand(i % 2 == 0 ? "alice@example.test" : "ALICE@example.test", "a unique registration passphrase", "Alice", "en", "UTC");
            return await new IdentityUnitOfWork(db).ExecuteInTransactionAsync(ct => Handler(db, hasher, factory).HandleAsync(command, ct));
        }));
        Assert.All(results, result => Assert.True(result.IsSuccess));
        Assert.All(results, result => Assert.Equal(results[0].Value.Message, result.Value.Message));
        await using var verify = fixture.Identity();
        var user = Assert.Single(await verify.Users.ToListAsync());
        Assert.Equal("pending", user.Status);
        var credential = Assert.Single(await verify.PasswordCredentials.ToListAsync());
        Assert.StartsWith("$argon2id$", credential.PasswordHash);
        Assert.Equal(user.Id, credential.UserId);
        Assert.NotEqual(DevFlow.Identity.Application.Security.PasswordVerificationResult.Failed, await hasher.VerifyAsync("a unique registration passphrase", credential.PasswordHash, default));
        var token = Assert.Single(await verify.OneTimeTokens.ToListAsync());
        Assert.Equal(user.Id, token.UserId);
        Assert.Equal(32, token.TokenHash.Length);
        Assert.Null(token.ConsumedAt);
        var message = Assert.Single(await verify.Set<OutboxMessage>().ToListAsync());
        Assert.DoesNotContain("alice@example.test", message.PayloadJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("registration passphrase", message.PayloadJson, StringComparison.Ordinal);
        string originalHash = credential.PasswordHash;
        await using (var duplicate = fixture.Identity())
        {
            var result = await new IdentityUnitOfWork(duplicate).ExecuteInTransactionAsync(ct => Handler(duplicate, hasher, factory).HandleAsync(
                new RegisterAccountCommand("ALICE@EXAMPLE.TEST", "a different registration passphrase", "Other", "en", "UTC"), ct));
            Assert.Equal(results[0].Value.Message, result.Value.Message);
        }

        await using var afterDuplicate = fixture.Identity();
        Assert.Equal(originalHash, (await afterDuplicate.PasswordCredentials.SingleAsync()).PasswordHash);
        Assert.Single(await afterDuplicate.OneTimeTokens.ToListAsync());
        Assert.Single(await afterDuplicate.Set<OutboxMessage>().ToListAsync());
    }

    [DeliveryDatabaseFact]
    public async Task EncryptionFailureAndOutboxInsertFailureRollBackAllAccountRows()
    {
        await using var fixture = await DeliveryFixture.CreateAsync();
        using var hasher = new Argon2PasswordHasher(Options.Create(new Argon2Options { MemoryKiB = 19456, Iterations = 2 }));
        var factory = new VerificationEmailRequestFactory("v1", fixture.Key.ExportSubjectPublicKeyInfoPem());
        var command = new RegisterAccountCommand("failure@example.test", "a unique registration passphrase", "Alice", "en", "UTC");
        await using (var db = fixture.Identity())
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => new IdentityUnitOfWork(db).ExecuteInTransactionAsync(ct => Handler(db, hasher, new FailingFactory()).HandleAsync(command, ct)));
        }

        Guid duplicateId = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;
        var seeded = factory.Create(duplicateId, "other@example.test", "Other", "en", "token", now, now.AddHours(1));
        await using (var db = fixture.Identity())
        {
            await new IdentityUnitOfWork(db).ExecuteInTransactionAsync(async ct => { await new IdentityEmailOutbox(db).StageAsync(seeded, ct); return true; });
        }

        await using (var db = fixture.Identity())
        {
            await Assert.ThrowsAnyAsync<DbUpdateException>(() => new IdentityUnitOfWork(db).ExecuteInTransactionAsync(ct => Handler(db, hasher, new FixedIdFactory(factory, duplicateId)).HandleAsync(command, ct)));
        }

        await using var verify = fixture.Identity();
        Assert.Empty(await verify.Users.ToListAsync());
        Assert.Empty(await verify.PasswordCredentials.ToListAsync());
        Assert.Empty(await verify.OneTimeTokens.ToListAsync());
        Assert.Single(await verify.Set<OutboxMessage>().ToListAsync());
    }

    [DeliveryDatabaseFact]
    public async Task TwoRelaysRecoverPublishedBeforeMarkWithStablePayload()
    {
        await using var fixture = await DeliveryFixture.CreateAsync();
        var request = PersistenceIntegrationTests.Request(fixture);
        await using (var seed = fixture.Identity())
        {
            await new IdentityUnitOfWork(seed).ExecuteInTransactionAsync(async ct =>
            {
                await new IdentityEmailOutbox(seed).StageAsync(request, ct);
                return true;
            });
        }

        var publisher = new CapturingPublisher();
        await using (var firstContext = fixture.Identity())
        {
            var first = Assert.Single(await new IdentityOutboxDispatchStore(firstContext).ClaimAsync(default));
            await publisher.PublishAsync(first, default); // Simulated broker confirmation before process failure.
        }

        await using (var update = fixture.Identity())
        {
            await update.Database.ExecuteSqlInterpolatedAsync($"UPDATE reliability.\"OutboxMessages\" SET \"LeaseUntil\" = {DateTimeOffset.UtcNow.AddMinutes(-1)} WHERE \"MessageId\" = {request.MessageId}");
        }

        var services = new ServiceCollection();
        services.AddScoped(_ => fixture.Identity());
        services.AddScoped<IOutboxDispatchStore, IdentityOutboxDispatchStore>();
        services.AddSingleton<IIntegrationEventPublisher>(publisher);
        await using var provider = services.BuildServiceProvider();
        var a = new OutboxRelay(provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<OutboxRelay>.Instance);
        var b = new OutboxRelay(provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<OutboxRelay>.Instance);
        await Task.WhenAll(a.RunOnceAsync(default), b.RunOnceAsync(default));
        Assert.Equal(2, publisher.Messages.Count);
        Assert.All(publisher.Messages, value => Assert.Equal((request.MessageId, publisher.Messages.First().Payload), value));
        await using var verify = fixture.Identity();
        Assert.Equal(OutboxStatus.Published, (await verify.Set<OutboxMessage>().SingleAsync()).Status);
    }

    private sealed class CapturingPublisher : IIntegrationEventPublisher
    {
        public ConcurrentQueue<(Guid Id, string Payload)> Messages { get; } = new();

        public Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken)
        {
            Messages.Enqueue((message.MessageId, message.PayloadJson));
            return Task.CompletedTask;
        }
    }

    private static RegisterAccountHandler Handler(IdentityDbContext db, Argon2PasswordHasher hasher, IVerificationEmailRequestFactory factory)
        => new(new UserRepository(db), new PasswordCredentialRepository(db), new OneTimeTokenRepository(db), hasher,
            new VerificationTokenGenerator(TimeProvider.System, Options.Create(new VerificationTokenOptions())), factory, new IdentityEmailOutbox(db));

    private sealed class FailingFactory : IVerificationEmailRequestFactory
    {
        public EmailDeliveryRequestedV1 Create(Guid messageId, string recipient, string displayName, string locale, string rawToken, DateTimeOffset createdAt, DateTimeOffset expiresAt)
            => throw new InvalidOperationException("Injected failure.");
    }

    private sealed class FixedIdFactory(IVerificationEmailRequestFactory inner, Guid id) : IVerificationEmailRequestFactory
    {
        public EmailDeliveryRequestedV1 Create(Guid messageId, string recipient, string displayName, string locale, string rawToken, DateTimeOffset createdAt, DateTimeOffset expiresAt)
            => inner.Create(id, recipient, displayName, locale, rawToken, createdAt, expiresAt);
    }
}
