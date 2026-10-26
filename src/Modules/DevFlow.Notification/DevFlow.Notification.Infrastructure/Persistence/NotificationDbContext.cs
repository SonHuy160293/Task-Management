using DevFlow.EntityFrameworkCore.Reliability;
using DevFlow.Notification.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace DevFlow.Notification.Infrastructure.Persistence;
public sealed class NotificationDbContext(DbContextOptions<NotificationDbContext> options) : DbContext(options)
{
    public DbSet<EmailDelivery> EmailDeliveries => Set<EmailDelivery>();
    public DbSet<EmailDeliveryAttempt> EmailDeliveryAttempts => Set<EmailDeliveryAttempt>();
    public DbSet<EmailSuppression> EmailSuppressions => Set<EmailSuppression>();
    public DbSet<EmailWebhookReceipt> EmailWebhookReceipts => Set<EmailWebhookReceipt>();
    public DbSet<EmailDeliveryControl> EmailDeliveryControls => Set<EmailDeliveryControl>();
    public DbSet<EmailOperation> EmailOperations => Set<EmailOperation>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.ApplyReliabilityConfigurations(ReliabilityDatabaseProvider.PostgreSql);
        DevFlow.Messaging.Outbox.OutboxAdministration.Configure(model);
        model.ApplyConfigurationsFromAssembly(typeof(NotificationDbContext).Assembly);
    }
}
