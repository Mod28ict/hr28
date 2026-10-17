# HR28 server alerts: shared functions (installed as /usr/local/lib/hr28/sms-lib.sh, sourced by
# hr28-boot-notify and hr28-monitor). Sends through Dhiraagu with the login in /etc/hr28/sms.env.
# Recipients: the active Administrators in HR28 (their mobile numbers), plus any numbers in
# /etc/hr28/alerts.env (ALERT_EXTRA_PHONES="7xxxxxx 9xxxxxx"). Never prints a number in full.

SITE_NAME="${SITE_NAME:-shamheed.com}"

mvt_time() { TZ=Indian/Maldives date +%H:%M; }

# 7771234 / 9607771234 / +960 777 1234 -> 9607771234; nothing if not a Maldivian mobile.
to_international() {
    local d
    d=$(printf '%s' "$1" | tr -cd '0-9')
    if [ ${#d} -eq 10 ] && [ "${d#960}" != "$d" ]; then d=${d#960}; fi
    if [[ "$d" =~ ^[79][0-9]{6}$ ]]; then printf '960%s' "$d"; fi
}

admin_phones() {
    local list=""
    if [ -f /etc/hr28/sql-ops.env ]; then
        # shellcheck disable=SC1091
        . /etc/hr28/sql-ops.env
        list=$(SQLCMDPASSWORD="$OPS_PASSWORD" /opt/mssql-tools18/bin/sqlcmd -S 127.0.0.1,1433 -U "$OPS_USER" -C -I -h -1 -W -l 10 -d HR28Db \
            -Q "SET NOCOUNT ON; SELECT DISTINCT u.MobileNumber FROM Users u JOIN UserRoles ur ON ur.UserId = u.Id JOIN Roles r ON r.Id = ur.RoleId WHERE r.Name = N'Super Administrator' AND u.IsActive = 1;" 2>/dev/null)
        unset OPS_PASSWORD
    fi
    if [ -f /etc/hr28/alerts.env ]; then
        # shellcheck disable=SC1091
        . /etc/hr28/alerts.env
    fi
    for n in $list ${ALERT_EXTRA_PHONES:-}; do to_international "$n"; echo; done | grep -E '^960' | sort -u | head -10
}

xml_escape() {
    printf '%s' "$1" | sed -e 's/&/\&amp;/g' -e 's/</\&lt;/g' -e 's/>/\&gt;/g' -e 's/"/\&quot;/g' -e "s/'/\&apos;/g"
}

# send_sms 9607771234 "text": success when Dhiraagu answers status 100.
send_sms() {
    [ -f /etc/hr28/sms.env ] || { echo "no /etc/hr28/sms.env"; return 1; }
    local endpoint user pass body
    endpoint=$(grep '^DhiraaguSms__Endpoint=' /etc/hr28/sms.env | cut -d= -f2-)
    user=$(grep '^DhiraaguSms__Username=' /etc/hr28/sms.env | cut -d= -f2-)
    pass=$(grep '^DhiraaguSms__Password=' /etc/hr28/sms.env | cut -d= -f2-)
    body="<?xml version=\"1.0\" encoding=\"UTF-8\"?><TELEMESSAGE><TELEMESSAGE_CONTENT><MESSAGE>"
    body+="<MESSAGE_INFORMATION><SUBJECT>SMS</SUBJECT></MESSAGE_INFORMATION>"
    body+="<USER_FROM><CIML><NAML><LOGIN_DETAILS><USER_NAME>$(xml_escape "$user")</USER_NAME>"
    body+="<PASSWORD>$(xml_escape "$pass")</PASSWORD></LOGIN_DETAILS></NAML></CIML></USER_FROM>"
    body+="<MESSAGE_CONTENT><TEXT_MESSAGE><MESSAGE_INDEX>0</MESSAGE_INDEX><TEXT>$(xml_escape "$2")</TEXT></TEXT_MESSAGE></MESSAGE_CONTENT>"
    body+="<USER_TO><CIML><DEVICE_INFORMATION><DEVICE_TYPE DEVICE_TYPE=\"SMS\"/><DEVICE_VALUE>$1</DEVICE_VALUE></DEVICE_INFORMATION></CIML></USER_TO>"
    body+="</MESSAGE></TELEMESSAGE_CONTENT><VERSION>1.6</VERSION></TELEMESSAGE>"
    unset pass
    printf '%s' "$body" | curl -s -m 30 -H "Content-Type: text/xml; charset=UTF-8" --data-binary @- "$endpoint" \
        | grep -q '<RESPONSE_STATUS>100</RESPONSE_STATUS>'
}

# notify_admins "text" [dry-run]: sends to every recipient; prints counts only.
notify_admins() {
    local n phones sent=0 failed=0
    phones=$(admin_phones)
    for n in $phones; do
        if [ "${2:-}" = "dry-run" ]; then echo "would send to •••${n: -3}: $1"; continue; fi
        if send_sms "$n" "$1"; then sent=$((sent + 1)); else failed=$((failed + 1)); fi
    done
    echo "$(date -u +%FT%TZ) SMS to administrators: $sent sent, $failed failed ($(printf '%s\n' "$phones" | grep -c .) recipients): $1"
}
