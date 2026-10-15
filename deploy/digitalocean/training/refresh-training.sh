#!/usr/bin/env bash
# Resets the TRAINING site to a fresh copy of live (owner, 2026-10-15: on request, e.g. before
# each training batch). Takes a new live backup, restores it as HR28Train, removes sign-in
# codes and devices, gives the training login access, and restarts training.
# Installed as /usr/local/sbin/hr28-train-refresh. Everything done on training is lost.
set -euo pipefail
[ "$(id -u)" -eq 0 ] || { echo "Run as root."; exit 1; }

SQLCMD=/opt/mssql-tools18/bin/sqlcmd
RESTORE_IN=/var/opt/mssql/backups/train-restore.bak
RESTORE_ON_HOST=/opt/mssql-data/backups/train-restore.bak

echo "1/5 Fresh backup of live..."
/usr/local/sbin/hr28-backup >/dev/null
LATEST="$(ls -t /var/backups/hr28/HR28Db-*.bak.gz | head -1)"
echo "    using $(basename "$LATEST")"

echo "2/5 Stopping the training site..."
systemctl stop hr28-train-web hr28-train-api 2>/dev/null || true

echo "3/5 Restoring the copy as HR28Train..."
gunzip -c "$LATEST" > "$RESTORE_ON_HOST"
chown 10001:0 "$RESTORE_ON_HOST"; chmod 640 "$RESTORE_ON_HOST"
trap 'rm -f "$RESTORE_ON_HOST"' EXIT

source /etc/hr28/sql-admin.env
export SQLCMDPASSWORD="$SA_PASSWORD"
unset SA_PASSWORD

files=$("$SQLCMD" -S 127.0.0.1,1433 -U sa -C -I -b -h -1 -W -Q "SET NOCOUNT ON; RESTORE FILELISTONLY FROM DISK = N'${RESTORE_IN}';" | awk '{print $1" "$3}')
DATA_NAME=$(echo "$files" | awk '$2=="D"{print $1; exit}')
LOG_NAME=$(echo "$files" | awk '$2=="L"{print $1; exit}')
[ -n "$DATA_NAME" ] && [ -n "$LOG_NAME" ] || { echo "Could not read the backup's file list."; exit 1; }

"$SQLCMD" -S 127.0.0.1,1433 -U sa -C -I -b -Q "
IF DB_ID('HR28Train') IS NOT NULL ALTER DATABASE HR28Train SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
RESTORE DATABASE HR28Train FROM DISK = N'${RESTORE_IN}' WITH REPLACE,
    MOVE N'${DATA_NAME}' TO N'/var/opt/mssql/data/HR28Train.mdf',
    MOVE N'${LOG_NAME}' TO N'/var/opt/mssql/data/HR28Train_log.ldf';
ALTER DATABASE HR28Train SET MULTI_USER;" >/dev/null

echo "4/5 Training-only clean-up and access..."
"$SQLCMD" -S 127.0.0.1,1433 -U sa -C -I -b -d HR28Train -i /usr/local/lib/hr28/training-cleanup.sql
unset SQLCMDPASSWORD

echo "5/5 Starting the training site..."
systemctl start hr28-train-api
for i in $(seq 1 30); do
    curl -fsS http://127.0.0.1:5011/health >/dev/null 2>&1 && break
    sleep 2
done
curl -fsS http://127.0.0.1:5011/health >/dev/null 2>&1 || { echo "Training API did not start: journalctl -u hr28-train-api"; exit 1; }
systemctl start hr28-train-web
sleep 3
code="$(curl -s -o /dev/null -w '%{http_code}' -H 'X-Forwarded-Proto: https' http://127.0.0.1:5010/Auth/Login)"
[ "$code" = "200" ] || { echo "Training web app answered $code: journalctl -u hr28-train-web"; exit 1; }

echo "Training is a fresh copy of live as of $(TZ=Indian/Maldives date '+%d %b %Y %H:%M') (Maldives time)."
