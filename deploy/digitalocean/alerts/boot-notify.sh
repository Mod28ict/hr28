#!/usr/bin/env bash
# After every server start: wait (up to 5 minutes) for HR28 to answer, then tell the
# Administrators by SMS. Installed as /usr/local/sbin/hr28-boot-notify (hr28-boot-notify.service).
# "hr28-boot-notify dry-run" shows what would be sent, without sending.
set -uo pipefail
# shellcheck disable=SC1091
. /usr/local/lib/hr28/sms-lib.sh

ok=no
for _ in $(seq 1 60); do
    if curl -fsS -m 5 http://127.0.0.1:5001/health >/dev/null 2>&1 &&
       [ "$(curl -s -o /dev/null -m 5 -w '%{http_code}' -H 'X-Forwarded-Proto: https' http://127.0.0.1:5000/Auth/Login)" = "200" ]; then
        ok=yes
        break
    fi
    [ "${1:-}" = "dry-run" ] && break
    sleep 5
done

booted=$(TZ=Indian/Maldives date -d "$(uptime -s) UTC" +%H:%M 2>/dev/null || mvt_time)
if [ "$ok" = yes ]; then
    msg="$SITE_NAME: the server restarted at $booted (Maldives time) and is back online."
else
    msg="$SITE_NAME ALERT: the server restarted at $booted (Maldives time) but the app is NOT responding. Please contact technical support."
fi
notify_admins "$msg" "${1:-}" | tee -a /var/log/hr28-alerts.log
