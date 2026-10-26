using System;
using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevFlow.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "dbo");

            migrationBuilder.EnsureSchema(
                name: "shared");

            migrationBuilder.CreateTable(
                name: "Permissions",
                schema: "shared",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    Code = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false),
                    Module = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    ScopeKind = table.Column<string>(type: "varchar(24)", maxLength: 24, nullable: false),
                    Description = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false),
                    IsSensitive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PkPermissions", x => x.Id);
                    table.UniqueConstraint("UqPermissionsCode", x => x.Code);
                    table.UniqueConstraint("UqPermissionsIdScopeKind", x => new { x.Id, x.ScopeKind });
                    table.CheckConstraint("CkPermissionsCode", "\"Code\" ~ '^[a-z][a-z0-9_.]*$'");
                    table.CheckConstraint("CkPermissionsId", "uuid_extract_version(\"Id\") = 7");
                    table.CheckConstraint("CkPermissionsScopeKind", "\"ScopeKind\" IN ('platform', 'organization', 'project', 'team', 'performance')");
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                schema: "shared",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    Code = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false),
                    DisplayName = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false),
                    ScopeKind = table.Column<string>(type: "varchar(24)", maxLength: 24, nullable: false),
                    Description = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    IsAssignable = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PkRoles", x => x.Id);
                    table.UniqueConstraint("UqRolesCode", x => x.Code);
                    table.UniqueConstraint("UqRolesIdScopeKind", x => new { x.Id, x.ScopeKind });
                    table.CheckConstraint("CkRolesCode", "\"Code\" ~ '^[a-z][a-z0-9_]*$'");
                    table.CheckConstraint("CkRolesId", "uuid_extract_version(\"Id\") = 7");
                    table.CheckConstraint("CkRolesScopeKind", "\"ScopeKind\" IN ('platform', 'organization', 'project', 'team', 'performance')");
                });

            migrationBuilder.CreateTable(
                name: "Users",
                schema: "shared",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    PrimaryEmail = table.Column<string>(type: "varchar(320)", maxLength: 320, nullable: false),
                    DisplayName = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false),
                    AvatarUrl = table.Column<string>(type: "text", nullable: true),
                    Locale = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false, defaultValue: "en"),
                    TimeZone = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false, defaultValue: "UTC"),
                    Status = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false, defaultValue: "pending"),
                    EmailVerifiedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    LastLoginAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PkUsers", x => x.Id);
                    table.CheckConstraint("CkUsersDisplayName", "char_length(btrim(\"DisplayName\")) BETWEEN 1 AND 160");
                    table.CheckConstraint("CkUsersId", "uuid_extract_version(\"Id\") = 7");
                    table.CheckConstraint("CkUsersPrimaryEmail", "char_length(btrim(\"PrimaryEmail\")) BETWEEN 3 AND 320 AND \"PrimaryEmail\" = btrim(\"PrimaryEmail\")");
                    table.CheckConstraint("CkUsersStatus", "\"Status\" IN ('pending', 'active', 'suspended', 'deleted')");
                });

            migrationBuilder.CreateTable(
                name: "RolePermissions",
                schema: "shared",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    PermissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScopeKind = table.Column<string>(type: "varchar(24)", maxLength: 24, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PkRolePermissions", x => x.Id);
                    table.UniqueConstraint("UqRolePermissionsRoleIdPermissionId", x => new { x.RoleId, x.PermissionId });
                    table.CheckConstraint("CkRolePermissionsId", "uuid_extract_version(\"Id\") = 7");
                    table.ForeignKey(
                        name: "FK_RolePermissions_Permissions_PermissionId_ScopeKind",
                        columns: x => new { x.PermissionId, x.ScopeKind },
                        principalSchema: "shared",
                        principalTable: "Permissions",
                        principalColumns: new[] { "Id", "ScopeKind" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RolePermissions_Roles_RoleId_ScopeKind",
                        columns: x => new { x.RoleId, x.ScopeKind },
                        principalSchema: "shared",
                        principalTable: "Roles",
                        principalColumns: new[] { "Id", "ScopeKind" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AuthSessions",
                schema: "shared",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientIp = table.Column<IPAddress>(type: "inet", nullable: true),
                    UserAgent = table.Column<string>(type: "varchar(512)", maxLength: 512, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PkAuthSessions", x => x.Id);
                    table.CheckConstraint("CkAuthSessionsId", "uuid_extract_version(\"Id\") = 7");
                    table.CheckConstraint("CkAuthSessionsTime", "\"ExpiresAt\" > \"CreatedAt\" AND (\"RevokedAt\" IS NULL OR \"RevokedAt\" >= \"CreatedAt\")");
                    table.ForeignKey(
                        name: "FK_AuthSessions_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "shared",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OneTimeTokens",
                schema: "shared",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Purpose = table.Column<string>(type: "varchar(24)", maxLength: 24, nullable: false),
                    TokenHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    ConsumedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PkOneTimeTokens", x => x.Id);
                    table.UniqueConstraint("UqOneTimeTokensTokenHash", x => x.TokenHash);
                    table.CheckConstraint("CkOneTimeTokensId", "uuid_extract_version(\"Id\") = 7");
                    table.CheckConstraint("CkOneTimeTokensPurpose", "\"Purpose\" IN ('verify_email', 'reset_password')");
                    table.CheckConstraint("CkOneTimeTokensTime", " \"ExpiresAt\" > \"CreatedAt\" AND (\"ConsumedAt\" IS NULL OR \"ConsumedAt\" >= \"CreatedAt\") ");
                    table.CheckConstraint("CkOneTimeTokensTokenHash", "octet_length(\"TokenHash\") = 32");
                    table.ForeignKey(
                        name: "FK_OneTimeTokens_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "shared",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Organizations",
                schema: "shared",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    Slug = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false),
                    DisplayName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false, defaultValue: "active"),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SuspendedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PkOrganizations", x => x.Id);
                    table.CheckConstraint("CkOrganizationsDisplayName", "char_length(btrim(\"DisplayName\")) BETWEEN 1 AND 200");
                    table.CheckConstraint("CkOrganizationsId", "uuid_extract_version(\"Id\") = 7");
                    table.CheckConstraint("CkOrganizationsSlug", "\"Slug\" ~ '^[a-z0-9][a-z0-9-]{2,79}$'");
                    table.CheckConstraint("CkOrganizationsStatus", "\"Status\" IN ('active', 'suspended', 'closed')");
                    table.ForeignKey(
                        name: "FK_Organizations_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalSchema: "shared",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PasswordCredentials",
                schema: "shared",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PasswordHash = table.Column<string>(type: "varchar(512)", maxLength: 512, nullable: false),
                    FailedAttempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    LockedUntil = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    PasswordChangedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PkPasswordCredentials", x => x.Id);
                    table.UniqueConstraint("UqPasswordCredentialsUserId", x => x.UserId);
                    table.CheckConstraint("CkPasswordCredentialsFailedAttempts", "\"FailedAttempts\" >= 0");
                    table.CheckConstraint("CkPasswordCredentialsId", "uuid_extract_version(\"Id\") = 7");
                    table.CheckConstraint("CkPasswordCredentialsPasswordHash", "char_length(\"PasswordHash\") >= 40");
                    table.ForeignKey(
                        name: "FK_PasswordCredentials_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "shared",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PlatformRoleAssignments",
                schema: "shared",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScopeKind = table.Column<string>(type: "varchar(24)", maxLength: 24, nullable: false, defaultValue: "platform"),
                    AssignedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssignedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PkPlatformRoleAssignments", x => x.Id);
                    table.CheckConstraint("CkPlatformAssignmentTime", "\"RevokedAt\" IS NULL OR \"RevokedAt\" >= \"AssignedAt\"");
                    table.CheckConstraint("CkPlatformRoleAssignmentsId", "uuid_extract_version(\"Id\") = 7");
                    table.CheckConstraint("CkPlatformRoleAssignmentsScopeKind", "\"ScopeKind\" = 'platform'");
                    table.ForeignKey(
                        name: "FK_PlatformRoleAssignments_Roles_RoleId_ScopeKind",
                        columns: x => new { x.RoleId, x.ScopeKind },
                        principalSchema: "shared",
                        principalTable: "Roles",
                        principalColumns: new[] { "Id", "ScopeKind" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PlatformRoleAssignments_Users_AssignedByUserId",
                        column: x => x.AssignedByUserId,
                        principalSchema: "shared",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PlatformRoleAssignments_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "shared",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                schema: "shared",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    ReplacedByTokenId = table.Column<Guid>(type: "uuid", nullable: true),
                    IssuedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    ConsumedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PkRefreshTokens", x => x.Id);
                    table.UniqueConstraint("UqRefreshTokensTokenHash", x => x.TokenHash);
                    table.CheckConstraint("CkRefreshTokensId", "uuid_extract_version(\"Id\") = 7");
                    table.CheckConstraint("CkRefreshTokensTime", " \"ExpiresAt\" > \"IssuedAt\" AND (\"ConsumedAt\" IS NULL OR \"ConsumedAt\" >= \"IssuedAt\") AND (\"RevokedAt\" IS NULL OR \"RevokedAt\" >= \"IssuedAt\") ");
                    table.CheckConstraint("CkRefreshTokensTokenHash", "octet_length(\"TokenHash\") = 32");
                    table.ForeignKey(
                        name: "FK_RefreshTokens_AuthSessions_SessionId",
                        column: x => x.SessionId,
                        principalSchema: "shared",
                        principalTable: "AuthSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RefreshTokens_RefreshTokens_ReplacedByTokenId",
                        column: x => x.ReplacedByTokenId,
                        principalSchema: "shared",
                        principalTable: "RefreshTokens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "AccessScopes",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScopeKind = table.Column<string>(type: "varchar(24)", maxLength: 24, nullable: false),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "varchar(12)", maxLength: 12, nullable: false, defaultValue: "active"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    ArchivedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PkAccessScopes", x => x.Id);
                    table.UniqueConstraint("UqAccessScopesTenantIdIdScopeKind", x => new { x.TenantId, x.Id, x.ScopeKind });
                    table.UniqueConstraint("UqAccessScopesTenantIdScopeKindResourceId", x => new { x.TenantId, x.ScopeKind, x.ResourceId });
                    table.CheckConstraint("CkAccessScopesArchived", "(\"Status\" = 'archived') = (\"ArchivedAt\" IS NOT NULL)");
                    table.CheckConstraint("CkAccessScopesId", "uuid_extract_version(\"Id\") = 7");
                    table.CheckConstraint("CkAccessScopesScopeKind", "\"ScopeKind\" IN ('organization', 'project', 'team', 'performance')");
                    table.CheckConstraint("CkAccessScopesStatus", "\"Status\" IN ('active', 'archived')");
                    table.CheckConstraint("CkOrgScopeResource", "\"ScopeKind\" <> 'organization' OR \"ResourceId\" = \"TenantId\"");
                    table.ForeignKey(
                        name: "FK_AccessScopes_Organizations_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "shared",
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationMembers",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    MembershipType = table.Column<string>(type: "varchar(12)", maxLength: 12, nullable: false, defaultValue: "employee"),
                    Status = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false, defaultValue: "active"),
                    JoinedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    RemovedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PkOrganizationMembers", x => x.Id);
                    table.UniqueConstraint("UqOrganizationMembersTenantIdId", x => new { x.TenantId, x.Id });
                    table.UniqueConstraint("UqOrganizationMembersTenantIdUserId", x => new { x.TenantId, x.UserId });
                    table.CheckConstraint("CkMemberRemoved", "(\"Status\" = 'removed') = (\"RemovedAt\" IS NOT NULL)");
                    table.CheckConstraint("CkOrganizationMembersId", "uuid_extract_version(\"Id\") = 7");
                    table.CheckConstraint("CkOrganizationMembersMembershipType", "\"MembershipType\" IN ('employee', 'contractor', 'guest')");
                    table.CheckConstraint("CkOrganizationMembersStatus", "\"Status\" IN ('active', 'suspended', 'removed')");
                    table.ForeignKey(
                        name: "FK_OrganizationMembers_Organizations_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "shared",
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrganizationMembers_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "shared",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PlatformSecurityAuditEvents",
                schema: "shared",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    TargetTenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    EventCode = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    TargetType = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: true),
                    Success = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    ClientIp = table.Column<IPAddress>(type: "inet", nullable: true),
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Details = table.Column<JsonElement>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PkPlatformSecurityAuditEvents", x => x.Id);
                    table.CheckConstraint("CkPlatformSecurityAuditEventsDetails", "jsonb_typeof(\"Details\") = 'object'");
                    table.CheckConstraint("CkPlatformSecurityAuditEventsId", "uuid_extract_version(\"Id\") = 7");
                    table.ForeignKey(
                        name: "FK_PlatformSecurityAuditEvents_Organizations_TargetTenantId",
                        column: x => x.TargetTenantId,
                        principalSchema: "shared",
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SecurityAuditEvents",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorMemberId = table.Column<Guid>(type: "uuid", nullable: true),
                    EventCode = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    TargetType = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: true),
                    Success = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    ClientIp = table.Column<IPAddress>(type: "inet", nullable: true),
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Details = table.Column<JsonElement>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PkSecurityAuditEvents", x => x.Id);
                    table.CheckConstraint("CkSecurityAuditEventsDetails", "jsonb_typeof(\"Details\") = 'object'");
                    table.CheckConstraint("CkSecurityAuditEventsId", "uuid_extract_version(\"Id\") = 7");
                    table.ForeignKey(
                        name: "FK_SecurityAuditEvents_Organizations_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "shared",
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Invitations",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    InviteeEmail = table.Column<string>(type: "varchar(320)", maxLength: 320, nullable: false),
                    MembershipType = table.Column<string>(type: "varchar(12)", maxLength: 12, nullable: false, defaultValue: "employee"),
                    TokenHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    Status = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false, defaultValue: "pending"),
                    InvitedByMemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcceptedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    AcceptedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PkInvitations", x => x.Id);
                    table.UniqueConstraint("UqInvitationsTenantIdId", x => new { x.TenantId, x.Id });
                    table.UniqueConstraint("UqInvitationsTokenHash", x => x.TokenHash);
                    table.CheckConstraint("CkInvitationAcceptance", " (\"AcceptedByUserId\" IS NULL) = (\"AcceptedAt\" IS NULL) AND (\"Status\" <> 'accepted' OR \"AcceptedAt\" IS NOT NULL) AND (\"Status\" <> 'revoked' OR \"RevokedAt\" IS NOT NULL) ");
                    table.CheckConstraint("CkInvitationsId", "uuid_extract_version(\"Id\") = 7");
                    table.CheckConstraint("CkInvitationsInviteeEmail", "char_length(btrim(\"InviteeEmail\")) BETWEEN 3 AND 320 AND \"InviteeEmail\" = btrim(\"InviteeEmail\")");
                    table.CheckConstraint("CkInvitationsMembershipType", "\"MembershipType\" IN ('employee', 'contractor', 'guest')");
                    table.CheckConstraint("CkInvitationsStatus", "\"Status\" IN ('pending', 'accepted', 'revoked', 'expired')");
                    table.CheckConstraint("CkInvitationsTokenHash", "octet_length(\"TokenHash\") = 32");
                    table.CheckConstraint("CkInvitationTimes", "\"ExpiresAt\" > \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_Invitations_OrganizationMembers_TenantId_InvitedByMemberId",
                        columns: x => new { x.TenantId, x.InvitedByMemberId },
                        principalSchema: "dbo",
                        principalTable: "OrganizationMembers",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Invitations_Organizations_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "shared",
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Invitations_Users_AcceptedByUserId",
                        column: x => x.AcceptedByUserId,
                        principalSchema: "shared",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MemberPermissionGrants",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScopeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScopeKind = table.Column<string>(type: "varchar(24)", maxLength: 24, nullable: false),
                    PermissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    GrantedByMemberId = table.Column<Guid>(type: "uuid", nullable: true),
                    Justification = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false),
                    GrantedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PkMemberPermissionGrants", x => x.Id);
                    table.CheckConstraint("CkMemberGrantTime", " (\"ExpiresAt\" IS NULL OR \"ExpiresAt\" > \"GrantedAt\") AND (\"RevokedAt\" IS NULL OR \"RevokedAt\" >= \"GrantedAt\") ");
                    table.CheckConstraint("CkMemberPermissionGrantsId", "uuid_extract_version(\"Id\") = 7");
                    table.CheckConstraint("CkMemberPermissionGrantsJustification", "char_length(btrim(\"Justification\")) >= 8");
                    table.ForeignKey(
                        name: "FK_MemberPermissionGrants_AccessScopes_TenantId_ScopeId_ScopeK~",
                        columns: x => new { x.TenantId, x.ScopeId, x.ScopeKind },
                        principalSchema: "dbo",
                        principalTable: "AccessScopes",
                        principalColumns: new[] { "TenantId", "Id", "ScopeKind" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MemberPermissionGrants_OrganizationMembers_TenantId_Granted~",
                        columns: x => new { x.TenantId, x.GrantedByMemberId },
                        principalSchema: "dbo",
                        principalTable: "OrganizationMembers",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MemberPermissionGrants_OrganizationMembers_TenantId_MemberId",
                        columns: x => new { x.TenantId, x.MemberId },
                        principalSchema: "dbo",
                        principalTable: "OrganizationMembers",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MemberPermissionGrants_Organizations_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "shared",
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MemberPermissionGrants_Permissions_PermissionId_ScopeKind",
                        columns: x => new { x.PermissionId, x.ScopeKind },
                        principalSchema: "shared",
                        principalTable: "Permissions",
                        principalColumns: new[] { "Id", "ScopeKind" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MemberRoleAssignments",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScopeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScopeKind = table.Column<string>(type: "varchar(24)", maxLength: 24, nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedByMemberId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true),
                    AssignedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PkMemberRoleAssignments", x => x.Id);
                    table.CheckConstraint("CkMemberRoleAssignmentsId", "uuid_extract_version(\"Id\") = 7");
                    table.CheckConstraint("CkMemberRoleTime", " (\"ExpiresAt\" IS NULL OR \"ExpiresAt\" > \"AssignedAt\") AND (\"RevokedAt\" IS NULL OR \"RevokedAt\" >= \"AssignedAt\") ");
                    table.ForeignKey(
                        name: "FK_MemberRoleAssignments_AccessScopes_TenantId_ScopeId_ScopeKi~",
                        columns: x => new { x.TenantId, x.ScopeId, x.ScopeKind },
                        principalSchema: "dbo",
                        principalTable: "AccessScopes",
                        principalColumns: new[] { "TenantId", "Id", "ScopeKind" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MemberRoleAssignments_OrganizationMembers_TenantId_Assigned~",
                        columns: x => new { x.TenantId, x.AssignedByMemberId },
                        principalSchema: "dbo",
                        principalTable: "OrganizationMembers",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MemberRoleAssignments_OrganizationMembers_TenantId_MemberId",
                        columns: x => new { x.TenantId, x.MemberId },
                        principalSchema: "dbo",
                        principalTable: "OrganizationMembers",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MemberRoleAssignments_Organizations_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "shared",
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MemberRoleAssignments_Roles_RoleId_ScopeKind",
                        columns: x => new { x.RoleId, x.ScopeKind },
                        principalSchema: "shared",
                        principalTable: "Roles",
                        principalColumns: new[] { "Id", "ScopeKind" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InvitationRoleGrants",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvitationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScopeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScopeKind = table.Column<string>(type: "varchar(24)", maxLength: 24, nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PkInvitationRoleGrants", x => x.Id);
                    table.UniqueConstraint("UqInvitationRoleGrantsTenantIdInvitationIdScopeIdRoleId", x => new { x.TenantId, x.InvitationId, x.ScopeId, x.RoleId });
                    table.CheckConstraint("CkInvitationRoleGrantsId", "uuid_extract_version(\"Id\") = 7");
                    table.ForeignKey(
                        name: "FK_InvitationRoleGrants_AccessScopes_TenantId_ScopeId_ScopeKind",
                        columns: x => new { x.TenantId, x.ScopeId, x.ScopeKind },
                        principalSchema: "dbo",
                        principalTable: "AccessScopes",
                        principalColumns: new[] { "TenantId", "Id", "ScopeKind" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InvitationRoleGrants_Invitations_TenantId_InvitationId",
                        columns: x => new { x.TenantId, x.InvitationId },
                        principalSchema: "dbo",
                        principalTable: "Invitations",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InvitationRoleGrants_Organizations_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "shared",
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InvitationRoleGrants_Roles_RoleId_ScopeKind",
                        columns: x => new { x.RoleId, x.ScopeKind },
                        principalSchema: "shared",
                        principalTable: "Roles",
                        principalColumns: new[] { "Id", "ScopeKind" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IxAuthSessionsUserExpiry",
                schema: "shared",
                table: "AuthSessions",
                columns: new[] { "UserId", "ExpiresAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IxInvitationsExpiry",
                schema: "dbo",
                table: "Invitations",
                column: "ExpiresAt",
                filter: "\"Status\" = 'pending'");

            migrationBuilder.CreateIndex(
                name: "IxMemberGrantsScope",
                schema: "dbo",
                table: "MemberPermissionGrants",
                columns: new[] { "TenantId", "ScopeId", "PermissionId" },
                filter: "\"RevokedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "UxMemberGrantsActive",
                schema: "dbo",
                table: "MemberPermissionGrants",
                columns: new[] { "TenantId", "MemberId", "ScopeId", "PermissionId" },
                unique: true,
                filter: "\"RevokedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IxMemberRolesScope",
                schema: "dbo",
                table: "MemberRoleAssignments",
                columns: new[] { "TenantId", "ScopeId", "RoleId" },
                filter: "\"RevokedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "UxMemberRolesActive",
                schema: "dbo",
                table: "MemberRoleAssignments",
                columns: new[] { "TenantId", "MemberId", "ScopeId", "RoleId" },
                unique: true,
                filter: "\"RevokedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IxOneTimeTokensExpiry",
                schema: "shared",
                table: "OneTimeTokens",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IxOneTimeTokensUserPurpose",
                schema: "shared",
                table: "OneTimeTokens",
                columns: new[] { "UserId", "Purpose", "CreatedAt" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IxOrganizationMembersStatus",
                schema: "dbo",
                table: "OrganizationMembers",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IxOrganizationsStatus",
                schema: "shared",
                table: "Organizations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "UxPlatformRoleActive",
                schema: "shared",
                table: "PlatformRoleAssignments",
                columns: new[] { "UserId", "RoleId" },
                unique: true,
                filter: "\"RevokedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IxPlatformSecurityAuditTime",
                schema: "shared",
                table: "PlatformSecurityAuditEvents",
                column: "OccurredAt",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IxRefreshTokensExpiry",
                schema: "shared",
                table: "RefreshTokens",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IxRefreshTokensSession",
                schema: "shared",
                table: "RefreshTokens",
                columns: new[] { "SessionId", "IssuedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "UxRefreshTokensReplacement",
                schema: "shared",
                table: "RefreshTokens",
                column: "ReplacedByTokenId",
                unique: true,
                filter: "\"ReplacedByTokenId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IxRolePermissionsPermission",
                schema: "shared",
                table: "RolePermissions",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IxSecurityAuditActorTime",
                schema: "dbo",
                table: "SecurityAuditEvents",
                columns: new[] { "TenantId", "ActorUserId", "OccurredAt" },
                descending: new[] { false, false, true },
                filter: "\"ActorUserId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IxSecurityAuditTenantTime",
                schema: "dbo",
                table: "SecurityAuditEvents",
                columns: new[] { "TenantId", "OccurredAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IxUsersStatus",
                schema: "shared",
                table: "Users",
                column: "Status");

            // Expression indexes cannot be represented by the current Fluent mappings.
            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX "UxUsersPrimaryEmailCi"
                    ON shared."Users" (lower("PrimaryEmail"));
                CREATE UNIQUE INDEX "UxOrganizationsSlugCi"
                    ON shared."Organizations" (lower("Slug"));
                CREATE UNIQUE INDEX "UxInvitationsPendingEmail"
                    ON dbo."Invitations" ("TenantId", lower("InviteeEmail"))
                    WHERE "Status" = 'pending';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InvitationRoleGrants",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "MemberPermissionGrants",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "MemberRoleAssignments",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "OneTimeTokens",
                schema: "shared");

            migrationBuilder.DropTable(
                name: "PasswordCredentials",
                schema: "shared");

            migrationBuilder.DropTable(
                name: "PlatformRoleAssignments",
                schema: "shared");

            migrationBuilder.DropTable(
                name: "PlatformSecurityAuditEvents",
                schema: "shared");

            migrationBuilder.DropTable(
                name: "RefreshTokens",
                schema: "shared");

            migrationBuilder.DropTable(
                name: "RolePermissions",
                schema: "shared");

            migrationBuilder.DropTable(
                name: "SecurityAuditEvents",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Invitations",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "AccessScopes",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "AuthSessions",
                schema: "shared");

            migrationBuilder.DropTable(
                name: "Permissions",
                schema: "shared");

            migrationBuilder.DropTable(
                name: "Roles",
                schema: "shared");

            migrationBuilder.DropTable(
                name: "OrganizationMembers",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Organizations",
                schema: "shared");

            migrationBuilder.DropTable(
                name: "Users",
                schema: "shared");
        }
    }
}
