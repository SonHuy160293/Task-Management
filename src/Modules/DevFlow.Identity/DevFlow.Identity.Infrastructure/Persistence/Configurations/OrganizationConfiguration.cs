using DevFlow.Identity.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevFlow.Identity.Infrastructure.Persistence.Configurations;

public sealed class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.ToTable("Organizations", "shared", table =>
        {
            table.HasCheckConstraint("CkOrganizationsId", "uuid_extract_version(\"Id\") = 7");
            table.HasCheckConstraint("CkOrganizationsSlug", "\"Slug\" ~ '^[a-z0-9][a-z0-9-]{2,79}$'");
            table.HasCheckConstraint("CkOrganizationsDisplayName", "char_length(btrim(\"DisplayName\")) BETWEEN 1 AND 200");
            table.HasCheckConstraint("CkOrganizationsStatus", "\"Status\" IN ('active', 'suspended', 'closed')");
        });

        builder.HasKey(x => x.Id).HasName("PkOrganizations");
        builder.Ignore(x => x.CreatedBy);
        builder.Ignore(x => x.UpdatedBy);

        builder.Property(x => x.Id).HasColumnName("Id").HasColumnType("uuid").IsRequired().HasDefaultValueSql("uuidv7()");
        builder.Property(x => x.Slug).HasColumnName("Slug").HasColumnType("varchar(80)").HasMaxLength(80).IsRequired();
        builder.Property(x => x.DisplayName).HasColumnName("DisplayName").HasColumnType("varchar(200)").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Status).HasColumnName("Status").HasColumnType("varchar(16)").HasMaxLength(16).IsRequired().HasDefaultValue("active");
        builder.Property(x => x.CreatedByUserId).HasColumnName("CreatedByUserId").HasColumnType("uuid").IsRequired();
        builder.Property(x => x.SuspendedAt).HasColumnName("SuspendedAt").HasColumnType("timestamptz").IsRequired(false);
        builder.Property(x => x.CreatedAt).HasColumnName("CreatedAt").HasColumnType("timestamptz").IsRequired().HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasColumnName("UpdatedAt").HasColumnType("timestamptz").IsRequired().HasDefaultValueSql("now()");

        // UpdatedAt is nullable only in CLR. Constructors initialize it and the SQL default
        // covers inserts; register the existing audit interceptor for mutation timestamps.

        builder.HasOne(x => x.CreatedByUser).WithMany()
            .HasForeignKey(x => x.CreatedByUserId)
            .IsRequired(true)
            .OnDelete(DeleteBehavior.Restrict);

        // InitialIdentity creates this expression index outside the Fluent model.
        // CREATE UNIQUE INDEX "UxOrganizationsSlugCi" ON shared."Organizations" (lower("Slug"));

        builder.HasIndex(x => x.Status).HasDatabaseName("IxOrganizationsStatus");
    }
}
