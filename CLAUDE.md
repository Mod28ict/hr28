# HR28 – Project Instructions for Claude Code

HR28 is a **private** voter feedback, relationship and campaign operations platform
(premium CRM-style). It is NOT a government system: never use ministry names, state
emblems, official titles or government branding.

Full context: `Docs/HR28_Voter_Management_System_Current_State_and_Enhancement_Roadmap.docx`
(current state, gaps, roadmap, backlog).

## Solution structure

Solution file: `HR28.slnx` (.NET 10). Clean-architecture layering:

- `HR28.Domain` – entities (`Entities/`), enums, shared base types (`Common/`). No dependencies.
- `HR28.Application` – DTOs and service interfaces (`DTOs/`, `Interfaces/`). References Domain.
- `HR28.Infrastructure` – EF Core `HR28DbContext` (`Data/`), migrations (`Migrations/`),
  service implementations (`Services/`), helpers. References Application and Domain.
- `HR28.API` – ASP.NET Core Web API: controllers, JWT auth, DI wiring in `Program.cs`.
  The EF startup project. References Application and Infrastructure.
- `HR28.Web` – ASP.NET Core MVC/Razor UI. No project references: it calls the API over
  HTTP with a JWT stored in session (`Services/`, `Filters/`, `Models/` view models).
- Data: Entity Framework Core + SQL Server, schema managed with migrations.
- Tests: **no test project exists yet.** Create one (e.g. `HR28.Tests`, xUnit) as part of
  Phase 1 item 6 and add it to `HR28.slnx`; until then `dotnet test` runs nothing.

## Commands

```
dotnet build HR28.slnx
dotnet test HR28.slnx
dotnet ef migrations add <Name> --project HR28.Infrastructure --startup-project HR28.API
dotnet ef database update --project HR28.Infrastructure --startup-project HR28.API
dotnet ef migrations remove --project HR28.Infrastructure --startup-project HR28.API
```

## Domain rules (must always hold)

- **A constituency contains many islands; each island belongs to exactly one
  constituency.** An island must never be linked to more than one constituency.
  (This replaces the earlier many-to-many `ConstituencyIsland` assumption; see the
  migration task in the backlog.)
- Not every constituency requires an island selection. Support constituency-only voters.
- If a voter has an island, it must belong to the voter's constituency.
- Roles grant capabilities; scopes decide which records those capabilities apply to.
  Role assignment and scope assignment are separate.
- Non-administrators cannot edit protected voter fields or access records outside
  their effective scope.
- National ID (and other chosen keys) must be duplicate-checked with friendly messages.
- Influencer–voter links must prevent accidental duplicates but allow legitimate updates.
- Every create, update and delete on critical records is audited: actor, timestamp,
  entity, action and what changed.

## Security rules

- **The API is the security boundary.** Every protected endpoint enforces role
  policies AND scope checks on the server. Hiding a menu or button is never enough.
- MVC must also check permissions, and handle 401/403 from the API by clearing the
  session and redirecting to login or an access-denied page – never a raw error page.
- OTPs: expiring, single-use (atomic consume), with attempt and resend limits.
- Never log or display OTPs, tokens, connection strings or keys outside the
  Development environment.
- Secrets live in User Secrets / environment variables, never in committed files.

## Data and privacy

- Development uses a separate dev database with **synthetic data only**. Never connect
  to or query the production voter registry.
- In Development, SMS goes to a fake sender that logs the OTP. Never call the real
  SMS provider from tests or local runs.
- Collect only approved fields. Do not add fields for ID card copies, photos, health,
  financial, family-sensitive data, precise location or free-text allegations.
- Out of scope unless explicitly requested: public self-service portal, automated
  persuasion or behavioral targeting, native mobile apps, offline sync, GIS tracking,
  biometrics, government-database integrations.

## UX rules

- Never show GUIDs, stack traces, raw API errors or technical identifiers to users.
- Use searchable, cascading selectors (constituency → island) showing readable names.
- Match the HR28 premium design (login, OTP and dashboard are the reference quality).
- Responsive, keyboard accessible, proper labels and focus states.
- Consistent loading, empty, success, warning and error states.

## Working style

- Work in small steps. Explain the plan before structural changes.
- Do not change the database schema without stating the migration and its rollback.
- Large voter datasets: server-side paging and filtering; never load the full registry
  into memory.
- After each change: build, run tests, and summarise what changed and how to verify it.
- Commit after each verified change with a clear message. Do not commit secrets.

## Definition of done

- Builds without correctness or security warnings.
- API and MVC both enforce the rule.
- Positive, negative and cross-scope tests pass.
- Validation and error messages are user-friendly.
- Audit events recorded where required.
- No raw identifiers, stack traces or secrets shown to users.
- UI matches the HR28 design system and works responsively.
- Database impact reviewed; migration has a rollback plan; data reconciled.
- Relevant documentation updated.

## Current focus: Phase 1 – Stabilize

1. Island model correction: one constituency → many islands, island → one constituency.
2. Permission matrix and API authorization policies.
3. Central session / 401 handling in the MVC app.
4. OTP single-use, attempt limits, resend cooldown and lockout.
5. Audit coverage checklist.
6. Regression tests: login, user provisioning, voter CRUD, scope isolation, pledges,
   encounters, influencer links.
