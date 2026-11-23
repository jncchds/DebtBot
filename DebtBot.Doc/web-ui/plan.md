# DebtBot: API extension, Telegram + password auth, full web UI

## Context
Today DebtBot is driven entirely by the Telegram bot. The API only covers part of what the bot can do. Tokens come only from the `/Bearer` command, which will also hand out a token for *another* user (`BearerCommand.cs:32-34`), or from a DEBUG-only `IdentityController`. There is no web UI on `main`.

Two earlier UI attempts exist only as remote branches, and both are reference material only:
- `origin/react_ui`: a read-only viewer built with MUI and a separate nginx container. The bot name and URL are hardcoded.
- `origin/react-ui-vibe`: also MUI. Enum strings don't match what the API sends, so the actions never show, and the Dockerfile has no Node stage.

Goal:
- Expose the bot's features over a clean, access-checked REST API.
- Let users sign in with the Telegram Login Widget or with an optional login and password.
- Ship a complete React UI in the user's home-family style. That style is the nopds/abook look: a collapsible sidebar with emoji nav, flat cards and pills, the Inter font, and nopds CSS tokens on Tailwind 4 (rule in `~/.claude/projects/-mnt-git-ANalyze/memory/web-visual-style.md`). DebtBot gets a palette no sibling uses.
- Serve the UI from the API image.

Decisions already taken:
- Telegram Login Widget only.
- Passwords are optional per user and there is no open sign-up.
- The UI is served by the API.
- **Palette:** Lime on graphite (chosen 2026-11-23 from `mockups/palette/`; exact tokens in `notes/web-ui-palette.md`).

Work is split into tasks T1–T9 (see "Task series" at the end), each run by its own worker session on `task/NN-*` branched from `web-ui`; the orchestrator session merges them into `web-ui`, which has one draft PR to main. How it runs: `notes/web-ui-orchestration.md`, `briefs/common.md`.

## Step 0: Choose the palette — DONE: Lime on graphite
Make one small PNG per option in `$CLAUDE_JOB_DIR/tmp/palettes/`, rendered with headless Chromium or Playwright from a tiny HTML file. Each image shows the light and dark versions side by side: text on the background, a surface card reading "You owe Anna 12.50 EUR", a primary button and a pill. Send the images to the user with SendUserFile and ask which one to use.
- **Lime on graphite:** accent `#4d7c0f` light, `#a3e635` dark.
- **Coral on slate:** `#d9532c` / `#ff8a65`.
- **Crimson/ruby:** `#b0263f` / `#f0607a`.
- **Cobalt:** `#2b50ec` / `#7b93ff`.

The images are in `mockups/palette/`. The chosen values become the `:root` and `.dark` tokens in `ClientApp/src/index.css`.

## Step 1: Backend

### 1a. Users and auth (DB and identity)
- **`DebtBot.DB/Entities/User.cs`:**
  - Add `Login` (nullable, unique, stored lowercase) and `PasswordHash` (nullable).
  - Add a new `RefreshToken` entity: `Id`, `UserId`, `TokenHash`, `ExpiresAt`, `RevokedAt`.
  - Add a migration with `dotnet ef migrations add WebAuth --project ../DebtBot.DB`, run from `DebtBot/` (same as `AddMigration.bat`).
- **New `Services/AuthService.cs`:**
  - Passwords use `Microsoft.AspNetCore.Identity.PasswordHasher<User>`, which ships in the shared framework, same as abook's `AuthController`.
  - Telegram Login Widget check:
    1. Build the data-check-string from the fields actually present, as sorted `key=value` lines joined by `\n`, leaving out `hash`.
    2. Compute `HMAC-SHA256(key = SHA256(botToken))` over it and compare in constant time.
    3. Reject if `auth_date` is more than 24h old.
  - The algorithm follows the react-ui-vibe branch, which got the optional fields right. It does not use react_ui's `LoginWidget` package, which breaks when optional fields are null.
  - Refresh tokens are issued and rotated as an httpOnly cookie, following abook's `JwtTokenService` pattern.
  - Reuse `IUserService.FindOrAddUser` and `ActualizeTelegramUser` so Telegram users are created and updated the same way the bot does it.
- **`IdentityService.GenerateJwtToken`:** keep it as is for access tokens (60 min).
- **New `Controllers/AuthController.cs`** at `api/v1/auth`:

  | Endpoint | Notes |
  |---|---|
  | `GET config` | anonymous. Returns `{ telegramBotUsername }` from `ITelegramBotClient.GetMeAsync()`, cached at startup. |
  | `POST telegram` | widget payload in, session `{ user, accessToken, expiresAt }` out. |
  | `POST login` | `{ login, password }` |
  | `POST refresh` | |
  | `POST logout` | |
  | `GET me` | |
  | `PUT password` | set a password or change it; changing needs the current one. Also sets `login` the first time. |

  - There are no password-only users. Every user is a Telegram identity, though some have never started the bot (placeholders created from @mentions). A password is just a second way to sign in for an existing user.
  - So there is no Telegram linking flow. There should be exactly one user per Telegram id. Duplicates aren't blocked by a DB constraint, because existing data may already have some. Instead they show up as merge suggestions on the admin Merge users screen (same Telegram id, or the same Telegram username ignoring case).

