#!/bin/sh
# Fresh database, seed, browser check. Run from the worktree root. usage: rerun.sh <screenshot dir>
T=$(dirname "$0")
C="-p debtbot-t7 -f docker-compose.yml -f $T/t7-compose.yml"
docker compose $C down -v 2>&1 | tail -2
docker compose $C up -d 2>&1 | tail -2
for i in $(seq 1 60); do curl -sf -o /dev/null http://localhost:18097/api/v1/auth/config && break; sleep 2; done
ID=$(docker exec debtbot-t7-db psql -U postgres -d debtBot -Atc 'select "Id" from "Users" where "TelegramId" = 100001')
echo "admin $ID"
rm -f "$1"/*.png
node "$T/seed7.mjs" "$ID" | tail -3
timeout 900 node "$T/check7.mjs" "$1" 2>&1 | tail -40
