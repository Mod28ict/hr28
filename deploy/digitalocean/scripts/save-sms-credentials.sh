#!/usr/bin/env bash
# RUN THIS YOURSELF in PuTTY: stores the Dhiraagu Bulk SMS login for HR28 in /etc/hr28/sms.env
# (root:hr28api, 640). The password is never shown. Installed as /usr/local/sbin/hr28-sms-credentials.
set -euo pipefail
[ "$(id -u)" -eq 0 ] || { echo "Run as root."; exit 1; }

FILE=/etc/hr28/sms.env
read -r -p "Dhiraagu Bulk SMS username: " SMS_USER
read -r -s -p "Dhiraagu Bulk SMS password (not shown): " SMS_PASS; echo
[ -n "$SMS_USER" ] && [ -n "$SMS_PASS" ] || { echo "Username and password are both needed. Nothing saved."; exit 1; }
case "$SMS_USER$SMS_PASS" in *$'\n'*|*'"'*) echo "Line breaks and double quotes are not supported. Nothing saved."; exit 1;; esac

umask 077
{
    echo "# Dhiraagu Bulk SMS (partner XML API). Written by hr28-sms-credentials; never commit."
    echo "DhiraaguSms__Endpoint=https://bulksms.dhiraagu.com.mv/partners/xmlMessage.jsp"
    echo "DhiraaguSms__Username=$SMS_USER"
    echo "DhiraaguSms__Password=$SMS_PASS"
} > "$FILE.new"
chown root:hr28api "$FILE.new"; chmod 640 "$FILE.new"; mv "$FILE.new" "$FILE"
unset SMS_PASS
echo "Saved in $FILE. Next: hr28-sms-test 9607XXXXXX (your own number)."
