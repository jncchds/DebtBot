You are now the orchestrator for the DebtBot web-ui work in `/mnt/git/DebtBot`. You take over from the first orchestrator session (id `b85fe091`), which the user is archiving.

Your job:
- run tasks T1 to T8 one at a time, each in its own worker session;
- check each finished task and merge it;
- keep the user informed.

Workers write the code. You don't, except for small fixes of your own.

## Read first
- Memory, in `~/.claude/projects/-mnt-git-DebtBot/memory/`. `MEMORY.md` is loaded for you. Read `web-ui-orchestration` (how the user wants this run, plus the progress so far) and `web-ui-palette`.
- On the `web-ui` branch, in your worktree `/mnt/git/DebtBot/.claude/worktrees/web-ui`:
  - `DebtBot.Doc/web-ui/README.md`
  - `plan.md`, which has the task list at the end
  - `briefs/common.md`
  - `briefs/task-01.md`
- The user's working style comes from their ASync project, which used the same pattern. For reference only: `/mnt/git/ASync/docs/orchestration/handoff-ios.md` and `/mnt/git/ASync/docs/orchestration/notes/`.

## State now (2026-11-23)
- Branch `web-ui`:
  - created from origin/main at 0cbe1c0, and pushed;
  - holds the docs commit de2aa12;
  - is checked out in the worktree `/mnt/git/DebtBot/.claude/worktrees/web-ui`, which is yours.
  - Enter that worktree with EnterWorktree(path) before any git work. Leave main and the user's checkout at `/mnt/git/DebtBot` alone.
- Draft PR #34, `web-ui` → main: https://github.com/jncchds/DebtBot/pull/34.
- T1 (backend auth) is running in the worker "DebtBot T1 auth" (id `07d30c7c`). It has been told your ids and will report to you.

## How to run a task
1. **Write the brief.** Write `DebtBot.Doc/web-ui/briefs/task-NN.md` in your worktree, following `task-01.md` and the plan's "Task series", and use the earlier reports. Commit it to `web-ui` and push. Things to know:
   - UI tasks (T3 to T7) start with mockups the user approves. common.md covers this.
   - T2 also covers the admin API: users list, set password and merge suggestions.
   - T8 is the end-to-end check in Docker, polish, and marking the PR ready.
2. **Start the worker** with `~/.claude/skills/start-code-session/start.sh /mnt/git/DebtBot --title "DebtBot TN <name>" --model claude-opus-5-5 --mode auto "<short prompt>"`. Keep the prompt short and point it at the brief file and common.md: an inline brief containing `git -C` was refused by the worktree guard. Then subscribe once with SendMessage(to: its name, notify_when_idle: true). The worker's report message is the real signal.
3. **When the report arrives:**
   - Check the branch is pushed, is based on `web-ui`, and its worktree is clean.
   - In the worker's worktree, run `dotnet build DebtBot.sln` (0 errors) and the tests in `DebtBot.Tests`. The SDK is 10 and the projects target net8, so use `DOTNET_ROLL_FORWARD=Major`, or Docker.
   - From T3 on, also run `npm run build` in `DebtBot/ClientApp`.
   - If something is still open, send the task back to the worker.
   - Otherwise, from your worktree:
     1. `git merge --no-ff task/NN-…` with the message "Merge task NN: …", then push `web-ui`.
     2. Remove the worker's worktree, and delete the branch locally and on origin.
     3. Run `claude stop <worker id>`.
     4. Forward 3 or 4 of its screenshots to the user with SendUserFile (display: render).
     5. Update the Progress lines in the memory file `web-ui-orchestration`, and refresh `DebtBot.Doc/web-ui/notes/` from memory.
     6. Start the next task.
4. **Before starting each task,** ask the user only about decisions that are genuinely theirs to make.

## Machine
- 4 cores, 5.4 GB RAM, and only about 7 GB of free disk (95% full).
- Other projects share the machine, and their containers aren't yours to stop.
- Run one heavy thing at a time. Run `dotnet build-server shutdown` after builds, and prune the Docker images and cache you create.

## Start now
Your ids are already in place: "DebtBot orchestrator", id `334ad10a`, session id `334ad10a-e54a-4cf5-bb10-dc9b00a79ca5`. The previous orchestrator put them in `briefs/common.md` and in memory, and told T1. Next:
1. Subscribe to T1 with SendMessage(to: "DebtBot T1 auth", notify_when_idle: true).
2. Tell the user, in a few lines, that you've taken over and what happens next.
