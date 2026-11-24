# T4 report: UI foundation

- **Branch:** `task/04-ui-foundation`, based on local `web-ui` at `fc1e0ab`. It was then fast-forwarded to `2f38b6c` with `git merge -X renormalize web-ui`, for the LF normalization (T3b).
- **Commits:**
  - `186e586` approved Shell and Login mockups;
  - `0740a68` ClientApp (shell, login, auth plumbing, i18n) and the Docker node stage;
  - the last commit adds this report and the screenshots.

## Mockups (approved by the user)
- **`mockups/login/`:** approved in round 1. The theme follows the last choice made in that browser, with system as the default, and the page has no toggle of its own.
- **`mockups/shell/`:** approved in round 2, after the user's changes:
  - every page is above the divider, ➕ New bill included, because it's used often;
  - below the divider, each page has its own sub-section in abeat style (section title, entries with a title and a subtitle, "… All x"). `sections.html` and `sections-*.png` show it for all 8 pages.
- **The user's rule: every view has its own URL,** so Back and Forward, reload and bookmarks work. Filters, paging and tabs go in the URL. The URL map and the per-page sub-sections, with the task that builds each one, are in `mockups/shell/README.md`. **T5–T8 must follow it.** The user's words: "I like the sub-buttons and if I remember anything, we'll deal with it later".

## What was built (`DebtBot/ClientApp`)
- **Stack:** the nopds setup:
  - React 19.2, Vite 8, TypeScript 6 (`strict`), Tailwind 4 with `@tailwindcss/vite`;
  - react-router 8, TanStack Query 5, i18next 26 with react-i18next 17;
  - `@fontsource-variable/inter`, `clsx`, oxlint and vitest.
  - Versions are pinned in `package-lock.json`. There's no PWA, lucide or signalr (not needed).
- **`src/index.css`:**
  - nopds' token structure, with Lime-on-graphite in `:root` and `.dark`, including `danger` and `success`;
  - utilities `btn-primary/secondary/ghost/danger`, `input`, `card`, `chip`, `pill-muted/accent/success/danger`, `alert-danger`, `link`, `label`, `muted`, `sidebar-btn`, and for the sub-sections `sidebar-entry` and `sidebar-section`;
  - `surface-2`, `input-bg`, `accent-hover` and `accent-faint` are shades derived from the palette, as nopds has them. No other colours.
- **Theme:**
  - `hooks/useTheme.ts`: system → light → dark, stored per browser under `debtbot.theme`; system removes the key;
  - an inline script in `index.html` applies it before the first paint, so the Login page uses it too, with no flash.
- **i18n:**
  - `i18n/index.ts` loads `locales/<lang>/<group>.json` through `import.meta.glob`; each file is a key group (`auth.json` → `t('auth.signIn')`);
  - English only, with no switch;
  - `scripts/check-locales.mjs` is adapted from nopds. It checks that every `t('key')`, `` t(`group.${x}`) `` and `xxxKey: 'group.key'` exists, and that all locales have the same keys. It runs in `npm run build` and `npm run lint`.
- **`api/client.ts`:**
  - `api<T>(path, { query, json })` against `/api/v1`;
  - the access token is kept in memory only;
  - errors become `ApiError` with `status`, `body` and `serverMessage` (from `{ message }`).
  - **On a 401:** one refresh, then one retry. Login, telegram, refresh, logout and config are excluded.
  - **Refresh is single-flight:** all callers in a tab share one promise. Across tabs, refreshes take turns through a Web Lock (`navigator.locks`, `debtbot.refresh`). Each tab sends the cookie the previous tab rotated, so the backend never sees a reused token. The brief only asked for a shared promise in one tab, but two tabs reloading at once each have their own JS, so the lock is what makes that case safe.
- **`api/types.ts`:** auth (Session, SessionUser, AuthConfig, LoginRequest, TelegramAuthData, PasswordChange), Profile/ProfileUpdate, PagingResult/Paging, UserDisplay, the string enums and ApiErrorBody.
- **`api/hooks.ts`:**
  - `createQueryClient()`: `staleTime` 30 s; no retry on 4xx, up to 2 retries otherwise; no refetch on focus; mutations aren't retried;
  - `useAuthConfig`, `useProfile`.
- **Auth:**
  - `auth/AuthContext.tsx` provides `user`, `isAdmin`, `ready`, `signIn`, `signInWithTelegram` and `signOut`. It refreshes from the cookie on load, clears the query cache on sign-in and sign-out, and sign-out calls `auth/logout`.
  - `auth/guards.tsx` has `RequireAuth`, which sends you to `/login` and remembers where you came from, and `RequireAdmin`, which shows non-admins the 404 page.
