#!/usr/bin/env bash
# Sends ONE test SMS through Dhiraagu with the login in /etc/hr28/sms.env and shows Dhiraagu's
# answer (never the password). Installed as /usr/local/sbin/hr28-sms-test. Usage: hr28-sms-test 9607XXXXXX
set -euo pipefail
[ "$(id -u)" -eq 0 ] || { echo "Run as root."; exit 1; }
[[ "${1:-}" =~ ^960[79][0-9]{6}$ ]] || { echo "Usage: hr28-sms-test 9607XXXXXX (10 digits, starting 9607 or 9609)"; exit 1; }
[ -f /etc/hr28/sms.env ] || { echo "No login saved yet: run hr28-sms-credentials first."; exit 1; }

set -a; . /etc/hr28/sms.env; set +a
esc() { printf '%s' "$1" | sed -e 's/&/\&amp;/g' -e 's/</\&lt;/g' -e 's/>/\&gt;/g' -e "s/'/\&apos;/g"; }
TEXT="HR28 test message from the server. If you received this, SMS works."
BODY=$(printf '%s' "<?xml version=\"1.0\" encoding=\"UTF-8\"?><TELEMESSAGE><TELEMESSAGE_CONTENT><MESSAGE><MESSAGE_INFORMATION><SUBJECT>HR28</SUBJECT></MESSAGE_INFORMATION><USER_FROM><CIML><NAML><LOGIN_DETAILS><USER_NAME>$(esc "$DhiraaguSms__Username")</USER_NAME><PASSWORD>$(esc "$DhiraaguSms__Password")</PASSWORD></LOGIN_DETAILS></NAML></CIML></USER_FROM><MESSAGE_CONTENT><TEXT_MESSAGE><MESSAGE_INDEX>0</MESSAGE_INDEX><TEXT>$(esc "$TEXT")</TEXT></TEXT_MESSAGE></MESSAGE_CONTENT><USER_TO><CIML><DEVICE_INFORMATION><DEVICE_TYPE DEVICE_TYPE=\"SMS\"/><DEVICE_VALUE>$1</DEVICE_VALUE></DEVICE_INFORMATION></CIML></USER_TO></MESSAGE></TELEMESSAGE_CONTENT><VERSION>1.6</VERSION></TELEMESSAGE>")
unset DhiraaguSms__Password

echo "Sending one test SMS to $1 ..."
printf '%s' "$BODY" | curl -s -m 30 -H "Content-Type: text/xml; charset=UTF-8" --data-binary @- "$DhiraaguSms__Endpoint" \
    | tr -d '\r' | grep -E "RESPONSE_STATUS|MESSAGE_ID|MESSAGE_KEY" | sed -e 's/^ *//' || echo "(Dhiraagu sent back no status)"
echo "Check your phone. Send the lines above (they contain no password) to Claude."
