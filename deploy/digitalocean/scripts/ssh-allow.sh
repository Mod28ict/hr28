#!/usr/bin/env bash
# Who may connect to the server by SSH (ASVS review L4, 2026-10-17). Installed as
# /usr/local/sbin/hr28-ssh-allow.
#
#   hr28-ssh-allow                    show the addresses allowed
#   hr28-ssh-allow lock <address>     SSH only from <address> and the DigitalOcean web console
#   hr28-ssh-allow add <address>      allow another address (e.g. your new office or home address)
#   hr28-ssh-allow remove <address>   stop allowing an address
#   hr28-ssh-allow open               SSH from anywhere again (undo)
#
# Locked out (your address changed)? Open the droplet in the DigitalOcean website,
# Access -> Droplet Console (or Recovery Console), and run: hr28-ssh-allow add <new address>
# Your current address: search "what is my IP" in a browser.
set -euo pipefail
[ "$(id -u)" -eq 0 ] || { echo "Run as root."; exit 1; }

# DigitalOcean's Droplet Console connects from these (seen in the server's SSH log).
DO_CONSOLE="162.243.190.66 162.243.188.66 198.211.111.194"
TAG="hr28-ssh"

valid() { [[ "$1" =~ ^[0-9]{1,3}(\.[0-9]{1,3}){3}$ || "$1" =~ ^[0-9a-fA-F:]+$ ]]; }

allow() { ufw allow proto tcp from "$1" to any port 22 comment "$TAG ${2:-}" >/dev/null; }

show() {
    if ufw status | grep -qE '^22/tcp +ALLOW +Anywhere'; then
        echo "SSH is open to every address (keys only, fail2ban on)."
    else
        echo "SSH is allowed only from:"
        ufw status | awk '/22\/tcp/ && /ALLOW/ {print "  " $3 "  " substr($0, index($0, "#"))}'
    fi
}

case "${1:-}" in
    "") show ;;
    lock)
        addr="${2:-}"; valid "$addr" || { echo "Usage: hr28-ssh-allow lock <your address>"; exit 1; }
        allow "$addr" "owner"
        for ip in $DO_CONSOLE; do allow "$ip" "DigitalOcean console"; done
        ufw delete allow 22/tcp >/dev/null 2>&1 || true
        show ;;
    add)
        addr="${2:-}"; valid "$addr" || { echo "Usage: hr28-ssh-allow add <address>"; exit 1; }
        allow "$addr" "added $(date +%F)"
        show ;;
    remove)
        addr="${2:-}"; valid "$addr" || { echo "Usage: hr28-ssh-allow remove <address>"; exit 1; }
        ufw delete allow proto tcp from "$addr" to any port 22 >/dev/null
        show ;;
    open)
        ufw allow 22/tcp >/dev/null
        show ;;
    *) sed -n '2,13p' "$0"; exit 1 ;;
esac
