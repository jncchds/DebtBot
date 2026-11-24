# DebtBot series 2: plan

Status: **draft, being agreed with the user** (2026-11-24). Orchestrator: "DebtBot orchestrator 2" (id `645e1162`). Branch `series-2` from `main` (after PR #34), one draft PR to main.

## 1. Creator can leave and rejoin a bill's people

### What the code does today
- **UI:** the creator is forced in. `lockedPeople()` (`ClientApp/src/lib/newBill.ts`) always adds `meId`, and the draft page's `billPeople()` (`pages/Bill.tsx`) always adds `meId` and locks everyone in it. `emptyNewBill(meId)` starts with you on the bill. Anyone on a line or a payment is locked (the chip shows • instead of ×).
- **Backend:** no rule needs the creator on a line or as a payer. Bill creation and validation (`BillService`, `fillLineParticipantUserIds`) only resolve the people named. `BillRepository.ensureParticipants` always adds the creator as a `BillParticipant`, which is what puts the bill in the creator's lists and keeps creator rights (edit, finalize, cancel). Finalize (`BillFinalizedConsumer`) works per line and payment, so a creator who is on neither simply gets no debts.

### Proposal
- UI-only change: "on the bill" (PeoplePicker) means taking part in lines or payments. The creator is still the owner: still a `BillParticipant` on the server, still sees the bill in Bills, still edits and finalizes it.
- Your own chip gets a × like anyone else's; "Add yourself" puts you back (you are in the search list as "Anna (you)").
- New bill starts with you on it, as today.
- **Removal while you are on lines or payments** (user decision, see Questions).
- Bill page: "People" count and "by Anna" subtitle are unchanged.
- Mockups first (New bill and draft bill people card).

## 2. API tokens (personal access tokens)

### How it's usually done
Personal access tokens, as on GitHub, GitLab and others:
- The user makes a token on the Profile page with a name and an optional expiry. It is a random secret with a recognisable prefix (`dbt_…`), shown **once**; only its hash is stored (as T1's refresh tokens are). The record keeps name, created, last used, expires and revoked times.
- Clients send `Authorization: Bearer dbt_…`. A policy scheme looks at the prefix and sends it to a PAT handler (or to JWT otherwise); the PAT handler maps it to the user, with the same claims, so every existing `[Authorize]` and access check applies unchanged.
- Users list and revoke their tokens. Usually a PAT can't manage tokens or passwords, there is a rate limit, and Swagger shows how to use one.
- Long-lived JWTs are the alternative, but they can't be revoked one by one, so PATs are the norm.

### Proposal
- Entity `ApiToken` (Id, UserId, Name, Prefix shown in the list such as `dbt_a1b2…`, Hash SHA-256, CreatedAt, LastUsedAt, ExpiresAt?, RevokedAt?, Scope) and its migration.
- Endpoints `GET/POST/DELETE Users/Own/Tokens` (JWT only: a PAT can't make or revoke tokens, change passwords, or link Telegram).
- Auth: policy scheme by prefix; `LastUsedAt` written at most once a minute; a rate limit per token.
- Profile: an "API tokens" card (list, create dialog that shows the secret once with Copy, revoke). Mockups first.
- Swagger: a Bearer security definition with a note about tokens.
- Open choices: see Questions (scopes, admin view, max lifetime).

## 3. LLM receipt parse and prefill (plan only for now)

### Flow
Upload a photo (or paste text) of a receipt on New bill → the server sends it to OpenRouter (OpenAI-compatible `/chat/completions`, image as a data URL, JSON schema in `response_format`) → the server validates the JSON → the New bill form is prefilled (description, date, currency, total, tips, lines) → the user picks people per line in the existing `LineEditor` and saves as usual. Never auto-finalize; nothing is saved until the user saves.

### Model
Free OpenRouter models that read images and support structured output, checked 2026-11-24 (the list changes): `nvidia/nemotron-3.5-super-vl:free`, `google/gemma-4-31b-it:free` (response_format only), `inclusionai/ling-3.2-flash:free`, and the router `openrouter/free`. Settings: `Llm:BaseUrl` (default `https://openrouter.ai/api/v1`), `Llm:Model`, `Llm:ApiKey` (secret), optional fallback model list (OpenRouter `models` array). No key → the feature is hidden (a `features` flag in an existing config endpoint).

### Points to settle
- **Privacy:** receipts go to a third party, and free models may log or train on prompts. Say so next to the upload button; send nothing except the image or text; don't store the image.
- **Limits and errors:** free models are rate-limited (about 20 per minute and a daily cap) and may fail. Timeout, one retry or the fallback model, then a clear message and manual entry. Our own rate limit per user.
- **Validation:** JSON schema on the server, then our rules (amounts ≥ 0, ISO currency, lines summing near the total; a warning, not a block, when they don't).
- **Numbers and currency:** the model returns decimals as strings and a currency code or symbol; the server normalises (comma decimals, symbol to code, fallback to the user's last currency).
- **People:** the model only reads items; people are chosen in the form (all bill people ticked by default, as now).
- **Bot:** a photo sent to the bot could make a draft bill the same way. Later task or out of scope (user decision).
- **Tests:** an `ILlmClient` interface with a fake in tests and CI, fixture receipts with recorded responses; one manual check with a real key.
- Mockups for the upload step and the prefilled form.

## User decisions (2026-11-24)
- **Creator removal:** the same lock as everyone else. Your chip shows • while you are on a line or a payment; once you are off them all it gets ×. No "remove from everything".
  - Amended by the user, during T1: **equal-split lines don't count for the lock.** Removing a person takes them off every equal-split line, and the split recalculates. Payments, by-parts lines and lines where they're the only participant still lock. The rule is the same for everyone.
  - More from the user during T1's mockups:
    - No warning text about equal lines, and no "Add yourself" section; you add yourself back from the People search.
    - **A draft opens in the same form as New bill**, prefilled, with lines (✎ edit, ✕ remove), payments (✕) and the preview. Unsaved changes are kept in the tab.
    - "Save" calls a new **PUT Bills/{id}**, which replaces the whole draft (draft only, creator or admin). "Save and finalize" saves, then finalizes. There's no per-line endpoint and no `?people=` value for drafts.
    - ✎ Edit line (people, parts, item, subtotal) works on New bill too, at `/bills/new/edit-line/<n>` and `/bills/<id>/edit-line/<n>`.
    - Mockups show the real page with each section once, not a sheet of states.
- **Token scopes:** chosen per token, "Read only" or "Read and write". A read-only token gets 403 on anything but GET (and HEAD/OPTIONS).
- **Admin and tokens:** Admin Users shows each user's active token count and a "Revoke all tokens" action. Admins never see individual tokens.
- **Receipt parse:** plan only in this series. The orchestrator writes `receipt-parse.md` (design, settings, schema, errors, tests, open questions) and agrees it with the user; building it, including mockups and any bot support, is a later series.
- Defaults the orchestrator picked: token expiry offered as 30, 90 or 365 days or never, 90 preselected; a token can't manage tokens, passwords or Telegram linking (JWT only); privacy note and manual fallback for receipts as above.

## Task series

| Task | What | Brief |
|---|---|---|
| T1 | Creator leaves and rejoins a bill's people (mockups, UI, tests) | `briefs/task-01.md` |
| T2 | API tokens: entity and migration, auth scheme, scopes, endpoints, rate limit, Profile card, Admin revoke-all, Swagger (mockups first) | `briefs/task-02.md` |
| T3 | End-to-end check (extend `web-ui/e2e`), polish, docs, PR description | `briefs/task-03.md` |

Alongside: the orchestrator writes `receipt-parse.md` with the user (no worker).
