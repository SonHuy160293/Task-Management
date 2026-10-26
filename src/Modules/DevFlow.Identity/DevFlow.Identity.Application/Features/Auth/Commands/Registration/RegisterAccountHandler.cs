using DevFlow.Identity.Application.Abstractions.Repositories;
using DevFlow.Identity.Application.Abstractions.Messaging;
using DevFlow.Identity.Application.Security;
using DevFlow.Identity.Application.Abstractions.Security;
using DevFlow.Identity.Domain.Models;
using DevFlow.SharedKernel.Abstractions.Message;
using DevFlow.SharedKernel.Abstractions.Shared;
using DevFlow.SharedKernel.Exceptions;

namespace DevFlow.Identity.Application.Features.Auth.Commands.Registration;

public sealed class RegisterAccountHandler(
   IUserRepository users,
   IPasswordCredentialRepository credentials,
   IOneTimeTokenRepository tokens,
   IPasswordHasher passwordHasher,
   IVerificationTokenGenerator verificationTokenGenerator,
   IVerificationEmailRequestFactory emailFactory,
   IVerificationEmailOutbox emailOutbox)
   : ICommandHandler<RegisterAccountCommand, RegistrationAcceptedResponse>
{
    public async Task<Result<RegistrationAcceptedResponse>> HandleAsync(
        RegisterAccountCommand request,
        CancellationToken cancellationToken)
    {
        string password = request.Password!;
        if (CommonPasswordBlocklist.Contains(password))
        {
            throw new ValidationException(
            [
                new ValidationError("request", "Password is too common."),
            ]);
        }

        string passwordHash = await passwordHasher.HashAsync(password, cancellationToken);
        VerificationToken generatedToken = verificationTokenGenerator.Generate(cancellationToken);
        string normalizedEmail = RegistrationEmail.Normalize(request.Email)!;

        var user = User.Create(
            normalizedEmail,
            request.DisplayName!.Trim(),
            locale: string.IsNullOrWhiteSpace(request.Locale) ? "en" : request.Locale.Trim(),
            timeZone: string.IsNullOrWhiteSpace(request.TimeZone) ? "UTC" : request.TimeZone.Trim());
        var credential = PasswordCredential.Create(user.Id, passwordHash);
        var token = OneTimeToken.Create(
            user.Id,
            generatedToken.Purpose,
            generatedToken.TokenHash,
            generatedToken.CreatedAt,
            generatedToken.ExpiresAt);

        if (!await users.ExistsForRegistrationAsync(normalizedEmail, cancellationToken))
        {
            await users.AddAsync(user, cancellationToken);
            await credentials.AddAsync(credential, cancellationToken);
            await tokens.AddAsync(token, cancellationToken);
            Guid messageId = Guid.CreateVersion7();
            var emailRequest = emailFactory.Create(messageId, normalizedEmail, request.DisplayName!.Trim(),
                string.IsNullOrWhiteSpace(request.Locale) ? "en" : request.Locale.Trim(),
                generatedToken.RawToken, generatedToken.CreatedAt, generatedToken.ExpiresAt);
            await emailOutbox.StageAsync(emailRequest, cancellationToken);
        }

        return Result.Success(new RegistrationAcceptedResponse(RegistrationAcceptedResponse.AcceptedMessage));
    }
}
