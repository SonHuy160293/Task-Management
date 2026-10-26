# Codex prompt - implement exactly one Identity endpoint

Copy the text below into Codex after replacing `ID-###` with a real Pending task ID and the full path to its task file from `docs/identity/BACKLOG.md`.

---

You are implementing **exactly one** DevFlow Identity API endpoint.

**Selected task:** `docs/identity/tasks/ID-###-replace-with-actual-file.md`

Execution instructions:

1. Read root/module `AGENTS.md`, `ARCHITECTURE.md`, `.editorconfig`, and the **installed user skill `add-cqrs-api-feature`**. Obey source repository conventions over examples. This is the Identity module; the skill's DevFlow.Tasks starter is not the target.
2. Read `docs/identity/EXECUTION_GUIDE.md`, `API_CONVENTIONS.md`, `PERMISSION_CATALOG.md`, relevant parts of `reference/identity_erd_postgresql18.md`, `reference/identity_postgresql18.sql`, `reference/Identity_EF_Core_Models_Codex_Implementation_Plan.md`, and the **one** selected card. Do not load all other task files.
3. Inspect the actual implementation with CodeGraph **before editing**. Use `codegraph_explore` (if configured) to locate Identity controller bases, `ISender`, custom CQRS message/validator/handler interfaces, `IUnitOfWork`, EF repository and DbContext, authentication, tenant scope resolution, permissions, audit and existing tests. Inspect symbol callers/blast radius. If CodeGraph is unavailable, say so rather than claiming usage.
4. Apply `add-cqrs-api-feature`: Controller -> Command/Query -> Validator -> Handler -> Application persistence/integration interface -> Infrastructure implementation -> DI registration -> response mapping. Keep Domain independent. Reuse real module patterns. NO MediatR and no new mapping library. Handler/repository MUST NOT call SaveChangesAsync; use the existing real UnitOfWork/transaction behavior.
5. Implement **only** the selected method and route. Do not create any other endpoints, migrations, features, scripts, or unrelated refactors. The selected card's dependencies are preconditions; if missing, stop and report without inventing their endpoints.
6. Match exact request DTO, query/path parameters, response DTO and status codes, validation, required authorization, business transitions, transaction/concurrency and audit behavior on the card. UUIDv7 PK; `shared` global, `dbo` tenant-scoped; PascalCase physical identifiers per the prior model plan.
7. Do not weaken RLS, guest restrictions, security audit, last-owner invariant, credential hashing, token rotation, delegation rules or cross-tenant protection to make an endpoint pass. If a precondition is missing (DB, RLS reads, roles seeded, mail, JWT, module auth), report it and leave the card Pending.
8. Run only the **targeted verification required by the skill and repo instructions** for the selected endpoint; focused tests should cover success, invalid input, unauthorized/cross-tenant access, conflict/replay/concurrency where relevant. Do not claim checks ran when they did not.
9. Report changed files, CodeGraph results used, endpoint contract, access and persistence assumptions, tests/checks run and blockers. Update the selected task's YAML + visible status and its `BACKLOG.md` row to **Completed** only if every acceptance item really passed. All other task statuses stay untouched.
10. Stop. Do not start the next task automatically.

---
