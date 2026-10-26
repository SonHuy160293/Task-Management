using DevFlow.SharedKernel.Abstractions.Message;

namespace DevFlow.Notification.Application.Features.EmailDelivery.Commands.AcceptEmailDelivery;

public sealed class AcceptEmailDeliveryValidator : IValidator<AcceptEmailDeliveryCommand>
{
    public Task ValidateAsync(AcceptEmailDeliveryCommand request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        try
        {
            request.Event.Validate();
            if (request.Event.CreatedAt > DateTimeOffset.UtcNow.AddMinutes(5))
            {
                throw new ArgumentException("Future event.");
            }
        }
        catch (ArgumentException)
        {
            throw new CqrsValidationException(["Invalid email delivery event."]);
        }

        return Task.CompletedTask;
    }
}
