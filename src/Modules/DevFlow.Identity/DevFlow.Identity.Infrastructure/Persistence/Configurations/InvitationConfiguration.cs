using DevFlow.Identity.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevFlow.Identity.Infrastructure.Persistence.Configurations;

public sealed class InvitationConfiguration : IEntityTypeConfiguration<Invitation>
{
    public void Configure(EntityTypeBuilder<Invitation> builder)
    {
        builder.ToTable("Invitations", "dbo", table =>
        {
            table.HasCheckConstraint("CkInvitationsId", "uuid_extract_version(\"Id\") = 7");
            table.HasCheckConstraint("CkInvitationsInviteeEmail", "char_length(btrim(\"InviteeEmail\")) BETWEEN 3 AND 320 AND \"InviteeEmail\" = btrim(\"InviteeEmail\")");
            table.HasCheckConstraint("CkInvitationsMembershipType", "\"MembershipType\" IN ('employee', 'contractor', 'guest')");
            table.HasCheckConstraint("CkInvitationsTokenHash", "octet_length(\"TokenHash\") = 32");
            table.HasCheckConstraint("CkInvitationsStatus", "\"Status\" IN ('pending', 'accepted', 'revoked', 'expired')");
            table.HasCheckConstraint("CkInvitationTimes", "\"ExpiresAt\" > \"CreatedAt\"");
            table.HasCheckConstraint("CkInvitationAcceptance", " (\"AcceptedByUserId\" IS NULL) = (\"AcceptedAt\" IS NULL) AND (\"Status\" <> 'accepted' OR \"AcceptedAt\" IS NOT NULL) AND (\"Status\" <> 'revoked' OR \"RevokedAt\" IS NOT NULL) ");
        });

        builder.HasKey(x => x.Id).HasName("PkInvitations");

        builder.Property(x => x.Id).HasColumnName("Id").HasColumnType("uuid").IsRequired().HasDefaultValueSql("uuidv7()");
        builder.Property(x => x.TenantId).HasColumnName("TenantId").HasColumnType("uuid").IsRequired();
        builder.Property(x => x.InviteeEmail).HasColumnName("InviteeEmail").HasColumnType("varchar(320)").HasMaxLength(320).IsRequired();
        builder.Property(x => x.MembershipType).HasColumnName("MembershipType").HasColumnType("varchar(12)").HasMaxLength(12).IsRequired().HasDefaultValue("employee");
        builder.Property(x => x.TokenHash).HasColumnName("TokenHash").HasColumnType("bytea").IsRequired();
        builder.Property(x => x.Status).HasColumnName("Status").HasColumnType("varchar(16)").HasMaxLength(16).IsRequired().HasDefaultValue("pending");
        builder.Property(x => x.InvitedByMemberId).HasColumnName("InvitedByMemberId").HasColumnType("uuid").IsRequired();
        builder.Property(x => x.AcceptedByUserId).HasColumnName("AcceptedByUserId").HasColumnType("uuid").IsRequired(false);
        builder.Property(x => x.CreatedAt).HasColumnName("CreatedAt").HasColumnType("timestamptz").IsRequired().HasDefaultValueSql("now()");
        builder.Property(x => x.ExpiresAt).HasColumnName("ExpiresAt").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.AcceptedAt).HasColumnName("AcceptedAt").HasColumnType("timestamptz").IsRequired(false);
        builder.Property(x => x.RevokedAt).HasColumnName("RevokedAt").HasColumnType("timestamptz").IsRequired(false);

        builder.HasAlternateKey(x => x.TokenHash).HasName("UqInvitationsTokenHash");
        builder.HasAlternateKey(x => new { x.TenantId, x.Id }).HasName("UqInvitationsTenantIdId");

        builder.HasOne(x => x.Tenant).WithMany()
            .HasForeignKey(x => x.TenantId)
            .IsRequired(true)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.AcceptedByUser).WithMany()
            .HasForeignKey(x => x.AcceptedByUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.InvitedByMember).WithMany()
            .HasForeignKey(x => new { x.TenantId, x.InvitedByMemberId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .IsRequired(true)
            .OnDelete(DeleteBehavior.Restrict);

        // InitialIdentity creates this expression index outside the Fluent model.
        // CREATE UNIQUE INDEX "UxInvitationsPendingEmail" ON dbo."Invitations" ("TenantId", lower("InviteeEmail")) WHERE "Status" = 'pending';

        builder.HasIndex(x => x.ExpiresAt).HasDatabaseName("IxInvitationsExpiry").HasFilter("\"Status\" = 'pending'");
    }
}
