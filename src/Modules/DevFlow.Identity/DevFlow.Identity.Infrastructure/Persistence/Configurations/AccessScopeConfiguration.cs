using DevFlow.Identity.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevFlow.Identity.Infrastructure.Persistence.Configurations;

public sealed class AccessScopeConfiguration : IEntityTypeConfiguration<AccessScope>
{
    public void Configure(EntityTypeBuilder<AccessScope> builder)
    {
        builder.ToTable("AccessScopes", "dbo", table =>
        {
            table.HasCheckConstraint("CkAccessScopesId", "uuid_extract_version(\"Id\") = 7");
            table.HasCheckConstraint("CkAccessScopesScopeKind", "\"ScopeKind\" IN ('organization', 'project', 'team', 'performance')");
            table.HasCheckConstraint("CkAccessScopesStatus", "\"Status\" IN ('active', 'archived')");
            table.HasCheckConstraint("CkOrgScopeResource", "\"ScopeKind\" <> 'organization' OR \"ResourceId\" = \"TenantId\"");
            table.HasCheckConstraint("CkAccessScopesArchived", "(\"Status\" = 'archived') = (\"ArchivedAt\" IS NOT NULL)");
        });

        builder.HasKey(x => x.Id).HasName("PkAccessScopes");

        builder.Property(x => x.Id).HasColumnName("Id").HasColumnType("uuid").IsRequired().HasDefaultValueSql("uuidv7()");
        builder.Property(x => x.TenantId).HasColumnName("TenantId").HasColumnType("uuid").IsRequired();
        builder.Property(x => x.ScopeKind).HasColumnName("ScopeKind").HasColumnType("varchar(24)").HasMaxLength(24).IsRequired();
        builder.Property(x => x.ResourceId).HasColumnName("ResourceId").HasColumnType("uuid").IsRequired();
        builder.Property(x => x.Status).HasColumnName("Status").HasColumnType("varchar(12)").HasMaxLength(12).IsRequired().HasDefaultValue("active");
        builder.Property(x => x.CreatedAt).HasColumnName("CreatedAt").HasColumnType("timestamptz").IsRequired().HasDefaultValueSql("now()");
        builder.Property(x => x.ArchivedAt).HasColumnName("ArchivedAt").HasColumnType("timestamptz").IsRequired(false);

        builder.HasAlternateKey(x => new { x.TenantId, x.ScopeKind, x.ResourceId }).HasName("UqAccessScopesTenantIdScopeKindResourceId");
        builder.HasAlternateKey(x => new { x.TenantId, x.Id, x.ScopeKind }).HasName("UqAccessScopesTenantIdIdScopeKind");

        builder.HasOne(x => x.Tenant).WithMany()
            .HasForeignKey(x => x.TenantId)
            .IsRequired(true)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
