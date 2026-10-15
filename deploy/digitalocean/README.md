# HR28 on a DigitalOcean droplet

One client per droplet (hr28-prod: Singapore SGP1, 4 vCPU / 8 GB / 160 GB, Ubuntu 24.04).

```
visitor ──HTTPS──> Cloudflare (Full strict) ──HTTPS──> Nginx :443 ──> HR28.Web 127.0.0.1:5000
                                                                       └──> HR28.API 127.0.0.1:5001 ──> SQL Server Express (Docker) 127.0.0.1:1433
```

Only Nginx (80/443) and SSH (22) are reachable from outside. The API and SQL Server listen on
the server itself only (Docker publishes SQL on 127.0.0.1 — Docker bypasses UFW, so never
publish it on 0.0.0.0).

## Files

| In this folder | On the server | Notes |
|---|---|---|
| `nginx/hr28.conf` | `/etc/nginx/sites-available/hr28` (+ link in `sites-enabled`) | web app only; API not published |
| `nginx/hr28-cloudflare-real-ip.conf` | `/etc/nginx/snippets/` | visitor address from Cloudflare only |
| `systemd/hr28-api.service`, `hr28-web.service` | `/etc/systemd/system/` | run as `hr28api` / `hr28web`, hardened |
| `env/*.env.example` | `/etc/hr28/api.env`, `/etc/hr28/web.env` | made by `setup-accounts-and-secrets.sh`; never in Git |
| `scripts/setup-accounts-and-secrets.sh` | run once | accounts, folders, **new** production keys (kept on re-run) |
| `scripts/rotate-sa-and-create-app-login.sh` | run **by the owner** in PuTTY | asks for the current sa password |
| `scripts/backup-db.sh` + `hr28-backup.cron` | `/usr/local/sbin/hr28-backup`, `/etc/cron.d/hr28-backup` | nightly 00:30 MVT, 14 days (DigitalOcean daily droplet backups 01:00-05:00 MVT, 7 days) |
| `scripts/deploy-release.sh` | `/usr/local/sbin/hr28-deploy` | `training`: new release → training only, staged; `live`: the staged (tested) release → live with backup, health check, roll back; `status` |
| `scripts/save-origin-cert.sh` | `/usr/local/sbin/hr28-origin-cert` | owner pastes the Cloudflare Origin Certificate + key |
| `scripts/cloudflare-only.sh` + `hr28-cloudflare-only.cron` | `/usr/local/sbin/hr28-cloudflare-only`, `/etc/cron.d/` | ports 80/443 from Cloudflare only + Nginx real-IP list; monthly |
| `ssh/10-hr28.conf` | `/etc/ssh/sshd_config.d/` | keys only, 3 tries, no X11 |
| `fail2ban/hr28.local` | `/etc/fail2ban/jail.d/` | 5 failed SSH tries in 10 min = banned 1 day |
| `scripts/save-sms-credentials.sh` | `/usr/local/sbin/hr28-sms-credentials` | owner stores the Dhiraagu Bulk SMS login in `/etc/hr28/sms.env` (password hidden) |
| `scripts/sms-test.sh` | `/usr/local/sbin/hr28-sms-test 9607XXXXXX` | one test SMS through Dhiraagu (shows status only) |
| `alerts/sms-lib.sh` | `/usr/local/lib/hr28/sms-lib.sh` | SMS to active Administrators (+ `ALERT_EXTRA_PHONES` in `/etc/hr28/alerts.env`) through Dhiraagu |
| `alerts/boot-notify.sh` + `hr28-boot-notify.service` | `/usr/local/sbin/hr28-boot-notify`, `/etc/systemd/system/` | after every start: "server restarted at HH:MM and is back online" (or NOT responding); `dry-run` to preview |
| `alerts/monitor.sh` + `hr28-monitor.cron` | `/usr/local/sbin/hr28-monitor`, `/etc/cron.d/` | every 5 min: API/DB, web, Nginx, SQL container, disk < 85%, backup < 26 h; SMS when a problem is seen twice in a row and when it clears; `status` to check; log `/var/log/hr28-alerts.log` |
| `alerts/52hr28-auto-reboot` | `/etc/apt/apt.conf.d/` | security updates restart the server when needed at 00:30 UTC = 05:30 MVT |
| `scripts/reset-code.sh` | `/usr/local/sbin/hr28-reset-code` | new authorization code from the server |
| `sql/cleanup-development-leftovers.sql` | run once | removes test accounts copied from development |

