# DebtBot web UI

React 19, Vite, TypeScript (strict), Tailwind 4, react-router, TanStack Query and i18next. It follows the nopds setup.
The API serves the built UI from `DebtBot/wwwroot`. That folder is gitignored and is filled by the `client` stage of `DebtBot/Dockerfile`.

## Run locally
1. **Start Postgres and RabbitMQ**, for example from the compose file. With the override, Postgres is published on port 5432:
   ```sh
   docker compose -f docker-compose.yml -f docker-compose.override.yml up -d db rabbit
   ```
   Then point the API at them with the usual `DebtBot_ConnectionStrings__*` settings, or with user secrets.
2. **Start the API** from `DebtBot/` (it targets net11.0). SDK 11 is installed user-local in `~/.dotnet`:
   ```sh
   PATH=~/.dotnet:$PATH DOTNET_ROOT=~/.dotnet dotnet run --launch-profile http
   ```
   It listens on http://localhost:5098 (`Properties/launchSettings.json`, Development). There, the refresh cookie isn't `Secure`, so sign-in works over plain http.
3. **Start the UI** from `DebtBot/ClientApp`:
   ```sh
   npm ci
   npm run dev
   ```
   Open http://localhost:5173. Vite proxies `/api` and `/swagger` to the API. To use another API address, set `DEBTBOT_BACKEND`, for example `DEBTBOT_BACKEND=http://localhost:8080 npm run dev` against `docker compose up`.

You sign in with a login and password (`PUT api/v1/auth/password` sets them for yourself, `POST api/v1/Users/{id}/password` for an admin). Or you sign in with the Telegram widget, which needs a bot token, BotFather `/setdomain` and HTTPS (see `DebtBot.Doc/web-ui/web-sign-in.md`).

## Scripts
| Script | What it does |
|---|---|
| `npm run dev` | Vite dev server with the API proxy |
| `npm run build` | i18n check, typecheck (`tsc -b`) and production build into `../wwwroot` |
| `npm run lint` | oxlint and the i18n check |
| `npm run lint:i18n` | `scripts/check-locales.mjs`: every `t('key')` exists, and all locales have the same keys |
| `npm test` | vitest: the API client's refresh logic, URL list state, money and bill helpers |

## Conventions
- **Strings:** every user-facing string goes through i18next. They live in `src/locales/en/<group>.json`, and each file is a key group (`auth.json` → `t('auth.signIn')`). English is the only language for now.
- **Colours:** only the tokens in `src/index.css` (Lime on graphite). "You owe" uses `danger` and "owed to you" uses `success`.
- **URLs:** every view, filter, page and tab lives in the URL, so Back and Forward, reload and bookmarks work. The URL map is in `DebtBot.Doc/web-ui/mockups/shell/README.md`.
- **Lists:** Bills, Debts, Spendings and Exchanges use the shared list pattern (`components/list/ListView.tsx`, `lib/listState.ts`, `lib/lists.ts`), described in `mockups/shell/README.md`, "List views".
- **Money:** never add or subtract API amounts as floats; use `lib/money.ts` (integer units of 1/10000) and show them with `useFormat().money()`.
- **Sidebar sub-sections:** a page's own entries go below the divider. Register them in `src/components/subnav/index.tsx`.
- **API:** call `api<T>()` from `src/api/client.ts` and put the shapes in `src/api/types.ts`. The access token stays in memory. On a 401 the client refreshes once and retries. Refreshes are single-flight within a tab and take turns across tabs (Web Locks), because the backend revokes a session when a refresh token is reused.
