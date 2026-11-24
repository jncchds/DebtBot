## Summary
DebtBot gets a full web app next to the Telegram bot, on the same data, and the REST API it needs.

- **API:** everything the bot can do, access-checked (`[Authorize]` everywhere; creator-or-admin for changes): bills
  with structured lines and payments, a text preview, finalize/cancel, exchanges as two draft bills accepted in one
  step, debts, spendings, contacts, subscriptions, profile, and admin endpoints for users, roles, passwords, merging
  duplicates and all bills. Paged lists take server-side filters and sorting. Enums go over the API as strings.
- **Web sign-in:** the Telegram Login Widget, or an optional login and password (no open sign-up: every web user is
  a Telegram identity). Short JWT access tokens plus rotated, httpOnly refresh cookies with reuse detection.
- **Web UI** (React 19, Vite, Tailwind 4, i18next), served by the API from the same Docker image: 💸 Debts,
  🧾 Bills and a bill's page with its draft actions, ➕ New bill (form or the bot's text format, with a preview),
  🔁 Exchange, 📊 Spendings, 👥 Contacts, 🔔 Subscriptions with read-only "following" (`?of=`), 👤 Profile, and
  🛠 Admin (Users, Merge users, All bills). Every view, filter and page has its own URL; light and dark; works on
  phones. Every page was built from a mockup the user approved (`DebtBot.Doc/web-ui/mockups/`).
- **Platform:** .NET 11, packages updated, LF line endings, compose healthchecks, and fixes for several bugs that
  were already on `main` (below).

