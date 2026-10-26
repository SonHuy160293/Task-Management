using DevFlow.Identity.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevFlow.Identity.Infrastructure.Persistence.Configurations;

public sealed class PlatformRoleAssignmentConfiguration : IEntityTypeConfiguration<PlatformRoleAssignment>
{
    public void Configure(EntityTypeBuilder<PlatformRoleAssignment> builder)
    {
        builder.ToTable("PlatformRoleAssignments", "shared", table =>
        {
            table.HasCheckConstraint("CkPlatformRoleAssignmentsId", "uuid_extract_version(\"Id\") = 7");
            table.HasCheckConstraint("CkPlatformRoleAssignmentsScopeKind", "\"ScopeKind\" = 'platform'");
            table.HasCheckConstraint("CkPlatformAssignmentTime", "\"RevokedAt\" IS NULL OR \"RevokedAt\" >= \"AssignedAt\"");
        });

        builder.HasKey(x => x.Id).HasName("PkPlatformRoleAssignments");

        builder.Property(x => x.Id).HasColumnName("Id").HasColumnType("uuid").IsRequired().HasDefaultValueSql("uuidv7()");
        builder.Property(x => x.UserId).HasColumnName("UserId").HasColumnType("uuid").IsRequired();
        builder.Property(x => x.RoleId).HasColumnName("RoleId").HasColumnType("uuid").IsRequired();
        builder.Property(x => x.ScopeKind).HasColumnName("ScopeKind").HasColumnType("varchar(24)").HasMaxLength(24).IsRequired().HasDefaultValue("platform");
        builder.Property(x => x.AssignedByUserId).HasColumnName("AssignedByUserId").HasColumnType("uuid").IsRequired(false);
        builder.Property(x => x.AssignedAt).HasColumnName("AssignedAt").HasColumnType("timestamptz").IsRequired().HasDefaultValueSql("now()");
        builder.Property(x => x.RevokedAt).HasColumnName("RevokedAt").HasColumnType("timestamptz").IsRequired(false);

        builder.HasOne(x => x.User).WithMany()
            .HasForeignKey(x => x.UserId)
            .IsRequired(true)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.AssignedByUser).WithMany()
            .HasForeignKey(x => x.AssignedByUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Role).WithMany()
            .HasForeignKey(x => new { x.RoleId, x.ScopeKind })
            .HasPrincipalKey(x => new { x.Id, x.ScopeKind })
            .IsRequired(true)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.UserId, x.RoleId }).HasDatabaseName("UxPlatformRoleActive").IsUnique().HasFilter("\"RevokedAt\" IS NULL");
    }
}
