---
name: series-2-orchestration
description: "How DebtBot series 2 (creator self-removal, API tokens, LLM receipt parse) is run — orchestrator 645e1162, branch series-2, decisions and Progress"
metadata:
  node_type: memory
  type: feedback
  originSessionId: 645e1162-a1e5-44ec-8c96-df03ffb46bbe
  modified: 2026-11-27T21:40:37.339Z
---

Series 2 of DebtBot, started 2026-11-24 after the web-ui series (PR #34) was merged to main. Run like [[web-ui-orchestration]] with the `orchestrate-tasks` skill.

**Sessions**
- Orchestrator: "DebtBot orchestrator 2", id `645e1162`, session id `645e1162-a1e5-44ec-8c96-df03ffb46bbe`. Worktree `/mnt/git/DebtBot/.claude/worktrees/series-2`, branch `series-2` (from origin/main e7a93a3, with origin/web-ui merged for the handoff).
- The old orchestrator "DebtBot orchestrator" (334ad10a) only finishes #34. Session "DebtBot" (3e4eb540) is the user's own, not a worker.
- Docs: `DebtBot.Doc/series-2/`. Draft PR: https://github.com/jncchds/DebtBot/pull/35. Workers branch `task/NN-name` from local `series-2`.
- Don't touch prod.

**Scope**
1. UI: the creator can remove themselves from a bill's people and add themselves back (New bill and drafts). Backend needs no creator on lines/payments; creator stays a BillParticipant (owner).
2. Personal API tokens (PATs) in the user's name.
3. Plan (then build, once agreed) an LLM receipt parse and prefill through a free OpenRouter vision model.

**Decisions**
- 2026-11-24 (user): creator chip locked like others while on a line/payment; tokens have a per-token scope (Read only / Read and write); admins get "Revoke all tokens" per user plus a count, never see tokens; receipt parse is **plan only** this series (orchestrator writes `receipt-parse.md` with the user, build later).
- 2026-11-24 (user, during T1): equal-split lines don't lock. Removing someone drops them from those lines; payments, by-parts lines and sole-participant lines still lock. The rule is the same for everyone.
- 2026-11-24 (user, T1 mockups; T1 scope widened):
  - A draft opens in the New bill form, prefilled; unsaved edits are kept in the tab.
  - Save calls a new PUT Bills/{id} that replaces the whole draft (draft only, creator or admin); "Save and finalize" saves, then finalizes.
  - ✎ edit line on New bill too, at /bills/new/edit-line/<n> and /bills/<id>/edit-line/<n>.
  - No "Add yourself" section, no equal-lines warning.
  - Mockups show the real page, each section once.
  - T2: read-only tokens must refuse PUT Bills/{id}.
- Defaults I picked: expiry 30/90/365/never (90 preselected); PATs can't manage tokens, passwords or Telegram linking.
- Tasks: T1 creator, T2 API tokens, T3 e2e + docs + PR description.

**Progress**
- 2026-11-24: series set up (branch, docs, memory); plan draft in `plan.md`, questions sent to the user.
- 2026-11-24: decisions recorded (7e3147c). T1 (creator leave) started as worker "DebtBot S2 T1 creator leave", id e6c8481a. Next for me alongside: write `receipt-parse.md` with the user.
- 2026-11-24: `receipt-parse.md` draft committed (65b67a4), with 4 open questions for the user.
- 2026-11-24: T1 merged into series-2 (aac0685): PUT Bills/{id} (draft replace, exchanges 400), BillForm shared by New bill and drafts, Edit line URLs, and the equal-split lock rule. 205 .NET tests, 122 vitest, e2e browser 74/74 (check.mjs updated). Worker e6c8481a stopped, worktree and branch removed. Open: finalizeBlockers is unused except in tests; exchange drafts can't be edited.
- 2026-11-24: T2 (API tokens) started as worker "DebtBot S2 T2 api tokens", id a771b97d.
- 2026-11-24: T2 merged into series-2: ApiToken entity and migration, JwtOrApiToken policy scheme, read-only scope middleware, [JwtOnly], 120/min per token, Profile › API tokens, Admin revoke-all.
  - User decisions: ▾ action strips (?open=) on the tokens list and Admin Users only; Swagger in every environment.
  - Merged users' tokens are revoked.
  - 231 .NET tests and 130 vitest pass; t2check 12/12; e2e flaky on load (bus not ready before seeding, Admin 390 "Loading…", finalize 15 s timeout), handed to T3.
  - Worker a771b97d stopped and cleaned up.
- 2026-11-24: T3 (e2e, polish, docs, PR description) started as worker "DebtBot S2 T3 e2e", id 4fbe496d. It doesn't mark the PR ready.
- 2026-11-24: T3 merged into series-2:
  - e2e: waits for the bus, polls finalize; likely flake cause was cdp.mjs Chrome profiles filling the 2.7 GB /tmp tmpfs, now removed on close. Runs 3–5 in a row passed API 75/75 and browser 91/91; upgrade from main 25/25.
  - finalizeBlockers removed. 231 .NET tests and 129 vitest pass.
  - The PR #35 body is set from `pr-description.md` via gh api PATCH. Worker 4fbe496d stopped and cleaned up.
  - 2026-11-24: at the user's request I marked PR #35 ready and merged it into main (ca57299, a merge commit like #34). The `series-2` branch is finished; a next series branches from main.
  - 2026-11-24, receipt plan agreed (user):
    - The privacy note is OK and shown every time. Limits are 10 an hour and 30 a day per user. The OpenRouter account has $10 of credits, so about 1000 free requests a day.
    - The image is never stored. The result opens in the visual form or the bill text format, chosen by a per-user setting with a per-receipt switch.
    - The bot gets it in the same series: it replies with the bill as text plus an "Open in the web form" button.
    - It's committed on branch `receipt-plan`; the user merged it as PR #36 (1a65510) on 2026-11-27 and confirmed my reading of "form or text". To be built in a later series, from `DebtBot.Doc/series-2/receipt-parse.md`.
  - **Series complete.**
  - 2026-11-27: the user asked for a new orchestrator to build the receipt feature. Its handoff is `DebtBot.Doc/series-2/handoff-receipts-series.md` on branch `receipts-handoff`, and it was started as "DebtBot orchestrator 3" (id 2d61b037). I (645e1162) no longer act on this work. 2026-11-27: at the user's request PR #36 (receipt-plan) was marked ready and merged into main (1a65510, merge commit). The receipt-parse.md questions are still open with the user.

**Why:** the user wants a clean context per task, mockup approval per view, and a durable record.
**How to apply:** follow this and the skill for every series-2 task. Related: [[web-ui-palette]].
