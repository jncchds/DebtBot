# T2 report: API tokens (personal access tokens)

- **Branch:** `task/02-api-tokens`, based on the local `series-2` at `7da7c53`.
- **Commits:**
  - `cc3c4da`: the approved mockups (`series-2/mockups/api-tokens/`);
  - `c1b4d2f`: backend (entity and migration, auth scheme, scopes, JWT-only endpoints, rate limit, admin revoke-all, merge, Swagger) and the xUnit tests;
  - `973e5e4`: the web UI (Profile › API tokens, Admin › Users ▾ actions) and vitest;
  - the last commit: docs (`api.md`, `README.md`, the shell URL map), the e2e Admin Users step, the runtime check script and screenshots, and this report.

## Mockup rounds (user decisions)
1. **Round 1:** a fourth Profile section "🗝️ API tokens" (list, New token form, the shown-once secret) and Admin › Users with a token count and "Revoke all".
2. **Round 2:** the user: "Table will just show values and an arrow down key at the very end and that will reveal the action buttons." Both tables now show values only. A ▾ at the end of a row opens an action strip under it, and the open row is in the URL (`?open=<id>`). Confirmations show inside the strip. This also moved T8's Admin Users buttons (Login & password, role, delete) into the strip. During this round the user also asked to **include Swagger in the production version**.
3. **Approved** in round 2, with "let's stick to these tables only for now": other lists keep their inline buttons.

## What was built
### Backend
- **`ApiToken`** (`DebtBot.DB/Entities/ApiToken.cs`):
  - `Id`, `UserId` (FK, cascade on user delete), `Name` (≤ 100), `Prefix` (8 characters after `dbt_`), `Hash` (unique index), `Scope` (`Read`/`ReadWrite`, `DebtBot.Models.Enums.ApiTokenScope`), `CreatedAt`, `LastUsedAt?`, `ExpiresAt?`, `RevokedAt?`;
  - migration `20261124165718_ApiTokens`, made with dotnet-ef 11. It applies on a fresh database in the compose run.
- **Token:** `dbt_` + base64url of 32 bytes from `RandomNumberGenerator` (47 characters). Only `AuthService.HashToken` (SHA-256 hex) is stored. The secret is returned once, by create.
- **`ApiTokenService`** (`Services/ApiTokenService.cs`): list, create (validation and the limit), revoke, revoke-all, and `AuthenticateAsync`.
  - Authentication reads the token and its user in one query. Revoked or expired tokens, unknown hashes and missing users give null.
  - `LastUsedAt` is written by a conditional `ExecuteUpdate` (`LastUsedAt IS NULL OR <= now − 1 min`), so it's written at most once a minute even with parallel requests.
- **Authentication** (`Identity/AuthSetup.cs`, `Identity/ApiTokenAuthentication.cs`):
  - The default scheme is now a policy scheme, `JwtOrApiToken`. `Bearer dbt_…` (any case of "Bearer") forwards to the `ApiToken` handler, and everything else to JWT, unchanged.
  - The handler builds the **same claims as the JWT** through `IdentityService.UserClaims` (extracted from `GenerateAccessToken`), read from the DB per request, plus `auth_method=pat`, `pat_scope` and `pat_id`.
  - The auth, rate-limit and pipeline setup moved out of `Program.cs` into `AuthSetup` (`AddDebtBotAuthentication`, `AddDebtBotRateLimiter`, `UseDebtBotAuth`), so the tests host the real pipeline.
- **Scope:** `ApiTokenScopeMiddleware` runs after authentication. A `Read` token gets 403 `{ message: "This API token is read-only." }` on anything but GET, HEAD and OPTIONS, globally.
- **JWT only:** `[JwtOnly]`, an authorization filter that gives 403 `{ message }` for any API token. It's on `GET/POST/DELETE profile/tokens`, `PUT auth/password`, `POST auth/refresh`, `POST auth/logout`, `POST Users/{id}/password` and `POST Users/{id}/tokens/revoke-all`. Telegram sign-in takes no bearer, so nothing else needed it.
- **Rate limit:** a global partitioned limiter, a fixed window per `pat_id`, `DebtBot:ApiTokens:RateLimitPerMinute` (default 120; 0 turns it off). It answers 429 `{ message }` with `Retry-After`. JWT traffic goes to a no-limit partition. `UseRateLimiter` now runs after `UseAuthentication`; the per-IP `auth` sign-in policy is unchanged.
- **Endpoints:**
  - `GET profile/tokens` lists active first, then expired, then revoked, newest first within each;
  - `POST profile/tokens` `{ name, scope, expiresInDays: 30|90|365|null }` returns the record plus `token`. It gives 400 for a bad name, scope or expiry, and 409 at 20 active tokens (`DebtBot:ApiTokens:MaxActivePerUser`);
  - `DELETE profile/tokens/{id}` gives 204 and is idempotent (the first `revokedAt` stays). It gives 404 for someone else's token or an unknown id.
