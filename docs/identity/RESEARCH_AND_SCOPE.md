# Identity user research, stories, functionality and delivery scope

## 1. Product users and why they interact with Identity

| Persona | Identity user need | Direct actions | Indirect controls |
|---|---|---|---|
| Employee / developer / QA / BA | Secure account, own profile, switch between organizations | Register, verify, login, update profile, recover account, list/revoke sessions, list memberships | Scoped project/team access, self authorization |
| Organization owner | Start company workspace, maintain accountability and ownership | Create organization, invite, assign/withdraw roles, offboard | Owner survival rule, tenant isolation, audit |
| Organization administrator / operations | Onboard workers and contractors, manage access | Invite/resend/revoke, list members, suspend/reactivate/remove, inspect role assignments | Only delegated grants, cannot silently promote self |
| Project manager / team lead | Access correct projects, assign selected contributors | In later Project/Team modules, delegate permitted scoped roles/grants | Project/team scope and owning-module validation |
| HR / performance reviewer | Access only assigned confidential reviews | Identity-granted performance-scoped access | Performance module enforces review state and relationship checks |
| External client / contractor | See permitted project without seeing company-wide information | Accept invitation, login, account recovery | Guest hard restrictions and limited view roles |
| Company executive | See permitted portfolio and organization views | Login, organization selection | Org-level `project.read_all` is a separate permission; no automatic inheritance |
| Platform security/support | Govern tenant availability and investigate security incidents | Suspend/reactivate tenants/users; platform audit and scoped admin roles | No automatic access to tenant business data |
| AI agent / integration | Eventually act with constrained machine identity | **Not in current ERD** | Requires separate service principal/API key tables; explicitly deferred |

A person's job title is **not** necessarily an authorization role. A Project Manager in project A may only be a viewer in project B. One human can have multiple organization memberships with different roles.

## 2. Research-backed product and security observations

