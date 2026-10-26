# Proposed Identity permission catalog and built-in roles

This file contains **proposed seeds** for the existing `shared.Permissions`, `shared.Roles` and `shared.RolePermissions` tables. These particular codes are product decisions derived from endpoint needs, not existing committed database values. Before implementing a protected endpoint, Codex must confirm the code is seeded/registered; it must **not invent** constants or hide missing registration by making endpoints public.

## Permission definitions

| Scope kind | Permission code | Intended capability |
|---|---|---|
| platform | `platform.tenants.read` | Search tenant registry |
| platform | `platform.tenants.suspend` | Suspend/reactivate tenant |
| platform | `platform.users.read` | Search global account registry without credential data |
| platform | `platform.roles.read` | Inspect privileged platform role assignments |
| platform | `platform.users.suspend` | Suspend/reactivate global account |
| platform | `platform.audit.read` | Read platform security audit |
| platform | `platform.roles.assign` | Grant/revoke platform roles with separate delegation guard |
| organization | `organization.profile.read` | Read organization profile |
| organization | `organization.profile.read_limited` | Read restricted org profile as guest |
| organization | `organization.settings.manage` | Update organization identity display settings |
| organization | `organization.members.read` | Read member directory |
| organization | `organization.members.invite` | Invite/revoke invitation |
| organization | `organization.members.manage` | Suspend/reactivate/offboard allowed targets |
| organization | `organization.access.manage` | Inspect/assign permitted scoped roles |
| organization | `organization.permissions.manage` | Inspect/grant/revoke direct scoped exceptions |
| organization | `organization.security_audit.read` | Read tenant identity audit (sensitive) |
| organization | `project.read_all` | Organization-level portfolio read hint; Project module checks explicitly |
| organization | `performance.cycles.manage` | HR review-cycle administration; Performance module guards confidentiality |
| project | `project.read` | Read one project according to its business rules |
| project | `project.update` | Update owned project |
| project | `task.read` | Read tasks within permitted project |
| project | `task.create` | Create tasks within permitted project |
| project | `task.update` | Update tasks within permitted project |
| project | `task.assign` | Assign work in permitted project |
| project | `project.release.trigger` | Proposed example sensitive direct grant with expiration |
| team | `team.read` | Read team within business rules |
| team | `team.members.manage` | Delegated team-scoped membership administration |
| performance | `performance.review.read_assigned` | Read review specifically assigned to reviewer |
| performance | `performance.review.submit` | Submit own assigned evaluation |

Mark `organization.permissions.manage`, `organization.security_audit.read`, `platform.roles.assign`, `platform.tenants.suspend`, `platform.users.suspend`, and `project.release.trigger` as `IsSensitive=true` when appropriate. Never use a wildcard code as an implicitly granted permission.

## Built-in role examples

| Role (all global seeded) | Scope kind | Initial permissions |
|---|---|---|
| `platform_admin` | platform | `platform.tenants.read`, `platform.tenants.suspend`, `platform.users.read`, `platform.users.suspend`, `platform.roles.read`, `platform.audit.read`, `platform.roles.assign` |
| `support_operator` | platform | `platform.tenants.read` only |
| `organization_owner` | organization | `organization.profile.read`, `organization.settings.manage`, `organization.members.read`, `organization.members.invite`, `organization.members.manage`, `organization.access.manage`, `organization.permissions.manage`, `organization.security_audit.read`, `project.read_all` |
| `organization_admin` | organization | `organization.profile.read`, `organization.settings.manage`, `organization.members.read`, `organization.members.invite`, `organization.members.manage`, `organization.access.manage`, `organization.permissions.manage` (subject to strict delegation policy) |
| `organization_member` | organization | `organization.profile.read` |
| `organization_guest` | organization | `organization.profile.read_limited` |
| `project_manager` | project | `project.read`, `project.update`, `task.read`, `task.create`, `task.update`, `task.assign` |
| `project_contributor` | project | `project.read`, `task.read`, `task.create`, `task.update` |
| `project_viewer` | project | `project.read`, `task.read` |
| `team_lead` | team | `team.read`, `team.members.manage` |
| `team_member` | team | `team.read` |
| `performance_reviewer` | performance | `performance.review.read_assigned`, `performance.review.submit` |
| `performance_hr_admin` | organization | `organization.profile.read`, `performance.cycles.manage` |

## Delegation matrix (business policy, not inferred from permission alone)

- Org Owner: may grant non-platform scopes within their tenant, including promoting a different active employee to Owner. Cannot grant platform roles or sensitive HR review access without explicit product rule.
- Org Admin: may grant approved member/project/team roles and direct grants **except** Owner, platform, sensitive performance and their own escalation; a separate guard checks the target role/permission code.
- Team Lead: may delegate allowed team roles within their own team only; may not modify organization or unrelated project scopes. Defer this delegation until owning Team module provides authoritative team linkage.
- Project Manager: may delegate approved contributor/viewer roles only within owned project if the Project module authorizes them; not universal member-manager authority.
- HR Admin: may assign only explicitly authorized performance reviewer access for the approved employee population. Actual review visibility remains inside Performance module.
- Guests: never grant, invite, administer roles, view tenant audit/member directory or receive sensitive performance grants.
- Platform Admin: platform operations only; tenant content requires audited explicit tenant authority, not automatic bypass.

**Scope-kind invariant:** Every role->permission, role assignment and direct grant must match `ScopeKind`. An organization-scoped administrative permission permits a carefully coded management *operation* on a project/team scope; it does not become an inherited project data permission.

## Seed and bootstrap delivery gate

Seed catalog data deterministically through a separate approved provisioning process. The current endpoint backlog does not contain seed endpoints. Privileged initial `platform_admin` must be seeded/granted securely out of band with an audit trail. Never add a public platform-admin registration route.
