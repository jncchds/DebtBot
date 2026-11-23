Task T1 (backend auth) of the DebtBot web-ui series. An orchestrator session runs the series one task at a time, with one session per task.

First, read `/mnt/git/DebtBot/DebtBot.Doc/web-ui/briefs/common.md` and follow it throughout. To read it before your worktree exists, use `git -C /mnt/git/DebtBot show web-ui:DebtBot.Doc/web-ui/briefs/common.md`.

- Worktree: `/mnt/git/DebtBot/.claude/worktrees/task-01`
- Branch: `task/01-auth`, based on the local `web-ui` branch.
- Plan sections to read: "Step 1 › 1a" and "1c" in `DebtBot.Doc/web-ui/plan.md`.

## Goal
Users can get an API session in two ways, safely:
- with the Telegram Login Widget;
- with an optional login and password that an existing user sets for themselves, or that an admin sets for them.

The `/Bearer` hole is closed, and the API is ready to serve a SPA.

## Scope
1. **Data:**
   - Add `Login` (nullable, unique, stored lowercase) and `PasswordHash` (nullable) to `DebtBot.DB/Entities/User.cs`.
   - Add a new `RefreshToken` entity: `Id`, `UserId` (foreign key), `TokenHash`, `CreatedAt`, `ExpiresAt`, `RevokedAt`, and optionally `ReplacedById`.
   - Create the migration `WebAuth` with `dotnet ef`, as described in common.md. Don't add a unique index on `TelegramId`; common.md explains why.
2. **Auth service** (new `DebtBot/Services/AuthService.cs` and its interface):
   - Passwords use `Microsoft.AspNetCore.Identity.PasswordHasher<User>`, with rehash on `SuccessRehashNeeded`. abook does the same: see `/mnt/git/abook/src/ABook.Api/Controllers/AuthController.cs`.
   - Telegram Login Widget verification:
     - Build the data-check-string from the fields present in the payload (`id`, `first_name`, `last_name`, `username`, `photo_url`, `auth_date`): sorted `key=value` lines joined with `\n`, without `hash`.
     - Compute `HMAC-SHA256` over it, keyed with `SHA256(BotToken)`, and compare it to `hash` in constant time.
     - Reject if `auth_date` is older than 24h.
     - A reference implementation is on the branch `origin/react-ui-vibe` (`git show origin/react-ui-vibe:DebtBot/Controllers/IdentityController.cs`). Use it as reference only.
   - After verification, find or create the user with the existing `IUserService.FindOrAddUser` and `ActualizeTelegramUser`, exactly as the bot does.
   - Refresh tokens:
     - Random 32 bytes. Store only the hash.
     - Rotate on every refresh. Reusing a revoked token revokes that whole chain.
     - Send it as an httpOnly, Secure (except in Development), SameSite=Strict cookie scoped to `/api/v1/auth`.
     - Lifetime is configurable, defaulting to 30 days.
     - Access tokens come from the existing `IdentityService.GenerateJwtToken`, 60 minutes by default.
3. **`AuthController`** at `api/v1/auth`:
   - `GET config` (anonymous): returns `{ telegramBotUsername }`. Get the username from `ITelegramBotClient.GetMeAsync()` once and cache it. Return null when there's no bot token or the call fails.
   - `POST telegram`: takes the widget payload (snake_case JSON names, as the widget sends them) and returns a session `{ user: { id, displayName, telegramUserName, isAdmin }, accessToken, accessTokenExpiresAt }`.
   - `POST login`: takes `{ login, password }` and returns a session. On failure, return the same 401 message whether the login or the password was wrong.
   - `POST refresh` and `POST logout`. Logout is anonymous, revokes the cookie's token and clears the cookie.
   - `GET me`.
   - `PUT password` for the current user, taking `{ login?, currentPassword?, newPassword }`:
     - The first time, it sets the login and password. After that, changing either needs the current password.
     - The login must be unique; return 409 if it's taken.
     - Set a minimum password length, for example 8.
   - Add basic brute-force protection on `login`, for example the ASP.NET Core rate limiter per IP and login.
4. **Admin:** add `POST api/v1/Users/{id}/password` to `UsersController`, which already requires Admin. It sets the login and password for any user.
5. **`DebtBot/Telegram/Commands/Message/BearerCommand.cs`:**
   - A token for a mentioned user is issued only when the sender is an admin.
   - Everyone else always gets their own token, and the mention is ignored.
6. **`DebtBot/Program.cs`:**
   - Add a global `JsonStringEnumConverter` to the MVC JSON options. Check whether anything else relies on numeric enums in the API JSON.
   - Replace the Swagger security scheme with HTTP bearer (`Type = Http`, `Scheme = "bearer"`).
   - Add `UseDefaultFiles()`, `UseStaticFiles()` and `MapFallbackToFile("index.html")`. The fallback must not swallow unknown `/api/*` routes or `/swagger`; those should still return 404.
   - Log a startup warning when `JwtConfiguration:Key` is still the value from appsettings.json.
   - Call `UseAuthentication()` explicitly before `UseAuthorization()`.
   - If the API can't start without a Telegram bot token, make the bot optional: skip polling and log a warning when `BotToken` is empty. This is so local runs work, and so the UI can hide the widget.
   - Don't add CORS, because the SPA is same-origin. The Vite dev proxy comes in T3.
7. **docker-compose.yml:** add `DebtBot_DebtBot__JwtConfiguration__Key` (with a placeholder and a comment), plus a note that the Telegram Login Widget needs BotFather `/setdomain` and HTTPS.
8. **Tests:** add `DebtBot.Tests` (xUnit, net8.0) to `DebtBot.sln`, covering:
   - Telegram verification:
     - a known-good payload computed in the test with a fake bot token;
     - a payload with optional fields left out;
     - a tampered field;
     - a wrong hash;
     - an expired `auth_date`.
   - Password set, verify and change rules.
   - Refresh token rotation and reuse detection. Use EF InMemory or SQLite, whichever fits the existing code, if needed.
   - Bearer authorization: non-admin with a mention gets their own token; admin gets the mentioned user's.
   - Make them pass. Run them as described in common.md.
9. **Runtime check:**
   - Run `docker compose` (the override builds from `DebtBot/Dockerfile`) and confirm that the migration applies.
   - Then with curl:
     - get a token through the DEBUG `GET api/v1/Identity` in Development, or with `/Bearer`;
     - `PUT auth/password`;
     - `POST auth/login`;
     - `POST auth/refresh` with the cookie (rotation, and reuse of the old token rejected);
     - `POST auth/logout`;
     - `GET auth/config`;
     - confirm that a non-admin gets 403 on `Users/{id}/password`;
     - confirm that `/api/v1/nonexistent` returns 404 and not index.html.
   - Bring the stack down afterwards and clean up images and cache as described in common.md.
10. **Docs:** add a short "Web sign-in" section to `DebtBot.Doc/` (or the report) covering the endpoints, the settings, and the BotFather domain step.

There are no screenshots for this task, since there's no UI.

When you're done, follow "Finishing" and "Waking the orchestrator" in common.md.
