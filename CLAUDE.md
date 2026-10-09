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
  Infrastructure; use these for every query on voters, encounters and pledges.
  **Influencers are global** (owner decision, 2026-10-03): everyone sees every
  influencer; edit/delete still need the granted rights. An influencer's linked
  voters (Influencers → "N linked voters", API `GET api/influencers/{id}/voters`) are
  listed only inside the user's areas; links outside them are counted, never shown.
  The Influencers page is a server-paged table (API `GET api/influencers/search`:
  name/ID/phone/island search, constituency, island, category). Influencer
  **categories** (MP, Island Council, GM Member, …) are a managed list in
  `InfluencerCategories` (Settings → Lists, administrators; API
  `api/InfluencerCategories`, audited; a used category can't be deleted) —
  migration `AddInfluencerCategories`; rollback:
  `dotnet ef database update AddPermissions`.
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
- **Each client sees only their own branding** (owner decision, 2026-10-08; "HR28" /
  "Hithaai Roohun 2028" is the first client's campaign slogan, not the product name).
  Never type a client's name, slogan or logo into the code. Settings → System →
  Branding (Administrator): campaign name, short name (≤ 8 letters/digits; empty =
  initials of the campaign name, "Hithaai Roohun 2028" → "HR28"), tagline, and a logo
  (`BrandLogos`, PNG/JPG ≤ 1 MB, cleaned by `PhotoSanitizer`). Public API
  `GET api/settings/branding` and `api/settings/logo` (the sign-in pages show them);
  web `BrandingService` (cached 1 min) and `Brand/Logo`. Used by the sign-in and code
  pages, sidebar, page titles, loader, Dashboard, the sign-in SMS, the phone-change SMS,
  and report CSV titles / file names. A new deployment starts neutral ("Campaign
  Intelligence" / "CI"). Internal names (code namespaces, cookie names, JWT issuer,
  Swagger) are not shown to users and stay. The footer keeps "Designed and Developed by:
  Ahmed Rasheed" for every client (owner decision, 2026-10-08: the developer's credit,
  not a client brand). Migration `AddBrandLogo`; rollback
  `dotnet ef database update AddUserSessionsEndedAt`.
- **Owner and Administrator are unrelated** (owner decision, 2026-10-02):
  - **Owner** = the platform owner. Works in **Azure only** (module switches, settings,
    deployments). There is **no Owner role or account inside the app**, so no screen
    or API can ever enable a paid module.
  - **Administrator** = the client's own top role inside HR28: manages users, roles,
    areas, system settings, constituencies and islands. Stored as
    `"Super Administrator"` (kept for existing data); always **shown** as
    "Administrator" (`Hr28Roles.DisplayName`).
  - Other roles: National Administrator (all data, no user management),
    Constituency Administrator, Island Administrator, Collector, Reporter (read-only
    reports). "Sees everything" = Super Administrator or National Administrator.
  - Records (voters, encounters, pledges, influencers) are protected by **named
    rights** per action (owner decision 2026-10-05, replaces the old `RecordWriter`
    rule): every endpoint has `[RequirePermission(PermissionCatalog.X)]`
    (API `Extensions/PermissionAuthorization.cs`), and screens show only what the
    person's rights allow (`Hr28Permissions.Has`). Administrators can create **custom
    roles** (Settings → Roles & rights); built-in roles can't be renamed or deleted,
    a role in use can't be deleted. Built-in defaults (migration `AddRoleRights`, same
    access as before): admin-type roles and Collector = view + add on everything,
    edit voters, edit pledges, link influencers (National Administrator also deletes
    voters); Reporter = view encounters and pledges only.
  - **Default voter-profile view per role** (`Roles.VoterProfileView`): "Full" or
    "AddEncounter" (opening a voter goes straight to Add encounter with a voter
    summary; if the person may also add pledges or link influencers, they get a
    "What would you like to add?" page of cards instead — `VoterQuickActions`).
    After saving they return to the same filtered Voters list. Several roles: Full wins. Profile sections still need their view right;
    the API returns them empty without it.
  - **Start page per role** (owner decision, 2026-10-06): `Roles.StartPage` "Dashboard"
    or "QuickEntry" (Settings → Roles & rights, "When someone with this role signs
    in"). QuickEntry hides the Dashboard link and sends sign-in to **Quick entry**
    (`QuickEntryController`): fetch a voter by ID card (API
    `GET api/voters/by-national-id/{nid}`, Voters.View, inside the user's areas only,
    rate-limited like search; outside = "not found"), shows photo (with
    `Voters.Photo.View`) and details, and keeps the Add encounter / Add pledge / Link
    influencer forms open (each only with its right); every successful save reloads
    the whole page. Several roles: Dashboard wins; the Administrator always gets the
    Dashboard. Migration `AddRoleStartPage`; rollback
    `dotnet ef database update AddVoterDateOfBirth`.
  - **Voter list upload** (Voters → Upload voter list, API `POST api/VoterImports`) is
    for administrators only (`Administrator` policy: Super + National), max 20 MB
    .xlsx/.xls, 10 uploads per hour per user, one audit entry per upload with counts.
  - **Party membership list upload** (Voters → Upload membership list, API
    `POST api/MembershipImports?partyId=`): same rules. Columns found by heading
    (NID required; GENDER, DOB, Phone optional). Matches by National ID only (rows
    without one are skipped), sets the party, and fills date of birth, mobile and
    gender only where empty (the registry always wins). Temp table + fixed
    parameterised SQL in one transaction (`PartyMembershipImportService`); repeatable.
  - Voter **date of birth** (owner decision, 2026-10-05): `Voters.DateOfBirth` (date,
    optional) — migration `AddVoterDateOfBirth`; rollback
    `dotnet ef database update AddVoterPhotos`. Gender ("M"/"F") and age are shown on
    the list and profile, with a gender filter.
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
  (This replaces the earlier many-to-many `ConstituencyIsland` assumption. Until that
  table is dropped, keep exactly one link row per island, matching
  `Island.ConstituencyId` — `IslandService` does this.)
- Not every constituency requires an island selection. Support constituency-only voters.
- If a voter has an island, it must belong to the voter's constituency.
- Roles grant capabilities; scopes decide which records those capabilities apply to.
  Role assignment and scope assignment are separate.
- **A user can have multiple roles AND multiple scopes** (implemented). Roles and
  scopes are independent: every role a user has applies in every scope they have.
  Screens call scopes "Areas".
  - Effective permissions = the union of all the user's roles.
  - Effective access = the union of all the user's scopes. A record is visible if it
    falls inside ANY of the user's scopes (a whole constituency, or a specific island).
  - Store assignments in separate tables (`UserRoles`, `UserScopes`), not single
    columns on the user.
  - Every scope check filters by the user's full scope list, never a single scope ID.
  - **Voter searches stay in the user's areas** (owner decision, 2026-10-06): the
    Voters list, `api/voters/search` and fetch by ID card use `InSearchScope` — an
    administrator (Super/National) with areas assigned searches only those, one with
    no areas searches the whole registry. Opening a record, reports and the Dashboard
    still use `InScope` (administrators see everything).
  - Changes to roles or scopes take effect immediately: resolve them on the server
    (cached, with invalidation on change), not from a list frozen in the login token.
    Implemented in `AccessScopeService` (30s cache, `Invalidate(userId)` on change)
    and the API's `AccessRequirementHandler`; roles in the JWT are ignored.
    Deactivated accounts get 401 on their next request.
  - Assigning or removing a role or scope is audited, and assignment screens must be
    simple for non-technical administrators.
- **Granted rights (owner decision, 2026-10-02; extended 2026-10-05).** The
  Administrator grants named rights either to a role (Settings → Roles & rights:
  everyone with the role gets them) or to one user (Users → Roles → "Extra rights").
  Both screens show the same grid (rows = Voters / Encounters / Pledges / Influencers,
  columns = View / Add / Edit / Delete / Link). Effective rights = rights of all the
  user's roles + the user's extra rights; the Administrator always has every right.
  - Catalog: `PermissionCatalog` (Application/Common): `Voters.View/Add/Edit/Delete`,
    `Encounters.View/Add/Edit/Delete`, `Encounters.Response` (only this right sets or
    changes the green/yellow/red response; without it encounters are saved with no
    response and edits keep it), `Voters.Status` ("Change support status", owner
    decision 2026-10-07: only this right sets or changes a voter's Supporter /
    Undecided / Opponent / Neutral status — the list pop-up, the Add and Edit forms;
    without it new voters are Undecided and edits keep the status; every role with
    "Edit voters" was given it — data migration `GrantVoterStatusToEditors`, rollback
    `dotnet ef database update AddRoleStartPage`; `RoleSeeder` does the same for new
    databases), `Pledges.View/Add/Edit/Delete`,
    `Influencers.View/Add/Edit/Delete/Link`, `Reports.View` / `Reports.Download`
    (owner decision 2026-10-07: Reports pages and their CSV download / print are rights;
    every existing role except the Administrator got both — data migration
    `GrantReportRights`, rollback `dotnet ef database update GrantVoterStatusToEditors`;
    `RoleSeeder` gives them to new databases' built-in roles). Encounter edits audit changed fields
    (notes only as "notes"). Deletes of voters, encounters and pledges are permanent,
    area-checked and audited with a readable name. Add new rights there; the screens
    list them automatically. Mirror the keys in web `Hr28Permissions`.
  - Stored in `RolePermissions` (RoleId, Permission) and `UserPermissions`
    (UserId, Permission) — migration `AddPermissions`; rollback:
    `dotnet ef database update HashAuthorizationCodes`.
  - Enforced on the server via `AccessScope.HasPermission(...)` (resolved live in
    `AccessScopeService`; a role's rights change calls `InvalidateAll()`), **and** for
    voter-owned records (encounters) the voter must be inside the user's areas
    (influencers are global, so their rights apply to every influencer). The web app only hides buttons
    (`Hr28Permissions.Has`, refreshed in the session about once a minute).
  - Granting/removing rights is audited ("Rights for role …", "Extra rights: …").
  - Influencer **delete is permanent** (owner decision): removes the influencer and
    their voter links, asks for confirmation showing the link count, and the audit
    entry stores the name and National ID. Influencer edits audit which fields changed.
- Non-administrators cannot edit protected voter fields (constituency, island) or
  access records outside their effective scope — enforced in the API and the UI.
- National ID (and other chosen keys) must be duplicate-checked with friendly messages.
- Influencer–voter links must prevent accidental duplicates but allow legitimate updates.
- Every create, update and delete on critical records is audited: actor, timestamp,
  entity, action and what changed.
- Election-day turnout records only **whether** someone voted, never **how** they voted.
- Voter political party (owner decision, 2026-10-05): `Voters.PoliticalPartyId`, empty =
  "Not known" (default; never default to a real party). Parties are a managed list
  (`PoliticalParties`, Settings → Lists). The Voters list opens on the party flagged
  `IsDefaultFilter` (MDP by default); links that show counts or look up a person pass
  `party=all`.
- Encounters (owner decision, 2026-10-05): outcome = Meet / Call / Request; response =
  Supports (green) / Undecided (yellow) / Does not support (red). Allowed values live
  in `EncounterValues` (Application/Common), mirrored in web `EncounterListFilterModel`.

## Security standard

- Target: **OWASP ASVS Level 2**. When reviewing or writing code, check against it.
- **The API is the security boundary.** Every protected endpoint enforces role
  policies AND scope checks on the server. Hiding a menu or button is never enough.
- MVC must also check permissions, and handle 401/403 from the API by clearing the
  session and redirecting to login or an access-denied page – never a raw error page.

### Authentication (SMS codes, designed for non-technical users)

Users sign in with SMS auth codes instead of authenticator apps. Build it so it is
strong despite that:

- **Decision (owner, 2026-10-02): no passwords.** Sign-in is two factors:
  1. **Authorization code** (something the user is given and keeps): 8 characters,
     cryptographically random, shown as `ABCD-EFGH`, typed case-insensitively with or
     without the dash. Stored **only** as HMAC-SHA256 with `Security:AuthorizationCodeKey`
     (User Secrets in dev, Key Vault in prod; every instance must use the same key or
     nobody can sign in). **New accounts (owner decision, 2026-10-09) start inactive and
     without a code; activation needs at least one role (API and UI); the first
     activation creates the code and sends it in a welcome SMS** (campaign name, first
     name, code, optional `App:SignInUrl`) — nobody else sees it. If that SMS can't be
     sent, the code is shown once to the administrator instead. Re-activating later sends
     an "active again" SMS and keeps the code. "Reset code" still shows the new code once
     to the administrator. Older 5-character codes still work.
  2. **SMS OTP** (something the user has).
  Do not add passwords or PINs without the owner's approval.
- SMS codes (implemented): 6 digits from `RandomNumberGenerator`, expiry set by the
  Administrator in Settings → System (default 5 min, 1–15), **single use** (atomic
  consume), constant-time comparison, max attempts per code (setting, default 5).
  Stored only as an HMAC-SHA256 bound to the code request (`HashOtp`, same key as
  authorization codes); the digits exist only in the SMS. Sign out is a POST form.
  **Temporary (owner, 2026-10-02):** until a real SMS provider is connected,
  `Security:StoreReadableOtpCodes: true` in `appsettings.Development.json` keeps codes
  readable in `OtpRequests.OtpCode` so testers can sign in. The API refuses to start
  with it on outside Development. Remove the setting when SMS is integrated.
- Rate limits (implemented): per IP 10 code requests and 20 code checks per 5 minutes;
  per account 60s between requests, 5 codes per hour, and 10 wrong codes in a row
  locks sign-in for 15 minutes. Per user: 120 searches/min, 20 report downloads/hour,
  10 voter list uploads/hour. The web app forwards the visitor IP; the API trusts it
  only from `ReverseProxy:KnownProxies` (loopback by default; set the web app's
  outbound IPs in Azure). Per phone number: not yet (there is no SMS sender yet).
- SMS text includes a warning, e.g. "HR28 code: 482913. Never share this code.
  HR28 staff will never ask for it."
- Use generic messages that don't reveal whether a user or phone number exists.
- **Phone number changes** only by an administrator, always audited, and the user is
  notified. (Whoever controls the number controls the account.)
- **Trusted devices — implemented (owner, 2026-10-07: "Remember me" for collectors who
  are not computer-oriented):** "Remember me on this device" on the sign-in page. After
  the SMS code is verified the API issues a random 32-byte device key; the browser keeps
  it only in the HttpOnly/Secure/SameSite=Strict cookie `HR28.Device`, the database only
  its HMAC (`TrustedDevices`, `HashDeviceToken`). On that device the user skips the
  authorization code ("Welcome back, <first name>" + "Send my sign-in code") but the SMS
  code, attempt limits and lockout still apply. Lasts Settings → System "Remember devices
  for (days)" (default 30, 0–90; 0 = off and stops all remembered devices); at most 5 per
  user. Forgotten by "Not you?" on the sign-in page, by Reset code, and by the
  administrator (Users → Edit → "Forget remembered devices"); deactivated accounts can't
  use them. Remembering and forgetting are audited. The authorization code itself is
  never stored in the browser. Migration `AddTrustedDevices`; rollback
  `dotnet ef database update GrantReportRights`.
- **Step-up verification**: require a fresh code before sensitive actions – data
  exports, role/scope changes, user management, phone number changes – even within
  a logged-in session.
- **Login alerts**: SMS the user when someone signs in from a new device.
- **Sessions** — idle and absolute timeouts implemented (owner, 2026-10-08):
  1 hour without activity ends the session; at 59 minutes the page asks "Are you still
  there?" with a 60-second countdown (Stay signed in / Sign out) and, unanswered, signs
  out to the sign-in page with a plain message (`_SessionTimeout`, `site.js`; activity in
  any tab counts). While someone works the 1-hour API token is quietly refreshed
  (`SessionKeeper`, `SessionTokenRefreshFilter`, web `Auth/KeepAlive`, API
  `POST api/auth/refresh`, keeps the `auth_time` claim) — never past 12 hours from
  sign-in, never for a deactivated account. Any other ended session lands on the
  sign-in page with "Your session has ended". **Administrators end a user's sessions**
  (owner, 2026-10-08): Users → Edit → "End all sessions" (API
  `POST api/users/{id}/end-sessions`, Administrator only, not your own account, audited)
  sets `Users.SessionsEndedAt`; the API's JWT `OnTokenValidated` refuses every token
  whose `auth_time` is at or before it (401 on the next request, refresh refused too),
  so the person is signed out everywhere. Remembered devices are not forgotten by this
  (separate button). Migration `AddUserSessionsEndedAt`; rollback
  `dotnet ef database update AddTrustedDevices`.
- **Stronger sign-in for high-risk accounts — deferred (owner, 2026-10-02):**
  Administrators should later use passkeys (fingerprint/face unlock via WebAuthn) or
  another phishing-resistant method; optional for everyone else. Not now. (The Owner
  does not sign in to the app.) Check the passkey support in the ASP.NET Core
  Identity version in use before implementing.
- Never log or display codes, tokens, passwords, connection strings or keys outside
  the Development environment.

### Web and API protection

- HTTPS only with HSTS; TLS 1.2+.
- Security headers: Content-Security-Policy, X-Content-Type-Options: nosniff,
  frame protection (frame-ancestors / X-Frame-Options), Referrer-Policy.
- Cookies: Secure, HttpOnly, SameSite.
- Anti-forgery tokens on every form and state-changing request in MVC (implemented
  site-wide with `AutoValidateAntiforgeryTokenAttribute` in web `Program.cs`; form tag
  helpers add the token automatically, JavaScript POSTs must send it).
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
- Required secrets for `HR28.API` (the API refuses to start without them):
  `Security:AuthorizationCodeKey` (base64, ≥32 bytes) and `Jwt:Key` (≥32 bytes; rotated
  2026-10-02, the old committed key no longer works). Set both with
  `dotnet user-secrets --project HR28.API` in development, Key Vault in production.
- `ConnectionStrings:DefaultConnection`: development uses Windows sign-in (no password)
  in `appsettings.Development.json`; production sets it in Key Vault / app settings.
  `appsettings.json` holds no secrets. The old `sa` password is still in Git history:
  change it on the SQL Server (the app no longer uses it).
- Azure SQL: firewall restricted to the app; no public database access.
- Keep NuGet packages free of known vulnerabilities.

## Data and privacy

- Development uses a separate dev database with **synthetic data only**. Never connect
  to or query the production voter registry.
- In Development, SMS goes to a fake sender that logs the code. Never call the real
  SMS provider from tests or local runs.
- Collect only approved fields. Do not add fields for ID card copies, health,
  financial, family-sensitive data, precise location or free-text allegations.
- **Voter photos are allowed** (owner decision, 2026-10-05, replaces the earlier "no
  photos" rule): optional, one per voter, table `VoterPhotos` (never wwwroot or a
  public URL), JPEG/PNG up to 2 MB, checked by content and rebuilt without metadata
  (EXIF/GPS, XMP, IPTC, comments, appended data — `PhotoSanitizer`). Viewing needs
  `Voters.Photo.View`, adding/replacing/removing needs `Voters.Photo.Edit` (granted
  rights, no role has them by default), always within the user's areas. API
  `GET/POST/DELETE api/voters/{id}/photo` (view rate-limited like search, never
  cached; changes 60/hour per user); every change audited. Quick entry also offers
  "Add photo" (camera button: phones offer the camera or a picture) / "Remove photo" by
  the same rights (2026-10-09); `site.js` (`data-photo-upload`) shrinks the picture on
  the device to 1280 px JPEG before upload, so phone photos fit the 2 MB limit. Migration
  `AddVoterPhotos`; rollback `dotnet ef database update AddRoleRights` (photos lost).
- Data exports are restricted by role and scope, require step-up verification, and
  are audited. (Today: report CSVs are scoped, rate-limited and audited; step-up
  verification is not built yet.)
- Out of scope unless explicitly requested: public self-service portal, automated
  persuasion or behavioral targeting, offline sync, GIS tracking, biometrics stored
  by HR28, government-database integrations.

## UX rules

- **All dates and times are Maldives time (UTC+05:00, no daylight saving)** — on
  screen, in CSV exports, and for "today"/"now" in business rules (overdue
  pledges, "not in the future"). Never use `DateTime.Now`/`Today`/`ToLocalTime()`:
  servers (Azure) run on UTC. Use `MaldivesTime` (Application/Common) on the server
  and `Hr28Time` (Web/Services) in the web app. System timestamps (CreatedAt,
  LinkedAt, LastLoginAt, UpdatedAt, PledgeDate, FulfilledDate) are stored in UTC and
  converted with `FromUtc`; dates people type (EncounterDate, DueDate) are Maldives
  dates stored as typed. Date filters convert the Maldives day to a UTC range.

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

Status as of 2026-10-02 (update when an item changes):

1. Island model correction — **partly done.** Island create/edit keeps exactly one
   `ConstituencyIsland` link in sync with `Island.ConstituencyId` and blocks moving
   islands that have voters. Still to do: reconcile existing data, then drop the
   many-to-many table and read islands from `Island.ConstituencyId` only.
2. Multiple roles and multiple scopes per user — **done** (no migration was needed;
   the tables already allowed many rows). Roles page = tick list; Areas page =
   add/remove.
3. Permission matrix and API authorization policies — **done (2026-10-05).** Policies:
   default (active account), `Administrator`, `SuperAdministrator`, plus a named right
   on every record endpoint, all resolved live. Custom roles and the full rights grid
   are in Settings → Roles & rights. Still open: report downloads/exports are not
   rights yet (scoped, rate-limited and audited).
4. Central session / 401 handling — **partly done.** `ApiClient` + `AppController`
   handle 401/403 for newer pages; `SessionRoleRefreshFilter` ends sessions of
   deactivated users. Older pages still call `DashboardService` directly (raw errors
   possible, including on 429).
5. Authentication hardening — **partly done:** 6-digit secure OTP, hashed
   authorization codes, attempt limits, cooldown, lockout, per-IP rate limits,
   inactive users blocked, anti-forgery on every form, SMS text with the
   "never share" warning (dev sender only), voter area changes limited to
   administrators in API and UI. Still to do: real SMS provider (with per-phone
   limits), step-up codes for exports/user
   management, trusted devices, login alerts, session timeouts/admin sign-out,
   passkeys (deferred).
6. OWASP ASVS L2 review — **not started** as a formal pass.
7. Audit coverage — **partly done:** voters, influencers, encounters, pledges,
   users (create, code reset, roles, areas), constituencies, islands, settings and
   report downloads are audited. Voter/influencer updates do not yet record which
   fields changed.
8. Regression tests — **not started; there is no test project yet.**

Known open issues (fix or confirm with the owner):
- No real SMS provider yet. `ISmsSender`: Development writes the message to the API
  log (`DevelopmentSmsSender`); other environments use `UnconfiguredSmsSender`, which
  refuses to send, so sign-in will not work outside Development until a provider is
  connected.
- The web Content-Security-Policy still allows `'unsafe-inline'` scripts and styles
  (sign-in pages and layout use inline blocks). Move them to files or nonces, then
  remove it. Other security headers and Secure/HttpOnly/SameSite cookies are in place.
- Report downloads are scoped, rate-limited and audited but have no step-up code.
- Influencer edit/delete with granted rights is built but not yet tested end to end
  with a signed-in non-Administrator. No right is granted to any role yet.
- `Users.AuthorizationCode` (plain, now always empty) can be dropped in a migration.
- Dev data to tidy: "Collector Demo" holds three roles incl. National Administrator.
  On 2026-10-04 the 6 placeholder constituencies (Addu Constituency, Hulhumale,
  Galolhu, Henveiru, Maafannu, Machangolhi) were deleted with their islands, 4 test
  voters, 8 influencers and 7 user areas (backup HR28Db_before_constituency_cleanup_
  2026-10-04.bak); 93 constituencies remain, all with codes. 13,270 voters lost the
  shared "Hithadhoo" island (now "No island"); Mariyam Waheed and Addu Coordinator
  have no area until one is assigned.

**In progress (October 2026): client feedback** — see
`Docs/Client-Feedback-2026-10.md` for the owner's decisions and the step order
(step 1 done; continue with step 2, Influencers).

Next, after Phase 1: module switch system → deployment pipeline → paid modules.
