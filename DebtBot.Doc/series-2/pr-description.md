## Summary
Series 2 builds on the web app from PR #34. It has two features and a plan for a third:

- **Leaving a bill, and editing drafts in the same form.** Anyone can be taken off a bill's people, including its
  creator. A draft now opens in the same form as New bill. Lines can be edited (✎) on a new bill and on a draft, and
  the whole draft is saved with one new endpoint, `PUT Bills/{id}`.
- **Personal API tokens.** Scripts and apps call the REST API with a `dbt_…` token made on 👤 Profile › API tokens.
  A token is read-only or read-and-write and has an expiry. It's rate-limited and can be revoked. An admin can
  revoke all of a user's tokens. Swagger is now served in every environment.
- **Receipt parse (planned, not built):** a design for prefilling New bill from a photo of a receipt through an LLM.
  See "Planned, not built" below.

Every new view was built from a mockup the user approved (`DebtBot.Doc/series-2/mockups/`). The plan, the decisions
and a report per task are in [`DebtBot.Doc/series-2/`](https://github.com/jncchds/DebtBot/tree/series-2/DebtBot.Doc/series-2).

## Tasks
| | |
|---|---|
| T1 | The creator can leave and rejoin a bill's people; the lock rule; a draft is the New bill form; ✎ Edit line; `PUT Bills/{id}` — [report](https://github.com/jncchds/DebtBot/blob/series-2/DebtBot.Doc/series-2/reports/task-01.md) |
| T2 | API tokens: entity and migration, the JWT-or-token scheme, scopes, JWT-only endpoints, the rate limit, Profile › API tokens, Admin › Users ▾ actions and revoke-all, Swagger — [report](https://github.com/jncchds/DebtBot/blob/series-2/DebtBot.Doc/series-2/reports/task-02.md) |
| T3 | End-to-end check (fresh stack ×3 and the upgrade from `main`), e2e stability, polish, docs, this description — [report](https://github.com/jncchds/DebtBot/blob/series-2/DebtBot.Doc/series-2/reports/task-03.md) |

## What changed for users
- **People on a bill.** Your own chip has a × like everyone else's. "On the bill" means taking part in a line or a
  payment. The lock rule is the same for everyone: a person shows • (can't be removed) while they paid, are on a
  by-parts line, or are the only person on a line. Removing someone takes them off every equal-split line, and those
  lines are split again among the people left. To add yourself back, use the People search ("Anna Petrova (you)").
  As the creator you still own the bill when you're on nothing: it stays in your Bills, and you can edit, finalize
  or cancel it. If you're on no line and no payment, finalizing gives you no debts and no spending.
- **Drafts are the New bill form.** The draft page shows the same Bill, People, Lines (✎ edit, ✕ remove), Payments
  and preview sections as New bill. Changes are kept in the tab until you **Save**. **Save and finalize** saves, then
  finalizes. ✎ Edit line (people, parts, item, subtotal) has its own URL: `/bills/new/edit-line/<n>` and
  `/bills/<id>/edit-line/<n>`.
- **👤 Profile › 🗝️ API tokens** (`/profile/tokens`): a list of your tokens, and New token (name, Read only or Read
  and write, an expiry of 30, 90 or 365 days or never). The secret is shown once, with Copy and a curl example. You
  revoke a token from the ▾ at the end of its row. The open row is in the URL (`?open=<id>`).
- **🛠 Admin › Users:** the rows now show values only, plus an API tokens count. The row's actions (login and
  password, role, 🚫 Revoke all N API tokens, delete) are in a ▾ strip under the row, with the confirmations inside
  it. Other lists still have their buttons inline.

## API changes
Details are in [`api.md`](https://github.com/jncchds/DebtBot/blob/series-2/DebtBot.Doc/web-ui/api.md).
- **`PUT Bills/{id}`** (new) replaces a draft's fields, lines and payments in one transaction. It's validated like
  `POST Bills` and allowed for the creator or an admin.
  - 409 when the bill is no longer a draft (checked again inside the transaction);
  - 400 for an exchange bill;
  - 403 for a participant who isn't the creator or an admin; 404 without access.
  - The bill's participants are rebuilt from the new lines and payments, and the creator always stays.
- **API tokens:** `Authorization: Bearer dbt_…` works wherever a session token does, with the same access rules.
  - **Endpoints:** `GET/POST/DELETE profile/tokens` for your own tokens; `POST Users/{id}/tokens/revoke-all` for
    admins. `AdminUser.apiTokenCount` is new. Admins never see other people's tokens.
  - **Scopes:** a read-only token gets 403 on anything but GET, HEAD and OPTIONS.
  - **JWT only:** a token gets 403 on the token endpoints, `PUT auth/password`, `POST auth/refresh`,
    `POST auth/logout`, `POST Users/{id}/password` and revoke-all.
  - **Rate limit:** 120 requests a minute per token, then 429 with `Retry-After`. Sessions aren't limited.
  - **Limits:** at most 20 active tokens per user (409 beyond that).
- **Authentication:** the default scheme is now a policy scheme. It sends `dbt_…` to the token handler and
  everything else to JWT, unchanged. A token's claims are read from the database on every request, so a role change
  applies at once.
- **Swagger is served in every environment**, Production included. Its "Authorize" takes a session token or an API
  token.
- **Merging users** now deletes the merged user's tokens, so they stop working; they aren't moved to the kept user.

## Upgrade notes
- **Migration `ApiTokens`** (applied automatically on start) adds the `ApiTokens` table. It has a unique index on
  `Hash` and a foreign key to `Users` with cascade delete. Nothing else in the schema or the data changes.
- **New optional settings:** `DebtBot_DebtBot__ApiTokens__RateLimitPerMinute` (120; 0 turns the limit off) and
  `DebtBot_DebtBot__ApiTokens__MaxActivePerUser` (20).
- **Swagger in Production:** `/swagger` is now public in every environment. It lists the API but doesn't expose
  data: every endpoint still needs a token.
- **Web clients only:** the web no longer calls `POST Bills/{id}/lines|payments`. Those endpoints stay for the API
  and the bot.

## How it was tested
- **Unit and integration:** `dotnet test` runs **231** tests (205 after T1, plus 26 for tokens) and
  `npm test` runs **129** vitest tests. `npm run typecheck` and `npm run lint` are clean.
  - The .NET tests cover `PUT Bills/{id}` (fields, participants, access, validation, the race with finalize,
    exchanges) and a creator who's on nothing.
  - The token tests run on a TestServer with the real auth pipeline: the handler, scopes, JWT-only endpoints, the
    rate limit, revoke and revoke-all, merge, and `LastUsedAt`.
- **End to end in Docker** (`DebtBot.Doc/web-ui/e2e/`, extended for this series; `run.sh` on a fresh database):
  E2E_NUMBERS
  - The API check adds series 2: `PUT Bills/{id}` and its status codes, a bill its creator isn't on, and the token
    rules (scopes, JWT-only, revoke, the 429 with a low limit on the e2e stack, revoke-all).
  - The browser check adds: you leave and rejoin a bill (the payer and by-parts locks, ✎ Edit line on New bill and
    on a draft, Save and Finalize); Profile › API tokens (the shown-once secret, revoke from the ▾ strip); Admin ›
    Users ▾ Revoke all; Swagger with a token. The tour now includes the new pages at 1280 and 390 px, light and
    dark.
- **Upgrade from `main`** (e7a93a3, the web-ui merge): main's image is seeded through its own API with the e2e data,
  then this branch runs on the same database. UPGRADE_NUMBERS
- **e2e stability:**
  - `run.sh` waits until the app's message bus is connected before it seeds;
  - finalize and accept checks poll the API for the status;
  - the Chrome driver now removes its profile. Leftover profiles had filled the small `/tmp`, and that stalled
    requests (FLAKE_NOTE).

## Planned, not built
- **Receipt parse and prefill:** a photo or the text of a receipt goes to an LLM through OpenRouter, and New bill is
  prefilled from it. People and payments are still chosen by hand, and nothing is saved automatically.
- The design is in
  [`DebtBot.Doc/series-2/receipt-parse.md`](https://github.com/jncchds/DebtBot/blob/series-2/DebtBot.Doc/series-2/receipt-parse.md):
  the flow, the endpoint, the settings, the JSON schema, errors and limits, privacy, tests and open questions.
- It gets built in a later series.

## Known follow-ups
- **Exchange drafts can't be edited** (`PUT` gives 400). They can be accepted or cancelled, as before. Editing one
  half would break the mirrored pair.
- **Other lists still have inline buttons** (Contacts, Subscriptions, Bills, Merge users). The ▾ action strip is
  only on Profile › API tokens and Admin › Users for now, by the user's decision.
- **Revoked and expired tokens are never purged.** They stay so the user can see their history. A purge job like
  `RefreshTokenPurgeService` could come later.
- **People who are on no line or payment aren't saved.** Until Save they live only in the tab's form, which matches
  "on the bill means on a line or a payment".
- **Carried over from PR #34:** the AutoMapper NU1903 advisories, the live Telegram widget check at deployment, and
  DataProtection keys that aren't persisted.

🤖 Generated with [Claude Code](https://claude.com/claude-code)

https://claude.ai/code/session_01LEFDEvixE1m3MmWhL5S4YV
