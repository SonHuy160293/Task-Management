using DevFlow.Identity.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevFlow.Identity.Infrastructure.Persistence.Configurations;

public sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("Permissions", "shared", table =>
        {
            table.HasCheckConstraint("CkPermissionsId", "uuid_extract_version(\"Id\") = 7");
            table.HasCheckConstraint("CkPermissionsCode", "\"Code\" ~ '^[a-z][a-z0-9_.]*$'");
            table.HasCheckConstraint("CkPermissionsScopeKind", "\"ScopeKind\" IN ('platform', 'organization', 'project', 'team', 'performance')");
        });

        builder.HasKey(x => x.Id).HasName("PkPermissions");

        builder.Property(x => x.Id).HasColumnName("Id").HasColumnType("uuid").IsRequired().HasDefaultValueSql("uuidv7()");
        builder.Property(x => x.Code).HasColumnName("Code").HasColumnType("varchar(120)").HasMaxLength(120).IsRequired();
        builder.Property(x => x.Module).HasColumnName("Module").HasColumnType("varchar(40)").HasMaxLength(40).IsRequired();
        builder.Property(x => x.ScopeKind).HasColumnName("ScopeKind").HasColumnType("varchar(24)").HasMaxLength(24).IsRequired();
        builder.Property(x => x.Description).HasColumnName("Description").HasColumnType("varchar(500)").HasMaxLength(500).IsRequired();
        builder.Property(x => x.IsSensitive).HasColumnName("IsSensitive").HasColumnType("boolean").IsRequired().HasDefaultValue(false);
        builder.Property(x => x.IsActive).HasColumnName("IsActive").HasColumnType("boolean").IsRequired().HasDefaultValue(true).HasSentinel(true);
        builder.Property(x => x.CreatedAt).HasColumnName("CreatedAt").HasColumnType("timestamptz").IsRequired().HasDefaultValueSql("now()");

        builder.HasAlternateKey(x => x.Code).HasName("UqPermissionsCode");
        builder.HasAlternateKey(x => new { x.Id, x.ScopeKind }).HasName("UqPermissionsIdScopeKind");
    }
}
