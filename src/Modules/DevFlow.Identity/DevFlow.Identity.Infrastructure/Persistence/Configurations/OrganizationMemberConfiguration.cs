using DevFlow.Identity.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevFlow.Identity.Infrastructure.Persistence.Configurations;

public sealed class OrganizationMemberConfiguration : IEntityTypeConfiguration<OrganizationMember>
{
    public void Configure(EntityTypeBuilder<OrganizationMember> builder)
    {
        builder.ToTable("OrganizationMembers", "dbo", table =>
        {
            table.HasCheckConstraint("CkOrganizationMembersId", "uuid_extract_version(\"Id\") = 7");
            table.HasCheckConstraint("CkOrganizationMembersMembershipType", "\"MembershipType\" IN ('employee', 'contractor', 'guest')");
            table.HasCheckConstraint("CkOrganizationMembersStatus", "\"Status\" IN ('active', 'suspended', 'removed')");
            table.HasCheckConstraint("CkMemberRemoved", "(\"Status\" = 'removed') = (\"RemovedAt\" IS NOT NULL)");
        });

        builder.HasKey(x => x.Id).HasName("PkOrganizationMembers");
        builder.Ignore(x => x.CreatedBy);
        builder.Ignore(x => x.UpdatedBy);

        builder.Property(x => x.Id).HasColumnName("Id").HasColumnType("uuid").IsRequired().HasDefaultValueSql("uuidv7()");
        builder.Property(x => x.TenantId).HasColumnName("TenantId").HasColumnType("uuid").IsRequired();
        builder.Property(x => x.UserId).HasColumnName("UserId").HasColumnType("uuid").IsRequired();
        builder.Property(x => x.MembershipType).HasColumnName("MembershipType").HasColumnType("varchar(12)").HasMaxLength(12).IsRequired().HasDefaultValue("employee");
        builder.Property(x => x.Status).HasColumnName("Status").HasColumnType("varchar(16)").HasMaxLength(16).IsRequired().HasDefaultValue("active");
        builder.Property(x => x.JoinedAt).HasColumnName("JoinedAt").HasColumnType("timestamptz").IsRequired().HasDefaultValueSql("now()");
        builder.Property(x => x.RemovedAt).HasColumnName("RemovedAt").HasColumnType("timestamptz").IsRequired(false);
        builder.Property(x => x.CreatedAt).HasColumnName("CreatedAt").HasColumnType("timestamptz").IsRequired().HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasColumnName("UpdatedAt").HasColumnType("timestamptz").IsRequired().HasDefaultValueSql("now()");

        // UpdatedAt is nullable only in CLR. Constructors initialize it and the SQL default
        // covers inserts; register the existing audit interceptor for mutation timestamps.

        builder.HasAlternateKey(x => new { x.TenantId, x.UserId }).HasName("UqOrganizationMembersTenantIdUserId");
        builder.HasAlternateKey(x => new { x.TenantId, x.Id }).HasName("UqOrganizationMembersTenantIdId");

        builder.HasOne(x => x.Tenant).WithMany()
            .HasForeignKey(x => x.TenantId)
            .IsRequired(true)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.User).WithMany()
            .HasForeignKey(x => x.UserId)
            .IsRequired(true)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.TenantId, x.Status }).HasDatabaseName("IxOrganizationMembersStatus");
    }
}
