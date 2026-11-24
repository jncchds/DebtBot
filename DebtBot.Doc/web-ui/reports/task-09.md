# T9 report: end-to-end check, open points, polish, docs, PR description

- **Branch:** `task/09-e2e`, based on local `web-ui` at `3b845ed` (T1–T8 merged).
- **Commits:**
  - `9ab6ce4` participants written with the bill, consistent paging, admins can't be deleted, refresh-token purge, opt-in forwarded headers, link old exchanges
  - `b2134ab` the bill page's back link returns to the list it was opened from
  - `8b2d371` polish: filter captions line up, Users table fits at 1280
  - `2a47cd0` polish: Subscriptions rows stack on phones; compose `version` key dropped
  - `33857d2` the e2e script set, top-level README, docs
  - the last commit: PR description, this report, screenshots.

## 1. End to end in Docker
**Cold start** with only the shipped `docker-compose.yml` + `docker-compose.override.yml` and a JWT key (one extra
compose file with just the key), project `debtbot-e2e`, `build --no-cache`, fresh `./dbdata`, no bot token:
- all 29 migrations applied on the first start (the app waited for healthy Postgres and RabbitMQ);
- the app runs without a bot token: `auth/config` → `{"telegramBotUsername":null}`; the only warnings are the
  expected ones (no bot token, DataProtection keys not persisted);
- the SPA is served on `/`, `/bills`, `/bills/<id>`, `/admin/users`, `/login`; `/api/v1/nonexistent`,
  `/api/nothing` and `/swagger/nothing` → 404; `/swagger/index.html` → 200.

**Upgrade path:** `origin/main` built as `debtbot-main:local` (its own net8 Dockerfile), started on a fresh DB and
seeded through *its* API the way the bot made data: bills from the bot's text format, a draft, and `/ExchangeDebt`
pairs as main's `ParseExchange` built them (one accepted, one waiting, plus an ambiguous triple). Then this branch's
image on the same DB (`e2e/upgrade.sh`): **7/7** — the 4 new migrations applied, Anna's debts are identical to what
main returned, every bill main listed is still listed, both old exchanges are linked both ways and show in
`GET Bills/exchanges` the right way round, the ambiguous lookalikes stay unlinked. The upgraded data also looks right
in the browser (Exchange list, Bills, Debts).

**One reusable script set** in `e2e/` replaces the per-task seeds and checks (see `e2e/README.md`):
`run.sh` (fresh DB, build, seed, API check, browser check), `seed.mjs` (admin Anna, regular Boris, subscriber Clara,
a second admin, placeholders, duplicates, ~40 bills, exchanges, contacts, subscriptions), `api-check.mjs`,
`check.mjs` (tour + flows), `upgrade.sh` with `upgrade-seed.mjs`/`upgrade-check.mjs`, and `lib/` (CDP driver with
console capture, API helpers). It uses throwaway named volumes, so it never touches a real `./dbdata`.

**Final run** (`run.sh`, rebuilt image after all the fixes, fresh DB): seed OK (11 users, 41 bills), **API check 50/50**, **browser check 74/74** (59 tour steps: 28 pages × 2 widths, each in light and dark, plus 3 sign-ins; and 15 flows), exit 0. An earlier run before the polish was also 50/50 and 74/74. Six screenshots from the final run, one per area, are in `task-09/` (`01-debts` … `06-admin-users`) and were sent to the user.

What the browser check covers: every page as admin, regular user and subscriber at 1280 and 390 px, light and dark
(data loads, nothing wider than the screen or than its card, no console errors or warnings); the flows: sign-in,
reload (refresh cookie), sign-out and back to the page you came from; Bills filters and "← Bills" back to the
filtered list (and "← Spendings", "← All bills", `?of=`); a draft's add line/add payment (own URLs) and finalize;
New bill form and paste text; an exchange created and accepted; Spendings filter; Contacts add/rename/remove;
confirming a subscription request; Profile save; Admin Users (no delete for admins, deleting a placeholder), Merge,
All bills; following as Clara (read-only); `/admin/*` is 404 for a regular user.

**Fixed while running it** (all in the test scripts, not the app): readiness probe that also works for main's app;
main's `GET Bills` being admin-only (`Bills/Own` there); Chrome needing up to a minute to start on this machine.

## 2. Open points
- **Paging race (T3).** The real cause wasn't the two queries: `GET Bills` filters on `BillParticipants`, which
  `BillService.Add` wrote later, through a fire-and-forget `Task.Run` publishing `EnsureBillParticipant` to RabbitMQ.
  Right after creating an exchange, one of its bills could still have no participant rows, so it was missing from
  the list (count and page agreed; both were short). Fix: `BillRepository` writes the participants (creator, line
  people, payers) in the same transaction as the bill, and in `AddLines`/`AddPayments`; the publishes are gone (the
  consumer stays, idempotent, for messages still queued during an upgrade). `ToPagingResult` is also made
  consistent: the page is read first; without a page size it's one query; a short page gives the total itself; a
  full page is counted but never reported below what it shows. Tests: a new bill is in every participant's list at
  once, a new exchange lists both drafts, paging edge cases, and a source that grows between the two reads. The API
  check creates 5 exchanges in a row and lists them at once for both people.
- **"← Bills" (T7b).** Links to a bill from a list (Bills, Spendings, Exchange, All bills; rows and title links) carry
  the list's URL in the router state; on the bill page (and its add-line/add-payment steps and the paired exchange
  bill) it's carried on. The back link goes there with the right label ("← Spendings", "← All bills"), keeping
  `?of=`, filters, sort and page; reload and Back/Forward keep it (history state); a new tab or bookmark falls back
  to `/bills`. Only same-site list paths are accepted. `lib/backToList.ts`, unit-tested.
