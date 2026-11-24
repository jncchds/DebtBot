# T1 report: backend auth

- **Branch:** `task/01-auth`, based on local `web-ui` at `de2aa12`. The orchestrator handover commit `857af01` came after it and touches only the docs.
- **Commits:**
  - `6497b2a` Web auth: login/password, refresh tokens, Telegram Login Widget, /Bearer fix
  - `dbe595d` Add DebtBot.Tests
  - `e8ae7d1` Compose: JWT key and BotFather docs; start without Telegram settings
  - the next commit adds these docs (`web-sign-in.md` and this report)

## What was built
**Data:**
- `User` gets `Login` (nullable, max 64 characters, unique index `IX_Users_Login`, stored lowercase) and `PasswordHash` (nullable).
- The new `RefreshToken` entity has `Id`, `UserId` (FK with cascade), `TokenHash` (unique index), `CreatedAt`, `ExpiresAt`, `RevokedAt` and `ReplacedById`.
- The migration `20261123164623_WebAuth` was made with `dotnet ef migrations add WebAuth --project ../DebtBot.DB`. There's no index on `TelegramId`. The snapshot keeps its CRLF line endings.

**`Services/AuthService.cs`** (with `IAuthService`):
- **Passwords:**
  - uses `PasswordHasher<User>`, and rehashes on `SuccessRehashNeeded`;
  - when the login is unknown, it still verifies against a dummy hash, so both failures take about the same time;
  - after 5 failures per login in 15 minutes, that login is locked (`LoginThrottle`, in memory).
- **Telegram:**
  - `TelegramLoginVerifier` builds the data-check-string from the non-null fields, computes HMAC-SHA256 keyed with SHA256(bot token), and compares in constant time with `FixedTimeEquals`;
  - it rejects an `auth_date` older than 24h, or more than 5 minutes in the future;
  - after that it calls `ActualizeTelegramUser`, then `FindOrAddUser({ TelegramId, @username, displayName })`, as the bot does.
- **Refresh tokens:**
  - random 32 bytes, stored as a SHA-256 hex hash;
  - rotated on every refresh, with `RevokedAt` and `ReplacedById` set on the old row;
  - a reused rotated token revokes the whole chain that descends from it, while the user's other sessions stay;
  - lifetime is `JwtConfiguration:RefreshTokenLifetimeDays`, default 30.
- **Access tokens:** they come from `IdentityService`. I added `GenerateAccessToken`, which returns the token and its UTC expiry, and `GenerateJwtToken` now delegates to it. Expiry now uses UTC, default 60 minutes.

**`TelegramBotInfoProvider`** (singleton): calls `GetMeAsync` once and caches the username. After a failure it retries at most every 5 minutes, and it returns null when there's no bot token.

**`Program.cs`:**
- global `JsonStringEnumConverter`;
- Swagger `Http`/`bearer` scheme;
- `UseDefaultFiles`, `UseStaticFiles`, and `MapFallbackToFile("{*path:regex(^(?!(api|swagger)(/|$)).*$):nonfile}", "index.html")`;
- the `auth` rate limiter (10 per minute per IP) on `login` and `telegram`;
- explicit `UseAuthentication()`;
- at startup:
  - it refuses to start with a JWT key under 32 bytes;
  - it logs a warning when the key is the appsettings default;
  - it logs a warning when there's no bot token.

**Bot optional:**
- `PollingService` doesn't poll and logs a warning when there's no real bot token (empty, or the `YOUR:TOKEN:HERE` placeholder).
- The bot client is built with a dummy token in that case, so the consumers that depend on it can still be constructed.

**Pre-existing bug fixed:** `docker-compose.yml` ships `DebtBot_DebtBot__Telegram__LogChatId=` empty, and that crashed config binding at startup (`'' is not a valid value for Int64`). `LogChatId` is now `long?`. Without this fix, the compose file as shipped never started.

**`BearerCommand`:** looks up the sender by Telegram id. Only when the sender is an admin does a mention pick the target. Otherwise the mention is ignored and the sender gets their own token.