- **Admin:**
  - `AdminUser.apiTokenCount` counts active tokens;
  - `POST Users/{id}/tokens/revoke-all` (admin, JWT only) returns `{ revoked }`, or 404 for an unknown user;
  - admins never see names or prefixes.
- **Merge (decision): the merged user's tokens are revoked (deleted with that user), not moved.** This matches what a merge already does to that user's web sessions. A merge done by mistake can't hand the kept user's data to whoever holds the old user's token. The kept user's tokens are untouched.
- **Swagger:**
  - served in every environment: the commented-out `IsDevelopment` check is gone, and the Release image serves it;
  - the "Bearer" definition says a session JWT or a `dbt_…` token works the same way, and "Create an API token on your Profile, under API tokens".

### Web UI
- **👤 Profile › 🗝️ API tokens** (`pages/ProfileTokens.tsx`), a fourth Profile sub-section entry:
  - **`/profile/tokens?open=<id>`:** the list. Name · `dbt_xxxxxxxx…` · scope pill · created · last used · expires or Never · status, then ▾ (active tokens only). The strip has 🚫 Revoke, then the inline confirmation. "N active · up to 20" is in the card header, and below 1024 px the list shows as cards.
  - **`/profile/tokens/new`:** name, scope as two radio cards (Read only is preselected), expiry pills 30 / 90 / 365 days / Never (90 preselected) with "On <date>.", Create token and Cancel.
  - **`/profile/tokens/created`:** replaces `/new` in history. It shows the secret with 📋 Copy, the warning "Copy it now. You won't see it again.", the curl example with this origin's `/api/v1/Debts/Own`, a `/swagger` link and Done. The new row is highlighted.
  - The three routes share one component instance (the same `key`), so the secret lives only in its state. A reload of `/created` goes to the list, and leaving `/created` forgets the secret.
