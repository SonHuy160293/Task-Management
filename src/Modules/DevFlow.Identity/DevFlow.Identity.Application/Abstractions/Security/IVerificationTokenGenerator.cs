using DevFlow.Identity.Application.Security;
namespace DevFlow.Identity.Application.Abstractions.Security;

public interface IVerificationTokenGenerator
{
    VerificationToken Generate(CancellationToken cancellationToken);
}
