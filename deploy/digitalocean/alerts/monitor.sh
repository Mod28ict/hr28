#!/usr/bin/env bash
# Every 5 minutes (cron): checks the app, API, database, Nginx, disk, last night's backup and
# security warnings (30+ failed sign-ins or refused requests in 15 minutes).
# A problem seen twice in a row: one SMS to the Administrators; all clear again: one SMS.
# Installed as /usr/local/sbin/hr28-monitor. "hr28-monitor status" prints the checks only.
set -uo pipefail
# shellcheck disable=SC1091
. /usr/local/lib/hr28/sms-lib.sh

STATE=/var/lib/hr28/monitor.state
DISK_LIMIT=85
SECURITY_LIMIT=30

problems=()
curl -fsS -m 10 http://127.0.0.1:5001/health 2>/dev/null | grep -q '"ok"' || problems+=("API or database not answering")
[ "$(curl -s -o /dev/null -m 10 -w '%{http_code}' -H 'X-Forwarded-Proto: https' http://127.0.0.1:5000/Auth/Login)" = "200" ] || problems+=("web app not answering")
systemctl is-active --quiet nginx || problems+=("Nginx stopped")
[ "$(docker inspect -f '{{.State.Running}}' sqlserver 2>/dev/null)" = "true" ] || problems+=("SQL Server stopped")
disk=$(df --output=pcent / | tail -1 | tr -dc '0-9')
[ "${disk:-0}" -lt "$DISK_LIMIT" ] || problems+=("disk ${disk}% full")
[ -n "$(find /var/backups/hr28 -name 'HR28Db-*.bak.gz' -mmin -1600 2>/dev/null | head -1)" ] || problems+=("no database backup in the last 26 hours")
# Wrong or unknown sign-in codes and refused requests (the API's "SECURITY:" log lines,
# ASVS review M2): many in 15 minutes looks like someone trying to get in.
security=$(journalctl -u hr28-api --since "-15 min" --no-pager -q 2>/dev/null | grep -c "SECURITY:")
[ "${security:-0}" -lt "$SECURITY_LIMIT" ] || problems+=("many failed sign-ins or refused requests (check the Audit Trail)")

summary=""
for p in "${problems[@]}"; do summary+="${summary:+; }$p"; done

if [ "${1:-}" = "status" ]; then
    echo "disk ${disk}%; security warnings (15 min): ${security:-0}; problems: ${summary:-none}"
    exit 0
fi

# Right after a start the services are still coming up: hr28-boot-notify reports that.
[ "$(cut -d. -f1 /proc/uptime)" -lt 600 ] && exit 0

mkdir -p /var/lib/hr28
prev_seen=""
prev_alerted=""
# shellcheck disable=SC1090
[ -f "$STATE" ] && . "$STATE"

alerted="$prev_alerted"
if [ -n "$summary" ] && [ "$summary" = "$prev_seen" ] && [ "$summary" != "$prev_alerted" ]; then
    notify_admins "$SITE_NAME ALERT ($(mvt_time) Maldives time): $summary." >> /var/log/hr28-alerts.log
    alerted="$summary"
elif [ -z "$summary" ] && [ -n "$prev_alerted" ]; then
    notify_admins "$SITE_NAME: all checks are OK again ($(mvt_time) Maldives time)." >> /var/log/hr28-alerts.log
    alerted=""
fi

printf 'prev_seen=%q\nprev_alerted=%q\n' "$summary" "$alerted" > "$STATE"
