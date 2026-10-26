using DevFlow.Identity.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevFlow.Identity.Infrastructure.Persistence.Configurations;

public sealed class MemberRoleAssignmentConfiguration : IEntityTypeConfiguration<MemberRoleAssignment>
{
    public void Configure(EntityTypeBuilder<MemberRoleAssignment> builder)
    {
        builder.ToTable("MemberRoleAssignments", "dbo", table =>
        {
            table.HasCheckConstraint("CkMemberRoleAssignmentsId", "uuid_extract_version(\"Id\") = 7");
            table.HasCheckConstraint("CkMemberRoleTime", " (\"ExpiresAt\" IS NULL OR \"ExpiresAt\" > \"AssignedAt\") AND (\"RevokedAt\" IS NULL OR \"RevokedAt\" >= \"AssignedAt\") ");
        });

        builder.HasKey(x => x.Id).HasName("PkMemberRoleAssignments");

        builder.Property(x => x.Id).HasColumnName("Id").HasColumnType("uuid").IsRequired().HasDefaultValueSql("uuidv7()");
        builder.Property(x => x.TenantId).HasColumnName("TenantId").HasColumnType("uuid").IsRequired();
        builder.Property(x => x.MemberId).HasColumnName("MemberId").HasColumnType("uuid").IsRequired();
        builder.Property(x => x.ScopeId).HasColumnName("ScopeId").HasColumnType("uuid").IsRequired();
        builder.Property(x => x.ScopeKind).HasColumnName("ScopeKind").HasColumnType("varchar(24)").HasMaxLength(24).IsRequired();
        builder.Property(x => x.RoleId).HasColumnName("RoleId").HasColumnType("uuid").IsRequired();
        builder.Property(x => x.AssignedByMemberId).HasColumnName("AssignedByMemberId").HasColumnType("uuid").IsRequired(false);
        builder.Property(x => x.Reason).HasColumnName("Reason").HasColumnType("varchar(300)").HasMaxLength(300).IsRequired(false);
        builder.Property(x => x.AssignedAt).HasColumnName("AssignedAt").HasColumnType("timestamptz").IsRequired().HasDefaultValueSql("now()");
        builder.Property(x => x.ExpiresAt).HasColumnName("ExpiresAt").HasColumnType("timestamptz").IsRequired(false);
        builder.Property(x => x.RevokedAt).HasColumnName("RevokedAt").HasColumnType("timestamptz").IsRequired(false);

        builder.HasOne(x => x.Tenant).WithMany()
            .HasForeignKey(x => x.TenantId)
            .IsRequired(true)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Member).WithMany()
            .HasForeignKey(x => new { x.TenantId, x.MemberId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .IsRequired(true)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.AssignedByMember).WithMany()
            .HasForeignKey(x => new { x.TenantId, x.AssignedByMemberId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Scope).WithMany()
            .HasForeignKey(x => new { x.TenantId, x.ScopeId, x.ScopeKind })
            .HasPrincipalKey(x => new { x.TenantId, x.Id, x.ScopeKind })
            .IsRequired(true)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Role).WithMany()
            .HasForeignKey(x => new { x.RoleId, x.ScopeKind })
            .HasPrincipalKey(x => new { x.Id, x.ScopeKind })
            .IsRequired(true)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.TenantId, x.MemberId, x.ScopeId, x.RoleId }).HasDatabaseName("UxMemberRolesActive").IsUnique().HasFilter("\"RevokedAt\" IS NULL");

        builder.HasIndex(x => new { x.TenantId, x.ScopeId, x.RoleId }).HasDatabaseName("IxMemberRolesScope").HasFilter("\"RevokedAt\" IS NULL");
    }
}
