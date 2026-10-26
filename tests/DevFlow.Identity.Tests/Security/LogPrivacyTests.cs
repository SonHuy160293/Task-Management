using DevFlow.Identity.Application.Features.Auth.Commands.Registration;
using DevFlow.SharedKernel.Abstractions.Shared;
using DevFlow.SharedKernel.Behaviors;
using Microsoft.Extensions.Logging;
using Xunit;

namespace DevFlow.Identity.Tests.Security;

public sealed class LogPrivacyTests
{
    [Fact]
    public async Task RegistrationPipelineLoggingExcludesCredentialPayload()
    {
        const string password = "never print this password";
        var request = new RegisterAccountCommand("private@example.test", password, "Private", null, null);
        var logger = new CapturingLogger<LoggingBehavior<RegisterAccountCommand, Result<RegistrationAcceptedResponse>>>();
        var behavior = new LoggingBehavior<RegisterAccountCommand, Result<RegistrationAcceptedResponse>>(logger);
        await behavior.HandleAsync(
            request,
            () => Task.FromResult(Result.Success(new RegistrationAcceptedResponse(RegistrationAcceptedResponse.AcceptedMessage))),
            CancellationToken.None);
        Assert.Equal(2, logger.Messages.Count);
        Assert.All(logger.Messages, message =>
        {
            Assert.DoesNotContain(password, message);
            Assert.DoesNotContain(request.Email!, message);
        });
        Assert.DoesNotContain(password, request.ToString());
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(
            LogLevel level,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
    }
}
