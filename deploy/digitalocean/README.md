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
| `scripts/backup-db.sh` + `hr28-backup.cron` | `/usr/local/sbin/hr28-backup`, `/etc/cron.d/hr28-backup` | nightly 02:30 MVT, 14 days |
| `scripts/deploy-release.sh` | `/usr/local/sbin/hr28-deploy` | backup, update, health check, roll back |
| `scripts/save-origin-cert.sh` | `/usr/local/sbin/hr28-origin-cert` | owner pastes the Cloudflare Origin Certificate + key |
| `scripts/cloudflare-only.sh` + `hr28-cloudflare-only.cron` | `/usr/local/sbin/hr28-cloudflare-only`, `/etc/cron.d/` | ports 80/443 from Cloudflare only + Nginx real-IP list; monthly |
| `ssh/10-hr28.conf` | `/etc/ssh/sshd_config.d/` | keys only, 3 tries, no X11 |
| `fail2ban/hr28.local` | `/etc/fail2ban/jail.d/` | 5 failed SSH tries in 10 min = banned 1 day |
| `scripts/sms-codes.sh` | `/usr/local/sbin/hr28-sms-codes` | TEMPORARY: read codes while SMS is not connected |
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
9. SMS provider (sign-in does not work in production until it is connected).
   **Temporary test mode (owner, 2026-10-12, until 2026-10-19):** `/var/lib/hr28/sms-outbox`
   (hr28api, 700) and in `api.env` `Sms__ServerFile__Folder=/var/lib/hr28/sms-outbox`,
   `Sms__ServerFile__Until=<last day>` (≤ 31 days ahead; after it, sending refuses again).
   Read codes as root with `hr28-sms-codes`. When SMS works: delete both lines, the folder
   and `hr28-sms-codes`, restart `hr28-api`.
10. Hardening (done 2026-10-12): `ssh/10-hr28.conf` (`sshd -t`, reload, test a NEW connection
    first); fail2ban (`hr28.local`; Ubuntu 24.04's unit is `ssh.service`); `hr28-cloudflare-only`
    (+ cron); Authenticated Origin Pulls: CA at `/etc/ssl/hr28/cloudflare-origin-pull-ca.pem`, turn
    it on in Cloudflare first, then the `ssl_verify_client` lines in `hr28.conf`;
    `Logging__LogLevel__Default=Warning` in both `.env` files. Cloudflare Pro: Managed + OWASP WAF,
    Super Bot Fight Mode, rate limit on `/Auth/`. Still to do: DigitalOcean backups / off-site copies.

## Updates

On the PC: `dotnet publish` API and Web (`-c Release -r linux-x64 --self-contained false`),
copy to `/tmp/hr28-release/{api,web}` (and `migrate.sql` from
`dotnet ef migrations script --idempotent` when the schema changed), then `hr28-deploy`.
