Task T4 (UI foundation) of the DebtBot web-ui series. An orchestrator session runs the series one task at a time, with one session per task.

First, read `/mnt/git/DebtBot/DebtBot.Doc/web-ui/briefs/common.md` and follow it throughout, especially "Mockups first". To read it before your worktree exists, use `git -C /mnt/git/DebtBot show web-ui:DebtBot.Doc/web-ui/briefs/common.md`.

- Worktree: `/mnt/git/DebtBot/.claude/worktrees/task-04`
- Branch: `task/04-ui-foundation`, based on the local `web-ui` branch (T1 to T3 are merged into it; the backend is on .NET 11).
- Read:
  - `plan.md`, "Step 2" (stack, shared pieces, plumbing, the Login row of the pages table) and "Step 3";
  - `notes/web-ui-palette.md`;
  - `web-sign-in.md` and `api.md`;
  - the reports `task-01.md` to `task-03.md`.
- Style references (read only, don't write there):
  - `/mnt/git/nopds/src/Nopds.Web/ClientApp`: the setup, `index.css` tokens and utilities, i18n;
  - `/mnt/git/abook/src/abook-ui/src/components/Sidebar.tsx`: the shell, plus `hooks/useTheme.ts` if abook has it;
  - `~/.claude/projects/-mnt-git-ANalyze/memory/web-visual-style.md`: the cross-project rule.

## Goal
The SPA foundation that T5 to T8 build pages into:
- a styled shell;
- a working sign-in;
- auth plumbing;
- i18n;
- the Docker build that serves the UI from the API image.

Only the Login page and the shell are real views in this task. The other nav entries lead to simple placeholder pages.

## Mockups first (do this before writing the app)
Make mockups for two views in one round, in light and dark at 1280 px and 390 px, and send them with SendUserFile (display: render):
1. **Shell:**
   - the abook-style collapsible sidebar with "DebtBot", a version pill, and the emoji nav from the plan (💸 Debts, 🧾 Bills, ➕ New bill, 🔁 Exchange, 📊 Spendings, 👥 Contacts, 👤 Profile, 🛠 Admin, the last for admins only);
   - sign-out and theme buttons at the bottom;
   - the phone layout (drawer or bottom behaviour, whatever abook does);
   - an empty content area showing a page title and a card.
2. **Login:**
   - a login/password form;
   - the Telegram Login Widget area;
   - the state when the widget is hidden because `auth/config` returns no bot username;
   - the error state for a failed login (one message for any failure).

Iterate until the user approves both, then commit them under `DebtBot.Doc/web-ui/mockups/shell/` and `mockups/login/`.

## Scope
1. **`DebtBot/ClientApp`**, copying the nopds setup:
   - React 19, Vite, TypeScript (strict);
   - Tailwind 4 with `@tailwindcss/vite`;
   - `react-router`, `@tanstack/react-query`, `@fontsource-variable/inter`, `clsx`.
   - Pin versions in `package-lock.json`.
2. **`index.css`:**
   - nopds' token structure and utilities (`btn-*`, `card`, `chip`, `pill-*`, `input`, `sidebar-btn`), with the Lime-on-graphite tokens from `notes/web-ui-palette.md` in `:root` and `.dark`, including danger and success for "you owe" and "owed to you".
   - No other colours.
3. **Theme:** `hooks/useTheme.ts`, the light/dark/system toggle as in abook, stored per browser.
4. **i18n (the user's decision):**
   - English only for now, but every user-facing string goes through i18next the way nopds does it (`i18n/`, `locales/en/*.json`);
   - copy nopds' locale check script if it has one, and wire it into `npm run lint` or the build;
   - no language switch yet.
5. **`api.ts` and `types.ts`:**
   - Fetch against `/api/v1`. The access token is kept in memory only, never in localStorage.
   - On app load, call `POST auth/refresh` to restore the session from the cookie.
   - On a 401, refresh once and retry.
   - **All callers share one in-flight refresh promise**, because the backend has no grace window on refresh reuse: a second concurrent refresh with the same cookie revokes the session (T1 report).
   - Write `types.ts` from `api.md`, covering at least auth, profile and the shared paging type. Later tasks add the rest. Enums are strings.
   - Wrap react-query around it with sensible defaults.
6. **Auth:**
   - an auth context (current user, `isAdmin`, sign-in, sign-out);
   - route guards: unauthenticated users go to `/login`, and the admin route is for admins only;
   - sign-out calls `auth/logout`.
7. **Login page**, as in the approved mockup:
   - the form calls `auth/login`;
   - the Telegram widget script is built from `GET auth/config`'s `telegramBotUsername`, and its callback posts the payload to `auth/telegram`;
   - the widget is hidden when there's no username;
   - errors come from the `{ message }` body.
8. **Shell and routes:**
   - the shell from the approved mockup, with a route for every nav entry;
   - placeholder pages for the views T5 to T8 will build;
   - the version pill shows the app version, from `package.json` or an API value if one exists;
   - a 404 page for unknown client routes.
9. **Dev:**
   - the Vite dev server proxies `/api` (and `/swagger`, if that's useful) to the backend;
   - document `npm run dev` together with how to run the API locally (SDK 11, as in common.md) in `ClientApp/README.md`.
10. **Docker and build (plan Step 3):**
    - add a `node:22-alpine` stage to `DebtBot/Dockerfile` that runs `npm ci && npm run build` and writes to `DebtBot/wwwroot`, and have the publish or final stage copy it in;
    - no npm step in the csproj, and `wwwroot` stays gitignored (T1 did this);
    - add a `.dockerignore` entry for `ClientApp/node_modules` if needed;
    - `.github/workflows/publish.yml` stays as it is.
11. **Checks:**
    - `npm run build`, including the typecheck, passes with no errors, and so does the i18n check.
    - The backend build and the tests (154) still pass.
12. **Runtime check:**
    - `docker compose up --build`, cold. Then, in headless Chrome:
      - the login page renders;
      - sign in with a password you set as T1 to T3 did;
      - the shell appears;
      - a reload keeps you signed in (refresh from the cookie);
      - opening two tabs and reloading both at once doesn't sign you out;
      - sign-out works;
      - a non-admin doesn't see 🛠 Admin;
      - `/bills` and other deep links load the SPA, and `/api/v1/nonexistent` is still 404.
    - Take screenshots of the real Login page and the shell in light and dark, at 1280 px and 390 px, and send them with SendUserFile.
    - Bring the stack down and clean up as in common.md, including `node_modules` if disk is short.

When you're done, follow "Finishing" and "Waking the orchestrator" in common.md. Put the screenshots under `DebtBot.Doc/web-ui/reports/task-04/`, and list their paths in your report so the orchestrator can forward them.