- **Old exchanges (T6): a data migration**, `LinkOldExchanges`, rather than showing unlinked pairs in the list. Why:
  linking once makes every reader right (the list, the bill page's banner, Accept, cancel-both) with no heuristics in
  hot queries, and the bot's `/ExchangeDebt` output is distinctive enough to match exactly: same creator, status and
  description text with the `\nExchange <rate> A/B` line, made within a minute, one payment and one "Exchange" line
  with one participant each, mirrored people (two different ones), swapped currencies, each payment equal to the
  other's total — and **no other bill matching either side** (ambiguous lookalikes stay unlinked). Only unlinked
  bills are touched, so it's idempotent; Down keeps the links. Verified on the upgrade path above (SQL can't run in
  the SQLite tests).
- **Deleting admins (T8).** `DELETE Users/{id}` gives 400 "Admins can't be deleted. Make them Regular first." for any
  admin, data or not (`DeleteUserResult.IsAdmin`); the Users page shows no 🗑 for admins. Test + API check.
- **Refresh-token purge (T1).** `RefreshTokenPurgeService` (hosted) runs a minute after start and every 12 hours:
  one `ExecuteDelete` of tokens that are expired or were revoked more than 7 days ago (kept that long so reuse
  detection still recognises a replayed old token). Test on SQLite.
- **Behind a proxy (T1).** Opt-in `DebtBot:ForwardedHeaders:Enabled` (+ `KnownProxies`, `KnownNetworks`):
  `UseForwardedHeaders` for `X-Forwarded-For`/`Proto`, `ForwardLimit` 1; with no list, any proxy is trusted
  (documented as safe only when the app is reachable only through the proxy). The Secure cookie never depended on
  it (Secure outside Development). Documented in `web-sign-in.md` and the README. Compiled and started with it off;
  not exercised behind a real proxy.

## 3. Polish
- **Filter captions (user, via the orchestrator):** in every filter row the pill choices and the checkbox were
  bottom-aligned with shorter heights, so their captions sat lower than the text boxes' ("Search" vs "Role" on
  Users). Fixed at the source: the ListView filter row is top-aligned, `FilterChoice`'s pills sit in a row as tall
  as an input (new `input-h` utility), and `FilterFlag` takes the caption's offset from `md` up (on phones it's on
  its own line). Applies to Users, Bills, Debts, Spendings, Exchange list, All bills, at both widths and themes. The
  forms (New bill, Exchange, Profile, Merge) had no such mix in a row and were already aligned. Before/after:
  `task-09/users-filters-before-after-{light,dark}.png`, sent to the user.
- **Users table at 1280:** with real data (`@telegram_username`) it was 30 px wider than its card, cutting the 🗑
  column. Long usernames now truncate (full one on hover). The browser check now also fails when a table is wider
  than its card.
- **Subscriptions on phones:** rows now stack as in the mockup (name on its own line, pill and buttons under it);
  names were being squeezed onto two lines.
- **Phone debt cards with `?of=`** differ from the following mockup's drawing, but the following spec says phone
  cards are the same as on your own pages, and they match the approved Debts mockup: no change.
- Compose files: the obsolete `version:` key is gone (Docker printed a warning on every command).
- Console: no errors or warnings on any page (only the expected 401 from the refresh probe on the login page, and
  the deliberate 403 on the no-access view). `npm run lint` and the i18n check are clean. The bundle stays split by
  route (one chunk per page; the entry chunk is 276 kB before gzip).

## 4. Docs
- New top-level `README.md`: what DebtBot is, the web UI, compose, the settings (JWT key, bot token, default admin,
  BotFather `/setdomain` and HTTPS, the proxy setting), the first sign-in, local development (SDK 11, `npm run dev`),
  checks, e2e, migrations; links `DebtBot.Doc/web-ui/`.
- `api.md`: participants written with the bill, paging consistency, `LinkOldExchanges`, admins can't be deleted.
- `web-sign-in.md`: the purge, the proxy setting and how to use it.
- `web-ui/README.md`: links `api.md`, `web-sign-in.md`, `e2e/`, `pr-description.md`.
- `e2e/README.md`.

## 5. PR description
`DebtBot.Doc/web-ui/pr-description.md`, for PR #34.

## 6. Checks
- `dotnet build DebtBot.sln`: 0 errors (warnings as before: NU1903 for AutoMapper, nullable).
- `dotnet test`: **198 passed** (187 + 11 new in `OpenPointsTests`; 4 test helpers updated because participants are
  now written by `BillService.Create` itself).
- `npm run build`, `npm run lint` (oxlint + i18n), `npm test`: **113 passed** (108 + 5 in `backToList.test.ts`).

## Runtime vs compiled only
- Runtime (Docker): everything in section 1, the participants fix, paging, back links, admin delete, the polish.
- Compiled and unit-tested only: the refresh-token purge (the hosted service starts; its first run is after a
  minute and there is nothing to purge on a fresh DB), forwarded headers (off in every run).
- Not covered: the live Telegram widget (needs a real bot, `/setdomain` and HTTPS).

## Left open
- The follow-ups in the PR description: "debts currency", AutoMapper advisories, the live widget check,
  DataProtection keys, `POST Users` without the id, no `:guid` route constraints.
- The old per-task scripts in `reports/task-0*/scripts/` are kept as records; `e2e/` supersedes them.
