# DebtBot

DebtBot keeps track of shared bills and who owes whom. You split a bill between people (equally or by parts, in one
currency and paid in another if needed), DebtBot works out the debts per person and currency, and keeps your
spendings. It's a **Telegram bot** and a **web app** on the same data:

- **Telegram bot:** add bills in a short text format, finalize them, see debts, spendings and bills, exchange
  currencies with someone (`/ExchangeDebt`), and get notified about the bills of people who let you subscribe.
- **Web app:** the same and more, from a browser: 💸 Debts, 🧾 Bills (with a bill's lines, payments and ledger, and
  a draft edited in the same form as a new bill), ➕ New bill (a form or the bot's text format, with a preview), 🔁 Exchange (two draft bills,
  accepted in one step), 📊 Spendings, 👥 Contacts, 🔔 Subscriptions (and read-only "following" of people who let
  you), 👤 Profile (with personal API tokens), and 🛠 Admin (users, merging duplicate users, all bills). Sign in with the Telegram Login Widget,
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
| `DebtBot_DebtBot__ApiTokens__RateLimitPerMinute`, `__MaxActivePerUser` | Optional: requests a minute per API token (120; 0 turns the limit off) and active tokens per user (20). See "Calling the API". |
| `DebtBot_DebtBot__Telegram__LogBotToken`, `__LogChatId` | Optional: send error logs to a Telegram chat. |
| `DebtBot_DebtBot__Llm__ApiKey` | Optional, a secret: turns on reading receipt photos. See "Receipts (optional)". |

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

## Receipts (optional)
DebtBot can read a receipt photo into a bill draft through an AI model on [OpenRouter](https://openrouter.ai) (or any
OpenAI-compatible API). It's off until a key is set:

| Variable | Default | Meaning |
|---|---|---|
| `DebtBot_DebtBot__Llm__ApiKey` | (empty) | **A secret.** Set it as an environment variable (or user secrets), never in `appsettings.json` or a committed file. Empty turns the feature off. |
| `DebtBot_DebtBot__Llm__Model` | `nvidia/nemotron-3.5-super-vl:free` | must take images; ideally structured output |
| `DebtBot_DebtBot__Llm__FallbackModels` | `google/gemma-4-31b-it:free` | comma-separated, tried by OpenRouter when the first fails |
| `DebtBot_DebtBot__Llm__BaseUrl` | `https://openrouter.ai/api/v1` | e.g. a local Ollama's `http://host:11434/v1` |
| `DebtBot_DebtBot__Llm__ResponseFormat` | `json_schema` | `json_object` for APIs without structured output |
| `DebtBot_DebtBot__Llm__TimeoutSeconds` | `45` | |
| `DebtBot_DebtBot__Llm__UserHourlyLimit`, `__UserDailyLimit` | `10`, `30` | receipts per user, web, API tokens and bot together |
| `DebtBot_DebtBot__WebUrl` | (empty) | the site's public URL, e.g. `https://debts.example.com`, for the bot's "Open in the web form" button; empty (or a link Telegram refuses, such as `localhost`) leaves the button out |

Free OpenRouter models allow 20 requests a minute, and 50 a day, or 1000 a day once $10 of credits has been bought
(checked 2026-11-27). Free models may log prompts, depending on the provider: check the data policy setting in your
OpenRouter account. The photo is never stored, and neither is the model's answer; a parsed draft is kept 15 minutes.
For development without a key, `DebtBot_DebtBot__Llm__Provider=Fake` answers with test fixtures (Development only).

**In the bot:** send a receipt photo (or an image file) in a private chat, or in a group with the bot mentioned in
the caption (`@<botname>`; other group photos are ignored). The bot answers with the bill in the `/AddBill` text format
in a code block: copy it, change the people and payers (they're you for now), and send it back. Discounts are already
folded into the lines. The first photo in a chat gets the privacy note; later replies a short footer. `/Format` says
the same.

## Calling the API
Scripts and apps call the REST API with a personal API token. Make one on 👤 Profile › API tokens: give it a name,
choose **Read only** or **Read and write**, and an expiry (30, 90 or 365 days, or never). The token (`dbt_…`) is shown
once; copy it then. Send it as a bearer token:
```sh
curl -H "Authorization: Bearer dbt_…" https://<your host>/api/v1/Debts/Own
```
A token acts as you, with your access. A read-only token gets 403 on anything but GET; no token can manage tokens,
change passwords or refresh web sessions. Each token is limited to 120 requests a minute. Revoke a token on the same
page; an admin can revoke all of a user's tokens on 🛠 Admin › Users. The API reference is at `/swagger` (Authorize
takes a token too), and [`api.md`](DebtBot.Doc/web-ui/api.md) has the details ("API tokens").

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
