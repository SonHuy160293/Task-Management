-- Identity & Access schema blueprint for a multi-tenant task management SaaS.
-- PostgreSQL 18+ is REQUIRED for uuidv7() and uuid_extract_version().
-- DDL for logical design review: run in a migration after adapting naming and grants.
-- One database, two physical schemas: shared (platform), dbo (tenant-owned).
-- Application handles member activation, last-owner protection, scope lifecycle,
-- authorization decisions, transactional invitation acceptance and token rotation.

BEGIN;

CREATE SCHEMA IF NOT EXISTS shared;
CREATE SCHEMA IF NOT EXISTS dbo;

-- ===========================================================
-- SHARED: globally unique account identities and tenant registry
-- ===========================================================
CREATE TABLE shared.users (
    id                 uuid PRIMARY KEY DEFAULT uuidv7() CHECK (uuid_extract_version(id) = 7),
    primary_email      varchar(320) NOT NULL CHECK (char_length(btrim(primary_email)) BETWEEN 3 AND 320 AND primary_email = btrim(primary_email)),
    display_name       varchar(160) NOT NULL CHECK (char_length(btrim(display_name)) BETWEEN 1 AND 160),
    avatar_url         text NULL,
    locale             varchar(16) NOT NULL DEFAULT 'en',
    time_zone          varchar(64) NOT NULL DEFAULT 'UTC',
    status             varchar(16) NOT NULL DEFAULT 'pending'
                       CHECK (status IN ('pending', 'active', 'suspended', 'deleted')),
    email_verified_at  timestamptz NULL,
    last_login_at      timestamptz NULL,
    created_at         timestamptz NOT NULL DEFAULT now(),
    updated_at         timestamptz NOT NULL DEFAULT now()
);
CREATE UNIQUE INDEX ux_users_primary_email_ci ON shared.users (lower(primary_email));
CREATE INDEX ix_users_status ON shared.users (status);

CREATE TABLE shared.organizations (
    id                 uuid PRIMARY KEY DEFAULT uuidv7() CHECK (uuid_extract_version(id) = 7),
    slug               varchar(80) NOT NULL CHECK (slug ~ '^[a-z0-9][a-z0-9-]{2,79}$'),
    display_name       varchar(200) NOT NULL CHECK (char_length(btrim(display_name)) BETWEEN 1 AND 200),
    status             varchar(16) NOT NULL DEFAULT 'active'
                       CHECK (status IN ('active', 'suspended', 'closed')),
    created_by_user_id uuid NOT NULL REFERENCES shared.users(id) ON DELETE RESTRICT,
    suspended_at       timestamptz NULL,
    created_at         timestamptz NOT NULL DEFAULT now(),
    updated_at         timestamptz NOT NULL DEFAULT now()
);
CREATE UNIQUE INDEX ux_organizations_slug_ci ON shared.organizations (lower(slug));
CREATE INDEX ix_organizations_status ON shared.organizations (status);

