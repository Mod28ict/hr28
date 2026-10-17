#!/usr/bin/env bash
# One-time setup of the TRAINING site (owner, 2026-10-15): https://tr.shamheed.com on the same
# droplet, fully separate from live: database HR28Train (login hr28_train, no access to live),
# its own JWT key and services (hr28-train-api 127.0.0.1:5011, hr28-train-web 127.0.0.1:5010),
# a TRAINING strip on every page and "TRAINING:" on every SMS. Uses the live authorization-code
# key so people sign in with their normal code. Safe to re-run (keeps keys and accounts).
# Then: hr28-train-refresh fills it with a fresh copy of live.
set -euo pipefail
[ "$(id -u)" -eq 0 ] || { echo "Run as root."; exit 1; }

HERE="$(cd "$(dirname "$0")" && pwd)"
SQLCMD=/opt/mssql-tools18/bin/sqlcmd
DOMAIN=tr.shamheed.com

# Accounts and folders
for u in hr28tapi hr28tweb; do
    id -u "$u" >/dev/null 2>&1 || useradd --system --no-create-home --shell /usr/sbin/nologin "$u"
done
install -d -m 755 /var/www/hr28-train-api /var/www/hr28-train
install -d -m 700 -o hr28tweb -g hr28tweb /var/lib/hr28/train-web-keys

# Secrets (generated once, kept on re-run). Letters, digits and "-" only.
if [ ! -f /etc/hr28/train-api.env ]; then
    TRAIN_DB_PASSWORD="$(openssl rand -base64 36 | tr -dc 'A-Za-z0-9' | head -c 32)-Tr1"
    CODE_KEY="$(grep '^Security__AuthorizationCodeKey=' /etc/hr28/api.env | cut -d= -f2-)"
    [ -n "$CODE_KEY" ] || { echo "No authorization-code key in /etc/hr28/api.env."; exit 1; }
    umask 027
    cat > /etc/hr28/train-api.env <<EOF
# TRAINING site API (written by setup-training.sh; never commit).
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://127.0.0.1:5011
ConnectionStrings__DefaultConnection=Server=127.0.0.1,1433;Database=HR28Train;User Id=hr28_train;Password=${TRAIN_DB_PASSWORD};Encrypt=True;TrustServerCertificate=True
Jwt__Key=$(openssl rand -base64 48 | tr -d '\n')
Security__AuthorizationCodeKey=${CODE_KEY}
App__SignInUrl=https://${DOMAIN}
App__SmsPrefix=TRAINING:
Logging__LogLevel__Default=Warning
EOF
    chown root:hr28tapi /etc/hr28/train-api.env; chmod 640 /etc/hr28/train-api.env
    unset TRAIN_DB_PASSWORD CODE_KEY
    echo "Created /etc/hr28/train-api.env (new JWT key, live code key)."
fi

if [ ! -f /etc/hr28/train-web.env ]; then
    umask 027
    cat > /etc/hr28/train-web.env <<'EOF'
# TRAINING site web app (written by setup-training.sh).
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://127.0.0.1:5010
ApiSettings__BaseUrl=http://127.0.0.1:5011/api/
ReverseProxy__BehindLocalProxy=true
DataProtection__KeysPath=/var/lib/hr28/train-web-keys
App__EnvironmentLabel="Practice only. Nothing you do here changes the live system."
Logging__LogLevel__Default=Warning
EOF
    chown root:hr28tweb /etc/hr28/train-web.env; chmod 640 /etc/hr28/train-web.env
    echo "Created /etc/hr28/train-web.env."
fi

# Database login for training only (the database itself is made by hr28-train-refresh).
source /etc/hr28/sql-admin.env
TRAIN_DB_PASSWORD="$(grep '^ConnectionStrings__DefaultConnection=' /etc/hr28/train-api.env | sed -E 's/.*Password=([^;]*);.*/\1/')"
SQLCMDPASSWORD="$SA_PASSWORD" "$SQLCMD" -S 127.0.0.1,1433 -U "${SA_USER:-sa}" -C -I -b -Q "
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = 'hr28_train')
    CREATE LOGIN hr28_train WITH PASSWORD = '${TRAIN_DB_PASSWORD}', CHECK_POLICY = ON, CHECK_EXPIRATION = OFF;
ELSE
    ALTER LOGIN hr28_train WITH PASSWORD = '${TRAIN_DB_PASSWORD}';"
unset SA_PASSWORD TRAIN_DB_PASSWORD
echo "Login hr28_train ready."

# App files: the same release as live.
rsync -a --delete /var/www/hr28-api/ /var/www/hr28-train-api/
rsync -a --delete /var/www/hr28/ /var/www/hr28-train/
chown -R root:root /var/www/hr28-train-api /var/www/hr28-train
chmod -R u=rwX,go=rX /var/www/hr28-train-api /var/www/hr28-train

# Services, Nginx site, refresh command
install -m 644 "$HERE/hr28-train-api.service" /etc/systemd/system/hr28-train-api.service
install -m 644 "$HERE/hr28-train-web.service" /etc/systemd/system/hr28-train-web.service
sed -i 's/\r$//' /etc/systemd/system/hr28-train-*.service
systemctl daemon-reload
systemctl enable -q hr28-train-api hr28-train-web

install -m 644 "$HERE/hr28-train.nginx" /etc/nginx/sites-available/hr28-train
sed -i 's/\r$//' /etc/nginx/sites-available/hr28-train
ln -sf /etc/nginx/sites-available/hr28-train /etc/nginx/sites-enabled/hr28-train
nginx -t && systemctl reload nginx

install -m 750 "$HERE/refresh-training.sh" /usr/local/sbin/hr28-train-refresh
install -m 640 "$HERE/training-cleanup.sql" /usr/local/lib/hr28/training-cleanup.sql
sed -i 's/\r$//' /usr/local/sbin/hr28-train-refresh /usr/local/lib/hr28/training-cleanup.sql

echo "Training site set up. Next: hr28-train-refresh (copies live into training and starts it)."