Secrets live only in `/etc/hr28/` on the server: `sql-admin.env` (root, 600), `api.env`
(root:hr28api, 640). The keys were generated there (2026-10-12): a **new** authorization-code
key, so every user needs a new code (first Administrator: `reset-code`, below; everyone else:
Users → Reset code).

## First setup (in order)

1. `setup-accounts-and-secrets.sh` — accounts `hr28api` / `hr28web`, `/etc/hr28/*.env`.
2. Owner, in PuTTY: `rotate-sa-and-create-app-login.sh` — new sa password, login `hr28_app`.
3. Clean `/var/www/hr28-api` and `/var/www/hr28`: replace `appsettings.json` with the
   committed one (no secrets) and delete `appsettings.Development.json`.
4. `sql/cleanup-development-leftovers.sql` (as sa; sqlcmd needs `-I`, the tables have filtered indexes).
5. Install both systemd units; remove the old `hr28.service`; start `hr28-api`, then `hr28-web`.
   Check: `curl http://127.0.0.1:5001/health` → `{"status":"ok"}`, and
   `curl -H 'X-Forwarded-Proto: https' -o /dev/null -w '%{http_code}' http://127.0.0.1:5000/Auth/Login` → `200`
   (without that header the web app answers 500: it only works over HTTPS).
6. First Administrator code: install `scripts/reset-code.sh` as `/usr/local/sbin/hr28-reset-code`,
   then `hr28-reset-code <ID card>` (prints the code once).
7. Backups: install `hr28-backup` + cron, run once, check `/var/backups/hr28`.
8. Cloudflare: Origin Certificate → `/etc/ssl/hr28/origin.pem` + `origin.key` (600);
   Nginx files; `nginx -t`; DNS `@` and `www` → droplet IP (proxied); SSL mode **Full (strict)**.
9. SMS (Dhiraagu Bulk SMS, connected 2026-10-15): owner runs `hr28-sms-credentials` (portal
   username/password; better a separate API login from Dhiraagu), then `hr28-sms-test 9607XXXXXX`
   (answer `RESPONSE_STATUS` 100 = sent). Restart `hr28-api` after changing the login. Interface:
   `https://bulksms.dhiraagu.com.mv/partners/xmlMessage.jsp` (TeleMessage XML; not in Dhiraagu's
   user guide, found and tested 2026-10-15). Delivers to Dhiraagu and Ooredoo numbers.