-- Password PHC strings (Argon2id) contain hashing parameters and salt.
-- Do not store a separate plaintext password, raw refresh token or raw reset token.
CREATE TABLE shared.password_credentials (
    id                 uuid PRIMARY KEY DEFAULT uuidv7() CHECK (uuid_extract_version(id) = 7),
    user_id            uuid NOT NULL UNIQUE REFERENCES shared.users(id) ON DELETE RESTRICT,
    password_hash      varchar(512) NOT NULL CHECK (char_length(password_hash) >= 40),
    failed_attempts    integer NOT NULL DEFAULT 0 CHECK (failed_attempts >= 0),
    locked_until       timestamptz NULL,
    password_changed_at timestamptz NOT NULL DEFAULT now(),
    created_at         timestamptz NOT NULL DEFAULT now(),
    updated_at         timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE shared.auth_sessions (
    id                 uuid PRIMARY KEY DEFAULT uuidv7() CHECK (uuid_extract_version(id) = 7),
    user_id            uuid NOT NULL REFERENCES shared.users(id) ON DELETE RESTRICT,
    client_ip          inet NULL,
    user_agent         varchar(512) NULL,
    created_at         timestamptz NOT NULL DEFAULT now(),
    last_seen_at       timestamptz NULL,
    expires_at         timestamptz NOT NULL,
    revoked_at         timestamptz NULL,
    CONSTRAINT ck_auth_sessions_time CHECK (expires_at > created_at AND (revoked_at IS NULL OR revoked_at >= created_at))
);
CREATE INDEX ix_auth_sessions_user_expiry ON shared.auth_sessions (user_id, expires_at DESC);

CREATE TABLE shared.refresh_tokens (
    id                 uuid PRIMARY KEY DEFAULT uuidv7() CHECK (uuid_extract_version(id) = 7),
    session_id         uuid NOT NULL REFERENCES shared.auth_sessions(id) ON DELETE RESTRICT,
    token_hash         bytea NOT NULL UNIQUE CHECK (octet_length(token_hash) = 32),
    replaced_by_token_id uuid NULL REFERENCES shared.refresh_tokens(id) ON DELETE SET NULL,
    issued_at          timestamptz NOT NULL DEFAULT now(),
    expires_at         timestamptz NOT NULL,
    consumed_at        timestamptz NULL,
    revoked_at         timestamptz NULL,
    CONSTRAINT ck_refresh_tokens_time CHECK (
        expires_at > issued_at AND
        (consumed_at IS NULL OR consumed_at >= issued_at) AND
        (revoked_at IS NULL OR revoked_at >= issued_at)
    )
);
CREATE UNIQUE INDEX ux_refresh_tokens_replacement ON shared.refresh_tokens (replaced_by_token_id)
    WHERE replaced_by_token_id IS NOT NULL;
CREATE INDEX ix_refresh_tokens_session ON shared.refresh_tokens (session_id, issued_at DESC);
CREATE INDEX ix_refresh_tokens_expiry ON shared.refresh_tokens (expires_at);

CREATE TABLE shared.one_time_tokens (
    id                 uuid PRIMARY KEY DEFAULT uuidv7() CHECK (uuid_extract_version(id) = 7),
    user_id            uuid NOT NULL REFERENCES shared.users(id) ON DELETE RESTRICT,
    purpose            varchar(24) NOT NULL CHECK (purpose IN ('verify_email', 'reset_password')),
    token_hash         bytea NOT NULL UNIQUE CHECK (octet_length(token_hash) = 32),
    created_at         timestamptz NOT NULL DEFAULT now(),
    expires_at         timestamptz NOT NULL,
    consumed_at        timestamptz NULL,
    CONSTRAINT ck_one_time_tokens_time CHECK (
        expires_at > created_at AND (consumed_at IS NULL OR consumed_at >= created_at)
    )
);
CREATE INDEX ix_one_time_tokens_user_purpose ON shared.one_time_tokens (user_id, purpose, created_at DESC);
CREATE INDEX ix_one_time_tokens_expiry ON shared.one_time_tokens (expires_at);

-- All built-in permission and role definitions are platform-managed, not tenant-owned.
-- Scope kind is duplicated in role_permissions and assignments intentionally,
-- permitting composite foreign keys to enforce role/permission/scope matching.
CREATE TABLE shared.permissions (
    id                 uuid PRIMARY KEY DEFAULT uuidv7() CHECK (uuid_extract_version(id) = 7),
    code               varchar(120) NOT NULL UNIQUE
                       CHECK (code ~ '^[a-z][a-z0-9_.]*$'),
    module             varchar(40) NOT NULL,
    scope_kind         varchar(24) NOT NULL
                       CHECK (scope_kind IN ('platform', 'organization', 'project', 'team', 'performance')),
    description        varchar(500) NOT NULL,
    is_sensitive       boolean NOT NULL DEFAULT false,
    is_active          boolean NOT NULL DEFAULT true,
    created_at         timestamptz NOT NULL DEFAULT now(),
    UNIQUE (id, scope_kind)
);

CREATE TABLE shared.roles (
    id                 uuid PRIMARY KEY DEFAULT uuidv7() CHECK (uuid_extract_version(id) = 7),
    code               varchar(80) NOT NULL UNIQUE
                       CHECK (code ~ '^[a-z][a-z0-9_]*$'),
    display_name       varchar(120) NOT NULL,
    scope_kind         varchar(24) NOT NULL
                       CHECK (scope_kind IN ('platform', 'organization', 'project', 'team', 'performance')),
    description        varchar(500) NULL,
    is_assignable      boolean NOT NULL DEFAULT true,
    is_active          boolean NOT NULL DEFAULT true,
    created_at         timestamptz NOT NULL DEFAULT now(),
    UNIQUE (id, scope_kind)
);

CREATE TABLE shared.role_permissions (
    id                 uuid PRIMARY KEY DEFAULT uuidv7() CHECK (uuid_extract_version(id) = 7),
    role_id            uuid NOT NULL,
    permission_id      uuid NOT NULL,
    scope_kind         varchar(24) NOT NULL,
    created_at         timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT fk_role_perm_role FOREIGN KEY (role_id, scope_kind)
      REFERENCES shared.roles (id, scope_kind) ON DELETE RESTRICT,
    CONSTRAINT fk_role_perm_permission FOREIGN KEY (permission_id, scope_kind)
      REFERENCES shared.permissions (id, scope_kind) ON DELETE RESTRICT,
    UNIQUE (role_id, permission_id)
);
CREATE INDEX ix_role_permissions_permission ON shared.role_permissions (permission_id);

CREATE TABLE shared.platform_role_assignments (
    id                 uuid PRIMARY KEY DEFAULT uuidv7() CHECK (uuid_extract_version(id) = 7),
    user_id            uuid NOT NULL REFERENCES shared.users(id) ON DELETE RESTRICT,
    role_id            uuid NOT NULL,
    scope_kind         varchar(24) NOT NULL DEFAULT 'platform' CHECK (scope_kind = 'platform'),
    assigned_by_user_id uuid NULL REFERENCES shared.users(id) ON DELETE RESTRICT,
    assigned_at        timestamptz NOT NULL DEFAULT now(),
    revoked_at         timestamptz NULL,
    CONSTRAINT fk_platform_role FOREIGN KEY (role_id, scope_kind)
      REFERENCES shared.roles(id, scope_kind) ON DELETE RESTRICT,
    CONSTRAINT ck_platform_assignment_time CHECK (revoked_at IS NULL OR revoked_at >= assigned_at)
);
CREATE UNIQUE INDEX ux_platform_role_active ON shared.platform_role_assignments (user_id, role_id)
    WHERE revoked_at IS NULL;

-- ===========================================================
-- DBO: tenant-owned identities, scopes and authorization grants
-- ===========================================================
CREATE TABLE dbo.organization_members (
    id                 uuid PRIMARY KEY DEFAULT uuidv7() CHECK (uuid_extract_version(id) = 7),
    tenant_id          uuid NOT NULL REFERENCES shared.organizations(id) ON DELETE RESTRICT,
    user_id            uuid NOT NULL REFERENCES shared.users(id) ON DELETE RESTRICT,
    membership_type    varchar(12) NOT NULL DEFAULT 'employee'
                       CHECK (membership_type IN ('employee', 'contractor', 'guest')),
    status             varchar(16) NOT NULL DEFAULT 'active'
                       CHECK (status IN ('active', 'suspended', 'removed')),
    joined_at          timestamptz NOT NULL DEFAULT now(),
    removed_at         timestamptz NULL,
    created_at         timestamptz NOT NULL DEFAULT now(),
    updated_at         timestamptz NOT NULL DEFAULT now(),
    UNIQUE (tenant_id, user_id),
    UNIQUE (tenant_id, id),
    CONSTRAINT ck_member_removed CHECK ((status = 'removed') = (removed_at IS NOT NULL))
);
CREATE INDEX ix_organization_members_status ON dbo.organization_members (tenant_id, status);

-- Projects, teams, performance review cycles / audiences are owned by other modules.
-- resource_id is a logical reference to that owning module's aggregate ID.
-- Only the organization scope has a locally-checkable matching resource ID.
CREATE TABLE dbo.access_scopes (
    id                 uuid PRIMARY KEY DEFAULT uuidv7() CHECK (uuid_extract_version(id) = 7),
    tenant_id          uuid NOT NULL REFERENCES shared.organizations(id) ON DELETE RESTRICT,
    scope_kind         varchar(24) NOT NULL
                       CHECK (scope_kind IN ('organization', 'project', 'team', 'performance')),
    resource_id        uuid NOT NULL,
    status             varchar(12) NOT NULL DEFAULT 'active'
                       CHECK (status IN ('active', 'archived')),
    created_at         timestamptz NOT NULL DEFAULT now(),
    archived_at        timestamptz NULL,
    UNIQUE (tenant_id, scope_kind, resource_id),
    UNIQUE (tenant_id, id, scope_kind),
    CONSTRAINT ck_org_scope_resource CHECK (scope_kind <> 'organization' OR resource_id = tenant_id),
    CONSTRAINT ck_access_scopes_archived CHECK ((status = 'archived') = (archived_at IS NOT NULL))
);

CREATE TABLE dbo.invitations (
    id                 uuid PRIMARY KEY DEFAULT uuidv7() CHECK (uuid_extract_version(id) = 7),
    tenant_id          uuid NOT NULL REFERENCES shared.organizations(id) ON DELETE RESTRICT,
    invitee_email      varchar(320) NOT NULL CHECK (char_length(btrim(invitee_email)) BETWEEN 3 AND 320 AND invitee_email = btrim(invitee_email)),
    membership_type    varchar(12) NOT NULL DEFAULT 'employee'
                       CHECK (membership_type IN ('employee', 'contractor', 'guest')),
    token_hash         bytea NOT NULL UNIQUE CHECK (octet_length(token_hash) = 32),
    status             varchar(16) NOT NULL DEFAULT 'pending'
                       CHECK (status IN ('pending', 'accepted', 'revoked', 'expired')),
    invited_by_member_id uuid NOT NULL,
    accepted_by_user_id uuid NULL REFERENCES shared.users(id) ON DELETE RESTRICT,
    created_at         timestamptz NOT NULL DEFAULT now(),
    expires_at         timestamptz NOT NULL,
    accepted_at        timestamptz NULL,
    revoked_at         timestamptz NULL,
    UNIQUE (tenant_id, id),
    FOREIGN KEY (tenant_id, invited_by_member_id)
      REFERENCES dbo.organization_members(tenant_id, id) ON DELETE RESTRICT,
    CONSTRAINT ck_invitation_times CHECK (expires_at > created_at),
    CONSTRAINT ck_invitation_acceptance CHECK (
        (accepted_by_user_id IS NULL) = (accepted_at IS NULL) AND
        (status <> 'accepted' OR accepted_at IS NOT NULL) AND
        (status <> 'revoked' OR revoked_at IS NOT NULL)
    )
);
CREATE UNIQUE INDEX ux_invitations_pending_email ON dbo.invitations (tenant_id, lower(invitee_email))
    WHERE status = 'pending';
CREATE INDEX ix_invitations_expiry ON dbo.invitations (expires_at) WHERE status = 'pending';

-- Invitation may grant an organization role and a project/team/performance role.
-- Apply these atomically when invitation is accepted, then enforce guest restrictions.
CREATE TABLE dbo.invitation_role_grants (
    id                 uuid PRIMARY KEY DEFAULT uuidv7() CHECK (uuid_extract_version(id) = 7),
    tenant_id          uuid NOT NULL REFERENCES shared.organizations(id) ON DELETE RESTRICT,
    invitation_id      uuid NOT NULL,
    scope_id           uuid NOT NULL,
    scope_kind         varchar(24) NOT NULL,
    role_id            uuid NOT NULL,
    created_at         timestamptz NOT NULL DEFAULT now(),
    FOREIGN KEY (tenant_id, invitation_id)
      REFERENCES dbo.invitations(tenant_id, id) ON DELETE RESTRICT,
    FOREIGN KEY (tenant_id, scope_id, scope_kind)
      REFERENCES dbo.access_scopes(tenant_id, id, scope_kind) ON DELETE RESTRICT,
    FOREIGN KEY (role_id, scope_kind)
      REFERENCES shared.roles(id, scope_kind) ON DELETE RESTRICT,
    UNIQUE (tenant_id, invitation_id, scope_id, role_id)
);

CREATE TABLE dbo.member_role_assignments (
    id                 uuid PRIMARY KEY DEFAULT uuidv7() CHECK (uuid_extract_version(id) = 7),
    tenant_id          uuid NOT NULL REFERENCES shared.organizations(id) ON DELETE RESTRICT,
    member_id          uuid NOT NULL,
    scope_id           uuid NOT NULL,
    scope_kind         varchar(24) NOT NULL,
    role_id            uuid NOT NULL,
    assigned_by_member_id uuid NULL,
    reason             varchar(300) NULL,
    assigned_at        timestamptz NOT NULL DEFAULT now(),
    expires_at         timestamptz NULL,
    revoked_at         timestamptz NULL,
    FOREIGN KEY (tenant_id, member_id)
      REFERENCES dbo.organization_members(tenant_id, id) ON DELETE RESTRICT,
    FOREIGN KEY (tenant_id, assigned_by_member_id)
      REFERENCES dbo.organization_members(tenant_id, id) ON DELETE RESTRICT,
    FOREIGN KEY (tenant_id, scope_id, scope_kind)
      REFERENCES dbo.access_scopes(tenant_id, id, scope_kind) ON DELETE RESTRICT,
    FOREIGN KEY (role_id, scope_kind)
      REFERENCES shared.roles(id, scope_kind) ON DELETE RESTRICT,
    CONSTRAINT ck_member_role_time CHECK (
        (expires_at IS NULL OR expires_at > assigned_at) AND
        (revoked_at IS NULL OR revoked_at >= assigned_at)
    )
);
CREATE UNIQUE INDEX ux_member_roles_active ON dbo.member_role_assignments
    (tenant_id, member_id, scope_id, role_id) WHERE revoked_at IS NULL;
CREATE INDEX ix_member_roles_scope ON dbo.member_role_assignments
    (tenant_id, scope_id, role_id) WHERE revoked_at IS NULL;

-- Direct ALLOW grants extend RBAC. No explicit DENY in version 1.
CREATE TABLE dbo.member_permission_grants (
    id                 uuid PRIMARY KEY DEFAULT uuidv7() CHECK (uuid_extract_version(id) = 7),
    tenant_id          uuid NOT NULL REFERENCES shared.organizations(id) ON DELETE RESTRICT,
    member_id          uuid NOT NULL,
    scope_id           uuid NOT NULL,
    scope_kind         varchar(24) NOT NULL,
    permission_id      uuid NOT NULL,
    granted_by_member_id uuid NULL,
    justification      varchar(500) NOT NULL CHECK (char_length(btrim(justification)) >= 8),
    granted_at         timestamptz NOT NULL DEFAULT now(),
    expires_at         timestamptz NULL,
    revoked_at         timestamptz NULL,
    FOREIGN KEY (tenant_id, member_id)
      REFERENCES dbo.organization_members(tenant_id, id) ON DELETE RESTRICT,
    FOREIGN KEY (tenant_id, granted_by_member_id)
      REFERENCES dbo.organization_members(tenant_id, id) ON DELETE RESTRICT,
    FOREIGN KEY (tenant_id, scope_id, scope_kind)
      REFERENCES dbo.access_scopes(tenant_id, id, scope_kind) ON DELETE RESTRICT,
    FOREIGN KEY (permission_id, scope_kind)
      REFERENCES shared.permissions(id, scope_kind) ON DELETE RESTRICT,
    CONSTRAINT ck_member_grant_time CHECK (
        (expires_at IS NULL OR expires_at > granted_at) AND
        (revoked_at IS NULL OR revoked_at >= granted_at)
    )
);
CREATE UNIQUE INDEX ux_member_grants_active ON dbo.member_permission_grants
    (tenant_id, member_id, scope_id, permission_id) WHERE revoked_at IS NULL;
CREATE INDEX ix_member_grants_scope ON dbo.member_permission_grants
    (tenant_id, scope_id, permission_id) WHERE revoked_at IS NULL;

-- Append-only tenant security log. actor/subject IDs are historical snapshots;
-- actor IDs intentionally have no strict FK, to permit retention after erasure.
CREATE TABLE dbo.security_audit_events (
    id                 uuid PRIMARY KEY DEFAULT uuidv7() CHECK (uuid_extract_version(id) = 7),
    tenant_id          uuid NOT NULL REFERENCES shared.organizations(id) ON DELETE RESTRICT,
    actor_user_id      uuid NULL,
    actor_member_id    uuid NULL,
    event_code         varchar(100) NOT NULL,
    target_type        varchar(80) NOT NULL,
    target_id          uuid NULL,
    success            boolean NOT NULL DEFAULT true,
    client_ip          inet NULL,
    correlation_id     uuid NULL,
    details            jsonb NOT NULL DEFAULT '{}'::jsonb CHECK (jsonb_typeof(details) = 'object'),
    occurred_at        timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX ix_security_audit_tenant_time ON dbo.security_audit_events (tenant_id, occurred_at DESC);
CREATE INDEX ix_security_audit_actor_time ON dbo.security_audit_events
    (tenant_id, actor_user_id, occurred_at DESC) WHERE actor_user_id IS NOT NULL;

CREATE TABLE shared.platform_security_audit_events (
    id                 uuid PRIMARY KEY DEFAULT uuidv7() CHECK (uuid_extract_version(id) = 7),
    actor_user_id      uuid NULL,
    target_tenant_id   uuid NULL REFERENCES shared.organizations(id) ON DELETE RESTRICT,
    event_code         varchar(100) NOT NULL,
    target_type        varchar(80) NOT NULL,
    target_id          uuid NULL,
    success            boolean NOT NULL DEFAULT true,
    client_ip          inet NULL,
    correlation_id     uuid NULL,
    details            jsonb NOT NULL DEFAULT '{}'::jsonb CHECK (jsonb_typeof(details) = 'object'),
    occurred_at        timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX ix_platform_security_audit_time ON shared.platform_security_audit_events (occurred_at DESC);

-- ===========================================================
-- OPTIONAL DEFENSE-IN-DEPTH: tenant Row Level Security
-- Important: run app as non-superuser, non-BYPASSRLS role. Use SET LOCAL
-- app.tenant_id = '<tenant_uuid>' inside a transaction for tenant queries.
-- This is NOT a replacement for explicit application authorization.
-- ===========================================================
DO $rls$
DECLARE
    table_name text;
BEGIN
    FOREACH table_name IN ARRAY ARRAY[
      'organization_members', 'access_scopes', 'invitations',
      'invitation_role_grants', 'member_role_assignments',
      'member_permission_grants', 'security_audit_events'
    ] LOOP
       EXECUTE format('ALTER TABLE dbo.%I ENABLE ROW LEVEL SECURITY', table_name);
       EXECUTE format('ALTER TABLE dbo.%I FORCE ROW LEVEL SECURITY', table_name);
       EXECUTE format(
         'CREATE POLICY tenant_isolation ON dbo.%I USING (tenant_id = nullif(current_setting(''app.tenant_id'', true), '''')::uuid) WITH CHECK (tenant_id = nullif(current_setting(''app.tenant_id'', true), '''')::uuid)',
         table_name
       );
    END LOOP;
END
$rls$;

COMMIT;
