# HR28 – Project Instructions for Claude Code

HR28 is a **private** voter feedback, relationship and campaign operations platform
(premium CRM-style). It is NOT a government system: never use ministry names, state
emblems, official titles or government branding.

The client requires a **top-security** portal for users who are **not technical**.
Every security control must be strong AND simple to use.

Full context: `Docs/HR28_Voter_Management_System_Current_State_and_Enhancement_Roadmap.docx`
(current state, gaps, roadmap, backlog).

## Solution structure

Solution file: `HR28.slnx` (.NET 10). Clean-architecture layering:

- `HR28.Domain` – entities (`Entities/`), enums, shared base types (`Common/`). No dependencies.
- `HR28.Application` – DTOs, service interfaces and shared exceptions (`DTOs/`,
  `Interfaces/`, `Common/`). References Domain.
- `HR28.Infrastructure` – EF Core `HR28DbContext` (`Data/`), migrations (`Migrations/`),
  service implementations (`Services/`), helpers. References Application and Domain.
- `HR28.API` – ASP.NET Core Web API: controllers, JWT auth, authorization policies
  (`Extensions/`), error middleware (`Middleware/`), DI wiring in `Program.cs`.
  The EF startup project. References Application and Infrastructure.
- `HR28.Web` – ASP.NET Core MVC/Razor UI. No project references: it calls the API over
  HTTP with a JWT stored in session (`Services/ApiClient`, `Controllers/AppController`,
  `Filters/`, `Models/` view models).
- Data: Entity Framework Core + SQL Server, schema managed with migrations.
- Scope filtering: `AccessScopeService` + `ScopeQueryExtensions.InScope(...)` in
  Infrastructure; use these for every query on voters, influencers, encounters and pledges.
- Tests: **no test project exists yet.** Create one (e.g. `HR28.Tests`, xUnit) as part of
  Phase 1 regression tests and add it to `HR28.slnx`; until then `dotnet test` runs nothing.

## Commands

```
dotnet build HR28.slnx
dotnet test HR28.slnx
dotnet list HR28.slnx package --vulnerable --include-transitive
dotnet ef migrations add <Name> --project HR28.Infrastructure --startup-project HR28.API
dotnet ef database update --project HR28.Infrastructure --startup-project HR28.API
dotnet ef migrations remove --project HR28.Infrastructure --startup-project HR28.API
```

## Deployment model

- HR28 is sold to multiple clients. **Each client gets a separate deployment**:
  its own web app, API and database, all running the **same codebase and version**.
  Never add client-specific code branches; differences come from configuration only.
