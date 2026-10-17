# OWASP ASVS Level 2 review — Campaign Intelligence Platform (HR28)

**Date:** 15 October 2026  **Standard:** OWASP ASVS 5.0, Level 2
**Version reviewed:** release `20261015-160337` (commit `c971940`), live at shamheed.com
**Method:** code review of API and web app, configuration review, read-only checks on the
live server. No penetration test and no changes were made during the review.

## Summary

The system is in good shape for Level 2. Sign-in, access control, area (scope) checks,
session handling, audit, output encoding, SQL safety, uploads and server hardening all pass.
There are **no high-severity findings**. Four medium and six low items remain; most are
small code changes.

| # | Severity | Finding | Effort |
|---|----------|---------|--------|
| M1 | Medium | One user can page through the whole voter registry in about 25 minutes | **Fixed 2026-10-17** |
| M2 | Medium | Ordinary sign-ins, wrong codes, lockouts and refused requests are not recorded | **Fixed 2026-10-17** |
| M3 | Medium | No server-side length limits on many text fields | **Fixed 2026-10-17** |
| M4 | Medium | Select2 is loaded from a CDN without an integrity check | **Fixed 2026-10-17** |
| L1 | Low | Session cookie is not renewed at sign-in | **Fixed 2026-10-17** |
| L2 | Low | Token check does not pin the signing algorithm | **Fixed 2026-10-17** |
| L3 | Low | Old 5-character authorization codes still accepted | **Fixed 2026-10-17** |
| L4 | Low | SSH is reachable from the whole internet (keys only) | **Fixed 2026-10-17** |
| L5 | Low | SQL `sa` login is enabled (local only, used by scripts) | **Fixed 2026-10-17** |
| L6 | Low | Old/legacy packages and "Server: Kestrel" header | **Fixed 2026-10-17** (xUnit 2 kept) |

## Findings

### M1 — Bulk extraction of the voter registry (V2 Business logic / anti-automation)

`GET api/voters` allows 100 voters per page and 120 requests per minute per user, so a
signed-in user whose areas cover the whole registry (or an administrator with no areas)
can read about 12,000 voters a minute, including mobile numbers: all 284,664 voters in
about 25 minutes. Each request is rate-limited but nothing caps the total or raises an
alert.

