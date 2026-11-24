# DebtBot

DebtBot keeps track of shared bills and who owes whom. You split a bill between people (equally or by parts, in one
currency and paid in another if needed), DebtBot works out the debts per person and currency, and keeps your
spendings. It's a **Telegram bot** and a **web app** on the same data:

- **Telegram bot:** add bills in a short text format, finalize them, see debts, spendings and bills, exchange
  currencies with someone (`/ExchangeDebt`), and get notified about the bills of people who let you subscribe.
- **Web app:** the same and more, from a browser: 💸 Debts, 🧾 Bills (with a bill's lines, payments and ledger, and
  the draft actions), ➕ New bill (a form or the bot's text format, with a preview), 🔁 Exchange (two draft bills,
  accepted in one step), 📊 Spendings, 👥 Contacts, 🔔 Subscriptions (and read-only "following" of people who let
  you), 👤 Profile, and 🛠 Admin (users, merging duplicate users, all bills). Sign in with the Telegram Login Widget,
  or with an optional login and password.

The server is ASP.NET Core (.NET 11) with PostgreSQL and RabbitMQ (MassTransit). The web UI (React 19, Vite,
Tailwind 4) is built into the same Docker image and served by the API. The REST API is under `/api/v1`, with
Swagger at `/swagger`.

## Run it with Docker Compose
```sh
docker compose up -d --build
```
`docker-compose.yml` runs the app (`jncchds/debtbot`), Postgres and RabbitMQ; `docker-compose.override.yml` (picked
up automatically) builds the image from this repo, keeps Postgres data in `./dbdata` and runs in Development. The app
waits for healthy Postgres and RabbitMQ and applies the database migrations on start. Open http://localhost:8080.

**Settings** (environment variables of the `debtbot` service in `docker-compose.yml`):

| Variable | |
|---|---|
| `DebtBot_DebtBot__JwtConfiguration__Key` | **Set it.** The secret that signs sign-in tokens: a random string of at least 32 characters, e.g. from `openssl rand -base64 48`. Without it a public default is used (the app logs a warning). |
| `DebtBot_DebtBot__Telegram__BotToken` | The bot token from BotFather. Optional: without it the bot is off, the app still runs, and the web UI offers only login and password. |
| `DEFAULT_USER_TELEGRAM_ID`, `DEFAULT_USER_TELEGRAM_USERNAME` | The first admin, created when there's none. |
| `DebtBot_DebtBot__ForwardedHeaders__Enabled` (and `__KnownProxies`, `__KnownNetworks`) | Behind a reverse proxy: take the client IP and scheme from `X-Forwarded-For`/`X-Forwarded-Proto`, so the per-IP sign-in limit sees real clients. Off by default. |
| `DebtBot_DebtBot__Telegram__LogBotToken`, `__LogChatId` | Optional: send error logs to a Telegram chat. |

**Telegram sign-in on the web** needs, besides the bot token:
1. BotFather `/setdomain` for the bot, set to the domain the web app is served from. The widget only works there.
2. **HTTPS.** Telegram requires it, and the refresh cookie is `Secure` outside Development. Behind a reverse proxy
   that terminates TLS, turn on the forwarded headers (above).

**First sign-in:** the default admin is a Telegram identity. Sign in with the widget and set a login and password on
👤 Profile › Login & password; or, without the widget, get a token from the bot with `/Bearer`, authorize with it
in Swagger and call `PUT /api/v1/auth/password`. An admin can set anyone's login and password on 🛠 Admin › Users.
There is no open sign-up: every web user is someone DebtBot knows from Telegram.

More: [`DebtBot.Doc/web-ui/web-sign-in.md`](DebtBot.Doc/web-ui/web-sign-in.md) (sessions, the proxy setting, the
widget) and [`DebtBot.Doc/web-ui/api.md`](DebtBot.Doc/web-ui/api.md) (the REST API).

## Local development
- **.NET SDK 11** (the projects target `net11.0`) and **Node 22**.
- Start Postgres and RabbitMQ: `docker compose up -d db rabbit` (the override publishes Postgres on 5432 and the
  RabbitMQ console on 15672).
- **API**, from `DebtBot/`: `dotnet run --launch-profile http` → http://localhost:5098 (Development; point
  `ConnectionStrings__DebtBot` and `DebtBot__RabbitMq__Host` at localhost, e.g. with user secrets).
- **Web UI**, from `DebtBot/ClientApp/`: `npm ci`, then `npm run dev` → http://localhost:5173 (Vite proxies `/api`
  and `/swagger` to the API; `DEBTBOT_BACKEND=http://localhost:8080 npm run dev` uses the compose app instead).
  Details and conventions: [`DebtBot/ClientApp/README.md`](DebtBot/ClientApp/README.md).
- **Checks:** `dotnet build DebtBot.sln`, `dotnet test DebtBot.Tests/DebtBot.Tests.csproj`, and in `ClientApp`
  `npm run build`, `npm run lint`, `npm test`.
- **End to end:** `DebtBot.Doc/web-ui/e2e/run.sh` builds and starts the stack on a throwaway database, seeds it and
  drives every page in headless Chrome ([`e2e/README.md`](DebtBot.Doc/web-ui/e2e/README.md)).
- **Migrations:** from `DebtBot/`, `dotnet ef migrations add <Name> --project ../DebtBot.DB` (dotnet-ef 11).
- Line endings are LF (`.gitattributes`); `*.bat`/`*.cmd` are CRLF.

## Docs
- [`DebtBot.Doc/web-ui/`](DebtBot.Doc/web-ui/): how the web UI and its API were built: the plan and decisions, the
  approved mockups (the spec for every page), the task reports and the end-to-end scripts.
