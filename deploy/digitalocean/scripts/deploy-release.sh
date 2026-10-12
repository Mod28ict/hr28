#!/usr/bin/env bash
# Installs a new HR28 release on the server and rolls back if it doesn't start.
#
# Upload first (from the development PC):
#   /tmp/hr28-release/api/   = output of: dotnet publish HR28.API -c Release -r linux-x64 --self-contained false
#   /tmp/hr28-release/web/   = output of: dotnet publish HR28.Web -c Release -r linux-x64 --self-contained false
# Optional: /tmp/hr28-release/migrate.sql = dotnet ef migrations script --idempotent (run with sa first).
# Then run as root: hr28-deploy
set -euo pipefail
[ "$(id -u)" -eq 0 ] || { echo "Run as root."; exit 1; }

REL=/tmp/hr28-release
STAMP="$(date -u +%Y%m%d-%H%M%S)"
KEEP=/var/www/.previous/${STAMP}

[ -f "$REL/api/HR28.API.dll" ] && [ -f "$REL/web/HR28.Web.dll" ] || { echo "No release in $REL."; exit 1; }

# Never deploy development settings; secrets come from /etc/hr28/*.env only.
rm -f "$REL"/api/appsettings.Development.json "$REL"/web/appsettings.Development.json

echo "Backing up the database before the update..."
/usr/local/sbin/hr28-backup

if [ -f "$REL/migrate.sql" ]; then
    echo "Applying database changes..."
    source /etc/hr28/sql-admin.env
    SQLCMDPASSWORD="$SA_PASSWORD" /opt/mssql-tools18/bin/sqlcmd -S 127.0.0.1,1433 -U sa -C -I -b -d HR28Db -i "$REL/migrate.sql"
    unset SA_PASSWORD
fi

install -d -m 700 "$KEEP"
systemctl stop hr28-web hr28-api
cp -a /var/www/hr28-api "$KEEP/api"
cp -a /var/www/hr28 "$KEEP/web"

rsync -a --delete "$REL/api/" /var/www/hr28-api/
rsync -a --delete "$REL/web/" /var/www/hr28/
chown -R root:root /var/www/hr28-api /var/www/hr28
chmod -R u=rwX,go=rX /var/www/hr28-api /var/www/hr28

systemctl start hr28-api
for i in $(seq 1 30); do
    curl -fsS http://127.0.0.1:5001/health >/dev/null 2>&1 && break
    sleep 2
done

if ! curl -fsS http://127.0.0.1:5001/health >/dev/null 2>&1; then
    echo "API did not start: rolling back."
    systemctl stop hr28-api
    rsync -a --delete "$KEEP/api/" /var/www/hr28-api/
    rsync -a --delete "$KEEP/web/" /var/www/hr28/
    systemctl start hr28-api hr28-web
    journalctl -u hr28-api -n 30 --no-pager
    exit 1
fi

systemctl start hr28-web
sleep 3
# As Nginx calls it: the app refuses plain-HTTP requests (secure cookies).
code="$(curl -s -o /dev/null -w '%{http_code}' -H 'X-Forwarded-Proto: https' http://127.0.0.1:5000/Auth/Login)"
[ "$code" = "200" ] || { echo "Web app answered $code: check 'journalctl -u hr28-web'."; exit 1; }

rm -rf "$REL"
find /var/www/.previous -mindepth 1 -maxdepth 1 -type d -mtime +30 -exec rm -rf {} +
echo "Release ${STAMP} is live. Previous version kept in ${KEEP}."
