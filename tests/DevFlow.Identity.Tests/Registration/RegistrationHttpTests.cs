using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DevFlow.AspNetCore.Filters;
using DevFlow.AspNetCore.DependencyInjection;
using DevFlow.AspNetCore.Middleware;
using DevFlow.Identity.API.Controllers;
using DevFlow.Identity.API.Security;
using DevFlow.Identity.Application.Abstractions.Repositories;
using DevFlow.Identity.Application.Abstractions.Messaging;
using DevFlow.Identity.Infrastructure.Messaging;
using DevFlow.Notification.Contracts;
using System.Security.Cryptography;
using DevFlow.Identity.Application.Abstractions.Security;
using DevFlow.Identity.Application.DependencyInjection;
using DevFlow.Identity.Application.Security;
using DevFlow.Identity.Domain.Models;
using DevFlow.SharedKernel.Abstractions.Persistence;
using DevFlow.SharedKernel.Abstractions.Shared;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace DevFlow.Identity.Tests.Registration;

public sealed class RegistrationHttpTests
{
    [Fact]
    public async Task AnonymousRegistrationAndDuplicateHaveSameResponseAndPreserveCredentials()
    {
        await using WebApplication app = await CreateAsync();
        using HttpClient client = app.GetTestClient();
        var request = new { email = " Alice@Example.COM ", password = "a long unique passphrase 2026", displayName = " Alice ", locale = "en", timeZone = "Asia/Ho_Chi_Minh" };
        using var first = await client.PostAsJsonAsync("/api/v1/auth/register", request);
        string original = await first.Content.ReadAsStringAsync();
        using var duplicate = await client.PostAsJsonAsync("/api/v1/auth/register", request with { email = "ALICE@example.com", password = "a different long password 2026" });
        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(original, await duplicate.Content.ReadAsStringAsync());
        var store = app.Services.GetRequiredService<Store>();
        Assert.Equal(1, store.UserAdds);
        Assert.Single(store.Messages);
        Assert.Equal(store.Messages[0].MessageId, store.Messages[0].CorrelationId);
        Assert.Null(store.Messages[0].CausationId);
        Assert.DoesNotContain("alice@example.com", JsonSerializer.Serialize(store.Messages[0]));
        Assert.DoesNotContain("secret", JsonSerializer.Serialize(store.Messages[0]));
        Assert.Equal("alice@example.com", store.User!.PrimaryEmail);
        Assert.Equal("Alice", store.User.DisplayName);
        Assert.Equal("pending", store.User.Status);
        Assert.Equal("phc:a long unique passphrase 2026", store.Credential!.PasswordHash);
        Assert.Equal("verify_email", store.Token!.Purpose);
        Assert.Null(store.Token.ConsumedAt);
        Assert.Equal(32, store.Token.TokenHash.Length);
        Assert.DoesNotContain("phc:", original);
    }

    [Theory]
    [InlineData("alice@example.com", "short", "UTC")]
    [InlineData("alice@example.com", "a long unique passphrase 2026", "bad/zone")]
    public async Task InvalidInputReturns400BeforeTransaction(string email, string password, string timeZone)
    {
        await using WebApplication app = await CreateAsync();
        using var client = app.GetTestClient();
        using var result = await client.PostAsJsonAsync("/api/v1/auth/register", new { email, password, timeZone, displayName = "Alice" });
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        using var body = JsonDocument.Parse(await result.Content.ReadAsStringAsync());
        AssertFailureEnvelope(body.RootElement, "Validation.Failure");
        Assert.NotEmpty(body.RootElement.GetProperty("error").GetProperty("errors").EnumerateObject());
        Assert.Equal(0, app.Services.GetRequiredService<Store>().Transactions);
    }

