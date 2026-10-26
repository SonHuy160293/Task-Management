namespace DevFlow.Identity.Application.Features.Auth.Commands.Registration;

public sealed record RegistrationAcceptedResponse(string Message)
{
    public const string AcceptedMessage = "If eligible, your registration request has been accepted.";
}
