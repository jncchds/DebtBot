---
name: receipts-orchestration
description: "How the DebtBot receipts series (LLM receipt parse via OpenRouter, web and bot) is run: orchestrator 2d61b037, branch receipts, decisions and Progress"
metadata:
  node_type: memory
  type: feedback
  originSessionId: 2d61b037-4fe9-4bda-b282-d89cc71bca09
  modified: 2026-11-27T21:41:59.989Z
---

The receipts series of DebtBot, started 2026-11-27 after series 2 (PR #35) and the receipt plan (PR #36) were merged to main. Run like [[series-2-orchestration]] and [[web-ui-orchestration]] with the `orchestrate-tasks` skill.

**Sessions**
- Orchestrator: "DebtBot orchestrator 3", id `2d61b037`, session id `2d61b037-4fe9-4bda-b282-d89cc71bca09`. Worktree `/mnt/git/DebtBot/.claude/worktrees/receipts`, branch `receipts` (from origin/main 1a65510, with origin/receipts-handoff merged in).
- "DebtBot orchestrator 2" (645e1162) is finished and wrote the handoff (`DebtBot.Doc/series-2/handoff-receipts-series.md`).
- Docs: `DebtBot.Doc/receipts/`. Spec: `DebtBot.Doc/series-2/receipt-parse.md`. Workers branch `task/NN-name` from local `receipts`.
- Don't touch prod; the user deploys it (publish.yml on main).

**Tasks:** T1 backend, T2 bot, T3 UI (mockups first), T4 e2e, docs, PR description and the real manual run.

**Decisions**
- 2026-11-24 (user, design): privacy note as written, shown every time; 10/h and 30/day per user; $10 of OpenRouter credits (about 1000 free requests a day); image never stored; result opens in Form or Text by a Profile setting with a per-receipt switch; bot in the same series; recheck the free models when building.
- Defaults I picked: the Profile "Receipts open in" setting is stored on the user server-side (T1 adds the migration), so it follows the user across devices.
- The API key is a secret: never committed, logged or shown in reports.

**Progress**
- 2026-11-27: series set up (branch `receipts`, docs, memory, common.md with my ids); plan in `plan.md`; questions about the key and the manual run sent to the user.

**Why:** the user wants a clean context per task, mockup approval per view, and a durable record.
**How to apply:** follow this and the skill for every receipts task. Related: [[web-ui-palette]].