- **Admin endpoints** (`UsersController`):
  - Add `POST {id}/password`, which sets the login and password, so admins can create password users.
  - Fix `GET own` being admin-only: it moves to the profile controller below.
- **`BearerCommand.cs`:** only admins can mint a token for a mentioned user. Everyone else only ever gets their own token.
- **`Program.cs`:**
  - Add a global `JsonStringEnumConverter`.
  - Replace the Swagger scheme with an HTTP bearer scheme.
  - Add `UseDefaultFiles`, `UseStaticFiles` and `MapFallbackToFile("index.html")`.
  - Log a startup warning if the JWT key is still the default from appsettings, and document `DebtBot_DebtBot__JwtConfiguration__Key` in docker-compose.

### 1b. API extension (closing the gaps with the bot)
Every endpoint below uses `[Authorize]` and checks access with `IBillService.HasAccess`, or creator-or-admin for anything that changes data.

- **BillsController:**
  - `GET` (no suffix): bills I take part in. Filters: `withUserId`, `currencyCode`, `status`, plus paging. Reuses `GetForUser`.
  - `GET Own`: unchanged.
  - The admin "all bills" list moves to `GET All`.
  - `POST {id}/Finalize`: add the missing access check.
  - New `POST {id}/Cancel`, backed by `CancelAsync`, for the creator or an admin.
  - New `POST {id}/lines` and `POST {id}/payments`, which take structured creation models.
    - They add overloads to `BillService` that map to the existing parser-model paths (`fillLineParticipantUserIds` and `fillPaymentUserIds`), as react-ui-vibe sketched.
    - Draft bills only.
  - New `GET lines/{lineId}`, backed by `GetLine`.
  - New `POST text/preview`: parses the text with `IParserService.ParseBill` and returns the model and validation without saving, so the UI's "paste text" mode can show it first.
  - New `POST exchange`:
    - `BillService.AddExchange` creates the forward and backward bills, as on react-ui-vibe, but adds checks: amounts must be above zero and the two users must be different.
    - It creates both bills as **drafts**, exactly like Telegram's `/ExchangeDebt`.
    - A separate `POST exchange/finalize { forwardBillId, backwardBillId }` accepts the exchange. It mirrors the "Finalize" button in `FinalizeExchangeCommand` and runs with the same access checks.
    - The UI lists a draft exchange with an Accept button.
- **DebtsController:** `GET Own` returns the full `PagingResult` instead of `.Items`. Add class-level `[Authorize]`.
- **New `SpendingsController`:** `GET Own` (paged), backed by `IBudgetService.GetSpendings`.
- **New `ProfileController`** at `api/v1/profile`:
  - `GET` returns a self view: display name, phone, email, Telegram username, `TelegramBotEnabled`, login, and whether a password is set.
  - `PUT` updates display name, phone and email only. The Telegram id is never editable, which closes the hole on react-ui-vibe.
- **UserContactsController:**
  - `GET Own` returns `[]` instead of 404 when there are no contacts.
  - Add `PUT Own/{contactId}` to rename a contact.
  - Add `DELETE Own/{contactId}`, with a new `IUserContactService` method.
  - Add `POST Own/by-telegram?username=`, which reuses the `EnsureContact` flow: it finds the user by Telegram username, or creates a placeholder user the way bills do today.
  - There is no global user search across all users.
- **New models** go in `DebtBot.Models` (`Auth/*`, `Exchange/ExchangeCreationModel`, `Profile/*`). Add AutoMapper mappings in `Mappings/`.

### 1c. Tests
Add a small `DebtBot.Tests` project (xUnit) to the solution with:
- Telegram hash verification: a known-good payload, a tampered one, and an expired one.
- Password set and verify.
- Exchange bill amounts.

## Step 2: Frontend (`DebtBot/ClientApp`)

**Stack:** copy the nopds setup (`/mnt/git/nopds/src/Nopds.Web/ClientApp`):
- React 19, Vite, TypeScript
- Tailwind 4 with `@tailwindcss/vite`
- `react-router`
- `@tanstack/react-query`
- `@fontsource-variable/inter`
- `clsx`

**Shared pieces to copy and adapt:**
- `index.css`: the token structure and utilities (`btn-*`, `card`, `chip`, `pill-*`, `input`, `sidebar-btn`), with the palette from step 0.
- `hooks/useTheme.ts`: the light/dark/system toggle from abook.
- `components/Sidebar.tsx`: the abook shell. "DebtBot", a version pill, the emoji nav, and sign-out and theme buttons at the bottom.

**Plumbing:**
- `api.ts`: fetch or axios against `/api/v1`. The access token lives in memory, a 401 triggers one refresh call through the cookie, and API types are written in `types.ts`.
- The Vite dev server proxies `/api` to the backend.

