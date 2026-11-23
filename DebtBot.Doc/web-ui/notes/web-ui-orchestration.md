---
name: web-ui-orchestration
description: "How the DebtBot web UI/API work is run — orchestrator session + one worker session per task, one branch/one PR, mockup approval per view, docs committed"
metadata:
  node_type: memory
  type: feedback
  originSessionId: b85fe091-00b4-4715-be20-d611e12ba132
  modified: 2026-11-23T17:10:31.055Z
---

On 2026-11-23 the user set up how the DebtBot API + auth + web UI work is run.

**Sessions**
- Run one orchestrator session. Each task gets its own worker session, started with a clean context through `~/.claude/skills/start-code-session/start.sh`. Tasks run one after another.
- The orchestrator is the session named "DebtBot orchestrator": id `334ad10a`, session id `334ad10a-e54a-4cf5-bb10-dc9b00a79ca5`. It took over on 2026-11-23; the first orchestrator, "DebtBot" (b85fe091), was archived.
- Every worker reports to the orchestrator with SendMessage when it finishes or is blocked.
- Workers never remove themselves: no removing their worktree or branch, no exiting, stopping or archiving their session. The orchestrator may send work back, and it cleans up after the merge (user, 2026-11-23; in common.md "Finishing").
- If the orchestrator isn't running (check `claude agents --json`), the worker resumes it with `start.sh /mnt/git/DebtBot --resume <session id> "Report from …"`. This is the same pattern as ASync's workers-wake-orchestrator.

**Git**
- One branch (`web-ui`) and one draft PR to main.
- Each worker works on `task/NN-name`, branched from `web-ui`. The orchestrator checks it, merges it into `web-ui`, pushes, and cleans up the worktree and branch.

**Views and mockups**
- Every UI view gets a mockup before it's built. The worker renders it to a PNG, sends it with SendUserFile (display: render), and iterates with the user until they approve. Only then does it implement the view.
- Approved mockups are committed as the spec.

**Docs**
- Briefs, plan, notes and mockups are committed in the repo under `DebtBot.Doc/web-ui/`.

**Product decisions**
- There are no password-only users. Every user is a Telegram identity; a password is just an extra way to sign in.
- There should be one user per Telegram id. Duplicates are shown to admins as suggestions on the Merge users screen.
- An exchange is created as a draft and accepted with a separate finalize step, the same as the Telegram button.
- An admin page is required.
- .NET 11 upgrade (added 2026-11-23) runs as T3, right after T2; UI tasks became T4–T8 and the end-to-end check T9. Packages go to their latest free versions (AutoMapper below 15, MassTransit 8.x; fix the AutoMapper advisory if a free fix exists), Telegram.Bot to latest with bot code adapted. The SDK 11 is installed user-local in ~/.dotnet (system SDK 10 stays).

**Progress**
- Orchestrator worktree: `/mnt/git/DebtBot/.claude/worktrees/web-ui` (branch `web-ui`). Draft PR: https://github.com/jncchds/DebtBot/pull/34. Docs: `DebtBot.Doc/web-ui/`.
- Workers get a short prompt that points to the brief file. A brief pasted inline that contains `git -C` gets refused by the worktree guard.
- 2026-11-23: T1 (auth) started as worker "DebtBot T1 auth", id 07d30c7c.
- 2026-11-23: orchestration handed over from b85fe091 to 334ad10a.
- 2026-11-23: T1 merged into web-ui (19ff92b). 52 tests pass. Note for T3: refresh reuse has no grace window, so the SPA must share one in-flight refresh. Orchestration was then handed to 334ad10a; the handoff is in `DebtBot.Doc/web-ui/handoff-orchestrator.md`, and T1 was told the new ids.
- 2026-11-23: T2 (API + admin API) started as worker "DebtBot T2 api", id 35c79555.
- 2026-11-23: T2 merged into web-ui (09b6243). 120 tests pass in sdk:8.0. On .NET 10 roll-forward AutoMapper 12 breaks, so T3 must update it. Open: DELETE Users/{id} never works; bot finalize/cancel commands have no creator check; compose doesn't wait for Postgres.

**Why:** the user wants a clean context per task, control over how each view looks, and a durable record of the work.
**How to apply:** follow this for every task in the series. Related: [[web-ui-palette]].
