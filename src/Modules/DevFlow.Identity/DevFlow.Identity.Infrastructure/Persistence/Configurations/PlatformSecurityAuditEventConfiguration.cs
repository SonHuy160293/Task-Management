using DevFlow.Identity.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevFlow.Identity.Infrastructure.Persistence.Configurations;

public sealed class PlatformSecurityAuditEventConfiguration : IEntityTypeConfiguration<PlatformSecurityAuditEvent>
{
    public void Configure(EntityTypeBuilder<PlatformSecurityAuditEvent> builder)
    {
        builder.ToTable("PlatformSecurityAuditEvents", "shared", table =>
        {
            table.HasCheckConstraint("CkPlatformSecurityAuditEventsId", "uuid_extract_version(\"Id\") = 7");
            table.HasCheckConstraint("CkPlatformSecurityAuditEventsDetails", "jsonb_typeof(\"Details\") = 'object'");
        });

        builder.HasKey(x => x.Id).HasName("PkPlatformSecurityAuditEvents");

        builder.Property(x => x.Id).HasColumnName("Id").HasColumnType("uuid").IsRequired().HasDefaultValueSql("uuidv7()");
        builder.Property(x => x.ActorUserId).HasColumnName("ActorUserId").HasColumnType("uuid").IsRequired(false);
        builder.Property(x => x.TargetTenantId).HasColumnName("TargetTenantId").HasColumnType("uuid").IsRequired(false);
        builder.Property(x => x.EventCode).HasColumnName("EventCode").HasColumnType("varchar(100)").HasMaxLength(100).IsRequired();
        builder.Property(x => x.TargetType).HasColumnName("TargetType").HasColumnType("varchar(80)").HasMaxLength(80).IsRequired();
        builder.Property(x => x.TargetId).HasColumnName("TargetId").HasColumnType("uuid").IsRequired(false);
        builder.Property(x => x.Success).HasColumnName("Success").HasColumnType("boolean").IsRequired().HasDefaultValue(true).HasSentinel(true);
        builder.Property(x => x.ClientIp).HasColumnName("ClientIp").HasColumnType("inet").IsRequired(false);
        builder.Property(x => x.CorrelationId).HasColumnName("CorrelationId").HasColumnType("uuid").IsRequired(false);
        builder.Property(x => x.Details).HasColumnName("Details").HasColumnType("jsonb").IsRequired().HasDefaultValueSql("'{}'::jsonb");
        builder.Property(x => x.OccurredAt).HasColumnName("OccurredAt").HasColumnType("timestamptz").IsRequired().HasDefaultValueSql("now()");

        builder.HasOne(x => x.TargetTenant).WithMany()
            .HasForeignKey(x => x.TargetTenantId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.OccurredAt).HasDatabaseName("IxPlatformSecurityAuditTime").IsDescending(true);

        // Append-only database grants are a future operational/migration concern.
        // SQL checks JSON object shape; the ERD's suggested 4 KB bound is not in the DDL.
    }
}
