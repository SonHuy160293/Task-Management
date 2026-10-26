using DevFlow.Notification.Application.Abstractions;
using DevFlow.SharedKernel.Abstractions.Message;

namespace DevFlow.Notification.Application.Features.EmailDelivery.Commands.RecordEmailWebhook;

public sealed record RecordEmailWebhookCommand(ProviderEmailEvent Event) : ICommand<bool>;