**Fix:** a per-user cap on voter rows read per hour and per day through the list, search
and ID lookup (for example 2,000 per hour and 10,000 per day, set in Settings → System),
with a plain message when reached, an audit entry, and an SMS to the Administrators.
**Fixed (2026-10-17, owner's choice):** limits in Settings → System, "Protect the voter
list" (default 2,000 voters per hour and 10,000 per day, Maldives time; 100–50,000 and
100–300,000; daily not below hourly; audited). They apply to every role, Administrators
included. The voter list, search and ID-card lookup count every voter shown (every lookup
counts, found or not). At a limit: a plain message ("…paused until 11:00" / "until
tomorrow"), an Audit Trail entry, a `SECURITY:` log line and one SMS to the Administrators;
raising a limit lifts the pause at once. Opening a record the person already has is not
affected. Tests: `VoterViewLimitTests`.

### M2 — Authentication and access-denied events not fully logged (V16 Security logging)

Recorded today: new-device sign-ins, first sign-in, remembered devices, step-up. **Not**
recorded: ordinary successful sign-ins (only `LastLoginAt` is updated), wrong SMS codes,
account lockouts, attempts with an unknown authorization code, and API refusals (403).
Without these an attack on accounts is invisible after the fact.

**Fixed (2026-10-17):** every successful sign-in ("Signed in on Chrome on Android",
"(remembered device)") and every lockout are in the Audit Trail. Wrong codes, unknown
authorization codes, unrecognised remembered devices and refused requests (missing right,
outside the user's areas) go to the API log as `SECURITY:` lines with user and IP, never a
code; request text is cleaned of line breaks. `hr28-monitor` SMSes the Administrators when
there are 30 or more in 15 minutes. Tests: `SignInAuditTests`.

### M3 — Missing input length limits (V2 Validation)

Encounter notes, pledge notes, settings, roles, parties and categories are checked. Voter
name/address/remarks, influencer fields, user fields and constituency/island names are not:
the database columns are `nvarchar(max)` and the API accepts request bodies up to about
30 MB. A user with an edit right could store very large text, slowing lists and reports.

**Fixed (2026-10-17):** `[MaxLength]` on every input DTO (names 150, address 250, remarks
1,000, phone and National ID 20, email 254, pledge title 200 / description 2,000, encounter
notes 1,000), answered as `{ "message": "Full name can be at most 150 characters." }` and
shown on the form; ordinary requests limited to 1 MB (the four uploads keep their own
limits). Tests: `InputLimitTests`.

### M4 — Third-party script without integrity check (V3 Web frontend)

`_Layout.cshtml` loads Select2 (script and stylesheet) from `cdn.jsdelivr.net` with no
`integrity` attribute, and the Content-Security-Policy trusts that host. If the CDN copy
were tampered with, the code would run on every page with the user's session.

**Fixed (2026-10-17):** Select2 4.1.0-rc.0 is served from `wwwroot/lib/select2` (taken
from the npm registry, checksum matched npm's published SHA-512) and jsDelivr is gone from
the policy: no outside website may supply code or styles (test in
`ContentSecurityPolicyTests`).

### L1 — Session not renewed at sign-in (V7 Session management)

The web session cookie (`HR28.Session`) keeps the same identifier from before sign-in to
after. Abuse needs an attacker to plant a cookie in the victim's browser first, which
HTTPS, HSTS with subdomains and HttpOnly cookies make hard. **Fixed (2026-10-17):** when
the SMS code is accepted the old session is emptied and its cookie deleted; the sign-in
waits on the server for at most a minute behind a one-time key (HttpOnly cookie limited to
`/Auth/Finish`), and `Auth/Finish` stores it in a brand-new session. Checked by signing in
twice in one browser: the cookie changed and the old one was no longer signed in.

### L2 — Signing algorithm not pinned (V9 Self-contained tokens)

The API accepts tokens signed with its secret key but does not list the allowed
algorithm. **Fixed (2026-10-17):** `ValidAlgorithms = [HmacSha256]` in the token check.

### L3 — Older 5-character authorization codes still work (V6 Authentication)

New codes have 8 characters (about 10¹² combinations); older 5-character codes (about 33
million) are still accepted. The SMS code is still required, so this is low risk.
**Fixed (2026-10-17):** only 8-character codes sign in. Live had none left (both accounts'
codes were issued on 15 October); six practice accounts on the training site still held old
codes and need Reset code there. Test: `AuthorizationCodeLengthTests`.

### L4 — SSH open to the internet (V13 Configuration)

Port 22 accepts connections from anywhere. It is keys-only, `MaxAuthTries 3`, and
fail2ban is active, so the risk is low. **Fix (optional):** a DigitalOcean cloud firewall
allowing SSH only from your own addresses, keeping the DigitalOcean web console as a
fallback. **Fixed (2026-10-17):** the owner ran `hr28-ssh-allow lock 124.195.218.145` (the
only address that signed in by SSH in 30 days) on the live server; SSH now accepts only that
address and DigitalOcean's Droplet Console, and a new session was checked before closing the
old one. Address changed? DigitalOcean website → Droplet Console → `hr28-ssh-allow add <address>`.

### L5 — SQL `sa` login enabled (V13 Configuration)

SQL Server listens on 127.0.0.1 only and the apps use `hr28_app` / `hr28_train` with
read/write rights only (no owner rights). The `sa` login remains enabled for the backup,
deploy and refresh scripts (password in a root-only file). **Fixed (2026-10-17):** the
everyday scripts use `hr28_ops` (dbcreator + db_owner of the two HR28 databases, not a
server administrator); setup work uses a separate `hr28_admin`; `sa` is disabled. New random
passwords in root-only files, never shown. Checked on the live server: backup, alert
look-up, a database change on training and a restore check all work; `sa` is refused.

### L6 — Housekeeping (V15 Secure coding / V13)

`HR28.Infrastructure` references the old `Microsoft.AspNetCore.Http.Abstractions 2.3.13`
(replace with the framework reference); the test project uses legacy xUnit 2; the apps
send `Server: Kestrel` (Cloudflare replaces it publicly). No package has a known
vulnerability (`dotnet list package --vulnerable`: none). **Fixed (2026-10-17):** the shared
framework reference replaces the old package and neither app sends a `Server` header. The
test project stays on xUnit 2 (test-only, still supported; moving to xUnit 3 is routine
upkeep, not a security issue).

## Accepted risks (owner decisions)

- **Remembered devices** skip the authorization code: SMS code plus the device's secret
  cookie (owner, 2026-10-07). Limits, lockout, expiry and "forget" controls apply.
- **SMS as the second factor** instead of an authenticator app; passkeys for
  administrators deferred (owner, 2026-10-02).
- **National ID duplicate check** tells people who may add or edit voters whether an ID
  exists anywhere in the registry (name shown only inside their areas; rate-limited).
- **Data-protection keys** are stored on disk unencrypted, in a folder only the web app's
  account can read.

## What passed

| ASVS chapter | Result |
|---|---|
| V1 Encoding and sanitization | Razor encodes all output; the two raw outputs are an icon path and server-built JSON (escaped). CSV downloads neutralise formulas. |
| V2 Validation and business logic | Formats for National ID and mobile, island-in-constituency, party, status rights, duplicate checks. Gaps: M1, M3. |
| V3 Web frontend | Nonce-based CSP with no inline code or handlers, frame-ancestors none, nosniff, Referrer- and Permissions-Policy, HSTS 1 year with subdomains, anti-forgery on every form, Secure/HttpOnly/SameSite cookies. Gap: M4. |
| V4 API | Every endpoint needs a signed-in, active account; every record endpoint has a named right; JSON-only API with a strict policy; API not reachable from outside the server. |
| V5 Files | Photos and logo checked by content and rebuilt without metadata, stored in the database; uploads size-limited, extension-checked, administrator-only and rate-limited. |
| V6 Authentication | No passwords; 8-character random code stored only as HMAC-SHA256; 6-digit SMS code from a secure generator, keyed hash, single use, atomic attempt counting, constant-time comparison; per-IP, per-account and per-phone limits; lockout; generic messages; codes never shown to administrators. Gap: L3. |
| V7 Session management | Server-side session; 1-hour idle and 12-hour absolute limits; sign-out by POST; administrators end sessions everywhere; deactivation takes effect on the next request. Gap: L1. |
| V8 Authorization | Roles and rights resolved live from the database; area checks on every voter, encounter, pledge and photo read and change; administrator-only management; step-up before sensitive actions. |
| V9 Tokens | Issuer, audience, lifetime (no clock skew) and signature checked; key ≥ 32 bytes from protected settings. Gap: L2. |
| V11 Cryptography | Secure random generator everywhere; HMAC-SHA256; no weak algorithms. |
| V12 Communication | Cloudflare Full (strict), Authenticated Origin Pulls, TLS 1.2+ at Cloudflare and Nginx, origin reachable only from Cloudflare. |
| V13 Configuration | Secrets only in root-owned files readable by the service account; services run as separate users with `NoNewPrivileges`, `ProtectSystem=strict`, `PrivateTmp`; app ports bound to 127.0.0.1; least-privilege database logins; automatic security updates; Swagger off outside Development. Gaps: L4, L5. |
| V14 Data protection | Only approved fields; exports scoped, rate-limited, audited and behind step-up; nightly and daily backups. Off-site copy still open (see roadmap). |
| V16 Logging and errors | Every record change audited with readable detail; friendly errors everywhere, no stack traces; codes and keys never logged. Gap: M2. |

## Recommended order

1. **M4, L2, M3**: small code changes, one release.
2. **M2**: sign-in and refusal logging.
3. **M1**: once the owner chooses the limits.
4. **L3**: owner resets any 5-character codes (2 live users).
5. **L1, L6**: next code release.
6. **L4, L5**: server changes, with the owner's approval.

Still open outside this review: off-site backups with a test restore, a separate Dhiraagu
API login, and passkeys (deferred).