10. Hardening (done 2026-10-12): `ssh/10-hr28.conf` (`sshd -t`, reload, test a NEW connection
    first); fail2ban (`hr28.local`; Ubuntu 24.04's unit is `ssh.service`); `hr28-cloudflare-only`
    (+ cron); Authenticated Origin Pulls: CA at `/etc/ssl/hr28/cloudflare-origin-pull-ca.pem`, turn
    it on in Cloudflare first, then the `ssl_verify_client` lines in `hr28.conf`;
    `Logging__LogLevel__Default=Warning` in both `.env` files. Cloudflare: see below.
    DigitalOcean daily droplet backups (01:00-05:00 MVT, 7 days); off-site database copies: not yet.

## Cloudflare settings (per client zone, Pro plan)

Set in the dashboard; nothing on the server depends on them except where noted.

| Where | Setting |
|---|---|
| DNS | `@` A → droplet IP (proxied); `www` CNAME → `@` (proxied); no other A/AAAA for `@`/`www` |
| SSL/TLS → Overview | **Full (strict)** (Custom, not Automatic) |
| SSL/TLS → Edge Certificates | Always Use HTTPS on; Minimum TLS 1.2; TLS 1.3 on; Automatic HTTPS Rewrites on; HSTS off (the app sends it) |
| SSL/TLS → Origin Server | Origin Certificate (15 years) → `hr28-origin-cert`; **Authenticated Origin Pulls** on (zone-level) **before** `ssl_verify_client on` in Nginx |
| Overview → Security quick start | Cloudflare Managed Ruleset, OWASP Core Ruleset, Super Bot Fight Mode, Client-side security: on |
| Security rules → Rate limiting | `Sign-in limit`: `(http.request.method eq "POST" and (starts_with(lower(http.request.uri.path), "/auth/login") or starts_with(lower(http.request.uri.path), "/auth/verifyotp")))`, per IP, 20 / 1 minute, Managed Challenge (not `/Auth/KeepAlive`: shared mobile IPs) |
| Security rules → Managed rules → Create rule (exception), **Place at: First** | `File uploads (photos, logo, voter lists)`: `(http.request.method eq "POST" and (starts_with(lower(http.request.uri.path), "/voters/uploadphoto/") or starts_with(lower(http.request.uri.path), "/voterimport") or lower(http.request.uri.path) eq "/settings/uploadlogo"))` → skip **OWASP 949110** (Inbound Anomaly Score Exceeded) only. JPEG/Excel bytes scored 105 and blocked photo uploads (2026-10-12), then the logo upload (2026-10-13). These are all of the app's uploads (`IFormFile` in HR28.Web controllers): add any new upload address here; the Cloudflare Managed Ruleset still applies, and the app checks and rebuilds every file |
| Security rules → Custom | `Challenge outside Maldives`: `(ip.src.country ne "MV")`, Managed Challenge (owner, 2026-10-12: users abroad pass once; adjust the country for a client elsewhere) |
| Security → Settings | Challenge Passage **16 hours** (covers the 12-hour session, so background calls don't hit a new challenge) |

Check after changes: from the PC `https://<domain>/cdn-cgi/trace` shows `loc=MV` and the sign-in page
answers 200; a test SQL-injection query string answers 403.

## Training site (tr.shamheed.com)

Owner decision 2026-10-15. Same droplet, separate copy: database `HR28Train` (login `hr28_train`,
no access to live; live login `hr28_app` has none to training), its own JWT key and services
`hr28-train-api` (127.0.0.1:5011) / `hr28-train-web` (127.0.0.1:5010, accounts `hr28tapi` /
`hr28tweb`), Nginx site `hr28-train` (same origin certificate and origin pulls). It uses the live
authorization-code key, so people sign in with their normal code (SMS codes and sign-ins are
separate). Every page shows a TRAINING strip (`App__EnvironmentLabel`) and every SMS starts with
"TRAINING:" (`App__SmsPrefix`) — `/etc/hr28/train-*.env`.

| File in `training/` | On the server |
|---|---|
| `setup-training.sh` | run once: accounts, env files, login, services, Nginx, installs the rest |
| `refresh-training.sh` | `/usr/local/sbin/hr28-train-refresh`: fresh live backup → restored as `HR28Train`, sign-in codes and devices removed, training restarted (everything done on training is lost) |
| `training-cleanup.sql` | `/usr/local/lib/hr28/training-cleanup.sql` (used by the refresh) |
| `hr28-train-*.service`, `hr28-train.nginx` | `/etc/systemd/system/`, `/etc/nginx/sites-available/hr28-train` |

Releases go PC → training → live (owner, 2026-10-15): `hr28-deploy training`, test on tr.shamheed.com, then `hr28-deploy live` installs exactly the staged release (`/var/lib/hr28/release-staged`).
Cloudflare: DNS `tr` → droplet IP, proxied; all zone rules (WAF, rate limit, outside-Maldives
challenge, origin pulls) apply. Training is not backed up nightly (refresh from live instead) and
not in `hr28-monitor`. Live was cleared of test influencers, pledges and encounters on 2026-10-15
(audited); a refresh therefore brings none — trainees create their own.

## Updates

On the PC: `dotnet publish` API and Web (`-c Release -r linux-x64 --self-contained false`),
copy to `/tmp/hr28-release/{api,web}` (and `migrate.sql` from
`dotnet ef migrations script <last on live> <new> --idempotent` when the schema changed), then:
1. `hr28-deploy training` — installs on tr.shamheed.com only (database changes on `HR28Train`);
   live is not touched. Test there.
2. `hr28-deploy live` — puts exactly that tested release on live (backup first, database
   changes, health check, automatic roll back). `hr28-deploy status` shows what each runs.
