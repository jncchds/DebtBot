---
name: series-2-orchestration
description: "How DebtBot series 2 (creator self-removal, API tokens, LLM receipt parse) is run — orchestrator 645e1162, branch series-2, decisions and Progress"
metadata:
  node_type: memory
  type: feedback
  originSessionId: 645e1162-a1e5-44ec-8c96-df03ffb46bbe
  modified: 2026-11-24T15:53:03.134Z
---

Series 2 of DebtBot, started 2026-11-24 after the web-ui series (PR #34) was merged to main. Run like [[web-ui-orchestration]] with the `orchestrate-tasks` skill.

**Sessions**
- Orchestrator: "DebtBot orchestrator 2", id `645e1162`, session id `645e1162-a1e5-44ec-8c96-df03ffb46bbe`. Worktree `/mnt/git/DebtBot/.claude/worktrees/series-2`, branch `series-2` (from origin/main e7a93a3, with origin/web-ui merged for the handoff).
- The old orchestrator "DebtBot orchestrator" (334ad10a) only finishes #34. Session "DebtBot" (3e4eb540) is the user's own, not a worker.
- Docs: `DebtBot.Doc/series-2/`. Workers branch `task/NN-name` from local `series-2`.
- Don't touch prod.

**Scope**
1. UI: the creator can remove themselves from a bill's people and add themselves back (New bill and drafts). Backend needs no creator on lines/payments; creator stays a BillParticipant (owner).
2. Personal API tokens (PATs) in the user's name.
3. Plan (then build, once agreed) an LLM receipt parse and prefill through a free OpenRouter vision model.

**Decisions**
- (none yet)

**Progress**
- 2026-11-24: series set up (branch, docs, memory); plan draft in `plan.md`, questions sent to the user.

**Why:** the user wants a clean context per task, mockup approval per view, and a durable record.
**How to apply:** follow this and the skill for every series-2 task. Related: [[web-ui-palette]].