- **Login page:**
  - the form calls `auth/login`;
  - the error is the server's `{ message }`, or the generic "Sign-in failed…" when the body is empty (a 429 from the rate limiter, the network, a 5xx);
  - `components/TelegramLogin.tsx` adds `telegram-widget.js?22` with `data-telegram-login` from `GET auth/config`; its callback `onDebtBotTelegramAuth(user)` posts the payload to `auth/telegram`;
  - the widget, "or" and the Telegram hint are hidden when there's no bot username;
  - once signed in, you go back to the page you came from.
- **Shell (`components/Layout.tsx` and `Sidebar.tsx`):**
  - **Sidebar:** the nopds/abook sidebar with ☰, "DebtBot" and the `v{package.json version}` pill linking to GitHub; the nav (🛠 Admin for admins only; Bills isn't highlighted on `/bills/new`); then the page's sub-section; then 🚪 Sign out (name) and the theme button at the bottom.
  - **Phones:** a 48 px rail; ☰ opens an overlay drawer with a backdrop, which closes after any navigation, Back included. The collapsed state is stored under `debtbot.sidebar`.
  - **Sub-sections:** these go in `components/subnav/index.tsx` (path pattern → element). T4 adds the ones that need no data: New bill (Form / Paste text), Profile (Details / Login & password / Telegram) and Admin (Users / Merge users / All bills). T5–T8 add Debts, Bills, Exchange, Spendings and Contacts there, and T6 adds the New bill drafts.
- **Routes (`App.tsx`):**
  - `/`, `/debts` (redirects to `/`);
  - `/bills`, `/bills/new`, `/bills/new/paste`, `/bills/:id`;
  - `/exchange`, `/spendings`, `/contacts`, `/contacts/requests`;
  - `/profile`, `/profile/password`, `/profile/telegram`;
  - `/admin` (redirects to `/admin/users`), `/admin/users`, `/admin/merge`, `/admin/bills`;
  - `*` shows the 404 page.
  - Every one except the 404 is a `Placeholder` (title and subtitle from `locales/en/pages.json`, plus a "Coming soon" card).
- **Dev:**
  - Vite proxies `/api` and `/swagger` to `DEBTBOT_BACKEND`, by default `http://localhost:5098` (the launchSettings `http` profile);
  - `ClientApp/README.md` covers running the API with SDK 11, `npm run dev`, the scripts and the conventions.

## Docker and build
- **`DebtBot/Dockerfile`:** a new `client` stage (`node:22-alpine`): `npm ci`, then `npm run build`, which writes `/src/DebtBot/wwwroot`. The final stage copies it to `/app/wwwroot`. The .NET stages are unchanged.
- **`DebtBot.csproj`:** `DefaultItemExcludes` gets `ClientApp\**`, so the SPA sources and `node_modules` stay out of the .NET build and publish. There's no npm step in the csproj. The image has no ClientApp files (checked).
- **`.dockerignore`:** `**/wwwroot` was added, so a local build doesn't leak into the context. `**/node_modules` was already there.
- `wwwroot` stays gitignored (T1). `publish.yml` is untouched.

## Checks
- **`npm run build`** (i18n check, `tsc -b` strict, vite): no errors. The output is 1 JS chunk of 365 kB (gzip 115 kB) and 21 kB of CSS.
- **`npm run lint`** (oxlint and the i18n check): 0 warnings, 0 errors.
- **`npm test`** (vitest): **8 passed**, in `src/api/client.test.ts`. They cover:
  - the bearer header and JSON;
  - the query string;
  - refresh on 401, then retry;
  - **concurrent 401s plus `refreshSession()` share one refresh**;
  - a failed refresh signs out after a single retry;
  - no refresh on a failed login, with `serverMessage`;
  - a 204;
  - an empty 429 body.
- **Backend:** `dotnet build DebtBot.sln` gives 0 errors. `dotnet test` gives **154 passed, 0 failed**. Both used SDK 11.

## Runtime check (docker compose, Development, cold)
**Setup:**
- the real `docker-compose.yml` plus my override (project `debtbot-t4`): built from the worktree, API on 18090, a JWT key, a named DB volume and no bot token;
- `up -d --build` from a cold cache: the node stage, then the .NET stages; the app waited for healthy db and rabbit.
- **Seed:** a minted JWT (as in T1–T3) set the admin's password, renamed the admin "Anna Petrova" and created Boris (Regular) with a password. Every later sign-in was through the UI.

**HTTP:**
- `/`, `/bills`, `/bills/123`, `/admin/users` and `/login` → 200 `index.html`;
- `/api/v1/nonexistent` → **404**;
- `/api/v1/auth/config` → `{"telegramBotUsername":null}`;
- `/swagger/index.html` → 200.

**Headless Chrome over CDP, all 26 checks passed:**
- **Signed out:**
  - `/bills?status=Draft` → `/login`, the form renders, and the widget is hidden (no bot);
  - with the theme set to system, the page follows the OS dark mode.
- **Sign-in:**
  - a wrong password → "Invalid login or password." in the danger alert;
  - the right password → the shell, back at `/bills?status=Draft`, with 🛠 Admin shown to the admin.
- **Reload:** on `/profile`, a reload stays signed in (refresh from the cookie). The Profile sub-section is shown.
- **History:** clicking sidebar links `/profile/password` → `/exchange`, then Back → `/profile/password` and Forward → `/exchange`.
- **Two tabs:**
  - a second tab on `/admin/users` is signed in;
  - **reloading both tabs at the same time keeps both signed in** (2 refresh calls, serialized);
  - doing it again keeps them signed in, so the chain wasn't revoked;
  - three rapid double reloads still keep both signed in.
- **Phone (390 px), light and dark:** the page starts as the icon rail; ☰ opens the drawer with the sub-section; the drawer closes after navigating.
- **404:** `/no/such/page` shows the 404 page inside the shell.
- **Sign-out:** goes to `/login`, and a reload of `/bills` then stays signed out.
- **Boris (non-admin):** he signs in and sees no 🛠 Admin, and `/admin/users` shows the 404 page.

**Screenshots** (real app, from the image) are in `DebtBot.Doc/web-ui/reports/task-04/`:
- `login-1280-light.png`, `login-1280-dark.png`, `login-390-light.png`, `login-390-dark.png`, `login-error-1280-light.png`;
- `shell-debts-1280-light.png`, `shell-debts-1280-dark.png`;
- `shell-admin-1280-light.png`, `shell-admin-1280-dark.png`;
- `shell-newbill-1280-light.png`, `shell-newbill-1280-dark.png`;
- `shell-profile-390-light.png`, `shell-profile-390-dark.png`, `shell-profile-390-light-drawer.png`, `shell-profile-390-dark-drawer.png`;
- `shell-nonadmin-1280-light.png`, `notfound-1280-light.png`.

**Clean-up:** `down -v`, then I removed the `debtbot-t4` image and the pulled postgres and rabbit images, and ran image and builder prune (2.3 GB of build cache). The local `wwwroot` is removed. `ClientApp/node_modules` (about 75 packages) is kept for the next UI task; about 17 GB of disk is free.

**Not verified live:**
- the real Telegram widget and `POST auth/telegram` through it (needs a bot token, `/setdomain` and HTTPS). The no-bot path, where the widget is hidden, was checked live;
- the cross-tab Web Lock over plain http on a non-localhost host. `navigator.locks` exists only in secure contexts (HTTPS or localhost). Without it, each tab still shares one refresh, but two tabs could race. Production is HTTPS, which the widget needs anyway.

## Decisions
- **Login errors:** the server's `{ message }` is shown, as the brief says. For a bad login or password it's always "Invalid login or password.", so it's one message for any credential failure. The mockup's generic text is the fallback when the body is empty.
- **Web Lock** for cross-tab refresh (see above), instead of adding a grace window to the backend.
- **The app version** is `package.json`'s `version` (1.0.0, as in the mockup), injected as `__APP_VERSION__`. The API exposes no version.
- **Admin routes** show non-admins the 404 page instead of redirecting them, so the routes aren't revealed.
- **`/` is Debts.** `/debts` redirects there.
- **Placeholder pages** are one component keyed by `pages.<name>` in the locale files.

## Open for later tasks
- **T5–T8:**
  - replace the placeholders in `App.tsx`;
  - add their sidebar sub-sections in `components/subnav/index.tsx`, following `mockups/shell/README.md` (Debts balances, Bills drafts and recent, Exchange waiting and recent, Spendings this month, Contacts requests and recent, the New bill drafts; T8 adds the merge badge);
  - keep filters, paging and tabs in the URL;
  - add the rest of the API shapes to `api/types.ts`.
- **The Telegram widget** needs checking at deployment (bot, `/setdomain`, HTTPS).
- **Bundle:** there's no route-level code splitting yet (one chunk of 365 kB). Use `lazy()` once the real pages make it worthwhile.
