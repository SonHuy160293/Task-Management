# Identity endpoint execution backlog

**Source of truth for progress:** `Status` in each task file. Update this index row when a card completes.

**Total:** 51 one-endpoint tasks; **Pending:** 50; **Completed:** 1; **P0:** 28; **P1:** 23.

Allowed statuses: `Pending` and `Completed` only.

| Task | Priority | Status | Endpoint | Depends on |
|---|---|---|---|---|
| [ID-001 - Register account](tasks/ID-001-register-account.md) | P0 | Completed | `POST /api/v1/auth/register` | Auth infrastructure, Argon2id, token generator; DB and IUnitOfWork ready. |
| [ID-002 - Verify email](tasks/ID-002-verify-email.md) | P0 | Pending | `POST /api/v1/auth/email-verifications/confirm` | ID-001 |
| [ID-003 - Resend verification email](tasks/ID-003-resend-verification-email.md) | P0 | Pending | `POST /api/v1/auth/email-verifications/resend` | ID-001, ID-002 |
| [ID-004 - Log in with password](tasks/ID-004-log-in-with-password.md) | P0 | Pending | `POST /api/v1/auth/login` | ID-002, auth JWT and cookie setup; real IUnitOfWork |
| [ID-005 - Rotate refresh token](tasks/ID-005-rotate-refresh-token.md) | P0 | Pending | `POST /api/v1/auth/refresh` | ID-004 |
| [ID-006 - Log out current session](tasks/ID-006-log-out-current-session.md) | P0 | Pending | `POST /api/v1/auth/logout` | ID-004, ID-005 |
| [ID-007 - Request password reset](tasks/ID-007-request-password-reset.md) | P0 | Pending | `POST /api/v1/auth/password-resets/request` | ID-001, mail integration |
| [ID-008 - Complete password reset](tasks/ID-008-complete-password-reset.md) | P0 | Pending | `POST /api/v1/auth/password-resets/confirm` | ID-004, ID-007 |
| [ID-009 - Change own password](tasks/ID-009-change-own-password.md) | P0 | Pending | `POST /api/v1/me/password/change` | ID-004, ID-006 |
| [ID-010 - Get own profile](tasks/ID-010-get-own-profile.md) | P0 | Pending | `GET /api/v1/me` | ID-004 |
| [ID-011 - Update own profile](tasks/ID-011-update-own-profile.md) | P1 | Pending | `PUT /api/v1/me` | ID-010 |
| [ID-012 - List my sessions](tasks/ID-012-list-my-sessions.md) | P1 | Pending | `GET /api/v1/me/sessions` | ID-004 |
| [ID-013 - Revoke one of my sessions](tasks/ID-013-revoke-one-of-my-sessions.md) | P1 | Pending | `DELETE /api/v1/me/sessions/{sessionId}` | ID-012, ID-006 |
| [ID-014 - Sign out other sessions](tasks/ID-014-sign-out-other-sessions.md) | P1 | Pending | `POST /api/v1/me/sessions/revoke-others` | ID-013 |
| [ID-015 - Create organization workspace](tasks/ID-015-create-organization-workspace.md) | P0 | Pending | `POST /api/v1/organizations` | ID-002, seed organization_owner; tenant DB/RLS bootstrap |
| [ID-016 - List my organizations](tasks/ID-016-list-my-organizations.md) | P0 | Pending | `GET /api/v1/me/organizations` | ID-004, ID-015; privileged but self-constrained membership discovery under RLS |
| [ID-017 - Get organization summary](tasks/ID-017-get-organization-summary.md) | P0 | Pending | `GET /api/v1/organizations/{tenantId}` | ID-015, tenant authorization checks |
| [ID-018 - Update organization display name](tasks/ID-018-update-organization-display-name.md) | P1 | Pending | `PUT /api/v1/organizations/{tenantId}` | ID-017, seeded organization.settings.manage |
| [ID-019 - List organization members](tasks/ID-019-list-organization-members.md) | P0 | Pending | `GET /api/v1/organizations/{tenantId}/members` | ID-015, RBAC permissions seed |
| [ID-020 - Get organization member](tasks/ID-020-get-organization-member.md) | P1 | Pending | `GET /api/v1/organizations/{tenantId}/members/{memberId}` | ID-019 |
| [ID-021 - Suspend organization member](tasks/ID-021-suspend-organization-member.md) | P0 | Pending | `POST /api/v1/organizations/{tenantId}/members/{memberId}/suspend` | ID-019, ID-034 for owner detection, effective-access cache strategy |
| [ID-022 - Reactivate suspended member](tasks/ID-022-reactivate-suspended-member.md) | P1 | Pending | `POST /api/v1/organizations/{tenantId}/members/{memberId}/reactivate` | ID-021 |
| [ID-023 - Remove organization member](tasks/ID-023-remove-organization-member.md) | P1 | Pending | `DELETE /api/v1/organizations/{tenantId}/members/{memberId}` | ID-021, ID-035, ID-038 |
| [ID-024 - Invite member](tasks/ID-024-invite-member.md) | P0 | Pending | `POST /api/v1/organizations/{tenantId}/invitations` | ID-015, ID-019, ID-030 and role catalog seed; mailer; scope registration by owner module |
| [ID-025 - List pending and past invitations](tasks/ID-025-list-pending-and-past-invitations.md) | P0 | Pending | `GET /api/v1/organizations/{tenantId}/invitations` | ID-024 |
| [ID-026 - Revoke invitation](tasks/ID-026-revoke-invitation.md) | P0 | Pending | `DELETE /api/v1/organizations/{tenantId}/invitations/{invitationId}` | ID-024 |
| [ID-027 - Preview invitation](tasks/ID-027-preview-invitation.md) | P1 | Pending | `POST /api/v1/invitations/preview` | ID-024 |
| [ID-028 - Accept invitation](tasks/ID-028-accept-invitation.md) | P0 | Pending | `POST /api/v1/invitations/accept` | ID-002, ID-024, ID-034; RLS bootstrap and guest policy |
| [ID-029 - Resend invitation](tasks/ID-029-resend-invitation.md) | P1 | Pending | `POST /api/v1/organizations/{tenantId}/invitations/{invitationId}/resend` | ID-024, ID-026 |
| [ID-030 - List assignable roles](tasks/ID-030-list-assignable-roles.md) | P0 | Pending | `GET /api/v1/access/roles` | Role catalog seed, organization membership access resolver |
| [ID-031 - Get role details and permissions](tasks/ID-031-get-role-details-and-permissions.md) | P1 | Pending | `GET /api/v1/access/roles/{roleId}` | ID-030 |
| [ID-032 - List permission catalog](tasks/ID-032-list-permission-catalog.md) | P0 | Pending | `GET /api/v1/access/permissions` | ID-030; permission seed |
| [ID-033 - List member role assignments](tasks/ID-033-list-member-role-assignments.md) | P0 | Pending | `GET /api/v1/organizations/{tenantId}/members/{memberId}/roles` | ID-019, ID-030 |
| [ID-034 - Assign role to member at scope](tasks/ID-034-assign-role-to-member-at-scope.md) | P0 | Pending | `POST /api/v1/organizations/{tenantId}/members/{memberId}/roles` | ID-030, ID-033 and access scope lifecycle from owning module |
| [ID-035 - Revoke member role assignment](tasks/ID-035-revoke-member-role-assignment.md) | P0 | Pending | `DELETE /api/v1/organizations/{tenantId}/members/{memberId}/roles/{assignmentId}` | ID-034 |
| [ID-036 - List extra member permission grants](tasks/ID-036-list-extra-member-permission-grants.md) | P0 | Pending | `GET /api/v1/organizations/{tenantId}/members/{memberId}/permissions` | ID-019, ID-032 |
| [ID-037 - Grant extra permission to member](tasks/ID-037-grant-extra-permission-to-member.md) | P0 | Pending | `POST /api/v1/organizations/{tenantId}/members/{memberId}/permissions` | ID-032, ID-036, scoped authorization policy and correct catalog IsSensitive |
| [ID-038 - Revoke direct user permission grant](tasks/ID-038-revoke-direct-user-permission-grant.md) | P0 | Pending | `DELETE /api/v1/organizations/{tenantId}/members/{memberId}/permissions/{grantId}` | ID-037 |
| [ID-039 - Get my effective permissions at scope](tasks/ID-039-get-my-effective-permissions-at-scope.md) | P0 | Pending | `GET /api/v1/organizations/{tenantId}/me/effective-permissions` | ID-034, ID-037, complete auth policy and RLS-safe read |
| [ID-040 - Search tenant security audit events](tasks/ID-040-search-tenant-security-audit-events.md) | P1 | Pending | `GET /api/v1/organizations/{tenantId}/security-events` | ID-034 and audit writer installed; RLS read strategy |
| [ID-041 - List organizations for platform support](tasks/ID-041-list-organizations-for-platform-support.md) | P1 | Pending | `GET /api/v1/platform/organizations` | ID-004, platform role seeding and bootstrap |
| [ID-042 - Suspend tenant organization](tasks/ID-042-suspend-tenant-organization.md) | P1 | Pending | `POST /api/v1/platform/organizations/{tenantId}/suspend` | ID-041, tenant status access enforcement |
| [ID-043 - Reactivate tenant organization](tasks/ID-043-reactivate-tenant-organization.md) | P1 | Pending | `POST /api/v1/platform/organizations/{tenantId}/reactivate` | ID-042 |
| [ID-044 - Suspend global user](tasks/ID-044-suspend-global-user.md) | P1 | Pending | `POST /api/v1/platform/users/{userId}/suspend` | ID-004, ID-006, ID-047 (or secured preseeded platform administrators) |
| [ID-045 - Reactivate global user](tasks/ID-045-reactivate-global-user.md) | P1 | Pending | `POST /api/v1/platform/users/{userId}/reactivate` | ID-044 |
| [ID-046 - Search platform security events](tasks/ID-046-search-platform-security-events.md) | P1 | Pending | `GET /api/v1/platform/security-events` | ID-041, platform audit writer |
| [ID-047 - Grant platform role](tasks/ID-047-grant-platform-role.md) | P1 | Pending | `POST /api/v1/platform/users/{userId}/roles` | Secure platform admin bootstrap + role/permission catalog, ID-041 |
| [ID-048 - Revoke platform role](tasks/ID-048-revoke-platform-role.md) | P1 | Pending | `DELETE /api/v1/platform/users/{userId}/roles/{assignmentId}` | ID-047 |
| [ID-049 - Search global user accounts](tasks/ID-049-search-global-user-accounts.md) | P1 | Pending | `GET /api/v1/platform/users` | ID-041 and platform.users.read role seeding |
| [ID-050 - List user platform role assignments](tasks/ID-050-list-user-platform-role-assignments.md) | P1 | Pending | `GET /api/v1/platform/users/{userId}/roles` | ID-049; seeded platform.roles.read or platform.roles.assign |
| [ID-051 - Change member classification](tasks/ID-051-change-member-classification.md) | P1 | Pending | `PUT /api/v1/organizations/{tenantId}/members/{memberId}/membership-type` | ID-019, ID-034, ID-035, ID-037, ID-038; catalog baseline roles |