- **🛠 Admin › Users:**
  - rows show values only, with a new **API tokens** column and ▾ (`?open=<id>`);
  - the strip holds ✏️ Change login or password / ＋ Set login and password, Make admin / Remove admin (disabled for yourself), 🚫 Revoke all N API tokens (when N > 0) and 🗑 Delete (placeholders that aren't you);
  - T8's confirmations (role, delete, 409 → Merge…) and the new Revoke all confirmation show inside the strip, tinted red when destructive;
  - phone cards show values ("N bills · N contacts · N API tokens") with ▾ next to the role pill.
- **Shared code:**
  - `ListView` gains `rowExpand` (the strip under a row or inside a card);
  - `components/list/ExpandButton.tsx`;
  - `lib/apiTokens.ts` (form checks, request, expiry date, mask, curl, the `open` URL helpers);
  - Profile's `Card`/`Field` moved to `components/profile/bits.tsx` and `inputProps` to `components/profile/inputProps.ts`;
  - new strings in `locales/en/tokens.json` plus additions to `admin`, `list`, `pages` and `subnav`.

## Tests
- **.NET: 231 passed**, 0 failed (205 before, plus 26). They live in `DebtBot.Tests/ApiTokenTests.cs`, over `ApiTokenHost`: a TestServer with the real controllers and `AuthSetup` over the SQLite fixture. The test project gains `Microsoft.AspNetCore.TestHost` 11.0.0. They cover:
  - create returns the secret once (47 characters, prefix, only the hash stored); the HTTP list never carries `token`, the secret or a hash;
  - bad names and expiries (400); the limit of 20, freed by a revoke or an expiry (409 over HTTP);
  - revoke is idempotent and keeps the first `revokedAt`; someone else's token, an admin's attempt and an unknown id all give 404; the list order;
  - the handler: valid, unknown, malformed, revoked, expired and a deleted user (401);
  - a JWT still works, and the scheme selector (prefix, "bearer" in lower case, no header);
  - an admin token gets admin; a role change applies at once both ways; the display name comes from the DB;
  - `LastUsedAt` throttling (30 s no write, 61 s write);
  - read-only: 403 on POST Bills, PUT Bills/{id}, DELETE Users/{id} and PUT profile (and nothing changed), GET works; read-and-write can write;
  - a token gets 403 on each of the 8 JWT-only endpoints while a JWT doesn't, and a reflection check that they carry `[JwtOnly]`;
  - revoke-all is admin only (403 for a regular user, 404 for an unknown one), revokes only active tokens, leaves others alone, and `apiTokenCount` counts active only without exposing prefixes;
  - merge: the old user's tokens stop working and the kept user's keep working;
  - the rate limit (a host at 5 a minute): the 6th request is 429 with the message, another token is unaffected, and a session is not limited.
- **vitest: 130 passed** (122 before, plus 8 in `lib/apiTokens.test.ts`): the form defaults (Read only, 90 days), name rule, request, expiry date, mask, curl, `canOpen`/`activeCount`, and the `open` URL helpers. The Admin test fixture gained `apiTokenCount`.
- `npm run typecheck` and `npm run lint` (oxlint and the i18n check) are clean.

## Runtime check (docker compose, Release image built from this branch)
- The e2e stack (`web-ui/e2e`: the shipped compose files plus `compose.e2e.yml`), then `seed.mjs`. The migration `20261124165718_ApiTokens` is applied.
- **`reports/task-02/scripts/t2check.mjs`: 12/12 passed.** It covers:
  - **UI:** Profile › API tokens is empty and the sidebar entry is highlighted; New token preselects Read only and 90 days; an empty name gives its error.
  - **Read-only token "Home Assistant":** the secret is shown with the warning and the curl example, and it's not in the URL, localStorage or sessionStorage. A reload of `/created` goes to the list, which shows only `dbt_<prefix>…`.
  - **Read-and-write token "Budget script", Never:** Done goes back to the list. The API list has no `token` field, the scopes and expiries are right (90 days for the read-only one, `null` for the other).
  - **curl (fetch):**
    - `GET Debts/Own` works with both tokens, and `lastUsedAt` is then set;
    - `POST Bills`, `PUT Bills/{id}` and `POST Bills/{id}/Cancel` give 403 with the read-only token and 200 with read-and-write;
    - 403 "API tokens can't be used here" on all 7 JWT-only calls tried (`PUT auth/password`, `GET/POST profile/tokens`, `POST auth/refresh`, `POST auth/logout`, `POST Users/{id}/password`, `revoke-all`);
    - Anna's (admin) token lists users;
    - no token and an unknown token both give 401.
  - **Revoke in the UI** (▾ → 🚫 Revoke → confirm): the row shows Revoked, that token gets 401 and the other still gets 200.
  - **Rate limit:** the first 429 came at request **121**; another token and a session still got 200.
  - **Admin › Users:** Boris makes 2 tokens. His row shows **2**, and ▾ shows "🚫 Revoke all 2 API tokens"; after the confirmation the count is 0. His token then gets 401 and his list is all Revoked. A reload keeps the open row and Back closes it. `revoke-all` as Boris gives 403.
  - **Swagger** (`/swagger`, Release build): the Authorize dialog shows the `dbt_`/Profile hint. After authorizing with the read-only token, "Try it out" on `GET /api/v1/profile` answered **200** with Anna's profile.
- **Full e2e** (`web-ui/e2e`, fresh database each time; I changed `check.mjs`'s Admin › Users step to the ▾ strip). The API check passed **50/50** in all three runs, and every browser check passed in at least one fresh run. The failures were timing on the loaded machine (about 170 MB of free memory), not the change:
  - **Run 1** (`run.sh --no-build`): browser 66/74. The seed ran before the app's message bus had connected to RabbitMQ (`Connection Failed`, then `Bus started` later in the log), so 33 finalized bills stayed in Processing and the Spendings and All bills pages missed their data.
  - **Run 2** (the same, but waiting for `Bus started` before seeding): every flow passed, including Admin › Users through the ▾ strip. The admin tour at 390 px sat on "Loading…" (session restore) for 15 pages (59/74). That's the same flake T1's report records.
  - **Run 3** (the same as run 2): browser **73/74**. The one failure was the draft's "Save and finalize" waiting for "Finalized". The bill was Finalized in the database a little later, past the check's 15 s timeout.
  - **For T3:** `run.sh` could wait for `Bus started` before seeding.
- **Screenshots** (`reports/task-02/screenshots/`), 1280 and 390 px, light and dark:
  - `tokens` (the list), `tokens-new`, `tokens-created`, `tokens-revoke` (the confirmation in the strip);
  - `admin-users` (Boris open), `admin-users-revoke`;
  - `swagger-authorize`, `swagger-profile` (1280 light).
- **Clean-up:** the stack is down with its volumes, the image is removed, dangling images and the build cache are pruned, `wwwroot`/`bin`/`obj` are removed, and `dotnet build-server shutdown` was run.

**Not verified live:** the clipboard Copy itself (headless Chrome has no clipboard permission; the button and the fallback text are wired, and the secret is `select-all`), and the token expiring on its own (covered by tests with a fake clock).

## Decisions
- **Merge revokes the merged user's tokens** (see Backend).
- **The ▾ pattern** is only on these two tables (user). The open row is in the URL; the inline confirmations stay out of it, as in T8.
- **Admin Users' `open` survives filter and page changes** (`listSearch` keeps unknown parameters). A row that isn't on the page simply isn't open.
- **Status codes:** at the limit, create gives **409**; a deleted user's token gives **401** (its rows go with the user by cascade).
- **`GET auth/me` works with a token.** It's read-only and not listed as JWT-only.
- **`DebtBot:ApiTokens`** settings: `RateLimitPerMinute` (120) and `MaxActivePerUser` (20).

## Left open
- **For T3:**
  - the e2e could fold in `t2check.mjs` (token create/revoke, the curl checks, the rate limit);
  - `check.mjs`'s Admin step now uses the ▾ strip;
  - the tour screenshots of Admin › Users now show the new columns.
- Tokens aren't purged: revoked and expired rows stay, so the user sees their history. A purge like `RefreshTokenPurgeService` could come later.
