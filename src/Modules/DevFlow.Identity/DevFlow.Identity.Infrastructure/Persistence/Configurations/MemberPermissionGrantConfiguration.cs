using DevFlow.Identity.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevFlow.Identity.Infrastructure.Persistence.Configurations;

public sealed class MemberPermissionGrantConfiguration : IEntityTypeConfiguration<MemberPermissionGrant>
{
    public void Configure(EntityTypeBuilder<MemberPermissionGrant> builder)
    {
        builder.ToTable("MemberPermissionGrants", "dbo", table =>
        {
            table.HasCheckConstraint("CkMemberPermissionGrantsId", "uuid_extract_version(\"Id\") = 7");
            table.HasCheckConstraint("CkMemberPermissionGrantsJustification", "char_length(btrim(\"Justification\")) >= 8");
            table.HasCheckConstraint("CkMemberGrantTime", " (\"ExpiresAt\" IS NULL OR \"ExpiresAt\" > \"GrantedAt\") AND (\"RevokedAt\" IS NULL OR \"RevokedAt\" >= \"GrantedAt\") ");
        });

        builder.HasKey(x => x.Id).HasName("PkMemberPermissionGrants");

        builder.Property(x => x.Id).HasColumnName("Id").HasColumnType("uuid").IsRequired().HasDefaultValueSql("uuidv7()");
        builder.Property(x => x.TenantId).HasColumnName("TenantId").HasColumnType("uuid").IsRequired();
        builder.Property(x => x.MemberId).HasColumnName("MemberId").HasColumnType("uuid").IsRequired();
        builder.Property(x => x.ScopeId).HasColumnName("ScopeId").HasColumnType("uuid").IsRequired();
        builder.Property(x => x.ScopeKind).HasColumnName("ScopeKind").HasColumnType("varchar(24)").HasMaxLength(24).IsRequired();
        builder.Property(x => x.PermissionId).HasColumnName("PermissionId").HasColumnType("uuid").IsRequired();
        builder.Property(x => x.GrantedByMemberId).HasColumnName("GrantedByMemberId").HasColumnType("uuid").IsRequired(false);
        builder.Property(x => x.Justification).HasColumnName("Justification").HasColumnType("varchar(500)").HasMaxLength(500).IsRequired();
        builder.Property(x => x.GrantedAt).HasColumnName("GrantedAt").HasColumnType("timestamptz").IsRequired().HasDefaultValueSql("now()");
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

        builder.HasOne(x => x.GrantedByMember).WithMany()
            .HasForeignKey(x => new { x.TenantId, x.GrantedByMemberId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Scope).WithMany()
            .HasForeignKey(x => new { x.TenantId, x.ScopeId, x.ScopeKind })
            .HasPrincipalKey(x => new { x.TenantId, x.Id, x.ScopeKind })
            .IsRequired(true)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Permission).WithMany()
            .HasForeignKey(x => new { x.PermissionId, x.ScopeKind })
            .HasPrincipalKey(x => new { x.Id, x.ScopeKind })
            .IsRequired(true)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.TenantId, x.MemberId, x.ScopeId, x.PermissionId }).HasDatabaseName("UxMemberGrantsActive").IsUnique().HasFilter("\"RevokedAt\" IS NULL");

        builder.HasIndex(x => new { x.TenantId, x.ScopeId, x.PermissionId }).HasDatabaseName("IxMemberGrantsScope").HasFilter("\"RevokedAt\" IS NULL");
    }
}
