#!/usr/bin/env bash
# Creates the service accounts, folders and secret files for HR28. Safe to run again:
# existing secrets are KEPT (re-running never changes keys, which would sign everyone out
# or break every authorization code). Run as root.
#
#   /etc/hr28/sql-admin.env  root only (600)          new sa password, for maintenance scripts
#   /etc/hr28/api.env        root:hr28api (640)       API settings + generated keys
#   /etc/hr28/web.env        root:hr28web (640)       web settings (no secrets)
#   /var/lib/hr28/web-keys   hr28web (700)            sign-in keys of the web app
set -euo pipefail
[ "$(id -u)" -eq 0 ] || { echo "Run as root."; exit 1; }

DOMAIN="${DOMAIN:-shamheed.com}"

# Random secrets without characters that break a connection string (; = ' ").
rand_key()      { openssl rand -base64 "$1" | tr -d '\n'; }
rand_password() { echo "$(openssl rand -base64 30 | tr -dc 'A-Za-z0-9' | head -c 28)Aa1-"; }

for account in hr28api hr28web; do
    id "$account" >/dev/null 2>&1 || useradd --system --no-create-home --shell /usr/sbin/nologin "$account"
done

install -d -m 755 -o root -g root /etc/hr28
install -d -m 755 -o root -g root /var/lib/hr28
install -d -m 700 -o hr28web -g hr28web /var/lib/hr28/web-keys

if [ ! -f /etc/hr28/sql-admin.env ]; then
    ( umask 077; printf 'SA_PASSWORD=%s\n' "$(rand_password)" > /etc/hr28/sql-admin.env )
    chmod 600 /etc/hr28/sql-admin.env
    echo "Created /etc/hr28/sql-admin.env (new sa password, not yet applied to SQL Server)."
else
    echo "Kept /etc/hr28/sql-admin.env."
fi

if [ ! -f /etc/hr28/api.env ]; then
    (
        umask 027
        {
            echo "ASPNETCORE_ENVIRONMENT=Production"
            echo "ASPNETCORE_URLS=http://127.0.0.1:5001"
            echo "ConnectionStrings__DefaultConnection=Server=127.0.0.1,1433;Database=HR28Db;User Id=hr28_app;Password=$(rand_password);Encrypt=True;TrustServerCertificate=True"
            echo "Jwt__Key=$(rand_key 48)"
            echo "Security__AuthorizationCodeKey=$(rand_key 32)"
            echo "App__SignInUrl=https://${DOMAIN}"
        } > /etc/hr28/api.env
    )
    chown root:hr28api /etc/hr28/api.env
    chmod 640 /etc/hr28/api.env
    echo "Created /etc/hr28/api.env (new JWT key, new authorization-code key, app database password)."
else
    echo "Kept /etc/hr28/api.env."
fi

if [ ! -f /etc/hr28/web.env ]; then
    {
        echo "ASPNETCORE_ENVIRONMENT=Production"
        echo "ASPNETCORE_URLS=http://127.0.0.1:5000"
        echo "ApiSettings__BaseUrl=http://127.0.0.1:5001/api/"
        echo "ReverseProxy__BehindLocalProxy=true"
        echo "DataProtection__KeysPath=/var/lib/hr28/web-keys"
    } > /etc/hr28/web.env
    chown root:hr28web /etc/hr28/web.env
    chmod 640 /etc/hr28/web.env
    echo "Created /etc/hr28/web.env."
else
    echo "Kept /etc/hr28/web.env."
fi

echo "Done. Secret values are not shown."
