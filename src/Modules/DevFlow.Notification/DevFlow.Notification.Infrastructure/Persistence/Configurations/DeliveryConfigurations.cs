using DevFlow.Notification.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevFlow.Notification.Infrastructure.Persistence.Configurations;
public sealed class EmailDeliveryConfiguration : IEntityTypeConfiguration<EmailDelivery>
{
    public void Configure(EntityTypeBuilder<EmailDelivery> b)
    {
        b.ToTable("EmailDeliveries", "notification", t =>
        {
            t.HasCheckConstraint("CkEmailDeliveryTime", "\"ExpiresAt\" > \"CreatedAt\"");
            t.HasCheckConstraint("CkEmailDeliveryAttempts", "\"Attempts\" >= 0");
            t.HasCheckConstraint("CkEmailDeliveryStatus", "\"Status\" IN ('Pending','Processing','ProviderAccepted','DeadLetter','Expired','Suppressed')");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Version).IsConcurrencyToken();
        b.Property(x => x.RequestHash).HasMaxLength(64).IsRequired();
        b.Property(x => x.RequestJson).HasColumnType("text").IsRequired();
        b.Property(x => x.FrozenRequestJson).HasColumnType("text");
        b.Property(x => x.Status).HasMaxLength(32).IsRequired();
        b.Property(x => x.Outcome).HasMaxLength(32).IsRequired();
        b.Property(x => x.FailureCode).HasMaxLength(64);
        b.Property(x => x.ProviderId).HasMaxLength(200);
        b.HasIndex(x => new { x.Status, x.NextAttemptAt });
        b.HasIndex(x => new { x.Status, x.LeaseUntil });
        b.HasIndex(x => x.ProviderId).IsUnique().HasFilter("\"ProviderId\" IS NOT NULL");
    }
}

public sealed class EmailAttemptConfiguration : IEntityTypeConfiguration<EmailDeliveryAttempt>
{
    public void Configure(EntityTypeBuilder<EmailDeliveryAttempt> b)
    {
        b.ToTable("EmailDeliveryAttempts", "notification");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Outcome).HasMaxLength(32);
        b.Property(x => x.FailureCode).HasMaxLength(64);
        b.HasIndex(x => new { x.DeliveryId, x.Attempt }).IsUnique();
        b.HasOne<EmailDelivery>().WithMany().HasForeignKey(x => x.DeliveryId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class EmailSuppressionConfiguration : IEntityTypeConfiguration<EmailSuppression>
{
    public void Configure(EntityTypeBuilder<EmailSuppression> b)
    {
        b.ToTable("EmailSuppressions", "notification");
        b.HasKey(x => x.RecipientHash);
        b.Property(x => x.RecipientHash).HasMaxLength(64);
        b.Property(x => x.Reason).HasMaxLength(64);
    }
}

public sealed class EmailWebhookConfiguration : IEntityTypeConfiguration<EmailWebhookReceipt>
{
    public void Configure(EntityTypeBuilder<EmailWebhookReceipt> b)
    {
        b.ToTable("EmailWebhookReceipts", "notification");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.EventId).HasMaxLength(200).IsRequired();
        b.HasIndex(x => x.EventId).IsUnique();
        b.Property(x => x.ProviderId).HasMaxLength(200).IsRequired();
        b.Property(x => x.PayloadHash).HasMaxLength(64).IsRequired();
        b.Property(x => x.EventType).HasMaxLength(64).IsRequired();
        b.Property(x => x.RecipientHash).HasMaxLength(64);
        b.HasIndex(x => new { x.ProcessedAt, x.ProviderId });
    }
}

public sealed class EmailControlConfiguration : IEntityTypeConfiguration<EmailDeliveryControl>
{
    public void Configure(EntityTypeBuilder<EmailDeliveryControl> b)
    {
        b.ToTable("EmailDeliveryControls", "notification");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.PauseReason).HasMaxLength(64);
        b.Property(x => x.Day).HasMaxLength(10);
        b.Property(x => x.Month).HasMaxLength(7);
        b.HasData(new EmailDeliveryControl());
    }
}

public sealed class EmailOperationConfiguration : IEntityTypeConfiguration<EmailOperation>
{
    public void Configure(EntityTypeBuilder<EmailOperation> b)
    {
        b.ToTable("EmailOperations", "notification");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Operator).HasMaxLength(160);
        b.Property(x => x.Action).HasMaxLength(64);
        b.Property(x => x.Reason).HasMaxLength(300);
    }
}
