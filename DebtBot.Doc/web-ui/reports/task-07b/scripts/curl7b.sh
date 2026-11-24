#!/bin/sh
# API access check with curl, as Anna (regular user): 200 for Boris (confirmed), 403 for Clara (pending) and Dmitry (none),
# bill reads for a subscriber, and 404 on every change endpoint for her. Run after seed7b.mjs. Prints "status path".
T=$(cd "$(dirname "$0")" && pwd)
J="$T/seed7b.json"
id() { node -e "console.log(require('$J').ids.$1)"; }
bill() { node -e "const b=require('$J').bills.$1; console.log(typeof b === 'string' ? b : JSON.stringify(b))"; }
ANNA=$(id anna)
TOKEN=$(node --input-type=module -e "import { mint } from '$T/seed7b.mjs'; console.log(mint('$ANNA', 'Anna Petrova'))")
API=http://localhost:18098/api/v1
FAIL=0
check() { # expected method path [json body]
  if [ -n "$4" ]; then
    S=$(curl -s -o /dev/null -w '%{http_code}' -X "$2" -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' -d "$4" "$API$3")
  else
    S=$(curl -s -o /dev/null -w '%{http_code}' -X "$2" -H "Authorization: Bearer $TOKEN" "$API$3")
  fi
  if [ "$S" = "$1" ]; then R=ok; else R="FAIL (expected $1)"; FAIL=1; fi
  echo "$S $2 $3 $R"
}
for who in boris clara dmitry; do
  U=$(id $who)
  E=403; [ $who = boris ] && E=200
  check $E GET "/Debts/Own?ofUserId=$U"
  check $E GET "/Bills?ofUserId=$U"
  check $E GET "/Spendings/Own?ofUserId=$U"
  check $E GET "/Spendings/Own/summary?ofUserId=$U"
done
check 200 GET "/Debts/Own?ofUserId=$ANNA"
# Reads: a bill of Boris's that Anna isn't on; one of Clara's (pending) and one of Dmitry's (none).
BD=$(bill borisDmitry)
check 200 GET "/Bills/$BD"
LINE=$(curl -s -H "Authorization: Bearer $TOKEN" "$API/Bills/$BD" | node -e "let s='';process.stdin.on('data',d=>s+=d).on('end',()=>console.log(JSON.parse(s).lines[0].id))")
check 200 GET "/Bills/lines/$LINE"
check 404 GET "/Bills/$(bill clara)"
check 404 GET "/Bills/$(bill dmitry)"
# Changes as a subscriber: Boris's draft and his draft exchange.
DR=$(bill draft)
check 404 POST "/Bills/$DR/Finalize"
check 404 POST "/Bills/$DR/Cancel"
check 404 POST "/Bills/$DR/lines" "[{\"itemDescription\":\"Milk\",\"subtotal\":2,\"participants\":[{\"userId\":\"$(id boris)\"}]}]"
check 404 POST "/Bills/$DR/payments" "[{\"userId\":\"$(id boris)\",\"amount\":2}]"
check 404 POST "/Bills/exchange/finalize" "$(bill exchange)"
check 404 POST "/Bills/$BD/Cancel"
echo "curl: $([ $FAIL = 0 ] && echo all as expected || echo FAILURES)"
exit $FAIL
