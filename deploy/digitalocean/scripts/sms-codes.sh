#!/usr/bin/env bash
# TEMPORARY SMS test mode: shows the messages the live server "sent" in the last 15 minutes
# (newest last) and deletes ones older than an hour. Installed as /usr/local/sbin/hr28-sms-codes.
# Run as root. Remove with the test mode once a real SMS provider is connected.
set -euo pipefail
[ "$(id -u)" -eq 0 ] || { echo "Run as root."; exit 1; }

DIR=/var/lib/hr28/sms-outbox
UNTIL="$(grep -s '^Sms__ServerFile__Until=' /etc/hr28/api.env | cut -d= -f2- || true)"
[ -n "$UNTIL" ] || { echo "The SMS test mode is off."; exit 0; }
[ -d "$DIR" ] || { echo "No messages folder ($DIR)."; exit 1; }

find "$DIR" -type f -name '*.txt' -mmin +60 -delete
echo "SMS test mode on until ${UNTIL} (Maldives). Messages from the last 15 minutes:"
found=0
while IFS= read -r f; do
    echo "----"
    cat "$f"
    found=1
done < <(find "$DIR" -type f -name '*.txt' -mmin -15 | sort)
[ "$found" -eq 1 ] || echo "(none yet: press 'Send verification code' on the sign-in page, then run this again)"