**`docker-compose.yml`:**
- a commented `DebtBot_DebtBot__JwtConfiguration__Key` placeholder, with how to generate one;
- notes on the bot token, BotFather `/setdomain` and HTTPS.

**`.gitignore`:** `DebtBot/wwwroot/`.

**Docs:** `DebtBot.Doc/web-ui/web-sign-in.md` covers the endpoints, rules, settings and the BotFather step.

## Endpoints
| Endpoint | Auth |
|---|---|
| `GET api/v1/auth/config` | anonymous |
| `POST api/v1/auth/telegram` | anonymous, rate-limited |
| `POST api/v1/auth/login` | anonymous, rate-limited, plus per-login lockout (429) |
| `POST api/v1/auth/refresh` | cookie |
| `POST api/v1/auth/logout` | anonymous, returns 204 |
| `GET api/v1/auth/me` | bearer |
| `PUT api/v1/auth/password` | bearer, returns 204, 400 `{message}` or 409 |
| `POST api/v1/Users/{id}/password` | Admin, returns 204, 400, 404 or 409 |

The session shape is `{ user: { id, displayName, telegramUserName, isAdmin }, accessToken, accessTokenExpiresAt }`. Errors are `{ message }`.

## Tests
`DebtBot.Tests` (xUnit 2.9.3, net8.0, NSubstitute, EF InMemory) is added to `DebtBot.sln`. **52 passed, 0 failed**, with `DOTNET_ROLL_FORWARD=Major dotnet test DebtBot.sln`.
- **`TelegramLoginVerifierTests`:**
  - a known-good payload signed independently in the test;
  - the data-check-string format;
  - optional fields left out;
  - an empty field that wasn't signed is rejected;
  - 5 tampered fields;
  - 5 wrong hashes;
  - a hash made with another bot's token;
  - an uppercase hash;
  - an expired `auth_date`, and one in the future.
- **`AuthServicePasswordTests`:**
  - first-time set, with lowercase storage and sign-in;
  - login and password required;
  - invalid logins;
  - too-short password;
  - login taken, ignoring case;
  - changing needs the right current password;
  - login-only change;
  - wrong login and wrong password give the same error;
  - a login without a password can't sign in;
  - lockout;
  - rehash from an IdentityV2 hash;
  - the admin set rules;
  - admin set signs the user out everywhere;
  - an own change keeps the current session.
- **`AuthServiceSessionTests`:**
  - tokens are random, 32 bytes, and stored only as a hash;
  - rotation;
  - reuse revokes the chain;
  - reuse doesn't touch other sessions;
  - expired and unknown tokens are rejected;
  - logout;
  - session and JWT claims;
  - Telegram sign-in calls Actualize and then FindOrAdd;
  - a bad hash doesn't touch users;
  - no bot token means rejection.
- **`BearerCommandTests`:**
  - a non-admin with or without a mention gets their own token;
  - an admin with a mention gets the mentioned user's token;
  - an admin without a mention gets their own;
  - an unknown sender gets none.

## Runtime check (docker compose, Development)
- **How I ran it:**
  - `docker compose -p debtbot-t1 -f docker-compose.yml -f docker-compose.override.yml -f <tmp>/compose.t1.yml up -d --build`;
  - the extra file sets a JWT key, uses a named DB volume instead of `./dbdata`, and mounts a stub `wwwroot/index.html`;
  - the image is a Release build, so the DEBUG `GET api/v1/Identity` doesn't exist and there's no bot for `/Bearer`;
  - for the first admin token, I minted a JWT with the same HS256 key and claims that `IdentityService` uses. All later tokens came from `auth/login`.
