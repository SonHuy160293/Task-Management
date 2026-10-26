using DevFlow.SharedKernel.Abstractions.Message;

namespace DevFlow.Identity.Application.Features.Auth.Commands.Registration;

public sealed record RegisterAccountCommand(
    string? Email,
    string? Password,
    string? DisplayName,
    string? Locale,
    string? TimeZone) : ICommand<RegistrationAcceptedResponse>
{
    public override string ToString() => "RegisterAccountCommand [REDACTED]";
}