- There is no shared multi-tenant database. Do not add tenant IDs to tables.
- Roles in each deployment: **Owner** (platform owner, not managed by the client),
  **Client Administrator** (manages that client's users, roles and scopes), and the
  client's other users with their roles and scopes.
- Planned hosting: Azure App Service + Azure SQL Database, one deployment per client,
  published by one automated pipeline to all clients.

## Module switches

- Paid modules (Election-day operations, Bulk SMS, Case management, Field teams,
  Events, Advanced analytics, Security pack) are enabled per client through
  **configuration only**, e.g. `Modules__ElectionDay=true` in Azure app settings.
- Only the Owner controls these (via Azure). A Client Administrator must never be able
  to enable a module, even with full admin rights inside HR28.
- When a module is off: hide its menus and views in MVC AND return "not available"
  from its API endpoints. Checking only the UI is not enough.
- Each module lives in its own area/folder (e.g. `Areas/ElectionDay`) and reuses the
  existing voters, users, roles, scopes and audit logging.

## Domain rules (must always hold)

- **A constituency contains many islands; each island belongs to exactly one
  constituency.** An island must never be linked to more than one constituency.
  (This replaces the earlier many-to-many `ConstituencyIsland` assumption; see the
  migration task in the backlog.)
- Not every constituency requires an island selection. Support constituency-only voters.
- If a voter has an island, it must belong to the voter's constituency.
- Roles grant capabilities; scopes decide which records those capabilities apply to.
  Role assignment and scope assignment are separate.
- **A user can have multiple roles AND multiple scopes.** (The current code supports
  only a single scope per user; this must change.) Roles and scopes are independent:
  every role a user has applies in every scope they have.
  - Effective permissions = the union of all the user's roles.
  - Effective access = the union of all the user's scopes. A record is visible if it
    falls inside ANY of the user's scopes (a whole constituency, or a specific island).
  - Store assignments in separate tables (`UserRoles`, `UserScopes`), not single
    columns on the user.
  - Every scope check filters by the user's full scope list, never a single scope ID.
  - Changes to roles or scopes take effect immediately: resolve them on the server
    (cached, with invalidation on change), not from a list frozen in the login token.
  - Assigning or removing a role or scope is audited, and assignment screens must be
    simple for non-technical administrators.
- Non-administrators cannot edit protected voter fields or access records outside
  their effective scope.
- National ID (and other chosen keys) must be duplicate-checked with friendly messages.
- Influencer–voter links must prevent accidental duplicates but allow legitimate updates.
- Every create, update and delete on critical records is audited: actor, timestamp,
  entity, action and what changed.
- Election-day turnout records only **whether** someone voted, never **how** they voted.

## Security standard

- Target: **OWASP ASVS Level 2**. When reviewing or writing code, check against it.
- **The API is the security boundary.** Every protected endpoint enforces role
  policies AND scope checks on the server. Hiding a menu or button is never enough.
- MVC must also check permissions, and handle 401/403 from the API by clearing the
  session and redirecting to login or an access-denied page – never a raw error page.

### Authentication (SMS codes, designed for non-technical users)

Users sign in with SMS auth codes instead of authenticator apps. Build it so it is
strong despite that:

- Login must be **two factors**: something the user knows (password or PIN) plus the
  SMS code. If login currently uses only phone/ID + code, flag it to the owner and
  confirm before changing the login flow.
- SMS codes: 6 digits, cryptographically random, ~5 minute expiry, **single use**
  (atomic consume), bound to that specific login attempt.
- Limit wrong attempts, then temporary lockout; cooldown between resends; rate limit
  per user, per phone number and per IP.
- SMS text includes a warning, e.g. "HR28 code: 482913. Never share this code.
  HR28 staff will never ask for it."
- Use generic messages that don't reveal whether a user or phone number exists.
- **Phone number changes** only by an administrator, always audited, and the user is
  notified. (Whoever controls the number controls the account.)
- **Trusted devices**: after a successful login a device may be remembered for a
  limited period so users enter codes less often. New devices always need a code.
  Users and admins can revoke trusted devices.
- **Step-up verification**: require a fresh code before sensitive actions – data
  exports, role/scope changes, user management, phone number changes – even within
  a logged-in session.
- **Login alerts**: SMS the user when someone signs in from a new device.
- **Sessions**: idle timeout, absolute timeout, real server-side logout, and admins
  can end any user's sessions.
- **Stronger sign-in for high-risk accounts**: Owner and Client Administrators must
  use passkeys (fingerprint/face unlock via WebAuthn) or another phishing-resistant
  method. Passkeys are optional for everyone else. Check the passkey support in the
  ASP.NET Core Identity version in use before implementing.
- Never log or display codes, tokens, passwords, connection strings or keys outside
  the Development environment.

### Web and API protection

- HTTPS only with HSTS; TLS 1.2+.
- Security headers: Content-Security-Policy, X-Content-Type-Options: nosniff,
  frame protection (frame-ancestors / X-Frame-Options), Referrer-Policy.
- Cookies: Secure, HttpOnly, SameSite.
- Anti-forgery tokens on every form and state-changing request in MVC.
- Validate every input on the server; use EF Core / parameterized queries only.
  No string-built SQL.
- Encode all output in Razor; never render user input as raw HTML.
- Rate limiting on login, code verification, search and export endpoints so nobody
  can bulk-download the voter registry.
- No stack traces, internal errors or technical identifiers shown to users.
- Least-privilege database account for the app (no db_owner in production).

### Secrets and infrastructure

- Secrets live in User Secrets (development) and Azure Key Vault via managed identity
  (production). Never in committed files.
- Azure SQL: firewall restricted to the app; no public database access.
- Keep NuGet packages free of known vulnerabilities.

## Data and privacy

- Development uses a separate dev database with **synthetic data only**. Never connect
  to or query the production voter registry.
- In Development, SMS goes to a fake sender that logs the code. Never call the real
  SMS provider from tests or local runs.
- Collect only approved fields. Do not add fields for ID card copies, photos, health,
  financial, family-sensitive data, precise location or free-text allegations.
- Data exports are restricted by role and scope, require step-up verification, and
  are audited.
- Out of scope unless explicitly requested: public self-service portal, automated
  persuasion or behavioral targeting, offline sync, GIS tracking, biometrics stored
  by HR28, government-database integrations.

## UX rules

- Users are not technical: keep every screen simple, with plain-language messages and
  clear next steps, especially in login, code entry and error screens.
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

- Builds without correctness or security warnings; no vulnerable packages.
- API and MVC both enforce the rule.
- Positive, negative and cross-scope tests pass.
- Validation and error messages are user-friendly.
- Audit events recorded where required.
- No raw identifiers, stack traces or secrets shown to users.
- UI matches the HR28 design system and works responsively.
- Database impact reviewed; migration has a rollback plan; data reconciled.
- Relevant documentation updated (including security decisions).

## Current focus: Phase 1 – Stabilize

1. Island model correction: one constituency → many islands, island → one constituency.
2. Multiple roles and multiple scopes per user (data model, migration of existing
   single-scope assignments, assignment screens, scope filtering everywhere).
3. Permission matrix and API authorization policies built on multiple roles/scopes.
4. Central session / 401 handling in the MVC app.
5. Authentication hardening per the "Authentication" section above.
6. Security review of the existing code against OWASP ASVS Level 2, and fixes.
7. Audit coverage checklist.
8. Regression tests: login, user provisioning, voter CRUD, scope isolation, pledges,
   encounters, influencer links, and users with multiple roles/scopes.

Next, after Phase 1: module switch system → deployment pipeline → paid modules.
