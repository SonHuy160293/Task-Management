using DevFlow.Identity.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevFlow.Identity.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users", "shared", table =>
        {
            table.HasCheckConstraint("CkUsersId", "uuid_extract_version(\"Id\") = 7");
            table.HasCheckConstraint("CkUsersPrimaryEmail", "char_length(btrim(\"PrimaryEmail\")) BETWEEN 3 AND 320 AND \"PrimaryEmail\" = btrim(\"PrimaryEmail\")");
            table.HasCheckConstraint("CkUsersDisplayName", "char_length(btrim(\"DisplayName\")) BETWEEN 1 AND 160");
            table.HasCheckConstraint("CkUsersStatus", "\"Status\" IN ('pending', 'active', 'suspended', 'deleted')");
        });

        builder.HasKey(x => x.Id).HasName("PkUsers");
        builder.Ignore(x => x.CreatedBy);
        builder.Ignore(x => x.UpdatedBy);

        builder.Property(x => x.Id).HasColumnName("Id").HasColumnType("uuid").IsRequired().HasDefaultValueSql("uuidv7()");
        builder.Property(x => x.PrimaryEmail).HasColumnName("PrimaryEmail").HasColumnType("varchar(320)").HasMaxLength(320).IsRequired();
        builder.Property(x => x.DisplayName).HasColumnName("DisplayName").HasColumnType("varchar(160)").HasMaxLength(160).IsRequired();
        builder.Property(x => x.AvatarUrl).HasColumnName("AvatarUrl").HasColumnType("text").IsRequired(false);
        builder.Property(x => x.Locale).HasColumnName("Locale").HasColumnType("varchar(16)").HasMaxLength(16).IsRequired().HasDefaultValue("en");
        builder.Property(x => x.TimeZone).HasColumnName("TimeZone").HasColumnType("varchar(64)").HasMaxLength(64).IsRequired().HasDefaultValue("UTC");
        builder.Property(x => x.Status).HasColumnName("Status").HasColumnType("varchar(16)").HasMaxLength(16).IsRequired().HasDefaultValue("pending");
        builder.Property(x => x.EmailVerifiedAt).HasColumnName("EmailVerifiedAt").HasColumnType("timestamptz").IsRequired(false);
        builder.Property(x => x.LastLoginAt).HasColumnName("LastLoginAt").HasColumnType("timestamptz").IsRequired(false);
        builder.Property(x => x.CreatedAt).HasColumnName("CreatedAt").HasColumnType("timestamptz").IsRequired().HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasColumnName("UpdatedAt").HasColumnType("timestamptz").IsRequired().HasDefaultValueSql("now()");

        // UpdatedAt is nullable only in CLR. Constructors initialize it and the SQL default
        // covers inserts; register the existing audit interceptor for mutation timestamps.

        // InitialIdentity creates this expression index outside the Fluent model.
        // CREATE UNIQUE INDEX "UxUsersPrimaryEmailCi" ON shared."Users" (lower("PrimaryEmail"));

        builder.HasIndex(x => x.Status).HasDatabaseName("IxUsersStatus");
    }
}
