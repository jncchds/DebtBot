Task T2 (API tokens) of the DebtBot series-2 series. An orchestrator session runs the series one task at a time, with one session per task.

First, read `/mnt/git/DebtBot/DebtBot.Doc/series-2/briefs/common.md` and follow it throughout. To read it before your worktree exists, use `git -C /mnt/git/DebtBot show series-2:DebtBot.Doc/series-2/briefs/common.md`.

- Worktree: `/mnt/git/DebtBot/.claude/worktrees/task-02`
- Branch: `task/02-api-tokens`, based on the local `series-2` branch.
- Read:
  - `DebtBot.Doc/series-2/plan.md` section 2 and "User decisions";
  - `reports/task-01.md` (PUT Bills/{id});
  - `DebtBot.Doc/web-ui/reports/task-01.md` (auth, refresh token hashing), `task-07.md` (Profile) and `task-08.md` (Admin Users);
  - `DebtBot.Doc/web-ui/api.md`, `web-sign-in.md`;
  - the approved mockups `DebtBot.Doc/web-ui/mockups/profile/` and `admin-users/`.

## Goal
Users can create personal access tokens (PATs) on their Profile and call the REST API with `Authorization: Bearer dbt_…` in their own name, with every existing access check applying unchanged.

## What exists
- `Program.cs` has one JWT Bearer scheme (the default), the `Admin` policy as a claim, a fixed-window rate limiter for `auth`, and a Swagger "Bearer" definition with `BearerFormat = "JWT"`.
- `AuthService.HashToken` hashes refresh tokens (reuse it, or the same approach: SHA-256 of a high-entropy secret, so no slow hash is needed).
- Controllers read the user from claims via `DebtBotControllerBase` (`UserId`, `IsAdmin`).

## Scope
1. **Entity `ApiToken`** in `DebtBot.DB` and a migration made with `dotnet ef` (common.md):
   - `Id`, `UserId` (FK, cascade on user delete), `Name` (1 to 100 characters), `Prefix` (the first 8 characters after `dbt_`, shown in lists), `Hash` (unique index);
   - `Scope` (`Read` / `ReadWrite`), `CreatedAt`, `LastUsedAt?`, `ExpiresAt?`, `RevokedAt?`.
2. **Token format:** `dbt_` plus at least 32 random bytes, base64url, from `RandomNumberGenerator`. Only the hash is stored. The raw value is returned once, by create.
3. **Authentication:**
   - a policy scheme (the new default) forwards to a new `ApiToken` handler when the bearer value starts with `dbt_`, and to JWT otherwise;
   - the handler looks up the hash and refuses revoked or expired tokens and unknown users;
   - it builds the **same claims** as the JWT for that user, read from the DB at request time, so a role change or a merge takes effect at once; plus `auth_method=pat` and `pat_scope`;
   - it writes `LastUsedAt` at most once a minute per token (no write on every request).
4. **Scope enforcement:** `Read` tokens get 403 on anything but GET, HEAD and OPTIONS. Do it globally (a filter or middleware keyed on `pat_scope`), not per controller.
5. **JWT only** (a PAT gets 403, whatever its scope):
   - token management, password change (`PUT Auth/password`), `POST Users/{id}/password`, refresh and logout;
   - Telegram linking or sign-in endpoints, if any take a bearer.
   Use one attribute or policy (for example `[JwtOnly]`) and list the endpoints in api.md.
6. **Endpoints** (JWT only, own tokens):
   - `GET Profile/tokens`: name, prefix, scope, created, last used, expires and status (active, expired or revoked);
   - `POST Profile/tokens` `{name, scope, expiresInDays: 30|90|365|null}` returns the record plus `token` once;
   - `DELETE Profile/tokens/{id}` revokes (sets `RevokedAt`; idempotent; 404 for someone else's).
   Follow the existing route style (`api/v1/profile`). A limit of 20 active tokens per user.
7. **Admin** (user decision): Admin Users shows each user's active token count, and `POST Users/{id}/tokens/revoke-all` (admin, JWT only) revokes them. Admins never see token names or prefixes of others. Merging users moves the merged user's tokens to the kept user, or revokes them; pick one and say which.
8. **Rate limit:** a per-token fixed window for PAT requests (for example 120 a minute, as a setting); JWT traffic is unchanged.
9. **Swagger:** add a second security definition (or adjust the description) so it's clear that a `dbt_…` token works the same way, with a one-line "create one on your Profile" hint.
10. **Profile UI:** an "API tokens" card on Profile:
    - the list (name, `dbt_a1b2c3d4…`, scope pill, created, last used, expires or Never, status), with Revoke and a confirm step;
    - "New token": a form with name, scope (Read only / Read and write) and expiry (30 / 90 / 365 days / Never, 90 preselected);
    - after creating, the secret is shown once with Copy and "You won't see it again", plus a curl example `curl -H "Authorization: Bearer dbt_…" <origin>/api/v1/Debts/Own`.
    Every view has its own URL (for example `/profile/tokens/new`); the secret never goes in the URL or in storage.
11. **Admin Users UI:** the token count and the "Revoke all tokens" action, in the approved admin-users pattern.

## Mockups first
- The Profile tokens card: a list with active, expired and revoked rows; the New token form; the shown-once secret.
- The Admin Users additions.
- Light and dark, 1280 and 390 px, from the approved profile and admin-users mockups. Show the real page, with each section once (the user's rule from T1), not a sheet of states.
- Get approval, commit under `DebtBot.Doc/series-2/mockups/api-tokens/`, then build.

## Tests
xUnit:
- the handler: valid, revoked, expired, unknown hash and a deleted user;
- a JWT still works; an admin PAT gets admin; a role change applies at once;
- a read-only token gets 403 on POST, PUT Bills/{id} and DELETE, and GET works;
- a PAT gets 403 on the JWT-only endpoints;
- create returns the secret once and list never does;
- revoke someone else's token gives 404; the limit of 20; revoke-all is admin only;
- LastUsedAt throttling; the merge behaviour.

vitest for the Profile card logic. All existing tests pass (205 .NET, 122 vitest after T1).

## Runtime check
`docker compose`:
- create a read-only and a read-write token in the UI;
- with curl: GET Debts/Own works with both; POST Bills works only with read-write; PUT Auth/password is 403 with a PAT;
- revoke a token and the next call is 401; revoke-all as admin;
- Swagger "Authorize" with a PAT works.

Screenshot the real views in both themes and widths. Bring it down and clean up.

## Docs
- `reports/task-02.md`;
- an "API tokens" section in `DebtBot.Doc/web-ui/api.md` (format, header, scopes, JWT-only endpoints, rate limit, errors);
- a short "Calling the API" section in the top-level `README.md`;
- new URLs in `mockups/shell/README.md`.

Send 3 or 4 screenshots of the built views with SendUserFile (display: render).

When you're done, follow "Finishing" and "Waking the orchestrator" in common.md.
