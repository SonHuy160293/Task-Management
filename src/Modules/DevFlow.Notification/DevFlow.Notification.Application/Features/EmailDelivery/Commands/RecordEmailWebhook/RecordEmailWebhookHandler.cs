using DevFlow.Notification.Application.Abstractions;
using DevFlow.SharedKernel.Abstractions.Message;
using DevFlow.SharedKernel.Abstractions.Shared;

namespace DevFlow.Notification.Application.Features.EmailDelivery.Commands.RecordEmailWebhook;

public sealed class RecordEmailWebhookHandler(IEmailDeliveryRepository repository) : ICommandHandler<RecordEmailWebhookCommand, bool>
{
    public async Task<Result<bool>> HandleAsync(RecordEmailWebhookCommand request, CancellationToken ct)
    {
        await repository.RecordWebhookAsync(request.Event, ct);
        return Result.Success(true);
    }
}
