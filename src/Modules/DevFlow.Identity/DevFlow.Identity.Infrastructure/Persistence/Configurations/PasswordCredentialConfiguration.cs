using DevFlow.Identity.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevFlow.Identity.Infrastructure.Persistence.Configurations;

public sealed class PasswordCredentialConfiguration : IEntityTypeConfiguration<PasswordCredential>
{
    public void Configure(EntityTypeBuilder<PasswordCredential> builder)
    {
        builder.ToTable("PasswordCredentials", "shared", table =>
        {
            table.HasCheckConstraint("CkPasswordCredentialsId", "uuid_extract_version(\"Id\") = 7");
            table.HasCheckConstraint("CkPasswordCredentialsPasswordHash", "char_length(\"PasswordHash\") >= 40");
            table.HasCheckConstraint("CkPasswordCredentialsFailedAttempts", "\"FailedAttempts\" >= 0");
        });

        builder.HasKey(x => x.Id).HasName("PkPasswordCredentials");
        builder.Ignore(x => x.CreatedBy);
        builder.Ignore(x => x.UpdatedBy);

        builder.Property(x => x.Id).HasColumnName("Id").HasColumnType("uuid").IsRequired().HasDefaultValueSql("uuidv7()");
        builder.Property(x => x.UserId).HasColumnName("UserId").HasColumnType("uuid").IsRequired();
        builder.Property(x => x.PasswordHash).HasColumnName("PasswordHash").HasColumnType("varchar(512)").HasMaxLength(512).IsRequired();
        builder.Property(x => x.FailedAttempts).HasColumnName("FailedAttempts").HasColumnType("integer").IsRequired().HasDefaultValue(0);
        builder.Property(x => x.LockedUntil).HasColumnName("LockedUntil").HasColumnType("timestamptz").IsRequired(false);
        builder.Property(x => x.PasswordChangedAt).HasColumnName("PasswordChangedAt").HasColumnType("timestamptz").IsRequired().HasDefaultValueSql("now()");
        builder.Property(x => x.CreatedAt).HasColumnName("CreatedAt").HasColumnType("timestamptz").IsRequired().HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasColumnName("UpdatedAt").HasColumnType("timestamptz").IsRequired().HasDefaultValueSql("now()");

        // UpdatedAt is nullable only in CLR. Constructors initialize it and the SQL default
        // covers inserts; register the existing audit interceptor for mutation timestamps.

        builder.HasAlternateKey(x => x.UserId).HasName("UqPasswordCredentialsUserId");

        builder.HasOne(x => x.User).WithOne()
            .HasForeignKey<PasswordCredential>(x => x.UserId)
            .IsRequired(true)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
