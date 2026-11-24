#!/bin/sh
# The upgrade path, from the repo root: start the app at the state of `main` on a fresh database, seed it through
# main's API with the e2e data set (seed.mjs) and note what main shows (upgrade-seed.mjs), then start this branch's
# image on the same database and check (upgrade-check.mjs). Needs two images:
#   debtbot-main:local    built from origin/main:  git archive origin/main | tar -x -C <dir>; docker build -t debtbot-main:local -f <dir>/DebtBot/Dockerfile <dir>
#   jncchds/debtbot:latest built from this branch (run.sh builds it, or `docker compose ... build`)
# usage: DebtBot.Doc/web-ui/e2e/upgrade.sh
set -e
E=$(dirname "$0")
C="-p debtbot-e2e -f docker-compose.yml -f docker-compose.override.yml -f $E/compose.e2e.yml"

docker compose $C down -v 2>&1 | tail -1
docker compose $C -f "$E/compose.main.yml" up -d --no-build 2>&1 | tail -1
"$E/wait-bus.sh" $C -f "$E/compose.main.yml"
node "$E/seed.mjs"
node "$E/upgrade-seed.mjs"
docker compose $C -f "$E/compose.main.yml" stop debtbot 2>&1 | tail -1
docker compose $C up -d --no-build --force-recreate debtbot 2>&1 | tail -1
"$E/wait-bus.sh" $C
node "$E/upgrade-check.mjs"
