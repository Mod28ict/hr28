#!/usr/bin/env bash
# RUN THIS YOURSELF in PuTTY: saves the Cloudflare Origin Certificate and its private key
# (Cloudflare → SSL/TLS → Origin Server → Create Certificate). The key is never shown.
# Installed as /usr/local/sbin/hr28-origin-cert. Run as root, then paste when asked.
set -euo pipefail
[ "$(id -u)" -eq 0 ] || { echo "Run as root."; exit 1; }

DIR=/etc/ssl/hr28
TMP="$(mktemp -d)"; chmod 700 "$TMP"
trap 'rm -rf "$TMP"' EXIT

# Reads pasted lines up to and including the END line (line by line, so nothing extra is taken).
read_block() {
    local end="$1" line
    while IFS= read -r line; do
        line="${line%$'\r'}"
        printf '%s\n' "$line"
        [[ "$line" == *"-----END "*"$end"* ]] && return 0
    done
    return 1
}

echo "1/2  Paste the ORIGIN CERTIFICATE (from -----BEGIN CERTIFICATE----- to -----END CERTIFICATE-----):"
read_block "CERTIFICATE-----" > "$TMP/origin.pem" || true
echo
echo "2/2  Paste the PRIVATE KEY (from -----BEGIN PRIVATE KEY----- to -----END PRIVATE KEY-----)."
echo "     It will not be shown on screen."
stty -echo 2>/dev/null || true
read_block "PRIVATE KEY-----" > "$TMP/origin.key" || true
stty echo 2>/dev/null || true
echo

openssl x509 -in "$TMP/origin.pem" -noout >/dev/null 2>&1 || { echo "That certificate could not be read. Nothing was saved."; exit 1; }
openssl pkey -in "$TMP/origin.key" -noout >/dev/null 2>&1 || { echo "That private key could not be read. Nothing was saved."; exit 1; }
if [ "$(openssl x509 -in "$TMP/origin.pem" -noout -pubkey)" != "$(openssl pkey -in "$TMP/origin.key" -pubout)" ]; then
    echo "The key does not belong to that certificate. Nothing was saved."; exit 1
fi

install -d -m 700 -o root -g root "$DIR"
install -m 644 -o root -g root "$TMP/origin.pem" "$DIR/origin.pem"
install -m 600 -o root -g root "$TMP/origin.key" "$DIR/origin.key"
echo "Saved. Certificate for:"
openssl x509 -in "$DIR/origin.pem" -noout -ext subjectAltName 2>/dev/null | tail -1
openssl x509 -in "$DIR/origin.pem" -noout -enddate
