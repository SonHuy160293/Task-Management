using DevFlow.Identity.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace DevFlow.Identity.Infrastructure.Persistence.DbContexts;

public sealed class IdentityDbContext : DbContext
{
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<PasswordCredential> PasswordCredentials => Set<PasswordCredential>();
    public DbSet<AuthSession> AuthSessions => Set<AuthSession>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<OneTimeToken> OneTimeTokens => Set<OneTimeToken>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<PlatformRoleAssignment> PlatformRoleAssignments => Set<PlatformRoleAssignment>();
    public DbSet<OrganizationMember> OrganizationMembers => Set<OrganizationMember>();
    public DbSet<AccessScope> AccessScopes => Set<AccessScope>();
    public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<InvitationRoleGrant> InvitationRoleGrants => Set<InvitationRoleGrant>();
    public DbSet<MemberRoleAssignment> MemberRoleAssignments => Set<MemberRoleAssignment>();
    public DbSet<MemberPermissionGrant> MemberPermissionGrants => Set<MemberPermissionGrant>();
    public DbSet<SecurityAuditEvent> SecurityAuditEvents => Set<SecurityAuditEvent>();
    public DbSet<PlatformSecurityAuditEvent> PlatformSecurityAuditEvents => Set<PlatformSecurityAuditEvent>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);
        // Preserve the SQL index inventory instead of adding convention-only FK indexes.
        configurationBuilder.Conventions.Remove(typeof(ForeignKeyIndexConvention));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        DevFlow.Messaging.Outbox.OutboxAdministration.Configure(modelBuilder);
        DevFlow.EntityFrameworkCore.Reliability.ReliabilityModelBuilderExtensions.ApplyReliabilityConfigurations(
            modelBuilder, DevFlow.EntityFrameworkCore.Reliability.ReliabilityDatabaseProvider.PostgreSql);
        modelBuilder.Entity<DevFlow.Identity.Infrastructure.Messaging.EmailDeliveryReceipt>(b =>
        {
            b.ToTable("EmailDeliveryReceipts", "reliability");
            b.HasKey(x => x.DeliveryMessageId);
            b.Property(x => x.DeliveryMessageId).ValueGeneratedNever();
        });
        modelBuilder.Entity<DevFlow.Identity.Infrastructure.Messaging.OutboxRecovery>(b =>
        {
            b.ToTable("OutboxRecoveries", "reliability");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
        });
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly,
            type => type.Namespace == "DevFlow.Identity.Infrastructure.Persistence.Configurations");
        // RLS and append-only database privileges require a future migration/operations task.
        // Tenant session settings require trusted runtime infrastructure and are not inferred here.
        // Enable and force RLS on all seven dbo tables; tenant_isolation must compare "TenantId"
        // with nullif(current_setting('app.tenant_id', true), '')::uuid in USING and WITH CHECK.
        // Runtime queries must use a trusted SET LOCAL app.tenant_id in their transaction,
        // under a non-superuser/non-BYPASSRLS role. Audit tables also need append-only grants.
    }
}
