Task T4 (e2e, real run, docs) of the DebtBot receipts series, the last one. An orchestrator session runs the series one task at a time, with one session per task.

First, read `/mnt/git/DebtBot/DebtBot.Doc/receipts/briefs/common.md` and follow it throughout, **especially "Receipts: secrets and the LLM"**. To read it before your worktree exists, use `git -C /mnt/git/DebtBot show receipts:DebtBot.Doc/receipts/briefs/common.md`.

- Worktree: `/mnt/git/DebtBot/.claude/worktrees/task-04`
- Branch: `task/04-receipt-e2e`, based on the local `receipts` branch.
- Read:
  - `DebtBot.Doc/series-2/receipt-parse.md` and `DebtBot.Doc/receipts/plan.md` ("User decisions", including the T3 changes);
  - **all of `DebtBot.Doc/receipts/reports/`**: `task-01.md` to `task-03.md`, with their runtime scripts `reports/task-0N/scripts/t{1,2,3}check.mjs`;
  - `DebtBot.Doc/web-ui/e2e/README.md` (the e2e set: `run.sh`, `wait-bus.sh`, `check.mjs`, `api-check.mjs`, the upgrade check) and `DebtBot.Doc/series-2/reports/task-03.md` (how series 2 extended it);
  - `DebtBot.Doc/series-2/pr-description.md` (the PR description's style).

## Goal
The receipt feature is covered by the reusable e2e set with the fake provider, it upgrades cleanly from main, it's documented, and one real run with the user's OpenRouter key shows it working on the web and through the bot.

## Scope
1. **e2e with the fake provider:** fold the lasting parts of `t1check`, `t2check` and `t3check` into `DebtBot.Doc/web-ui/e2e/` (the API set and the browser set), so `run.sh` covers receipts:
   - API: `POST Bills/receipt` (a draft, an error code, the read-only token 403, the limit 429 with low limits set for the e2e stack, 413), `GET Bills/receipt/{id}` (owner and 404), `auth/config` flags, Profile `receiptOpenIn`;
   - the bot through `POST api/v1/dev/telegram/update`: a private photo reply with the web button, a group photo without a mention ignored, the reply text sent back as `/AddBill` making the bill;
   - browser: From a receipt on New bill (form result, a warning, Replace) and on Paste text (text result), an error, the bot's link (`?receipt=`, and 404), and the Profile "New bill opens" setting with the sidebar link.
   Keep the runs stable: run the whole set 3 times in a row and report each result.
2. **Upgrade check:** extend `upgrade.sh` / `upgrade-check.mjs` so an upgrade from `main`'s image with data applies the new migrations (`ReceiptOpenIn`, `TelegramChatStates`) and the old data still reads.
3. **Polish and docs:**
   - `docker-compose.yml`: a commented `DebtBot_DebtBot__WebUrl=` line with a one-line note, next to the LLM key line;
   - the top-level `README.md`: one coherent "Receipts (optional)" section (the key, `WebUrl`, the models and their free limits, the privacy note, the bot in groups only when mentioned, the limits);
   - `DebtBot.Doc/web-ui/api.md` and `mockups/shell/README.md` agree with the code;
   - anything small the earlier reports left open that fits.
4. **The real run with the key.** **Ask the user for the OpenRouter key** with a message when you get here (they will paste it to you; it was decided that no key is on the machine before this). Keep it only in the environment of the commands that need it (common.md): never in a file, `.env`, compose file, commit, log, report, screenshot or message. Then, on the local stack (Development, `Provider` OpenRouter):
   - the web: three or four real receipt photos from different shops and currencies (use real receipts you can find or make, for example photos of printed test receipts; say what they were), through the real UI in headless Chrome, with both the form and the text result;
   - the bot: the same photos through `POST api/v1/dev/telegram/update` (no real Telegram photo is sent; user decision);
   - record per receipt: the model that answered, the time, whether the fallback was used, the warnings, how the lines and total compared with the receipt, and how the real model named discounts (T2's word list);
   - try `particle-ai/particle-1:free` as the fallback once (T1 report) and say whether it should replace gemma;
   - afterwards, take the stack down with its volumes and **drop the key**: unset it, and confirm it's in no file in the repo (`git grep` for its prefix) and in no container left behind.
   - The user checks the OpenRouter account's data policy setting themselves; just say so in the report.
5. **PR description:** write `DebtBot.Doc/receipts/pr-description.md` for PR #38, in the style of series 2's: what the feature does (web and bot), the settings to set in prod (`DebtBot_DebtBot__Llm__ApiKey`, `DebtBot_DebtBot__WebUrl`), the migrations, the user decisions, the tests and checks with numbers, and the real run's results. The PR also carries the build versioning from PR #37 (merged from local main); mention it in one line.

## Tests
All .NET tests and vitest pass. Add tests for anything you fix.

## Runtime check
Point 1 (3 e2e runs), point 2 (upgrade) and point 4 (the real run). Bring everything down and clean up afterwards (common.md), and check `df -h /`: the disk is tight (about 6.5 GB free).

## Docs
Point 3, the PR description, and `DebtBot.Doc/receipts/reports/task-04.md`.

Screenshots: send 3 or 4 from the real run (the form and the text filled from a real receipt, light and dark) with SendUserFile (display: render). Make sure the key isn't visible in any of them.

When you're done, follow "Finishing" and "Waking the orchestrator" in common.md. Don't mark the PR ready.
