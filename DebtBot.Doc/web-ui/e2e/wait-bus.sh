#!/bin/sh
# Waits until the debtbot container's MassTransit bus has connected to RabbitMQ ("Bus started" in its log), up to
# 4 minutes. On a loaded machine the first connection attempts fail and MassTransit retries; anything published
# before that (a finalize) waits in the outbox or gets lost, so the e2e scripts seed only after this.
# usage: wait-bus.sh <docker compose options...>   (the same -p/-f options as the run)
for i in $(seq 1 240); do
  if docker compose "$@" logs --no-log-prefix debtbot 2>/dev/null | grep -q "Bus started"; then
    echo "bus started after ${i} s"
    exit 0
  fi
  sleep 1
done
echo "the app's bus didn't start in 4 minutes; last log lines:"
docker compose "$@" logs --no-log-prefix --tail 20 debtbot
exit 1
