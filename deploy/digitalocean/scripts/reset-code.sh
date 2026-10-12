#!/usr/bin/env bash
# Gives a user a new authorization code from the server (e.g. the first Administrator after
# new production keys). Prints the code ONCE; old code and remembered devices stop working.
# Installed as /usr/local/sbin/hr28-reset-code. Usage (as root): hr28-reset-code <ID card>
set -euo pipefail
[ "$(id -u)" -eq 0 ] || { echo "Run as root."; exit 1; }
[ $# -eq 1 ] || { echo "Usage: hr28-reset-code <ID card number>"; exit 1; }

exec systemd-run --quiet --wait --pipe --collect \
    -p User=hr28api -p EnvironmentFile=/etc/hr28/api.env \
    -p Environment=Logging__LogLevel__Default=Warning -p Environment=Logging__LogLevel__Microsoft=Warning \
    -p WorkingDirectory=/var/www/hr28-api \
    /usr/bin/dotnet HR28.API.dll reset-code "$1"
