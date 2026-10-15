#!/usr/bin/env bash
# Installs HR28 releases in two steps (owner, 2026-10-15): PC -> training -> live.
#
#   hr28-deploy training   New release from /tmp/hr28-release goes to the TRAINING site only
#                          (tr.shamheed.com, database HR28Train) and is kept as the staged release.
#   hr28-deploy live       The staged release (exactly what was tested on training) goes to LIVE:
#                          backup, database changes, install, health check, automatic rollback.
#   hr28-deploy status     Shows what training and live are running.
#
# Upload first (from the development PC):
#   /tmp/hr28-release/api/   = dotnet publish HR28.API -c Release -r linux-x64 --self-contained false
#   /tmp/hr28-release/web/   = dotnet publish HR28.Web -c Release -r linux-x64 --self-contained false
# Optional: /tmp/hr28-release/migrate.sql = dotnet ef migrations script <from> <to> --idempotent
set -euo pipefail
[ "$(id -u)" -eq 0 ] || { echo "Run as root."; exit 1; }

REL=/tmp/hr28-release
STAGED=/var/lib/hr28/release-staged
STAMP="$(date -u +%Y%m%d-%H%M%S)"
SQLCMD=/opt/mssql-tools18/bin/sqlcmd

version_of() { cat "$1/VERSION" 2>/dev/null || echo "unknown"; }

run_sql() {   # run_sql <database> <file>
    source /etc/hr28/sql-admin.env
    SQLCMDPASSWORD="$SA_PASSWORD" "$SQLCMD" -S 127.0.0.1,1433 -U sa -C -I -b -d "$1" -i "$2"
    unset SA_PASSWORD
}

wait_health() {   # wait_health <port>
    for _ in $(seq 1 30); do
        curl -fsS "http://127.0.0.1:$1/health" >/dev/null 2>&1 && return 0
        sleep 2
    done
    return 1
}

web_ok() {   # web_ok <port> (as Nginx calls it: the app refuses plain-HTTP requests)
    sleep 3
    [ "$(curl -s -o /dev/null -w '%{http_code}' -H 'X-Forwarded-Proto: https' "http://127.0.0.1:$1/Auth/Login")" = "200" ]
}

deploy_training() {
    [ -f "$REL/api/HR28.API.dll" ] && [ -f "$REL/web/HR28.Web.dll" ] || { echo "No release in $REL."; exit 1; }
    [ -d /var/www/hr28-train-api ] || { echo "The training site isn't set up (training/setup-training.sh)."; exit 1; }

    # Never deploy development settings; secrets come from /etc/hr28/*.env only.
    rm -f "$REL"/api/appsettings.Development.json "$REL"/web/appsettings.Development.json

    # Stage it: this exact copy is what "hr28-deploy live" will install.
    rm -rf "$STAGED.new"
    install -d -m 750 "$STAGED.new"
    cp -a "$REL/api" "$REL/web" "$STAGED.new/"
    [ -f "$REL/migrate.sql" ] && cp "$REL/migrate.sql" "$STAGED.new/"
    echo "$STAMP" > "$STAGED.new/VERSION"

    if [ -f "$STAGED.new/migrate.sql" ]; then
        echo "Applying database changes to the training copy..."
        run_sql HR28Train "$STAGED.new/migrate.sql" >/dev/null
    fi

    echo "Installing on the training site..."
    systemctl stop hr28-train-web hr28-train-api
    rsync -a --delete "$STAGED.new/api/" /var/www/hr28-train-api/
    rsync -a --delete "$STAGED.new/web/" /var/www/hr28-train/
    chown -R root:root /var/www/hr28-train-api /var/www/hr28-train
    chmod -R u=rwX,go=rX /var/www/hr28-train-api /var/www/hr28-train
    systemctl start hr28-train-api
    wait_health 5011 || { echo "Training API did not start: journalctl -u hr28-train-api -n 30. Live was not touched."; exit 1; }
    systemctl start hr28-train-web
    web_ok 5010 || { echo "Training web app did not answer: journalctl -u hr28-train-web -n 30. Live was not touched."; exit 1; }

    rm -rf "$STAGED"
    mv "$STAGED.new" "$STAGED"
    echo "$STAMP" > /var/www/hr28-train-api/.release
    rm -rf "$REL"
    echo "Release ${STAMP} is on TRAINING (https://tr.shamheed.com). Test it, then: hr28-deploy live"
}

deploy_live() {
    [ -f "$STAGED/api/HR28.API.dll" ] && [ -f "$STAGED/web/HR28.Web.dll" ] || {
        echo "Nothing staged. First: hr28-deploy training (and test it on tr.shamheed.com)."; exit 1; }

    local version keep
    version="$(version_of "$STAGED")"
    keep=/var/www/.previous/${STAMP}

    if [ "$(cat /var/www/hr28-api/.release 2>/dev/null)" = "$version" ]; then
        echo "Live already runs release ${version}."; exit 0
    fi
    echo "Putting release ${version} (tested on training) on LIVE..."

    echo "Backing up the database before the update..."
    /usr/local/sbin/hr28-backup

    if [ -f "$STAGED/migrate.sql" ]; then
        echo "Applying database changes..."
        run_sql HR28Db "$STAGED/migrate.sql"
    fi

    install -d -m 700 "$keep"
    systemctl stop hr28-web hr28-api
    cp -a /var/www/hr28-api "$keep/api"
    cp -a /var/www/hr28 "$keep/web"

    rsync -a --delete "$STAGED/api/" /var/www/hr28-api/
    rsync -a --delete "$STAGED/web/" /var/www/hr28/
    chown -R root:root /var/www/hr28-api /var/www/hr28
    chmod -R u=rwX,go=rX /var/www/hr28-api /var/www/hr28

    systemctl start hr28-api
    if ! wait_health 5001; then
        echo "API did not start: rolling back."
        systemctl stop hr28-api
        rsync -a --delete "$keep/api/" /var/www/hr28-api/
        rsync -a --delete "$keep/web/" /var/www/hr28/
        systemctl start hr28-api hr28-web
        journalctl -u hr28-api -n 30 --no-pager
        exit 1
    fi

    systemctl start hr28-web
    web_ok 5000 || { echo "Web app did not answer: check 'journalctl -u hr28-web'."; exit 1; }

    echo "$version" > /var/www/hr28-api/.release
    find /var/www/.previous -mindepth 1 -maxdepth 1 -type d -mtime +30 -exec rm -rf {} +
    echo "Release ${version} is LIVE. Previous version kept in ${keep}."
}

case "${1:-}" in
    training) deploy_training ;;
    live) deploy_live ;;
    status)
        echo "training: $(cat /var/www/hr28-train-api/.release 2>/dev/null || echo unknown)"
        echo "staged:   $(version_of "$STAGED")"
        echo "live:     $(cat /var/www/hr28-api/.release 2>/dev/null || echo unknown)"
        ;;
    *)
        echo "Usage: hr28-deploy training   (new release -> training site, for testing)"
        echo "       hr28-deploy live       (the tested release -> live)"
        echo "       hr28-deploy status"
        exit 1
        ;;
esac
