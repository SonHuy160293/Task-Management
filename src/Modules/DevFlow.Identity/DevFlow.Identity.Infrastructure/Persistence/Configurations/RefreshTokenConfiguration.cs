using DevFlow.Identity.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevFlow.Identity.Infrastructure.Persistence.Configurations;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens", "shared", table =>
        {
            table.HasCheckConstraint("CkRefreshTokensId", "uuid_extract_version(\"Id\") = 7");
            table.HasCheckConstraint("CkRefreshTokensTokenHash", "octet_length(\"TokenHash\") = 32");
            table.HasCheckConstraint("CkRefreshTokensTime", " \"ExpiresAt\" > \"IssuedAt\" AND (\"ConsumedAt\" IS NULL OR \"ConsumedAt\" >= \"IssuedAt\") AND (\"RevokedAt\" IS NULL OR \"RevokedAt\" >= \"IssuedAt\") ");
        });

        builder.HasKey(x => x.Id).HasName("PkRefreshTokens");

        builder.Property(x => x.Id).HasColumnName("Id").HasColumnType("uuid").IsRequired().HasDefaultValueSql("uuidv7()");
        builder.Property(x => x.SessionId).HasColumnName("SessionId").HasColumnType("uuid").IsRequired();
        builder.Property(x => x.TokenHash).HasColumnName("TokenHash").HasColumnType("bytea").IsRequired();
        builder.Property(x => x.ReplacedByTokenId).HasColumnName("ReplacedByTokenId").HasColumnType("uuid").IsRequired(false);
        builder.Property(x => x.IssuedAt).HasColumnName("IssuedAt").HasColumnType("timestamptz").IsRequired().HasDefaultValueSql("now()");
        builder.Property(x => x.ExpiresAt).HasColumnName("ExpiresAt").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.ConsumedAt).HasColumnName("ConsumedAt").HasColumnType("timestamptz").IsRequired(false);
        builder.Property(x => x.RevokedAt).HasColumnName("RevokedAt").HasColumnType("timestamptz").IsRequired(false);

        builder.HasAlternateKey(x => x.TokenHash).HasName("UqRefreshTokensTokenHash");

        builder.HasOne(x => x.Session).WithMany()
            .HasForeignKey(x => x.SessionId)
            .IsRequired(true)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ReplacedByToken).WithMany()
            .HasForeignKey(x => x.ReplacedByTokenId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.ReplacedByTokenId).HasDatabaseName("UxRefreshTokensReplacement").IsUnique().HasFilter("\"ReplacedByTokenId\" IS NOT NULL");

        builder.HasIndex(x => new { x.SessionId, x.IssuedAt }).HasDatabaseName("IxRefreshTokensSession").IsDescending(false, true);

        builder.HasIndex(x => x.ExpiresAt).HasDatabaseName("IxRefreshTokensExpiry");
    }
}
