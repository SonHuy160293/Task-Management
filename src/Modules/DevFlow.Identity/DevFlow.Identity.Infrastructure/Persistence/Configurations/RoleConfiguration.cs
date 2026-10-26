using DevFlow.Identity.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevFlow.Identity.Infrastructure.Persistence.Configurations;

public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Roles", "shared", table =>
        {
            table.HasCheckConstraint("CkRolesId", "uuid_extract_version(\"Id\") = 7");
            table.HasCheckConstraint("CkRolesCode", "\"Code\" ~ '^[a-z][a-z0-9_]*$'");
            table.HasCheckConstraint("CkRolesScopeKind", "\"ScopeKind\" IN ('platform', 'organization', 'project', 'team', 'performance')");
        });

        builder.HasKey(x => x.Id).HasName("PkRoles");

        builder.Property(x => x.Id).HasColumnName("Id").HasColumnType("uuid").IsRequired().HasDefaultValueSql("uuidv7()");
        builder.Property(x => x.Code).HasColumnName("Code").HasColumnType("varchar(80)").HasMaxLength(80).IsRequired();
        builder.Property(x => x.DisplayName).HasColumnName("DisplayName").HasColumnType("varchar(120)").HasMaxLength(120).IsRequired();
        builder.Property(x => x.ScopeKind).HasColumnName("ScopeKind").HasColumnType("varchar(24)").HasMaxLength(24).IsRequired();
        builder.Property(x => x.Description).HasColumnName("Description").HasColumnType("varchar(500)").HasMaxLength(500).IsRequired(false);
        builder.Property(x => x.IsAssignable).HasColumnName("IsAssignable").HasColumnType("boolean").IsRequired().HasDefaultValue(true).HasSentinel(true);
        builder.Property(x => x.IsActive).HasColumnName("IsActive").HasColumnType("boolean").IsRequired().HasDefaultValue(true).HasSentinel(true);
        builder.Property(x => x.CreatedAt).HasColumnName("CreatedAt").HasColumnType("timestamptz").IsRequired().HasDefaultValueSql("now()");

        builder.HasAlternateKey(x => x.Code).HasName("UqRolesCode");
        builder.HasAlternateKey(x => new { x.Id, x.ScopeKind }).HasName("UqRolesIdScopeKind");
    }
}