1. **B2B SaaS membership:** Linear distinguishes owners, admins, team owners, members and restricted guests. External collaborators receive narrow visibility rather than unrestricted workspace-wide access. Source: [Linear members and roles](https://linear.app/docs/members-roles).
2. **Authorization by resource:** Microsoft explains that `[Authorize]` evaluated before resource loading is insufficient for business object authorization; the owning module must check the loaded resource and current actor. Source: [ASP.NET Core resource-based authorization](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/resourcebased?view=aspnetcore-10.0).
3. **Least privilege:** OWASP recommends deny by default, permission checks on every request, and explicit authorization testing; never trust a client-controlled tenant ID. Source: [OWASP Authorization Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Authorization_Cheat_Sheet.html).
4. **Password policy:** The finalized NIST SP 800-63B-4 recommends a minimum of 15 characters for single-factor passwords, accepting at least 64 characters, rejecting common/compromised choices and avoiding mandatory special-character composition/periodic changes. Source: [NIST SP 800-63B-4](https://pages.nist.gov/800-63-4/sp800-63b.html).
5. **Account recovery:** OWASP recommends generic responses for known and unknown accounts, short-lived random single-use reset tokens, and anti-enumeration/rate limiting. Source: [OWASP Forgot Password Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Forgot_Password_Cheat_Sheet.html).
6. **Session lifecycle:** OWASP recommends server-side expiry, visible logout, revocation, and users' ability to inspect/terminate active sessions. Source: [OWASP Session Management Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Session_Management_Cheat_Sheet.html).
7. **Refresh replay protection:** RFC 9700 requires sender-constrained or rotated refresh tokens for OAuth public clients and describes replay detection; this plan uses one-time rotating opaque tokens for its own session design. Source: [RFC 9700](https://www.rfc-editor.org/rfc/rfc9700.html).
8. **CodeGraph for Codex:** CodeGraph supports Codex CLI through its integration; standard MCP surface exposes `codegraph_explore` to inspect real symbols and the blast radius. Source: [CodeGraph MCP server](https://colbymchenry.github.io/codegraph/reference/mcp-server/) and [Integrations](https://colbymchenry.github.io/codegraph/reference/integrations/).

These sources inform recommended design; they do **not** establish whether DevFlow has working mail, JWT, tenant RLS, policy enforcement or persistence registration today.

## 3. User story groups mapped to endpoint tasks

| Story group | Actor / business outcome | Tasks | Core acceptance journey |
|---|---|---|---|
| US-001..US-009 | New/existing person authenticates and recovers account | ID-001..ID-009 | Register -> email verified -> login -> rotate -> recover -> old token/session invalid |
| US-010..US-014 | User controls personal data and devices | ID-010..ID-014 | Read/update profile -> list and selectively revoke sessions |
| US-015..US-018 | Workspace owner creates and finds tenants | ID-015..ID-018 | User creates organization + organization scope + first owner atomically -> selects tenant |
| US-019..US-023, US-051 | Admin controls employee/contractor membership | ID-019..ID-023, ID-051 | Enumerate members -> suspend/reactivate/remove without global-account deletion |
| US-024..US-029 | Invite recipient and admin manage access invitation | ID-024..ID-029 | Invite with role grants -> verify recipient -> single-use acceptance -> only delegated scope access |
| US-030..US-039 | Authorized personnel assign and inspect scoped access | ID-030..ID-039 | Catalog -> role grant/revoke -> extra permission grant/revoke -> effective scope lookup |
| US-040..US-050 | Security teams govern tenant/platform lifecycle | ID-040..ID-050 | Audit tenant actions, suspend/reactivate tenant/user and carefully administer platform roles |

## 4. Selected core user journeys

**Journey A - first workspace:** A new founder registers and verifies email; logs in; creates organization. System creates the tenant registry record, an organization scope, their active membership, and their `organization_owner` assignment in one transaction. The new owner may invite others. Registration by itself never grants platform privileges.

**Journey B - external client:** Owner invites a verified client's email as Guest with a scoped viewer role. Client sees masked invitation preview, logs in/creates account and verifies email, then accepts invite. Access is restricted to the approved scope. Guest denies take priority over potentially corrupt grants.

**Journey C - temporary exception:** Developer holds `project_contributor` in Project A; team lead/authorized admin grants `project.release.trigger` directly at Project A with reason and expiry. Authorization at Project A succeeds until expired or revoked; Project B remains denied.

**Journey D - offboarding:** Organization admin suspends or removes member. Global user persists and still belongs to other tenants. Suspended membership blocks API data immediately; removal revokes tenant grants and preserves audit history. Last active organization owner cannot be removed.

**Journey E - compromised session:** User logs in on two devices, revokes one session, and confirms token rotation on that device fails; the other session is unaffected. A suspicious refresh-token replay revokes the affected session family.

## 5. P0 scope and P1 scope

**P0:** Registration, email verification, session sign-in/refresh/logout, password recovery/change, self profile read, workspace creation/lookup, member listing/suspension, invite/accept/revoke, role catalog/read/assign/revoke, direct permissions read/assign/revoke, effective-permission lookup.

**P1:** Profile edit, device inventory and selective remote revocation, organizational renaming, member inspection/reactivation/removal, invitation preview/resend, detailed catalogs, security audit search and platform governance. Some P1 tasks such as removal are essential before production employee offboarding; choose deployment priorities accordingly.

## 6. Deliberately deferred: require new tables/security decisions

- SAML/OIDC SSO / social sign-in -> `shared.ExternalIdentities` / IdP mappings plus provider validation.
- MFA/TOTP/WebAuthn/passkeys -> authenticator credentials, challenges, recovery codes and step-up rules.
- SCIM employee provisioning -> external provisioning identifiers, conflict/ownership rules.
- Custom tenant-defined roles -> tenant custom role tables; cannot reuse single FK to `shared.Roles` naively.
- Machine identities, API keys, AI agents -> dedicated service account / key / scoped assignment tables, not a human login.
- Email address change, account merging/deletion/GDPR erasure -> verification of both addresses, history and retention rules not modeled in current ERD.
- Feature subscriptions, billing, cross-company mergers -> outside Identity bounded context.
- Project / team / performance business CRUD -> owning modules; Identity can only hold logical access scope anchors and permission assignments.

## 7. ERD and skill-derived limitations requiring explicit treatment

- Exactly 18 source tables: 11 `shared`, 7 `dbo`. All PK UUIDv7; `shared.Users` is global, `dbo.OrganizationMembers` is one row per (TenantId,UserId).
- Existing source DDL has **no outbox**; reliable email delivery is an integration prerequisite, not a schema capability you can assume.
- `dbo.AccessScopes` has a logical ResourceId, not a foreign key to other modules; organization scope resource = TenantId. Project/team/performance scopes must be registered/archived by owning modules before related assignments.
- Direct permission grants are ALLOW-only, always scope-bound. No explicit DENY or automatic org->project inheritance.
- No role management endpoints: seeded built-in roles/permissions are platform-owned. No custom role editor in this backlog.
- The uploaded skill describes a DevFlow Tasks starter and custom CQRS; Codex must inspect the actual **Identity** module instead of writing to DevFlow.Tasks.
- The SQL reference names columns snake_case; previous EF plan specifies PascalCase physical table/column identifiers. Migrations must use a consistent version of that decision.
- All DBO tables currently have `FORCE ROW LEVEL SECURITY` planned. Because the skill's TransactionPipelineBehavior opens transactions for **commands only**, ordinary tenant queries cannot rely on `SET LOCAL app.tenant_id` unless an explicit RLS-compatible read strategy is present. This is a **hard delivery gate**.
