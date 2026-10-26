using System.Security.Authentication;
using MassTransit;

namespace DevFlow.Messaging.Configuration;
public static class RabbitConfiguration
{
    public static void ConfigureHost(IRabbitMqBusFactoryConfigurator bus, MessagingOptions options)
    {
        if (!options.Valid())
        {
            throw new InvalidOperationException("Invalid broker configuration or missing secret.");
        }

        bus.Host(options.Host, options.Port, options.VirtualHost, host =>
        {
            host.Username(options.Username);
            host.Password(File.ReadAllText(options.PasswordFile).Trim());
            host.PublisherConfirmation = true;
            if (options.UseTls)
            {
                host.UseSsl(ssl =>
                {
                    ssl.Protocol = SslProtocols.Tls12 | SslProtocols.Tls13;
                });
            }
        });
    }

    public static void ConfigureQueue(IRabbitMqReceiveEndpointConfigurator endpoint, MessagingOptions options)
    {
        endpoint.Durable = true;
        endpoint.AutoDelete = false;
        endpoint.SetQuorumQueue(options.QuorumSize);
        endpoint.PrefetchCount = 8;
        endpoint.ConcurrentMessageLimit = 1;
        endpoint.PublishFaults = false;
        endpoint.UseMessageRetry(retry => retry.Intervals(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(30)));
    }
}
