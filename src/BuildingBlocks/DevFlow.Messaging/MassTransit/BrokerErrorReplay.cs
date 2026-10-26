using System.Text.Json;
using DevFlow.Messaging.Configuration;
using MassTransit;
using RabbitMQ.Client;

namespace DevFlow.Messaging.MassTransit;
public static class BrokerErrorReplay
{
    public static async Task<int> ReplayAsync<T>(MessagingOptions options, IBus bus, string queue, int limit, CancellationToken ct)
        where T : class
    {
        if (limit is < 1 or > 100)
        {
            throw new ArgumentException("Replay limit is 1..100.");
        }

        var factory = new ConnectionFactory
        {
            HostName = options.Host,
            Port = options.Port,
            VirtualHost = options.VirtualHost,
            UserName = options.Username,
            Password = File.ReadAllText(options.PasswordFile).Trim(),
            Ssl = new SslOption
            {
                Enabled = options.UseTls,
                ServerName = options.Host
            },
        };
        await using var connection = await factory.CreateConnectionAsync(ct);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: ct);
        int replayed = 0;
        for (int i = 0; i < limit; i++)
        {
            var item = await channel.BasicGetAsync(queue, false, ct);
            if (item is null)
            {
                break;
            }

            try
            {
                using var envelope = JsonDocument.Parse(item.Body);
                var message = envelope.RootElement.GetProperty("message").Deserialize<T>(new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? throw new ArgumentException("Invalid broker error message.");
                var id = envelope.RootElement.GetProperty("messageId").GetGuid();
                await bus.Publish(message, context =>
                {
                    context.MessageId = id;
                    if (context.TryGetPayload<RabbitMqSendContext>(out var rabbit))
                    {
                        rabbit.Mandatory = true;
                    }
                }, ct);
                await channel.BasicAckAsync(item.DeliveryTag, false, ct);
                replayed++;
            }
            catch
            {
                await channel.BasicNackAsync(item.DeliveryTag, false, true, CancellationToken.None);
                throw;
            }
        }

        return replayed;
    }
}
