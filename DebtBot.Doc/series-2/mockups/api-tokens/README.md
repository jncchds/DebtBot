# API tokens: Profile › API tokens and Admin › Users (approved 2026-11-24, round 2)

`profile-tokens.html` (query: `theme=dark`, `phone=1`, `state=tokens|new|created`) and `admin-users-tokens.html` (`state=main|revoke`) with the PNGs are the spec. `scripts/render.mjs` renders them.

## User decisions (rounds)
- **Round 1 → 2:** "Table will just show values and an arrow down key at the very end and that will reveal the action buttons." Rows hold values only. A ▾ button at the end of the row opens an action strip under it (▴ closes it). Only one row is open at a time, and it is in the URL as `?open=<id>`. Confirmations appear inside the open strip (danger tint).
- **Only these two tables** use the ▾ pattern for now (user, round 2). Other lists keep their inline buttons.
- Swagger is served in production too (user, during round 2).
- Approved in round 2.

## Profile › API tokens
A fourth Profile section: 🗝️ API tokens "Scripts and apps".
- **`/profile/tokens`** (`?open=<id>`): title "API tokens", subtitle "Let your scripts and apps call the DebtBot API as you. Treat a token like a password.", a primary **＋ New token** on the title row. Card "🗝️ Your tokens" with "N active · up to 20" in its header.
  - Columns: Name · Token (`dbt_<prefix>…`, monospace) · Scope (muted pill "Read only" / accent pill "Read and write") · Created · Last used ("never" muted) · Expires (date or "Never") · Status (success "Active", muted "Expired", danger "Revoked" with the revoke date under it) · ▾.
  - Order: active first, newest first; then expired; then revoked. Expired and revoked rows are faded and have no ▾.
  - The strip: **🚫 Revoke** → "Revoke **Name** (`dbt_…`)? Anything using it stops working at once. This can't be undone." **🚫 Revoke** / Keep.
  - Phone: cards (name + status + ▾; token + scope; created · last used; expires).
- **`/profile/tokens/new`:** card "＋ New token" (34rem) above the list: Name (help "So you can tell your tokens apart, such as the app or script that uses it."), Scope as two radio cards ("Read only: See your bills, debts, spendings and contacts." / "Read and write: Also create, change, finalize and cancel bills as you."), Expires as pills 30 days / **90 days** / 365 days / Never with "On <date>." (or "Never expires.") under it; **Create token** / Cancel (→ `/profile/tokens`).
- **`/profile/tokens/created`** (replaces `/new` in history): card "✅ Token created · Name": the warning "**Copy it now. You won't see it again.** Keep it secret: it works as you until it expires or you revoke it.", the token in a monospace box with **📋 Copy**, "Try it" with `curl -H "Authorization: Bearer dbt_…" <origin>/api/v1/Debts/Own`, a link to `/swagger`, and **Done** (→ `/profile/tokens`). The new row is highlighted in the list. The secret lives only in memory: a reload of `/created` goes to `/profile/tokens`.

## Admin › Users
- New column **API tokens** (active count, muted at 0) before ▾. The Login & password and Role columns lose their buttons.
- The open row's strip: **✏️ Change login or password** / **＋ Set login and password** (opens the existing dialog), **Make admin** / **Remove admin** (disabled for yourself), **🚫 Revoke all N API tokens** (when N > 0), **🗑 Delete** (placeholders that aren't you). The role, delete and 409 confirmations from T8 show inside the strip; Revoke all: "Revoke all **N** API tokens of **Name**? Scripts and apps using them stop working at once." **🚫 Revoke all** / Cancel.
- URL: `/admin/users?…&open=<id>`; the password dialog stays `/admin/users/<id>/password`.
- Phone cards: values only ("N bills · N contacts · N API tokens"), ▾ next to the role pill, the strip inside the card.

## Built (T2)
- `pages/ProfileTokens.tsx` (the three routes share one component, so the secret lives only in its state), `lib/apiTokens.ts`, `components/list/ExpandButton.tsx`, `ListView`'s `rowExpand`, and `pages/AdminUsers.tsx`.
- Built as specified. Smaller points:
  - after Revoke, the row closes (`open` is removed from the URL, replacing the entry);
  - "Copy" changes to "Copied"; if the clipboard is refused, a note asks to copy by hand (the token is `select-all`);
  - the tokens table switches to cards below 1024 px (`lg`), like Contacts;
  - Admin › Users keeps `open` when the filters or page change; a row that isn't on the page simply isn't open.

| PNG | What |
|---|---|
| `tokens-*` | the list, Monthly report script open with the revoke confirmation |
| `tokens-new-*` | the New token form, Home Assistant open |
| `tokens-created-*` | the shown-once secret and the list with the new token |
| `admin-users-*` | Users with the API tokens column, Boris open |
| `admin-users-revoke-*` | the Revoke all confirmation |
