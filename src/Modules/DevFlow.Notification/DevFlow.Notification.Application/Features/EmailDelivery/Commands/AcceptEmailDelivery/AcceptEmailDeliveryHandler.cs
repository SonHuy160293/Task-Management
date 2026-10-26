using DevFlow.Notification.Application.Abstractions;
using DevFlow.SharedKernel.Abstractions.Message;
using DevFlow.SharedKernel.Abstractions.Shared;

namespace DevFlow.Notification.Application.Features.EmailDelivery.Commands.AcceptEmailDelivery;

public sealed class AcceptEmailDeliveryHandler(IEmailDeliveryRepository repository) : ICommandHandler<AcceptEmailDeliveryCommand, bool>
{
    public async Task<Result<bool>> HandleAsync(AcceptEmailDeliveryCommand request, CancellationToken ct)
    {
        await repository.AcceptAsync(request.Event, ct);
        return Result.Success(true);
    }
}
