#!/usr/bin/env bash
# Nightly HR28 database backup (installed as /usr/local/sbin/hr28-backup, run by cron).
# Full backup with checksum, verified, compressed, kept 14 days in /var/backups/hr28 (root only).
# Optional copy off the server: put SPACES_REMOTE / SPACES_BUCKET in /etc/hr28/backup.env
# and configure rclone for DigitalOcean Spaces.
set -euo pipefail
# Limited SQL login (hr28_ops: back up / restore / update the HR28 databases; ASVS L5).
source /etc/hr28/sql-ops.env

SQLCMD=/opt/mssql-tools18/bin/sqlcmd
STAMP="$(date -u +%Y%m%d-%H%M%S)"
NAME="HR28Db-${STAMP}.bak"
IN_CONTAINER="/var/opt/mssql/backups/${NAME}"
ON_HOST="/opt/mssql-data/backups/${NAME}"
KEEP_DAYS="${KEEP_DAYS:-14}"
DEST=/var/backups/hr28

install -d -m 700 -o root -g root "$DEST"
install -d -m 770 -o 10001 -g 0 /opt/mssql-data/backups

export SQLCMDPASSWORD="$OPS_PASSWORD"
"$SQLCMD" -S 127.0.0.1,1433 -U "$OPS_USER" -C -b -Q "BACKUP DATABASE HR28Db TO DISK = N'${IN_CONTAINER}' WITH CHECKSUM, INIT, NAME = N'HR28Db nightly ${STAMP}';"
"$SQLCMD" -S 127.0.0.1,1433 -U "$OPS_USER" -C -b -Q "RESTORE VERIFYONLY FROM DISK = N'${IN_CONTAINER}' WITH CHECKSUM;"
unset SQLCMDPASSWORD OPS_PASSWORD

gzip -c "$ON_HOST" > "${DEST}/${NAME}.gz"
rm -f "$ON_HOST"
chmod 600 "${DEST}/${NAME}.gz"

find "$DEST" -name 'HR28Db-*.bak.gz' -mtime +"$KEEP_DAYS" -delete

if [ -f /etc/hr28/backup.env ] && command -v rclone >/dev/null 2>&1; then
    source /etc/hr28/backup.env
    rclone copy "${DEST}/${NAME}.gz" "${SPACES_REMOTE}:${SPACES_BUCKET}/hr28/" --quiet
fi

echo "$(date -u +%FT%TZ) backup ok: ${NAME}.gz ($(du -h "${DEST}/${NAME}.gz" | cut -f1))"
