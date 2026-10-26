using DevFlow.Identity.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevFlow.Identity.Infrastructure.Persistence.Configurations;

public sealed class InvitationRoleGrantConfiguration : IEntityTypeConfiguration<InvitationRoleGrant>
{
    public void Configure(EntityTypeBuilder<InvitationRoleGrant> builder)
    {
        builder.ToTable("InvitationRoleGrants", "dbo", table =>
        {
            table.HasCheckConstraint("CkInvitationRoleGrantsId", "uuid_extract_version(\"Id\") = 7");
        });

        builder.HasKey(x => x.Id).HasName("PkInvitationRoleGrants");

        builder.Property(x => x.Id).HasColumnName("Id").HasColumnType("uuid").IsRequired().HasDefaultValueSql("uuidv7()");
        builder.Property(x => x.TenantId).HasColumnName("TenantId").HasColumnType("uuid").IsRequired();
        builder.Property(x => x.InvitationId).HasColumnName("InvitationId").HasColumnType("uuid").IsRequired();
        builder.Property(x => x.ScopeId).HasColumnName("ScopeId").HasColumnType("uuid").IsRequired();
        builder.Property(x => x.ScopeKind).HasColumnName("ScopeKind").HasColumnType("varchar(24)").HasMaxLength(24).IsRequired();
        builder.Property(x => x.RoleId).HasColumnName("RoleId").HasColumnType("uuid").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("CreatedAt").HasColumnType("timestamptz").IsRequired().HasDefaultValueSql("now()");

        builder.HasAlternateKey(x => new { x.TenantId, x.InvitationId, x.ScopeId, x.RoleId }).HasName("UqInvitationRoleGrantsTenantIdInvitationIdScopeIdRoleId");

        builder.HasOne(x => x.Tenant).WithMany()
            .HasForeignKey(x => x.TenantId)
            .IsRequired(true)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Invitation).WithMany()
            .HasForeignKey(x => new { x.TenantId, x.InvitationId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .IsRequired(true)
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
    }
}
