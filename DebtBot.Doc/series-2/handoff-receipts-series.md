You are the orchestrator for the **receipts** series of DebtBot, in `/mnt/git/DebtBot`. The user asked for you on 2026-11-27.

Series 2 is finished. Its PR #35 (creator leaves a bill, API tokens) and PR #36 (the agreed receipt plan) are both merged into `main`. Its orchestrator, "DebtBot orchestrator 2" (id `645e1162`), has nothing left to run; everything below is yours. Run it with the **`orchestrate-tasks` skill** (`~/.claude/skills/orchestrate-tasks/`): one worker session per task, mockups first for UI, merge, clean up, report. This project's memory and this handoff add to the skill.

## Read first
- The skill, `~/.claude/skills/orchestrate-tasks/SKILL.md` and `templates.md`.
- Memory in `~/.claude/projects/-mnt-git-DebtBot/memory/`:
  - `series-2-orchestration` and `web-ui-orchestration`: how the user runs a series, every product decision so far, and their Progress lists;
  - `web-ui-palette`.
- On `main`:
  - **`DebtBot.Doc/series-2/receipt-parse.md`: the agreed plan and the user's decisions. This is your scope.**
  - `DebtBot.Doc/series-2/plan.md`, `briefs/common.md` (worker rules; copy and adapt it for your series), and `reports/task-01.md` to `task-03.md`.
  - `DebtBot.Doc/web-ui/`:
    - `api.md`;
    - `mockups/shell/README.md` (the URL map and shared components);
    - `e2e/README.md` (the e2e set, which since series 2 also covers that series: `run.sh`, `wait-bus.sh`, the upgrade check).
  - The top-level `README.md`.

## State now (2026-11-27)
- **`main`** has everything, up to `1a65510` (PR #36). This handoff is on branch `receipts-handoff`; merge it into your branch with `git merge origin/receipts-handoff`, and after that the handoff branch is finished.
- **Prod:** the user deploys it themselves (`publish.yml` runs on main). Series 2 added the `ApiTokens` migration. Don't touch prod.
- **Machine:**
  - 4 cores, 5.4 GB of RAM, about 15 GB of free disk. Other projects share it.
  - SDK 11 is in `~/.dotnet` (see common.md). Line endings are LF.
  - `/tmp` is a 2.7 GB tmpfs. Series 2 found headless Chrome profiles from `cdp.mjs` filling it, and `cdp.mjs` now removes them. Workers should check `df -h /tmp` if things fail oddly.
- **Usage limits:** the account hits its 5-hour limit during long tasks. If a worker goes quiet without a report, read the end of its transcript, and after the reset tell it to continue (see the skill).

## First steps
1. **Git:**
   - a new integration branch from `origin/main` (for example `receipts`), with `origin/receipts-handoff` merged in;
   - one draft PR to main;
   - your own worktree under `/mnt/git/DebtBot/.claude/worktrees/`.

   The user's checkout at `/mnt/git/DebtBot` is on `main`; leave it alone.
2. **Docs:** create your series folder (for example `DebtBot.Doc/receipts/`) with:
   - `README.md` and `plan.md`;
   - `briefs/common.md`, adapted from series-2's, with **your** name and ids in "Waking the orchestrator" (`$CLAUDE_CODE_SESSION_ID`, and your row in ListAgents and `claude agents --json`);
   - `briefs/`, `reports/`, `notes/` and `mockups/`.
3. **Memory:** add `receipts-orchestration` (type feedback) with your name, id and session id, the decisions and Progress. Add one line for it to `MEMORY.md`.
4. **Plan:** turn `receipt-parse.md` into `plan.md` with a task series. The plan is already agreed, so ask the user only what is still theirs, for example:
   - **the OpenRouter API key:** how it gets to the dev stack and to prod. It's a secret, so never commit it. Ideas: an environment variable or a `.env` that git ignores, and the prod setting is the user's to make;
   - who makes the one real manual run (web and bot) with the key.

   Then run the tasks one at a time.

## The scope, in short (details in `receipt-parse.md`)
Build split, as agreed:
1. **Backend:**
   - `ILlmClient` and the OpenRouter client;
   - the receipt service, JSON schema validation and normalisation (numbers, currency, tips);
   - the text builder (the existing bill text format, with the sender as a stand-in, round-tripped through the parser);
   - short-lived receipt drafts (15 minutes, owner only);
   - `POST Bills/receipt` (not for read-only API tokens; rate limit per user of 10 an hour and 30 a day);
   - settings (`Llm:*`) and the feature flag in `GET Auth/config`;
   - a fake client for tests and CI.
2. **Bot:**
   - a photo in a private chat, or in a group only when the bot is mentioned in the caption, gets the bill as text plus an "Open in the web form" button;
   - a privacy note once per chat;
   - the same limits and errors as the web.
3. **UI**, mockups first:
   - "From a receipt" on New bill and Paste text;
   - the panel with the Form/Text switch and the privacy note shown every time;
   - error and warning states;
   - the prefilled form and text;
   - the Profile setting "Receipts open in: Form / Text".
4. **End-to-end check** with the fake client, the upgrade check, docs, the PR description, and one real manual run on the web and in the bot.

**User decisions** (2026-11-24):
- The privacy note is fine as written, and it's shown every time.
- The limits are fine.
- The OpenRouter account has $10 of credits, so about 1000 free requests a day.
- The image is never stored. The result opens in the visual form or the bill text format, chosen per user, with a switch per receipt.
- The bot gets it in the same series.
- The free model list changes, so recheck it when building (the default in the plan is `nvidia/nemotron-3.5-super-vl:free`, with `google/gemma-4-31b-it:free` as the fallback).

## Cross-session
- "DebtBot orchestrator 2" (id `645e1162`) wrote this handoff. It's finished and won't act on reports.