- **Migrations:** they apply on a fresh DB, `__EFMigrationsHistory` has `20261123164623_WebAuth`, and `RefreshTokens` and `IX_Users_Login` exist.
- **Results:**
  - `GET auth/config` → 200 `{"telegramBotUsername":null}` (no bot token).
  - `GET auth/me`:
    - no token → 401;
    - admin → 200.
  - `GET Users` → `"role":"Admin"`, so enums are now strings.
  - `PUT auth/password`:
    - short password → 400;
    - first set → 204;
    - stored as `admin.user`, with the hash starting `AQAAAAIAAYag` (Identity V3);
    - change without the current password → 400;
    - change with a wrong current password → 400.
  - `POST auth/login`:
    - wrong password → 401 and unknown login → 401, with the same message;
    - `ADMIN.USER` → 200, with `Set-Cookie: debtbot_refresh=…; path=/api/v1/auth; samesite=strict; httponly` (no `secure` in Development).
  - `POST auth/refresh` → 200, and the cookie value changed.
  - The **old** cookie again → 401, and after that the **new** cookie → 401 too (chain revoked).
  - After `POST auth/logout` → 204, with the cookie cleared. Refreshing again → 401. All 3 `RefreshTokens` rows are revoked.
  - `POST Users/{id}/password` as admin:
    - login taken → 409;
    - set → 204, and that user can then sign in.
  - `POST Users/{id}/password` as a non-admin → **403**, and without a token → 401.
  - `POST auth/telegram` with no bot → 401 "Telegram sign-in is not configured."
  - Fallback:
    - `/`, `/bills` and `/bills/123` → index.html;
    - `/api/v1/nonexistent`, `/api` and `/swagger/nope` → **404**;
    - `/favicon.ico` → 404.
  - Swagger lists the scheme as `{type: http, scheme: bearer}`, and `UserRole` as a string enum.
  - Rate limit: 12 quick logins gave `401 ×4`, then `429 ×8` (earlier logins in the run had used up the rest of the 10 per minute).
  - With the default appsettings key, the warning is logged. With `Key=short`, startup fails with a clear message.
  - Warnings for no bot token and "not polling Telegram" are logged.
- **Clean-up:** the stack is down with `-v`, I removed the built `jncchds/debtbot` image and the pulled postgres/rabbit images, and ran image and builder prune.
- **Only compiled or unit-tested, not run live:**
  - the real Telegram widget, which needs a bot, `/setdomain` and HTTPS;
  - `GET config` with a real bot;
  - the `/Bearer` command in Telegram.

## Decisions
- **No grace window on reuse.** If two tabs refresh with the same cookie at once, the loser triggers reuse detection, which signs that browser out. T3's `api.ts` should share one in-flight refresh promise. abook allows a 30-second grace window. That's easy to add later if the UI needs it, but the brief asked for strict rejection.
- Two concurrent rotations of the same token aren't serialized (no row lock). The worst case is two child tokens. It's acceptable for now.
- **Password rules:**
  - first time: a login (unless one is already set) and a password;
  - later: `currentPassword` for any change;
  - `newPassword` can be left out to change only the login;
  - a password change revokes the user's other refresh tokens, keeping the caller's cookie;
  - an admin set revokes all of them.
- Validation errors return 400 rather than 401/403, so the SPA's "401 → refresh" logic isn't triggered by a wrong current password.
- Telegram `first_name` is technically optional. When it's missing, the display name falls back to the username, then the id.
- **String enums:** nothing in the repo relied on numeric enums in API JSON. There's no UI on main, MassTransit has its own serializer, and query binding of `UserRole` accepts names and numbers. `ProcessingState`, `UserRole` and `ObjectType` now go out as names.
- The JWT key check is strict (it fails under 32 bytes), because HS256 signing would throw on such a key anyway.

## Open for later tasks
- **T2:** `UsersController.GET own` is still admin-only, as planned for the profile controller. The users list for admins doesn't yet show `login` or whether a password is set.
- **T3:**
  - the SPA should call `POST auth/refresh` on load and on 401, with one shared promise;
  - the widget script uses `config.telegramBotUsername`;
  - `wwwroot` is now in `.gitignore`.
- **Deployment:**
  - forwarded headers (`X-Forwarded-For`/`Proto`) aren't set up, so behind a proxy the per-IP limit is shared;
  - DataProtection keys aren't persisted, which doesn't affect JWT or refresh tokens.
- Old refresh-token rows are never purged. A periodic clean-up could come later.
