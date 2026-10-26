using DevFlow.Identity.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevFlow.Identity.Infrastructure.Persistence.Configurations;

public sealed class SecurityAuditEventConfiguration : IEntityTypeConfiguration<SecurityAuditEvent>
{
    public void Configure(EntityTypeBuilder<SecurityAuditEvent> builder)
    {
        builder.ToTable("SecurityAuditEvents", "dbo", table =>
        {
            table.HasCheckConstraint("CkSecurityAuditEventsId", "uuid_extract_version(\"Id\") = 7");
            table.HasCheckConstraint("CkSecurityAuditEventsDetails", "jsonb_typeof(\"Details\") = 'object'");
        });

        builder.HasKey(x => x.Id).HasName("PkSecurityAuditEvents");

        builder.Property(x => x.Id).HasColumnName("Id").HasColumnType("uuid").IsRequired().HasDefaultValueSql("uuidv7()");
        builder.Property(x => x.TenantId).HasColumnName("TenantId").HasColumnType("uuid").IsRequired();
        builder.Property(x => x.ActorUserId).HasColumnName("ActorUserId").HasColumnType("uuid").IsRequired(false);
        builder.Property(x => x.ActorMemberId).HasColumnName("ActorMemberId").HasColumnType("uuid").IsRequired(false);
        builder.Property(x => x.EventCode).HasColumnName("EventCode").HasColumnType("varchar(100)").HasMaxLength(100).IsRequired();
        builder.Property(x => x.TargetType).HasColumnName("TargetType").HasColumnType("varchar(80)").HasMaxLength(80).IsRequired();
        builder.Property(x => x.TargetId).HasColumnName("TargetId").HasColumnType("uuid").IsRequired(false);
        builder.Property(x => x.Success).HasColumnName("Success").HasColumnType("boolean").IsRequired().HasDefaultValue(true).HasSentinel(true);
        builder.Property(x => x.ClientIp).HasColumnName("ClientIp").HasColumnType("inet").IsRequired(false);
        builder.Property(x => x.CorrelationId).HasColumnName("CorrelationId").HasColumnType("uuid").IsRequired(false);
        builder.Property(x => x.Details).HasColumnName("Details").HasColumnType("jsonb").IsRequired().HasDefaultValueSql("'{}'::jsonb");
        builder.Property(x => x.OccurredAt).HasColumnName("OccurredAt").HasColumnType("timestamptz").IsRequired().HasDefaultValueSql("now()");

        builder.HasOne(x => x.Tenant).WithMany()
            .HasForeignKey(x => x.TenantId)
            .IsRequired(true)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.TenantId, x.OccurredAt }).HasDatabaseName("IxSecurityAuditTenantTime").IsDescending(false, true);

        builder.HasIndex(x => new { x.TenantId, x.ActorUserId, x.OccurredAt }).HasDatabaseName("IxSecurityAuditActorTime").IsDescending(false, false, true).HasFilter("\"ActorUserId\" IS NOT NULL");

        // Append-only database grants are a future operational/migration concern.
        // SQL checks JSON object shape; the ERD's suggested 4 KB bound is not in the DDL.
    }
}
