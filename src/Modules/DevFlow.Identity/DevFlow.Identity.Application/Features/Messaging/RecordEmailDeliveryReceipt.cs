using DevFlow.Identity.Application.Abstractions.Messaging;
using DevFlow.Notification.Contracts;
using DevFlow.SharedKernel.Abstractions.Message;
using DevFlow.SharedKernel.Abstractions.Shared;

namespace DevFlow.Identity.Application.Features.Messaging;
public sealed record RecordEmailDeliveryReceiptCommand(EmailDeliveryRecordedV1 Receipt) : ICommand<bool>;
public sealed class RecordEmailDeliveryReceiptHandler(IEmailDeliveryReceiptStore store) : ICommandHandler<RecordEmailDeliveryReceiptCommand, bool>
{
    public async Task<Result<bool>> HandleAsync(RecordEmailDeliveryReceiptCommand request, CancellationToken ct)
    {
        await store.RecordAsync(request.Receipt, ct);
        return Result.Success(true);
    }
}
