Task T9 (end-to-end check, polish, docs, PR description) of the DebtBot web-ui series. This is the last task. An orchestrator session runs the series one task at a time, with one session per task.

First, read `/mnt/git/DebtBot/DebtBot.Doc/web-ui/briefs/common.md` and follow it throughout. To read it before your worktree exists, use `git -C /mnt/git/DebtBot show web-ui:DebtBot.Doc/web-ui/briefs/common.md`.

- Worktree: `/mnt/git/DebtBot/.claude/worktrees/task-09`
- Branch: `task/09-e2e`, based on the local `web-ui` branch (all of T1 to T8 are merged into it).
- Read:
  - `plan.md`, especially "Verification" and the decisions;
  - `notes/web-ui-orchestration.md` (Progress lists every open point);
  - **every report** in `reports/`;
  - `api.md` and `web-sign-in.md`;
  - `mockups/shell/README.md`;
  - the check scripts the earlier tasks left in `reports/task-0*/scripts/`.

## Goal
The whole `web-ui` branch works end to end in Docker, as a user would run it. The small open points are fixed, the docs are complete, and the PR description is written. **You don't mark the PR ready or merge anything.** The orchestrator does that with the user.

## Scope
1. **One end-to-end run, from a cold start:**
   - `docker compose up --build`, with only the shipped `docker-compose.yml` and `docker-compose.override.yml` plus a JWT key, and a fresh DB. Then check:
     - the migrations apply;
     - the app starts without a bot token;
     - the SPA is served;
     - unknown `/api/*` routes give 404.
   - Upgrade path: also start the image on a DB at the state of **main** (`origin/main`, build its image the way T3 did), and check that the migrations apply and the existing data still shows correctly.
   - Merge the earlier tasks' seeds and browser checks into **one** reusable script set in `DebtBot.Doc/web-ui/e2e/`, with a README on how to run it. It covers every page, as admin, as a regular user and as a subscriber:
     - sign-in, refresh and sign-out;
     - Debts;
     - Bills, a bill and its draft actions;
     - New bill, form and paste;
     - Exchange;
     - Spendings;
     - Contacts;
     - Subscriptions;
     - Following;
     - Profile;
     - Admin: Users, Merge and All bills.
   - Run it at 1280 px and 390 px, in light and dark.
   - Fix what breaks. Explain any fix that isn't trivial in the report.
2. **Open points to fix**, each with a test where it makes sense:
   - **Paging race** (T3): `ToPagingResult` runs the count and the page as two separate queries, and once returned an inconsistent page. Make them consistent, for example with one snapshot or transaction, or a stable sort with a tie-breaker, after finding the real cause.
   - **The bill page's "← Bills"** (T7b): it should go back to the list you came from (keeping `?of=` and the filters), not always your own `/bills`.
   - **Old exchanges** (T6): pairs made by `/ExchangeDebt` before the `ExchangeBillId` link don't show in `GET Bills/exchanges`. Either link them with a safe, idempotent data migration (only where the pairing is certain), or show them in the list. Pick one and explain why.
   - **Deleting admins** (T8): the API should refuse to delete any user with the Admin role, and the UI should reflect that.
   - **Refresh-token purge** (T1): remove expired and revoked refresh tokens periodically, for example in a hosted service or at sign-in. Keep it cheap.
   - **Behind a proxy** (T1): add forwarded-headers handling (`X-Forwarded-For`/`Proto`) behind an opt-in setting, so the per-IP rate limit and the Secure cookie work behind a reverse proxy. Document it.
3. **Polish:**
   - Go through every page against its approved mockup and fix visible drift: spacing, wording, empty states, the phone overflow rule.
   - Check the console for errors and warnings, `npm run lint`, and the i18n check.
   - Check that the bundle stays split by route.
   - Don't redesign anything. Ask the user (with SendUserFile) only if a mockup and the built page disagree in a way you can't settle.
4. **Docs:**
   - Update the repo's top-level `README.md` (create one if there's none) with what DebtBot is, the web UI, and how to run it: compose, the settings (the JWT key, the bot token, BotFather `/setdomain` and HTTPS for the widget, the proxy setting), and local development (SDK 11, `npm run dev`).
   - Link `DebtBot.Doc/web-ui/` from it.
   - Bring `api.md` and `web-sign-in.md` up to date.
5. **The PR description** goes in `DebtBot.Doc/web-ui/pr-description.md`, for the orchestrator to put on PR #34. Cover:
   - **Summary:** what the series added;
   - **Tasks:** one line per task, T1 to T9, plus T3b and T7b, linking the reports;
   - **Breaking changes and upgrade notes:**
     - .NET 11;
     - string enums in the API;
     - `/Bearer` is admin-only for other users;
     - LF line endings;
     - the new settings;
     - migrations;
   - **How it was tested;**
   - **Known follow-ups:**
     - the "debts currency" (ChargeInPaymentCurrency), which the user decided is out of this series;
     - the AutoMapper advisories (no free fix);
     - the live Telegram widget check at deployment (bot, `/setdomain`, HTTPS);
     - anything you leave open.
   - End it with the line `🤖 Generated with [Claude Code](https://claude.com/claude-code)`.
6. **Checks:**
   - `dotnet build` (0 errors);
   - all the .NET tests (187 plus yours);
   - `npm run build`, `npm run lint` and `npm test`.
7. Send the user about 6 screenshots from the final run with SendUserFile, one per main area, and put all of them under `reports/task-09/`. Bring the stack down and clean up as in common.md.

When you're done, follow "Finishing" and "Waking the orchestrator" in common.md.
