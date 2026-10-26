using System.Text.Json;
using DevFlow.EntityFrameworkCore.Reliability;
using DevFlow.Identity.Infrastructure.Messaging;
using DevFlow.Identity.Infrastructure.Persistence.Core;
using DevFlow.Identity.Infrastructure.Persistence.Outbox;
using DevFlow.Messaging.Configuration;
using DevFlow.Messaging.MassTransit;
using DevFlow.Messaging.Outbox;
using DevFlow.Notification.Application.Features.EmailDelivery.Commands.AcceptEmailDelivery;
using DevFlow.Notification.Contracts;
using DevFlow.SharedKernel.Cqrs;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DevFlow.Notification.Tests;
public sealed class BrokerIntegrationTests
{
    [DeliveryRestartFact]
    public async Task PoisonMessageErrorQueueSurvivesBrokerRestartAndCanBeReplayed()
    {
        string? container = Environment.GetEnvironmentVariable("DEVFLOW_DELIVERY_TEST_RABBIT_CONTAINER");
        ArgumentNullException.ThrowIfNull(container);
        if (!container.StartsWith("devflow-delivery-tests-", StringComparison.Ordinal) || !container.EndsWith("-rabbitmq", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Restart testing requires a disposable test broker.");
        }

        await using var fixture = await DeliveryFixture.CreateAsync();
        var broker = Broker(fixture);
        string queue = "devflow.poison.test." + Guid.NewGuid().ToString("N");
        var bus = Bus.Factory.CreateUsingRabbitMq(config =>
        {
            RabbitConfiguration.ConfigureHost(config, broker);
            config.Message<EmailDeliveryRequestedV1>(m => m.SetEntityName(queue + ".exchange"));
            config.ReceiveEndpoint(queue, endpoint =>
            {
                endpoint.SetQuorumQueue(1);
                endpoint.PublishFaults = false;
                endpoint.Handler<EmailDeliveryRequestedV1>(_ => throw new InvalidOperationException("Poison test."));
            });
        });
        await bus.StartAsync();
        var request = PersistenceIntegrationTests.Request(fixture);
        await bus.Publish(request, context => context.MessageId = request.MessageId);
        var factory = new RabbitMQ.Client.ConnectionFactory
        {
            HostName = broker.Host,
            Port = broker.Port,
            UserName = broker.Username,
            Password = File.ReadAllText(broker.PasswordFile).Trim(),
            VirtualHost = "/",
        };
        try
        {
            bool deadLettered = false;
            for (int i = 0; i < 30 && !deadLettered; i++)
            {
                await using var connection = await factory.CreateConnectionAsync();
                await using var channel = await connection.CreateChannelAsync();
                try
                {
                    var count = await channel.QueueDeclarePassiveAsync(queue + "_error");
                    deadLettered = count.MessageCount == 1;
                }
                catch (RabbitMQ.Client.Exceptions.OperationInterruptedException)
                {
                }

                if (!deadLettered)
                {
                    await Task.Delay(200);
                }
            }

            Assert.True(deadLettered);
        }
        finally
        {
            await bus.StopAsync();
        }

        await DisasterRecoveryTests.DockerAsync(["restart", container]);
        string binding = (await DisasterRecoveryTests.DockerOutputAsync(["port", container, "5672/tcp"])).Trim();
        broker.Port = ushort.Parse(binding.Split(':')[^1]);
        factory.Port = broker.Port;
        Environment.SetEnvironmentVariable("DEVFLOW_DELIVERY_TEST_RABBIT_PORT", broker.Port.ToString(System.Globalization.CultureInfo.InvariantCulture));
        bool restored = false;
        for (int i = 0; i < 30 && !restored; i++)
        {
            try
            {
                await using var connection = await factory.CreateConnectionAsync();
                await using var channel = await connection.CreateChannelAsync();
                var message = await channel.BasicGetAsync(queue + "_error", false);
                Assert.NotNull(message);
                string json = System.Text.Encoding.UTF8.GetString(message.Body.Span);
                Assert.DoesNotContain("alice@example.test", json);
                Assert.DoesNotContain(new string ('a', 43), json);
                // Return it so operator recovery still has the original durable message.
                await channel.BasicNackAsync(message.DeliveryTag, false, true);
                restored = true;
            }
            catch (RabbitMQ.Client.Exceptions.BrokerUnreachableException)
            {
                await Task.Delay(500);
            }
        }

        Assert.True(restored);
        var replayComplete = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var receiver = Bus.Factory.CreateUsingRabbitMq(config =>
        {
            RabbitConfiguration.ConfigureHost(config, broker);
            config.Message<EmailDeliveryRequestedV1>(m => m.SetEntityName(queue + ".exchange"));
            config.ReceiveEndpoint(queue, endpoint =>
            {
                endpoint.SetQuorumQueue(1);
                endpoint.PublishFaults = false;
                endpoint.Handler<EmailDeliveryRequestedV1>(context =>
                {
                    Assert.Equal(request.MessageId, context.MessageId);
                    replayComplete.TrySetResult();
                    return Task.CompletedTask;
                });
            });
        });
        await receiver.StartAsync();
        try
        {
            Assert.Equal(1, await BrokerErrorReplay.ReplayAsync<EmailDeliveryRequestedV1>(broker, receiver, queue + "_error", 1, default));
            await replayComplete.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            await receiver.StopAsync();
        }
    }

    private static MessagingOptions Broker(DeliveryFixture fixture)
    {
        string path = Path.Combine(fixture.Keys, "broker.secret");
        File.WriteAllText(path, Environment.GetEnvironmentVariable("DEVFLOW_DELIVERY_TEST_RABBIT_PASSWORD"));
        return new()
        {
            Host = "localhost",
            Port = ushort.Parse(Environment.GetEnvironmentVariable("DEVFLOW_DELIVERY_TEST_RABBIT_PORT")!),
            VirtualHost = "/",
            Username = "devflow_test",
            PasswordFile = path,
            Development = true,
            QuorumSize = 1
        };
    }

    [DeliveryBrokerFact]
    public async Task RealOutboxPublishConsumerRetryAfterCommitAndReceiptAreDurable()
    {
        await using var fixture = await DeliveryFixture.CreateAsync();
        await using var services = fixture.Services();
        var request = PersistenceIntegrationTests.Request(fixture);
        await using (var seed = fixture.Identity())
        {
            await new IdentityUnitOfWork(seed).ExecuteInTransactionAsync(async ct =>
            {
                await new IdentityEmailOutbox(seed).StageAsync(request, ct);
                return true;
            });
        }

        int calls = 0;
        var complete = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        string queue = "devflow.test." + Guid.NewGuid().ToString("N");
        var options = Broker(fixture);
        var bus = Bus.Factory.CreateUsingRabbitMq(config =>
        {
            RabbitConfiguration.ConfigureHost(config, options);
            config.Message<EmailDeliveryRequestedV1>(m => m.SetEntityName(queue + ".exchange"));
            config.ReceiveEndpoint(queue, endpoint =>
            {
                RabbitConfiguration.ConfigureQueue(endpoint, options);
                endpoint.Handler<EmailDeliveryRequestedV1>(async context =>
                {
                    await using var scope = services.CreateAsyncScope();
                    await scope.ServiceProvider.GetRequiredService<ISender>().SendAsync(new AcceptEmailDeliveryCommand(context.Message), context.CancellationToken);
                    if (Interlocked.Increment(ref calls) == 1)
                    {
                        throw new InvalidOperationException("Simulated crash after commit.");
                    }

                    complete.TrySetResult();
                });
            });
        });
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await bus.StartAsync(deadline.Token);
        try
        {
            await using var db = fixture.Identity();
            var store = new IdentityOutboxDispatchStore(db);
            var row = (await store.ClaimAsync(deadline.Token)).Single();
            var publisher = new MassTransitEventPublisher(bus, [new(EmailDeliveryRequestedV1.EventType, typeof(EmailDeliveryRequestedV1))]);
            await publisher.PublishAsync(row, deadline.Token);
            await store.PublishedAsync(row.MessageId, row.LeaseToken!.Value, deadline.Token);
            await complete.Task.WaitAsync(deadline.Token);
            Assert.Equal(2, calls);
            await using var notification = fixture.Notification();
            Assert.Single(await notification.EmailDeliveries.ToListAsync());
            Assert.Single(await notification.Set<InboxMessage>().ToListAsync());
            var receipt = JsonSerializer.Deserialize<EmailDeliveryRecordedV1>((await notification.Set<OutboxMessage>().OrderBy(x => x.OccurredAt).FirstAsync()).PayloadJson)!;
            await using var identity = fixture.Identity();
            await new IdentityUnitOfWork(identity).ExecuteInTransactionAsync(async ct =>
            {
                await new EmailDeliveryReceiptStore(identity).RecordAsync(receipt, ct);
                return true;
            });
            Assert.Single(await identity.Set<EmailDeliveryReceipt>().ToListAsync());
        }
        finally
        {
            await bus.StopAsync(CancellationToken.None);
        }
    }

    [DeliveryBrokerFact]
    public async Task ConfirmedPublicationMustNotSilentlySucceedWithNoBoundQueue()
    {
        await using var fixture = await DeliveryFixture.CreateAsync();
        var options = Broker(fixture);
        var bus = Bus.Factory.CreateUsingRabbitMq(config =>
        {
            RabbitConfiguration.ConfigureHost(config, options);
            config.Message<EmailDeliveryRequestedV1>(m => m.SetEntityName("devflow.unrouted.test." + Guid.NewGuid().ToString("N")));
        });
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await bus.StartAsync(deadline.Token);
        try
        {
            var request = PersistenceIntegrationTests.Request(fixture);
            var row = OutboxMessage.Create(request.MessageId, EmailDeliveryRequestedV1.EventType, 1, request.MessageId, 1, JsonSerializer.Serialize(request), request.CorrelationId, null, request.CreatedAt);
            var publisher = new MassTransitEventPublisher(bus, [new(EmailDeliveryRequestedV1.EventType, typeof(EmailDeliveryRequestedV1))]);
            await Assert.ThrowsAnyAsync<Exception>(() => publisher.PublishAsync(row, deadline.Token));
        }
        finally
        {
            await bus.StopAsync(CancellationToken.None);
        }
    }

    [DeliveryRestartFact]
    public async Task BrokerOutageLeavesCommittedOutboxRetryable()
    {
        string container = Environment.GetEnvironmentVariable("DEVFLOW_DELIVERY_TEST_RABBIT_CONTAINER")!;
        if (!container.StartsWith("devflow-delivery-tests-", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Outage testing requires a disposable test broker.");
        }
        await using var fixture = await DeliveryFixture.CreateAsync();
        var request = PersistenceIntegrationTests.Request(fixture);
        await using (var db = fixture.Identity())
        {
            await new IdentityUnitOfWork(db).ExecuteInTransactionAsync(async ct =>
            {
                await new IdentityEmailOutbox(db).StageAsync(request, ct);
                return true;
            });
        }
        var broker = Broker(fixture);
        string exchange = "devflow.outage.test." + Guid.NewGuid().ToString("N");
        var bus = Bus.Factory.CreateUsingRabbitMq(config =>
        {
            RabbitConfiguration.ConfigureHost(config, broker);
            config.Message<EmailDeliveryRequestedV1>(m => m.SetEntityName(exchange));
            config.ReceiveEndpoint(exchange, endpoint =>
            {
                RabbitConfiguration.ConfigureQueue(endpoint, broker);
                endpoint.Handler<EmailDeliveryRequestedV1>(_ => Task.CompletedTask);
            });
        });
        await bus.StartAsync();
        bool paused = false;
        try
        {
            var registrations = new ServiceCollection();
            registrations.AddScoped(_ => fixture.Identity());
            registrations.AddScoped<IOutboxDispatchStore, IdentityOutboxDispatchStore>();
            registrations.AddSingleton<IIntegrationEventPublisher>(new MassTransitEventPublisher(bus,
                [new(EmailDeliveryRequestedV1.EventType, typeof(EmailDeliveryRequestedV1))]));
            await using var services = registrations.BuildServiceProvider();
            await DisasterRecoveryTests.DockerAsync(["pause", container]);
            paused = true;
            var relay = new OutboxRelay(services.GetRequiredService<IServiceScopeFactory>(), NullLogger<OutboxRelay>.Instance);
            await relay.RunOnceAsync(default);
            await using var verify = fixture.Identity();
            var row = await verify.Set<OutboxMessage>().SingleAsync();
            Assert.Equal(OutboxStatus.Pending, row.Status);
            Assert.Null(row.PublishedAt);
            Assert.Null(row.LeaseToken);
            Assert.Equal("PublicationUnavailable", row.FailureCode);
        }
        finally
        {
            if (paused)
            {
                await DisasterRecoveryTests.DockerAsync(["unpause", container]);
            }
            await bus.StopAsync();
        }
    }
}