The plan, the decisions and a report per task are in [`DebtBot.Doc/web-ui/`](https://github.com/jncchds/DebtBot/tree/web-ui/DebtBot.Doc/web-ui).
How to run it is in the new top-level [`README.md`](https://github.com/jncchds/DebtBot/blob/web-ui/README.md).

## Tasks
| | |
|---|---|
| T1 | Backend auth: login/password, refresh tokens, Telegram Login Widget check, `/Bearer` fix, string enums, Swagger bearer scheme, static hosting, the test project — [report](https://github.com/jncchds/DebtBot/blob/web-ui/DebtBot.Doc/web-ui/reports/task-01.md) |
| T2 | API extension (bills, exchanges, debts, spendings, profile, contacts) and the admin API (users, passwords, merge suggestions) — [report](https://github.com/jncchds/DebtBot/blob/web-ui/DebtBot.Doc/web-ui/reports/task-02.md) |
| T3 | .NET 11 and package updates (Telegram.Bot latest, AutoMapper 14, MassTransit 8), compose healthchecks, pre-existing bug fixes — [report](https://github.com/jncchds/DebtBot/blob/web-ui/DebtBot.Doc/web-ui/reports/task-03.md) |
| T3b | `.gitattributes`: LF everywhere (`*.bat`/`*.cmd` CRLF), files renormalized, BOMs kept — [notes](https://github.com/jncchds/DebtBot/blob/web-ui/DebtBot.Doc/web-ui/notes/web-ui-orchestration.md) |
| T4 | UI foundation: ClientApp, Lime-on-graphite tokens, sidebar shell, login, auth plumbing (single-flight refresh across tabs), Docker Node stage — [report](https://github.com/jncchds/DebtBot/blob/web-ui/DebtBot.Doc/web-ui/reports/task-04.md) |
| T5 | Debts, Bills, the bill page; the shared list pattern (URL state, server-side sort and filters, table/cards) — [report](https://github.com/jncchds/DebtBot/blob/web-ui/DebtBot.Doc/web-ui/reports/task-05.md) |
| T6 | New bill (form and paste text), Exchange (amounts or a rate, drafts, accept) and the exchanges list — [report](https://github.com/jncchds/DebtBot/blob/web-ui/DebtBot.Doc/web-ui/reports/task-06.md) |
| T7 | Spendings, Contacts, Subscriptions, Profile — [report](https://github.com/jncchds/DebtBot/blob/web-ui/DebtBot.Doc/web-ui/reports/task-07.md) (mockups: [report](https://github.com/jncchds/DebtBot/blob/web-ui/DebtBot.Doc/web-ui/reports/mockups-78.md)) |
| T7b | Following: a subscribed person's Debts, Bills and Spendings read-only (`?of=`), with backend access — [report](https://github.com/jncchds/DebtBot/blob/web-ui/DebtBot.Doc/web-ui/reports/task-07b.md) |
| T8 | Admin: Users, Merge users, All bills — [report](https://github.com/jncchds/DebtBot/blob/web-ui/DebtBot.Doc/web-ui/reports/task-08.md) |
| T9 | End-to-end check in Docker (cold start and upgrade from `main`), the open points, polish, docs, this description — [report](https://github.com/jncchds/DebtBot/blob/web-ui/DebtBot.Doc/web-ui/reports/task-09.md) |

## Breaking changes and upgrade notes
- **.NET 11.** All projects target `net11.0`; the Docker images are `sdk`/`aspnet` 11.0. Building locally needs SDK 11.
- **String enums in the API.** Enums are sent as names (`"Finalized"`, `"Admin"`) instead of numbers; numbers are
  still accepted on input. Anything reading the API's numeric enums must switch.
- **`/Bearer`** in Telegram gives a token for someone else (`/Bearer @user`) to **admins only**; everyone else gets
  their own token.
- **Other API changes:** `GET Debts/Own` returns a `PagingResult` (was the items array); `GET Bills` is now the
  caller's bills (the admin list moved to `GET Bills/All`); `GET Users/own` moved to `GET profile`;
  `DELETE Users/{id}` works now but only for stray users (409 while they have data, 400 for admins and yourself);
  `PUT Users/{id}` can't change a Telegram id. Details: `DebtBot.Doc/web-ui/api.md`.
- **LF line endings** (`.gitattributes`, T3b). Branches cut before it merge cleanly with `git merge -X renormalize`.
- **New settings:**
  - `DebtBot_DebtBot__JwtConfiguration__Key` — **set it in production** (random, ≥ 32 characters). The app refuses
    to start with a shorter key and warns while the public default is used.
  - `DebtBot_DebtBot__JwtConfiguration__RefreshTokenLifetimeDays` (30).
  - `DebtBot_DebtBot__Telegram__BotToken` is now optional: without it the bot is off and the web UI offers only
    login and password.
  - `DebtBot_DebtBot__ForwardedHeaders__Enabled` / `__KnownProxies` / `__KnownNetworks` — opt-in, for running
    behind a reverse proxy (see `web-sign-in.md`).
  - For the Telegram widget: BotFather `/setdomain` for the bot, and HTTPS.
- **Migrations** (applied automatically on start):
  - `WebAuth` — `Users.Login` (unique), `Users.PasswordHash`, the `RefreshTokens` table;
  - `ExchangePair` — `Bills.ExchangeBillId`, linking an exchange's two bills;
  - `ContactAddedAt` — `UserContactsLinks.AddedAt` (null for existing rows);
  - `LinkOldExchanges` — data only: links exchange pairs made by `/ExchangeDebt` before `ExchangeBillId`, where the
    pairing is certain (ambiguous lookalikes are left alone). Idempotent; its Down keeps the links.
- **Image size and build:** the Dockerfile has a Node stage that builds the UI into `wwwroot` (not committed).
  `docker-compose.yml` adds healthchecks so the app waits for Postgres and RabbitMQ.

## How it was tested
- **Unit/integration:** `dotnet test` — 198 tests (xUnit; SQLite for the repositories and services): auth (Telegram
  hash, passwords, sessions), bill rules and services, exchanges, lists and paging, following, admin, merge,
  pre-existing bug fixes. `npm test` — 113 vitest tests. `npm run build` (typecheck, i18n check), `npm run lint`.
- **End to end in Docker** (`DebtBot.Doc/web-ui/e2e/`, reusable: `run.sh`):
  - cold start with only the shipped `docker-compose.yml` + override and a JWT key, on a fresh DB, no bot token:
    migrations apply, the app starts, the SPA is served, unknown `/api/*` routes give 404;
  - API check, 50 checks: sessions (login, refresh rotation, reuse detection, sign-out), access as admin, regular user
    and subscriber, the admin rules, paging;
  - browser check in headless Chrome, every page as admin, regular user and subscriber at 1280 and 390 px, light and
    dark (data loads, nothing wider than the screen, no console errors or warnings), plus the main flows: sign-in,
    bills and the way back to the list, a draft's actions, New bill form and paste, an exchange, contacts,
    subscriptions, profile, admin users/merge/all bills, following;
  - **upgrade path:** the app at the state of `main` on a fresh DB, seeded the way the bot made data (including
    pre-link `/ExchangeDebt` pairs), then this branch on the same DB: migrations apply, debts and bills are unchanged,
    the old exchanges are linked and listed, ambiguous ones aren't.
- Each task also ran its own runtime checks; see the reports.

## Known follow-ups
- **"Debts currency"** (count a bill's debts in its bill currency or its payment currency, the unmerged
  "ChargeInPaymentCurrency" on `origin/feature-currency_selection`): left out of this series by the user's decision.
- **AutoMapper NU1903 advisories** (3, high): no free fix. Only AutoMapper 15+ (commercial) or replacing AutoMapper
  clears them.
- **Live Telegram widget check at deployment:** a real bot token, BotFather `/setdomain` for the domain, and HTTPS.
  The hash check is unit-tested; the widget itself couldn't be exercised without them.
- **DataProtection keys** aren't persisted (a startup warning). JWTs and refresh tokens don't use them, so sign-in is
  unaffected; persist them if anything starts relying on them.
- `POST Users` (admin, older endpoint) still returns 200 without the new user's id; `PUT`/`DELETE Users/{id}` have no
  `:guid` route constraint.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
