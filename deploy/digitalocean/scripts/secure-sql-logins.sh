#!/usr/bin/env bash
# One-time (ASVS review L5, 2026-10-17): stop using the built-in SQL "sa" login.
#
#   hr28_ops    everyday scripts (backup, deploy, training refresh, alert SMS look-ups):
#               dbcreator + db_owner of HR28Db and HR28Train only. Can't change server
#               settings, create logins or run operating-system commands.
#               -> /etc/hr28/sql-ops.env    (OPS_USER, OPS_PASSWORD; root only)
#   hr28_admin  setup work only (new training site, new logins): sysadmin.
#               -> /etc/hr28/sql-admin.env  (SA_USER, SA_PASSWORD; root only)
#   sa          disabled at the end. hr28_admin can switch it back on if ever needed:
#               ALTER LOGIN sa ENABLE;
#
# New random passwords are made here and never printed. Safe to run again: existing logins
# get new passwords, and sa stays disabled.
set -euo pipefail
[ "$(id -u)" -eq 0 ] || { echo "Run as root."; exit 1; }

SQLCMD=/opt/mssql-tools18/bin/sqlcmd
rand_password() { echo "Hr28$(openssl rand -base64 30 | tr -dc 'A-Za-z0-9' | head -c 32)!"; }

# Current administrator login (sa before the first run, hr28_admin after).
source /etc/hr28/sql-admin.env
CURRENT_USER="${SA_USER:-sa}"
CURRENT_PASSWORD="$SA_PASSWORD"
unset SA_PASSWORD

OPS_PASSWORD="$(rand_password)"
ADMIN_PASSWORD="$(rand_password)"

work="$(mktemp -d)"
chmod 700 "$work"
trap 'rm -rf "$work"' EXIT

cat > "$work/logins.sql" <<SQL
SET NOCOUNT ON;
IF SUSER_ID(N'hr28_ops') IS NULL
    CREATE LOGIN hr28_ops WITH PASSWORD = N'${OPS_PASSWORD}', CHECK_POLICY = ON, DEFAULT_DATABASE = HR28Db;
ELSE
    ALTER LOGIN hr28_ops WITH PASSWORD = N'${OPS_PASSWORD}';
ALTER SERVER ROLE dbcreator ADD MEMBER hr28_ops;

IF SUSER_ID(N'hr28_admin') IS NULL
    CREATE LOGIN hr28_admin WITH PASSWORD = N'${ADMIN_PASSWORD}', CHECK_POLICY = ON;
ELSE
    ALTER LOGIN hr28_admin WITH PASSWORD = N'${ADMIN_PASSWORD}';
ALTER SERVER ROLE sysadmin ADD MEMBER hr28_admin;
GO
USE HR28Db;
IF USER_ID(N'hr28_ops') IS NULL CREATE USER hr28_ops FOR LOGIN hr28_ops;
ALTER ROLE db_owner ADD MEMBER hr28_ops;
GO
IF DB_ID(N'HR28Train') IS NOT NULL
    EXEC (N'USE HR28Train;
            IF USER_ID(N''hr28_ops'') IS NULL CREATE USER hr28_ops FOR LOGIN hr28_ops;
            ALTER ROLE db_owner ADD MEMBER hr28_ops;');
GO
SQL

echo "1/4 Creating the logins..."
SQLCMDPASSWORD="$CURRENT_PASSWORD" "$SQLCMD" -S 127.0.0.1,1433 -U "$CURRENT_USER" -C -I -b -i "$work/logins.sql" >/dev/null

echo "2/4 Checking them..."
ops_check() {   # ops_check <database>: dbcreator, not sysadmin, owner of that database
    SQLCMDPASSWORD="$OPS_PASSWORD" "$SQLCMD" -S 127.0.0.1,1433 -U hr28_ops -C -I -b -h -1 -W -d "$1" -Q "SET NOCOUNT ON;
SELECT CONCAT(IS_SRVROLEMEMBER('dbcreator'), IS_SRVROLEMEMBER('sysadmin'), IS_ROLEMEMBER('db_owner'));"
}
[ "$(ops_check HR28Db)" = "101" ] || { echo "hr28_ops doesn't have the expected rights on HR28Db. sa was NOT disabled."; exit 1; }
if [ -n "$(SQLCMDPASSWORD="$CURRENT_PASSWORD" "$SQLCMD" -S 127.0.0.1,1433 -U "$CURRENT_USER" -C -I -h -1 -W -Q "SET NOCOUNT ON; SELECT DB_ID('HR28Train');" | tr -d ' \r' | grep -v NULL)" ]; then
    [ "$(ops_check HR28Train)" = "101" ] || { echo "hr28_ops doesn't have the expected rights on HR28Train. sa was NOT disabled."; exit 1; }
fi
admin_check=$(SQLCMDPASSWORD="$ADMIN_PASSWORD" "$SQLCMD" -S 127.0.0.1,1433 -U hr28_admin -C -I -b -h -1 -W -Q "SET NOCOUNT ON; SELECT IS_SRVROLEMEMBER('sysadmin');")
[ "$admin_check" = "1" ] || { echo "hr28_admin isn't an administrator. sa was NOT disabled."; exit 1; }

echo "3/4 Saving the new passwords (root only)..."
( umask 077
  printf 'OPS_USER=hr28_ops\nOPS_PASSWORD=%s\n' "$OPS_PASSWORD" > /etc/hr28/sql-ops.env.new
  printf 'SA_USER=hr28_admin\nSA_PASSWORD=%s\n' "$ADMIN_PASSWORD" > /etc/hr28/sql-admin.env.new )
chown root:root /etc/hr28/sql-ops.env.new /etc/hr28/sql-admin.env.new
chmod 600 /etc/hr28/sql-ops.env.new /etc/hr28/sql-admin.env.new
mv /etc/hr28/sql-ops.env.new /etc/hr28/sql-ops.env
mv /etc/hr28/sql-admin.env.new /etc/hr28/sql-admin.env

echo "4/4 Switching off sa..."
SQLCMDPASSWORD="$ADMIN_PASSWORD" "$SQLCMD" -S 127.0.0.1,1433 -U hr28_admin -C -I -b -Q "SET NOCOUNT ON; ALTER LOGIN sa DISABLE;" >/dev/null
unset OPS_PASSWORD ADMIN_PASSWORD CURRENT_PASSWORD

echo "Done: everyday scripts use hr28_ops, setup scripts hr28_admin, and sa is disabled."