    [Fact]
    public async Task InvalidEmailIsRejectedByProtectionBeforeTransaction()
    {
        await using WebApplication app = await CreateAsync();
        using var client = app.GetTestClient();
        using var result = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { email = "bad", password = "a long unique passphrase 2026", timeZone = "UTC", displayName = "Alice" });
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        Assert.Equal(0, app.Services.GetRequiredService<Store>().Transactions);
    }

    [Theory]
    [InlineData(RegistrationLimitStatus.Limited, 429)]
    [InlineData(RegistrationLimitStatus.Unavailable, 503)]
    public async Task LimiterFailureStopsHandler(RegistrationLimitStatus status, int code)
    {
        await using WebApplication app = await CreateAsync(status);
        using var client = app.GetTestClient();
        using var response = await client.PostAsJsonAsync("/api/v1/auth/register", new { email = "alice@example.com" });
        Assert.Equal(code, (int)response.StatusCode);
        if (code == 429)
        {
            Assert.NotNull(response.Headers.RetryAfter);
        }

        Assert.Equal(0, app.Services.GetRequiredService<Store>().Transactions);
    }

    [Fact]
    public async Task CommonPasswordReturns400WithExistingEnvelope()
    {
        await using WebApplication app = await CreateAsync();
        using var client = app.GetTestClient();
        using var response = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { email = "alice@example.com", password = "PASSWORDPASSWORD", displayName = "Alice" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        AssertFailureEnvelope(body.RootElement, "Validation.Failure");
        var errors = body.RootElement.GetProperty("error").GetProperty("errors").GetProperty("request");
        Assert.Equal("Password is too common.", errors[0].GetString());
        Assert.Equal(0, app.Services.GetRequiredService<Store>().UserAdds);
        Assert.Equal(0, app.Services.GetRequiredService<Hasher>().HashCalls);
    }

    [Fact]
    public async Task PayloadAndDuplicateFieldsAreRejected()
    {
        await using WebApplication app = await CreateAsync();
        using var client = app.GetTestClient();
        using var large = await client.PostAsync("/api/v1/auth/register", new StringContent(new string('a', 17000)));
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, large.StatusCode);
        using var ambiguous = await client.PostAsync(
            "/api/v1/auth/register",
            new StringContent("{\"email\":\"a@example.com\",\"Email\":\"b@example.com\"}"));
        Assert.Equal(HttpStatusCode.BadRequest, ambiguous.StatusCode);
    }

    private static void AssertFailureEnvelope(JsonElement body, string code)
    {
        Assert.False(body.GetProperty("success").GetBoolean());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("data").ValueKind);
        var error = body.GetProperty("error");
        Assert.Equal(code, error.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(error.GetProperty("traceId").GetString()));
    }

    private static async Task<WebApplication> CreateAsync(RegistrationLimitStatus limit = RegistrationLimitStatus.Allowed)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddLogging();
        builder.Services.AddApplication();
        builder.Services.AddConfiguredApiVersioning();
        builder.Services.AddControllers(options => options.Filters.Add<ApiResponseEnvelopeFilter>()).AddApplicationPart(typeof(AuthController).Assembly);
        builder.Services.AddSingleton<Store>();
        builder.Services.AddSingleton<IUserRepository>(sp => sp.GetRequiredService<Store>());
        builder.Services.AddSingleton<IPasswordCredentialRepository>(sp => sp.GetRequiredService<Store>());
        builder.Services.AddSingleton<IOneTimeTokenRepository>(sp => sp.GetRequiredService<Store>());
        builder.Services.AddSingleton<IUnitOfWork>(sp => sp.GetRequiredService<Store>());
        builder.Services.AddSingleton<IRegistrationRateLimiter>(new Limiter(limit));
        builder.Services.AddSingleton<Hasher>();
        builder.Services.AddSingleton<IPasswordHasher>(sp => sp.GetRequiredService<Hasher>());
        builder.Services.AddSingleton<IVerificationTokenGenerator, Tokens>();
        using RSA rsa = RSA.Create(3072);
        builder.Services.AddSingleton<IVerificationEmailRequestFactory>(new VerificationEmailRequestFactory("test", rsa.ExportSubjectPublicKeyInfoPem()));
        builder.Services.AddSingleton<IVerificationEmailOutbox>(sp => sp.GetRequiredService<Store>());
        var app = builder.Build();
        app.UseMiddleware<GlobalExceptionHandlingMiddleware>();
        app.Use(async (context, next) =>
        {
            context.Connection.RemoteIpAddress = IPAddress.Loopback;
            await next(context);
        });
        app.UseIdentityRegistrationProtection();
        app.MapControllers();
        await app.StartAsync();
        return app;
    }

    private sealed class Store : IUserRepository, IPasswordCredentialRepository, IOneTimeTokenRepository, IUnitOfWork, IVerificationEmailOutbox
    {
        public List<EmailDeliveryRequestedV1> Messages { get; } = [];
        public Task StageAsync(EmailDeliveryRequestedV1 request, CancellationToken ct) { Messages.Add(request); return Task.CompletedTask; }
        public int UserAdds { get; private set; }
        public int Transactions { get; private set; }
        public User? User { get; private set; }
        public PasswordCredential? Credential { get; private set; }
        public OneTimeToken? Token { get; private set; }
        public Task<bool> ExistsForRegistrationAsync(string email, CancellationToken ct) => Task.FromResult(User is not null);
        public Task AddAsync(User entity, CancellationToken ct)
        {
            UserAdds++;
            User = entity;
            return Task.CompletedTask;
        }

        public Task AddAsync(PasswordCredential entity, CancellationToken ct)
        {
            Credential = entity;
            return Task.CompletedTask;
        }

        public Task AddAsync(OneTimeToken entity, CancellationToken ct)
        {
            Token = entity;
            return Task.CompletedTask;
        }
        Task<User> DevFlow.Identity.Application.Abstractions.Persistence.IRepositoryBase<User>.GetByIdAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
        Task<PasswordCredential> DevFlow.Identity.Application.Abstractions.Persistence.IRepositoryBase<PasswordCredential>.GetByIdAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
        Task<OneTimeToken> DevFlow.Identity.Application.Abstractions.Persistence.IRepositoryBase<OneTimeToken>.GetByIdAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
        public Task UpdateAsync(User entity, CancellationToken ct) => throw new NotSupportedException();
        public Task UpdateAsync(PasswordCredential entity, CancellationToken ct) => throw new NotSupportedException();
        public Task UpdateAsync(OneTimeToken entity, CancellationToken ct) => throw new NotSupportedException();
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public async Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct = default)
        {
            Transactions++;
            return await action(ct);
        }
    }

    private sealed class Limiter(RegistrationLimitStatus status) : IRegistrationRateLimiter
    {
        public Task<RegistrationLimitResult> CheckIpAsync(IPAddress address, CancellationToken ct) => Task.FromResult(new RegistrationLimitResult(status, TimeSpan.FromSeconds(5)));
        public Task<RegistrationLimitResult> CheckEmailAsync(string email, CancellationToken ct) => Task.FromResult(new RegistrationLimitResult(status, TimeSpan.FromSeconds(5)));
    }

    private sealed class Hasher : IPasswordHasher
    {
        public int HashCalls { get; private set; }

        public Task<string> HashAsync(string password, CancellationToken ct)
        {
            HashCalls++;
            return Task.FromResult("phc:" + password);
        }

        public Task<PasswordVerificationResult> VerifyAsync(string password, string hash, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class Tokens : IVerificationTokenGenerator
    {
        public VerificationToken Generate(CancellationToken ct) { var now = DateTimeOffset.UtcNow; return new("secret", new byte[32], now, now.AddDays(1)); }
    }
}
