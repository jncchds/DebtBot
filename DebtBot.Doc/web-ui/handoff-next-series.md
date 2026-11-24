You are the orchestrator for the **next** DebtBot task series, in `/mnt/git/DebtBot`. The user started you on 2026-11-24.

The web-ui series (T1 to T9) is finished. Its orchestrator, "DebtBot orchestrator" (id `334ad10a`), stays responsible only for PR #34 (marking it ready when the user says so). Everything below is yours. Run it with the **`orchestrate-tasks` skill** (`~/.claude/skills/orchestrate-tasks/`): one worker session per task, mockups first for UI, merge, clean up, report. This project's memory and this handoff add to the skill.

## Read first
- The skill, `~/.claude/skills/orchestrate-tasks/SKILL.md` and `templates.md`.
- Memory in `~/.claude/projects/-mnt-git-DebtBot/memory/`: `web-ui-orchestration` (how the user wants this run, every product decision, and Progress) and `web-ui-palette`.
- On branch `web-ui`, in `DebtBot.Doc/web-ui/`:
  - `README.md`, `plan.md` (decisions), `briefs/common.md` (worker rules: copy and adapt it for your series);
  - `api.md`, `web-sign-in.md`, `reports/task-09.md` (the final state), `e2e/README.md` (the reusable end-to-end check);
  - `mockups/shell/README.md`: the URL map, "List views" and the shared components.
- The top-level `README.md`.

## State now (2026-11-24)
- **`web-ui`:** has everything from T1 to T9 (head `d8b6806` or later). **PR #34** (`web-ui` → main) is still a **draft**; the user hasn't said to mark it ready. It hasn't been merged to main.
- **Prod:** the user deployed it: the migrations up to `LinkOldExchanges` are applied on prod. They were checking whether the app is serving after a slow-looking start. Don't touch prod.
- **Machine:** 4 cores, 5.4 GB of RAM, about 15 GB of free disk. Other projects share it. SDK 11 is in `~/.dotnet` (see common.md). Line endings are LF.
- **Usage limits:** the account hits its 5-hour limit during long tasks. If a worker goes quiet without a report, read the end of its transcript, and after the reset tell it to continue (see the skill).

## First steps
1. **Git:** decide with the user where the series lives. My recommendation is a new integration branch from `web-ui`, because these features need the web UI and #34 isn't merged yet: for example `web-ui-2`, with its own draft PR. Its base is `web-ui` until #34 merges, then `main`. Make your own worktree under `/mnt/git/DebtBot/.claude/worktrees/`.
2. **Docs:** create your series folder (for example `DebtBot.Doc/series-2/`) with `README.md`, `plan.md`, `briefs/common.md` (adapted from web-ui's, with **your** name and ids in "Waking the orchestrator"), `briefs/`, `reports/`, `notes/` and `mockups/`.
3. **Memory:** add a memory file for your series (for example `series-2-orchestration`, type feedback), with your name, id and session id, the decisions, and Progress. Add one line for it to `MEMORY.md`.
4. **Plan:** write the plan for the three items below with the user, and split it into tasks. Ask only about the decisions that are theirs, then run the tasks one at a time.

## The user's scope
1. **On the UI, the creator can remove themselves from a bill's participants, and add themselves back if needed.**
   - Today `PeoplePicker` ("People on this bill", T5) lists the bill's people. Check whether the creator is forced in: in the UI (`usePeople`, `PeoplePicker`, the New bill form) and the backend (bill creation and validation, `fillLineParticipantUserIds`, whether a bill needs the creator as a participant or a payer).
   - It applies to the New bill form and to drafts. Settle with the user what happens to the lines and payments that use the creator when they're removed.
   - UI change, so mockups first.
2. **REST API access with an API token in the user's name.** The user asked "how is it usually handled?", so answer that first, then plan. The usual approach is **personal access tokens (PATs)**:
   - The user creates a token on the Profile page, with a name and an optional expiry.
     - It's a random secret with a recognisable prefix (for example `dbt_…`), shown **once**.
     - Only its hash is stored, as the refresh tokens already are (T1).
     - The record keeps a name, a created time, a last-used time, an expiry, and a revoked time.
   - Clients send it as `Authorization: Bearer <token>`.
     - A separate authentication handler, or a policy scheme that picks JWT versus PAT by the prefix, maps it to the user.
     - All the existing `[Authorize]` access checks then apply unchanged.
   - Users list and revoke their tokens. Admins may be able to see or revoke anyone's.
   - Optional: scopes (read-only versus read/write).
   - Usually: a PAT can't manage tokens or passwords; there's a rate limit; and Swagger shows how to use it.
   - Long-lived JWTs are the alternative, but they can't be revoked one by one, so PATs are the norm.
   - Profile UI change, so mockups first.
3. **Plan an "LLM receipt parse and prefill" feature** through a free OpenRouter model (OpenAI-compatible API). The user said **plan**, so agree the plan with them before building anything:
   - **Flow:** upload a photo or paste text of a receipt; the model returns structured JSON (items, amounts, currency, date, tips); the result prefills the New bill form; the user reviews and saves. Never auto-finalize.
   - **Model:** a free model that reads images (vision). The list of free models changes, so check OpenRouter's current list when planning. Make the model and base URL settings. The API key is a secret setting, and the feature is hidden when no key is set.
   - **To settle:**
     - privacy (receipts go to a third party; say so in the UI);
     - limits and errors (free models are rate-limited and may fail: fall back to manual entry);
     - JSON schema validation;
     - currency and number parsing;
     - mapping items to people (the existing `LineEditor` and the people picker);
     - whether the bot gets it too (a photo sent to the bot);
     - how to test without calling the real API in CI (a fake client).
   - The UI part needs mockups.

## Cross-session
- If you need the web-ui orchestrator, it's "DebtBot orchestrator" (id `334ad10a`), but it's only finishing #34.
- Another plain "DebtBot" session (id `3e4eb540`) was started for the user's own use. It isn't a worker of yours.
