using DevFlow.Identity.Application.Abstractions.Messaging;
using DevFlow.Identity.Infrastructure.Messaging;
using DevFlow.Identity.Infrastructure.Persistence.Outbox;
using DevFlow.Messaging.Configuration;
using DevFlow.Messaging.Outbox;
using DevFlow.Messaging.MassTransit;
using DevFlow.Notification.Contracts;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
namespace DevFlow.Identity.Infrastructure.DepedencyInjection;
public static class IdentityMessagingExtensions
{
    public static IServiceCollection AddIdentityMessaging(this IServiceCollection services, IConfiguration configuration)
    {
        var broker = configuration.GetSection("Messaging").Get<MessagingOptions>() ?? new();
        services.Configure<MassTransitHostOptions>(options =>
        {
            options.WaitUntilStarted = true;
            options.StartTimeout = TimeSpan.FromSeconds(60);
            options.StopTimeout = TimeSpan.FromSeconds(30);
        });
        services.AddScoped<IVerificationEmailOutbox, IdentityEmailOutbox>();
        services.AddScoped<IEmailDeliveryReceiptStore, EmailDeliveryReceiptStore>();
        services.AddScoped<IOutboxDispatchStore, IdentityOutboxDispatchStore>();
        services.AddScoped<IIntegrationEventPublisher, MassTransitEventPublisher>();
        services.AddSingleton(new IntegrationEventRegistration(EmailDeliveryRequestedV1.EventType, typeof(EmailDeliveryRequestedV1), value => ((EmailDeliveryRequestedV1)value).Validate()));
        services.AddMassTransit(x =>
        {
            x.AddConsumer<EmailDeliveryRecordedConsumer>();
            x.UsingRabbitMq((context, bus) =>
            {
                RabbitConfiguration.ConfigureHost(bus, broker);
                bus.OverrideDefaultBusEndpointQueueName("devflow.identity.worker.bus." + Guid.NewGuid().ToString("N"));
                bus.Message<EmailDeliveryRequestedV1>(m => m.SetEntityName("devflow.notification.email-requested.v1"));
                bus.Message<EmailDeliveryRecordedV1>(m => m.SetEntityName("devflow.notification.email-recorded.v1"));
                bus.ReceiveEndpoint("devflow.identity.email-recorded.v1", endpoint =>
                {
                    RabbitConfiguration.ConfigureQueue(endpoint, broker);
                    endpoint.ConfigureConsumer<EmailDeliveryRecordedConsumer>(context);
                });
            });
        });
        services.AddHostedService<OutboxRelay>();
        services.AddHostedService<IdentityOutboxMaintenanceWorker>();
        return services;
    }
}
