using DevFlow.Identity.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevFlow.Identity.Infrastructure.Persistence.Configurations;

public sealed class AuthSessionConfiguration : IEntityTypeConfiguration<AuthSession>
{
    public void Configure(EntityTypeBuilder<AuthSession> builder)
    {
        builder.ToTable("AuthSessions", "shared", table =>
        {
            table.HasCheckConstraint("CkAuthSessionsId", "uuid_extract_version(\"Id\") = 7");
            table.HasCheckConstraint("CkAuthSessionsTime", "\"ExpiresAt\" > \"CreatedAt\" AND (\"RevokedAt\" IS NULL OR \"RevokedAt\" >= \"CreatedAt\")");
        });

        builder.HasKey(x => x.Id).HasName("PkAuthSessions");

        builder.Property(x => x.Id).HasColumnName("Id").HasColumnType("uuid").IsRequired().HasDefaultValueSql("uuidv7()");
        builder.Property(x => x.UserId).HasColumnName("UserId").HasColumnType("uuid").IsRequired();
        builder.Property(x => x.ClientIp).HasColumnName("ClientIp").HasColumnType("inet").IsRequired(false);
        builder.Property(x => x.UserAgent).HasColumnName("UserAgent").HasColumnType("varchar(512)").HasMaxLength(512).IsRequired(false);
        builder.Property(x => x.CreatedAt).HasColumnName("CreatedAt").HasColumnType("timestamptz").IsRequired().HasDefaultValueSql("now()");
        builder.Property(x => x.LastSeenAt).HasColumnName("LastSeenAt").HasColumnType("timestamptz").IsRequired(false);
        builder.Property(x => x.ExpiresAt).HasColumnName("ExpiresAt").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.RevokedAt).HasColumnName("RevokedAt").HasColumnType("timestamptz").IsRequired(false);

        builder.HasOne(x => x.User).WithMany()
            .HasForeignKey(x => x.UserId)
            .IsRequired(true)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.UserId, x.ExpiresAt }).HasDatabaseName("IxAuthSessionsUserExpiry").IsDescending(false, true);
    }
}
