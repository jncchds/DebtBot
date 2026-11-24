#!/bin/sh
# The whole end-to-end run from the repo root: a fresh database, build and start the stack, seed, API check, browser
# check. Screenshots go to the given folder (default $TMPDIR/debtbot-e2e). Leaves the stack running; stop it with
#   docker compose -p debtbot-e2e -f docker-compose.yml -f docker-compose.override.yml -f DebtBot.Doc/web-ui/e2e/compose.e2e.yml down -v
# usage: DebtBot.Doc/web-ui/e2e/run.sh [screenshot dir] [--no-build]
set -e
E=$(dirname "$0")
OUT=${1:-${TMPDIR:-/tmp}/debtbot-e2e}
C="-p debtbot-e2e -f docker-compose.yml -f docker-compose.override.yml -f $E/compose.e2e.yml"

docker compose $C down -v 2>&1 | tail -1
if [ "$2" = "--no-build" ]; then
  docker compose $C up -d 2>&1 | tail -1
else
  docker compose $C up -d --build 2>&1 | tail -1
fi
# Finalizing goes through RabbitMQ: seeding before the app's bus is connected leaves bills in Processing.
"$E/wait-bus.sh" $C

STATUS=0
node "$E/seed.mjs"
node "$E/api-check.mjs" || STATUS=1
node "$E/check.mjs" "$OUT" || STATUS=1
echo "screenshots: $OUT"
exit $STATUS
