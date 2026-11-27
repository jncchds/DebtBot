# DebtBot receipts series: plan

Status: **design agreed** (`../series-2/receipt-parse.md`, 2026-11-24; merged in PR #36). The open questions about running it are below. Orchestrator: "DebtBot orchestrator 3" (id `2d61b037`). Branch `receipts` from `main` (1a65510), one draft PR to main.

## Scope
Everything in `../series-2/receipt-parse.md`. That file is the spec. This plan only splits it into tasks and records what was decided while building.

## User decisions
From the design (2026-11-24):
- The privacy note is fine as written, and it's shown every time.
- The limits are 10 an hour and 30 a day per user.
- The OpenRouter account has $10 of credits, so about 1000 free requests a day.
- The image is never stored. The result opens in the visual form or the bill text format, chosen per user (Profile), with a switch per receipt.
- The bot gets it in the same series.
- The free model list changes, so T1 rechecks it (default `nvidia/nemotron-3.5-super-vl:free`, fallback `google/gemma-4-31b-it:free`).

For this series (2026-11-27):
- **The API key:** no key is on the machine before T4. The user pastes it to the T4 worker for the manual run only, and it isn't kept afterwards: not in a file, a commit, a log or a report. T1 to T3 use the fake client only. Prod is the user's to set (`docker-compose.yml` gets a commented line for it, like the bot token's).
- **The manual run** (T4) is done by the worker alone: the web flow on the local stack with real receipt photos and the real key, and the bot path through the real receipt service driven by a fake Telegram update. Nobody sends a real Telegram photo.
- Orchestrator's default: the Profile "Receipts open in" setting is stored on the user server-side (T1 adds it), so it follows the user across devices.

## Task series

| Task | What | Brief |
|---|---|---|
| T1 | Backend: `ILlmClient` and `OpenRouterClient`; `IReceiptService` with schema validation and normalisation (numbers, currency, tips); the text builder (round-tripped through the parser); 15-minute receipt drafts (`GET Bills/receipt/{id}`, owner only); `POST Bills/receipt` (not for read-only tokens, 10/h and 30/day per user, 5 MB); `Llm` settings and `receiptParse` in `GET Auth/config`; the per-user "Receipts open in: Form / Text" setting stored on the user (migration, read and set through `Users/Own`); `FakeLlmClient` and fixtures; recheck the free models and limits | `briefs/task-01.md` |
| T2 | Bot: a photo in a private chat, or in a group when the bot is mentioned in the caption, gets the bill as text plus an "Open in the web form" button; the privacy note once per chat; the same limits and errors; tests with fake updates | `briefs/task-02.md` |
| T3 | UI, mockups first: "From a receipt" on New bill and Paste text; the panel (picker, Form/Text switch, privacy note); error and warning states; the prefilled form and text; `/bills/new?receipt=<id>`; the Profile setting "Receipts open in: Form / Text"; vitest | `briefs/task-03.md` |
| T4 | End-to-end check with the fake client (extend `web-ui/e2e`), the upgrade check, docs (README, `api.md`, URL map), the PR description, and one real manual run on the web and in the bot | `briefs/task-04.md` |