**Pages:**
| Page | Contents |
|---|---|
| Login | Login/password form, plus the Telegram widget script built from `GET auth/config`. The widget is hidden if no bot username comes back. |
| 💸 Debts (home) | Balances per person and currency. "You owe" uses danger colour and "owed to you" uses success colour. Buttons for "Bills with them" and "Exchange / settle". |
| 🧾 Bills | Filters (person, currency, status, created by me), status pills, paging. |
| Bill detail | Header, lines with participants and parts, payments, ledger records. Draft actions: add lines, add payments, finalize, cancel. |
| ➕ New bill | **Form mode:** currency, payment currency, date, tips; lines that split between contacts from a picker (equal or by part); payments. **Paste text mode:** preview first, then create. Create as draft or finalize straight away. |
| 🔁 Exchange | Two users, two currencies, two amounts; shows the rate. |
| 📊 Spendings | Paged list linking to bills. |
| 👥 Contacts | List, rename, remove, add by @username, subscription request / confirm / decline. |
| 👤 Profile | Display name, phone and email; set or change password and login; Telegram account (read-only); bot status. |
| 🛠 Admin | Required. Admin only. **Users:** table with search, role, set password. **Merge users:** suggested duplicate pairs (same Telegram id or username) with a one-click merge, plus a manual merge picker. **All bills:** read-only. |

**Every view gets a mockup before it's built.** I render a static HTML mockup to a PNG and send it to you. You say what to change, I iterate, and I build only after you approve. Approved mockups are kept as the spec.

The UI shows the enums as strings, now that the API sends them that way.

## Step 3: Build and deploy
- **`DebtBot/Dockerfile`:** add a `node:22-alpine` stage that runs `npm ci && npm run build`, writing to `DebtBot/wwwroot`. The publish stage copies that output in.
  - Unlike the vibe branch, there is no npm step inside the csproj.
  - `wwwroot` goes into `.gitignore`, so no built bundle is committed.
- `.github/workflows/publish.yml` stays as is, because the image already includes the UI.
- `docker-compose.yml`: add the JWT key environment variable and a note about running BotFather `/setdomain` for the widget.

## Critical files
- **Changed:** `DebtBot/Program.cs`, `DebtBot/Controllers/{Bills,Debts,Users,UserContacts}Controller.cs`, `DebtBot/Services/BillService.cs` (and `IBillService`), `DebtBot/Telegram/Commands/Message/BearerCommand.cs`, `DebtBot.DB/Entities/User.cs`, `DebtBot.DB/DebtContext.cs`, `DebtBot/Dockerfile`, `docker-compose.yml`, `.gitignore`, `DebtBot.sln`
- **New:** `Controllers/{Auth,Profile,Spendings}Controller.cs`, `Services/AuthService.cs`, the migration, `DebtBot.Tests/`, `DebtBot/ClientApp/**`

## Verification
- `dotnet build` and `dotnet test`. The SDK is 10.0.115 and the projects target net8. If the net8 runtime is missing, run the tests inside the `sdk:8.0` container.
- `docker compose up --build` to start the API, Postgres and RabbitMQ. Then:
  - Check the migration applies.
  - In Swagger, sign in with the debug token, set a password with `PUT auth/password`, sign in with `POST auth/login`, and check `refresh` and `logout`.
  - Create a bill from the form endpoints, add lines and payments, finalize it, and check `Debts/Own`. Run an exchange. Check that another user's token gets 403/404 on finalize and cancel.
- **UI:** `npm run build` (typecheck) and `npm run dev` against the running API. Drive every page with the browser at desktop and phone widths, in light and dark, and take screenshots.
- **Telegram widget:** this needs the bot's domain set up and HTTPS. The hash check itself is covered by the unit test, so the live widget is checked at deployment.
- At the end, commit on `web-ui`, push, and open a draft PR.


## Task series
| # | Task | Brief |
|---|---|---|
| T1 | Backend auth: users' login/password, refresh tokens, Telegram Login Widget, `/Bearer` fix, JSON enums, Swagger, static hosting, test project | `briefs/task-01.md` |
| T2 | API extension (1b) plus the admin API: users list, set password, merge suggestions | `briefs/task-02.md` |
| T3 | Upgrade to .NET 11: all projects to net11.0, Docker images to 11.0, packages to their latest **free** versions (AutoMapper stays below 15, MassTransit on 8.x; Telegram.Bot to latest with the bot code adapted). Added 2026-11-23 | `briefs/task-03.md` |
| T4 | UI foundation: ClientApp scaffold, tokens, sidebar shell, login page, auth plumbing, Docker node stage. Mockups first | `briefs/task-04.md` |
| T5 | UI: Debts, Bills list, Bill detail. Mockups first | `briefs/task-05.md` |
| T6 | UI: New bill (form and paste-text modes), Exchange (draft, then accept). Mockups first | `briefs/task-06.md` |
| T7 | UI: Contacts, Spendings, Profile. Mockups first | `briefs/task-07.md` |
| T8 | UI: Admin (users, merge suggestions, all bills). Mockups first | `briefs/task-08.md` |
| T9 | End-to-end check in Docker, polish, docs, PR ready for review | `briefs/task-09.md` |
