#!/usr/bin/env bash
# RUN THIS YOURSELF in PuTTY: it asks for the CURRENT sa password, which only you know.
#   1. Changes the sa password to the new one in /etc/hr28/sql-admin.env.
#   2. Creates the database login hr28_app (read + write on HR28Db only) with the password
#      in /etc/hr28/api.env, so the API never uses sa.
# Only results are printed. Run as root after setup-accounts-and-secrets.sh.
set -euo pipefail
[ "$(id -u)" -eq 0 ] || { echo "Run as root."; exit 1; }

SQLCMD=/opt/mssql-tools18/bin/sqlcmd
source /etc/hr28/sql-admin.env
APP_PASSWORD="$(grep '^ConnectionStrings__DefaultConnection=' /etc/hr28/api.env | sed -E 's/.*Password=([^;]*);.*/\1/')"
[ -n "$APP_PASSWORD" ] || { echo "No app password in /etc/hr28/api.env."; exit 1; }

read -r -s -p "Current sa password: " CURRENT_SA; echo

# Passwords reach sqlcmd through the environment (not visible in the process list).
# The generated passwords contain only letters, digits and "-", so they are safe in SQL.
SQLCMDPASSWORD="$CURRENT_SA" "$SQLCMD" -S 127.0.0.1,1433 -U sa -C -b \
    -Q "ALTER LOGIN sa WITH PASSWORD = '${SA_PASSWORD}';" \
    && echo "sa password changed."

SQLCMDPASSWORD="$SA_PASSWORD" "$SQLCMD" -S 127.0.0.1,1433 -U sa -C -b -Q "
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = 'hr28_app')
    CREATE LOGIN hr28_app WITH PASSWORD = '${APP_PASSWORD}', CHECK_POLICY = ON, CHECK_EXPIRATION = OFF, DEFAULT_DATABASE = HR28Db;
ELSE
    ALTER LOGIN hr28_app WITH PASSWORD = '${APP_PASSWORD}';
" && echo "Login hr28_app ready."

SQLCMDPASSWORD="$SA_PASSWORD" "$SQLCMD" -S 127.0.0.1,1433 -U sa -C -b -d HR28Db -Q "
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = 'hr28_app')
    CREATE USER hr28_app FOR LOGIN hr28_app;
ALTER ROLE db_datareader ADD MEMBER hr28_app;
ALTER ROLE db_datawriter ADD MEMBER hr28_app;
" && echo "hr28_app can read and write HR28Db (nothing else)."

unset CURRENT_SA SA_PASSWORD APP_PASSWORD
