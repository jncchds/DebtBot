Task T3 (end-to-end check, polish, docs) of the DebtBot series-2 series. This is the last task. An orchestrator session runs the series one task at a time, with one session per task.

First, read `/mnt/git/DebtBot/DebtBot.Doc/series-2/briefs/common.md` and follow it throughout. To read it before your worktree exists, use `git -C /mnt/git/DebtBot show series-2:DebtBot.Doc/series-2/briefs/common.md`.

- Worktree: `/mnt/git/DebtBot/.claude/worktrees/task-03`
- Branch: `task/03-e2e`, based on the local `series-2` branch.
- Read:
  - `DebtBot.Doc/series-2/plan.md` (all of it, including the decisions added during T1 and T2);
  - `reports/task-01.md`, `reports/task-02.md`;
  - `DebtBot.Doc/web-ui/e2e/README.md` and `web-ui/reports/task-09.md` (how the e2e set, the upgrade check and the PR description were done last time);
  - `receipt-parse.md` (only to mention it in the PR description).

## Goal
Prove the series works end to end on a fresh stack and on an upgrade from `main`, fix the flaky e2e steps, tidy what T1 and T2 left open, and write the PR description.

## Scope
1. **e2e stability** (both workers hit these on a loaded machine):
   - `run.sh` waits for the bus ("Bus started" in the logs, or a health check) before seeding, so no bill gets stuck in Processing;
   - the Admin Users 390 px "Loading…" flake: find the cause, whether it's in the page (a query that never settles, or a missing retry) or in check.mjs (waiting on the wrong thing), and fix it at its root;
   - the finalize check: wait for the status with a longer, polling timeout rather than a fixed 15 s.
   It must pass three fresh runs in a row; report all three.
2. **e2e coverage** for the series. Fold T1's `t1check.mjs` and T2's `t2check.mjs` into the e2e set (API and browser parts), rather than leaving separate scripts:
   - creator removed and re-added, equal-split lines dropping a person, by-parts and payment locks, Edit line on New bill and on a draft, PUT Bills/{id} (409 when not a draft, 400 for an exchange, 403 for someone else), Save and finalize;
   - tokens: create read-only and read-write, the scope 403, JWT-only 403s, revoke gives 401, the 429 at the limit (keep it fast, with a low limit set for the e2e stack), admin revoke-all, and the ▾ strips with `?open=`.
3. **Upgrade from main:** run the previous `main` (e7a93a3) with seeded data, then this branch on the same volume. The `ApiTokens` migration applies, old drafts open in the new form and save with PUT, and nothing else changes. Use the T9 upgrade check as the base.
4. **Open points from T1 and T2:**
   - remove `finalizeBlockers` from `lib/bills.ts` if it's now only used by its tests, or use it;
   - check that the README, api.md and the shell README agree on every new URL and endpoint.
   Anything bigger goes to "Open" in the report, not into this task.
5. **Polish:** a pass over the new views (New bill and draft form, Edit line, Profile › API tokens, Admin Users strip) in both themes at 1280 and 390 px. Fix small layout or text issues; ask the user only if something looks like a design change.
6. **PR description** in `DebtBot.Doc/series-2/pr-description.md`, in the style of `web-ui/pr-description.md`:
   - what changed for users;
   - API changes (PUT Bills/{id}, profile/tokens, revoke-all, Swagger in every environment);
   - the migration;
   - tests and e2e numbers;
   - a "Planned, not built" section pointing to `receipt-parse.md`;
   - follow-ups (exchange drafts can't be edited, other lists still have inline buttons).
   The orchestrator puts it on PR #35.

## Tests
All tests pass (231 .NET and 130 vitest after T2). Add tests for anything you fix.

## Runtime check
The e2e set: three fresh runs plus the upgrade run, as in the scope. Bring everything down and clean up (common.md).

## Docs
- `reports/task-03.md`;
- `DebtBot.Doc/web-ui/e2e/README.md` updated, or the e2e set moved to a place that fits both series; say which;
- `pr-description.md`.

Send 3 or 4 screenshots of anything you changed in the polish pass (SendUserFile, display: render), or say there was nothing to show.

Don't mark the PR ready; that's for the orchestrator with the user.

When you're done, follow "Finishing" and "Waking the orchestrator" in common.md.
