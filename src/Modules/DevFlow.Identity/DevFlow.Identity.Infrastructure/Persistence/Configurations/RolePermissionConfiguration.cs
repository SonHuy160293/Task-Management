using DevFlow.Identity.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevFlow.Identity.Infrastructure.Persistence.Configurations;

public sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("RolePermissions", "shared", table =>
        {
            table.HasCheckConstraint("CkRolePermissionsId", "uuid_extract_version(\"Id\") = 7");
        });

        builder.HasKey(x => x.Id).HasName("PkRolePermissions");

        builder.Property(x => x.Id).HasColumnName("Id").HasColumnType("uuid").IsRequired().HasDefaultValueSql("uuidv7()");
        builder.Property(x => x.RoleId).HasColumnName("RoleId").HasColumnType("uuid").IsRequired();
        builder.Property(x => x.PermissionId).HasColumnName("PermissionId").HasColumnType("uuid").IsRequired();
        builder.Property(x => x.ScopeKind).HasColumnName("ScopeKind").HasColumnType("varchar(24)").HasMaxLength(24).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("CreatedAt").HasColumnType("timestamptz").IsRequired().HasDefaultValueSql("now()");

        builder.HasAlternateKey(x => new { x.RoleId, x.PermissionId }).HasName("UqRolePermissionsRoleIdPermissionId");

        builder.HasOne(x => x.Role).WithMany()
            .HasForeignKey(x => new { x.RoleId, x.ScopeKind })
            .HasPrincipalKey(x => new { x.Id, x.ScopeKind })
            .IsRequired(true)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Permission).WithMany()
            .HasForeignKey(x => new { x.PermissionId, x.ScopeKind })
            .HasPrincipalKey(x => new { x.Id, x.ScopeKind })
            .IsRequired(true)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.PermissionId).HasDatabaseName("IxRolePermissionsPermission");
    }
}
