using DevFlow.Identity.Domain.Models;
using DevFlow.Identity.Infrastructure.Persistence.Core;
using DevFlow.Identity.Infrastructure.Persistence.DbContexts;
using DevFlow.Identity.Infrastructure.Persistence.Repositories;
using DevFlow.SharedKernel.Abstractions.Shared;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace DevFlow.Identity.Tests.Persistence;

public sealed class PostgreSqlFactAttribute : FactAttribute
{
    public PostgreSqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DEVFLOW_TEST_POSTGRES")))
        {
            Skip = "Set DEVFLOW_TEST_POSTGRES to a provisioned isolated PostgreSQL 18 database.";
        }
    }
}

public sealed class PostgreSqlRegistrationTests
{
    private static IdentityDbContext CreateContext()
        => new(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("DEVFLOW_TEST_POSTGRES")).Options);

    [PostgreSqlFact]
    public async Task FailedResultAndExceptionRollBackWithoutPersistingStagedUser()
    {
        string email = Guid.NewGuid().ToString("N") + "@example.test";
        await using var context = CreateContext();
        var unitOfWork = new IdentityUnitOfWork(context);
        Result response = await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await context.Users.AddAsync(User.Create(email, "Dependency test"), ct);
            return Result.Failure(new Error("Test.Failure", "Expected test failure."));
        });
        Assert.True(response.IsFailure);
        Assert.Empty(context.ChangeTracker.Entries());
        Assert.False(await context.Users.AnyAsync(user => user.PrimaryEmail == email));
        await Assert.ThrowsAsync<InvalidOperationException>(() => unitOfWork.ExecuteInTransactionAsync<Result>(async ct =>
        {
            await context.Users.AddAsync(User.Create(email, "Dependency test"), ct);
            throw new InvalidOperationException("Expected test exception.");
        }));
        Assert.False(await context.Users.AnyAsync(user => user.PrimaryEmail == email));
    }

    [PostgreSqlFact]
    public async Task ConcurrentCaseVariantsCommitOnlyOneAtomicRegistration()
    {
        string email = Guid.NewGuid().ToString("N") + "@example.test";
        try
        {
            bool[] created = await Task.WhenAll(Enumerable.Range(0, 8).Select(async index =>
            {
                await using var context = CreateContext();
                var repository = new UserRepository(context);
                var unitOfWork = new IdentityUnitOfWork(context);
                return await unitOfWork.ExecuteInTransactionAsync(async ct =>
                {
                    if (await repository.ExistsForRegistrationAsync(index % 2 == 0 ? email : email.ToUpperInvariant(), ct))
                    {
                        return false;
                    }

                    var user = User.Create(email, "Concurrency test");
                    await context.Users.AddAsync(user, ct);
                    await context.PasswordCredentials.AddAsync(PasswordCredential.Create(user.Id, new string('x', 50)), ct);
                    var now = DateTimeOffset.UtcNow;
                    await context.OneTimeTokens.AddAsync(
                        OneTimeToken.Create(
                        user.Id, "verify_email", System.Security.Cryptography.RandomNumberGenerator.GetBytes(32), now, now.AddDays(1)), ct);
                    return true;
                });
            }));
            Assert.Equal(1, created.Count(value => value));
            await using var verification = CreateContext();
            Guid id = await verification.Users.Where(user => user.PrimaryEmail == email).Select(user => user.Id).SingleAsync();
            Assert.Equal(1, await verification.PasswordCredentials.CountAsync(credential => credential.UserId == id));
            Assert.Equal(1, await verification.OneTimeTokens.CountAsync(token => token.UserId == id && token.ConsumedAt == null));
        }
        finally
        {
            await using var cleanup = CreateContext();
            Guid[] ids = await cleanup.Users.Where(user => user.PrimaryEmail == email).Select(user => user.Id).ToArrayAsync();
            await cleanup.OneTimeTokens.Where(token => ids.Contains(token.UserId)).ExecuteDeleteAsync();
            await cleanup.PasswordCredentials.Where(credential => ids.Contains(credential.UserId)).ExecuteDeleteAsync();
            await cleanup.Users.Where(user => ids.Contains(user.Id)).ExecuteDeleteAsync();
        }
    }

    [PostgreSqlFact]
    public async Task RequiredUniqueEmailIndexExists()
    {
        await using var context = CreateContext();
        await context.Database.OpenConnectionAsync();
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = """
            SELECT EXISTS (
              SELECT 1 FROM pg_index i
              JOIN pg_class t ON t.oid = i.indrelid
              JOIN pg_namespace n ON n.oid = t.relnamespace
              WHERE n.nspname = 'shared' AND t.relname = 'Users'
                AND i.indisunique AND i.indisvalid AND i.indpred IS NULL
                AND i.indnkeyatts = 1
                AND pg_get_expr(i.indexprs, i.indrelid) IN
                  ('lower(("PrimaryEmail")::text)', 'lower("PrimaryEmail")')
            )
            """;
        Assert.Equal(true, await command.ExecuteScalarAsync());
    }
}
