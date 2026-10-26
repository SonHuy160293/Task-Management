using DevFlow.Messaging.Configuration;
using DevFlow.Notification.Contracts;
using MassTransit;
using Microsoft.Extensions.Configuration;

namespace DevFlow.Notification.Infrastructure.Messaging;
public static class NotificationTopology
{
    public static async Task DeployAsync(IConfiguration configuration, CancellationToken ct)
    {
        var broker = configuration.GetSection("Messaging").Get<MessagingOptions>() ?? new();
        var bus = Bus.Factory.CreateUsingRabbitMq(config =>
        {
            RabbitConfiguration.ConfigureHost(config, broker);
            config.DeployTopologyOnly = true;
            config.Message<EmailDeliveryRequestedV1>(m => m.SetEntityName("devflow.notification.email-requested.v1"));
            config.Message<EmailDeliveryRecordedV1>(m => m.SetEntityName("devflow.notification.email-recorded.v1"));
            config.ReceiveEndpoint("devflow.notification.email-requested.v1", endpoint =>
            {
                RabbitConfiguration.ConfigureQueue(endpoint, broker);
                endpoint.ConfigureConsumeTopology = false;
                endpoint.Bind<EmailDeliveryRequestedV1>();
            });
            config.ReceiveEndpoint("devflow.identity.email-recorded.v1", endpoint =>
            {
                RabbitConfiguration.ConfigureQueue(endpoint, broker);
                endpoint.ConfigureConsumeTopology = false;
                endpoint.Bind<EmailDeliveryRecordedV1>();
            });
            foreach (string queue in new[]
            {
                "devflow.notification.email-requested.v1_error",
                "devflow.notification.email-requested.v1_skipped",
                "devflow.identity.email-recorded.v1_error",
                "devflow.identity.email-recorded.v1_skipped",
                "devflow.messaging.unrouted"
            }

            )
            {
                config.ReceiveEndpoint(queue, endpoint =>
                {
                    RabbitConfiguration.ConfigureQueue(endpoint, broker);
                    endpoint.ConfigureConsumeTopology = false;
                });
            }
        });
        try
        {
            await bus.StartAsync(ct);
        }
        finally
        {
            await bus.StopAsync(CancellationToken.None);
        }
    }
}
