# End-to-end checks

One reusable script set for the whole web UI and its API, run against the real Docker stack. It replaces the
per-task seeds and checks in `reports/task-0*/scripts/` (kept as records of those tasks).

## Run
From the repo root, with Docker, Node 22 and `google-chrome`:
```sh
DebtBot.Doc/web-ui/e2e/run.sh [screenshot dir] [--no-build]
```
It takes about 15 minutes. It:
1. stops the `debtbot-e2e` stack and drops its volumes, so the database is fresh;
2. builds and starts the shipped `docker-compose.yml` + `docker-compose.override.yml`, plus `compose.e2e.yml`
   (a JWT key, and Postgres/RabbitMQ in throwaway named volumes instead of `./dbdata`). No bot token: the app runs
   without Telegram. The app is on http://localhost:8080;
3. runs `seed.mjs`, `api-check.mjs` and `check.mjs`, and exits non-zero if any check failed.

The stack stays up afterwards, so you can look around (sign in as `anna` / `anna-pass-123`). Stop it with:
```sh
docker compose -p debtbot-e2e -f docker-compose.yml -f docker-compose.override.yml -f DebtBot.Doc/web-ui/e2e/compose.e2e.yml down -v
```
The stack uses the shipped container names (`debtbot_postgres`, `debtbot_rabbit`) and port 8080, so it can't run
next to another DebtBot compose stack on the same machine.

## The scripts
| File | What it does |
|---|---|
| `seed.mjs` | One data set for everything, through the API (plus SQL only for `TelegramBotEnabled`, which only the bot's `/start` sets). Anna Petrova (**admin**, `anna`/`anna-pass-123`), Boris Ivanov (**regular**, `boris`/`boris-pass-123`, most of the bills), Clara Müller (**subscriber**: follows Boris, `clara`/`clara-pass-123`), a second admin, placeholders, two duplicate pairs for Merge users; about 40 bills in EUR, PLN and USD (one paid in another currency), drafts, a cancelled bill, accepted and waiting exchanges; contacts with own names; subscriptions in every state. Writes `seed.json` for the checks. |
| `api-check.mjs` | HTTP: the SPA is served on client routes, unknown `/api/*` and `/swagger/*` give 404; password sign-in, refresh rotation, reuse detection and sign-out; a regular user gets 403 on admin endpoints; admins can't be deleted (nor yourself), placeholders with data give 409; the subscriber's `ofUserId` access (200 for Boris, 403 for a pending one) and 404 on every change; a new exchange is listed at once for both people; paging totals. |
| `check.mjs [dir] [--no-tour] [--no-flows]` | Headless Chrome. **Tour:** every page as admin, regular user and subscriber, at 1280 and 390 px, light and dark: the data loads, nothing is wider than the screen, the console has no errors or warnings, and a screenshot of each (`<role>-<page>-<width>-<theme>.png`). **Flows:** sign-in, reload (refresh) and sign-out, returning to the page you came from; Bills filters and "← Bills" back to the filtered list (and "← Spendings", "← All bills", `?of=`); a draft's add line, add payment and finalize; New bill form and paste text; an exchange created and accepted; Spendings filter; Contacts add, rename and remove; confirming a subscription request; Profile save; Admin Users (no delete for admins, deleting a placeholder), Merge users, All bills; following as Clara (read-only); `/admin/*` is 404 for a regular user. A failed step saves `FAIL-<n>.png`. |
| `upgrade.sh` | The upgrade path: starts the app **at the state of `main`** on a fresh database (`compose.main.yml`, image `debtbot-main:local`), seeds it with `upgrade-seed.mjs` the way the bot made data (bills from the bot's text format, and `/ExchangeDebt` pairs from before `ExchangeBillId`), then starts this branch's image on the same database and runs `upgrade-check.mjs`: the migrations apply, Anna's debts and bills are what main showed, the old exchange pairs are linked and listed, and ambiguous lookalikes are left alone. Build the main image first: `git archive origin/main \| tar -x -C <dir>` then `docker build -t debtbot-main:local -f <dir>/DebtBot/Dockerfile <dir>`. |
| `lib/api.mjs` | Settings (`E2E_URL`, `E2E_KEY`, `E2E_DB`), token minting with the compose key (same claims as `IdentityService`, so seeding doesn't hit the sign-in rate limit), API calls, `psql` through `docker exec`. |
| `lib/cdp.mjs` | A small Chrome DevTools Protocol driver (Node 22's WebSocket): navigation, waiting, clicks, React-friendly input, viewport and colour scheme, screenshots, and console/exception capture. |

## Notes
- Sign-in is limited to 10 requests a minute per IP; `check.mjs` waits and retries when it hits the limit.
- The checks expect a fresh seed: the flows change data (finalize the draft, merge a pair, delete a placeholder),
  so run `run.sh` again rather than `check.mjs` twice.
- Telegram sign-in isn't covered: the widget needs a real bot, BotFather `/setdomain` and HTTPS. Its hash check is
  unit-tested (`TelegramLoginVerifierTests`).
