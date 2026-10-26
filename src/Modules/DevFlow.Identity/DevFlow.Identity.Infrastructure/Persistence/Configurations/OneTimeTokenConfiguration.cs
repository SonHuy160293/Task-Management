using DevFlow.Identity.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevFlow.Identity.Infrastructure.Persistence.Configurations;

public sealed class OneTimeTokenConfiguration : IEntityTypeConfiguration<OneTimeToken>
{
    public void Configure(EntityTypeBuilder<OneTimeToken> builder)
    {
        builder.ToTable("OneTimeTokens", "shared", table =>
        {
            table.HasCheckConstraint("CkOneTimeTokensId", "uuid_extract_version(\"Id\") = 7");
            table.HasCheckConstraint("CkOneTimeTokensPurpose", "\"Purpose\" IN ('verify_email', 'reset_password')");
            table.HasCheckConstraint("CkOneTimeTokensTokenHash", "octet_length(\"TokenHash\") = 32");
            table.HasCheckConstraint("CkOneTimeTokensTime", " \"ExpiresAt\" > \"CreatedAt\" AND (\"ConsumedAt\" IS NULL OR \"ConsumedAt\" >= \"CreatedAt\") ");
        });

        builder.HasKey(x => x.Id).HasName("PkOneTimeTokens");

        builder.Property(x => x.Id).HasColumnName("Id").HasColumnType("uuid").IsRequired().HasDefaultValueSql("uuidv7()");
        builder.Property(x => x.UserId).HasColumnName("UserId").HasColumnType("uuid").IsRequired();
        builder.Property(x => x.Purpose).HasColumnName("Purpose").HasColumnType("varchar(24)").HasMaxLength(24).IsRequired();
        builder.Property(x => x.TokenHash).HasColumnName("TokenHash").HasColumnType("bytea").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("CreatedAt").HasColumnType("timestamptz").IsRequired().HasDefaultValueSql("now()");
        builder.Property(x => x.ExpiresAt).HasColumnName("ExpiresAt").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.ConsumedAt).HasColumnName("ConsumedAt").HasColumnType("timestamptz").IsRequired(false);

        builder.HasAlternateKey(x => x.TokenHash).HasName("UqOneTimeTokensTokenHash");

        builder.HasOne(x => x.User).WithMany()
            .HasForeignKey(x => x.UserId)
            .IsRequired(true)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.UserId, x.Purpose, x.CreatedAt }).HasDatabaseName("IxOneTimeTokensUserPurpose").IsDescending(false, false, true);

        builder.HasIndex(x => x.ExpiresAt).HasDatabaseName("IxOneTimeTokensExpiry");
    }
}
