# Rules for every DebtBot web-ui worker session

This task is one of a series (T1 to T9) of web-ui tasks. An orchestrator session runs them one at a time, and each task gets its own worker session. Read this file before your brief, and follow it for the whole task.

## Read first
- `DebtBot.Doc/web-ui/plan.md`: the agreed plan, the user's decisions and the task list.
- `DebtBot.Doc/web-ui/notes/`: the palette tokens and how this work is run.
- `DebtBot.Doc/web-ui/reports/`: reports from the earlier tasks, covering what exists, the decisions made and anything left open.
- Approved mockups in `DebtBot.Doc/web-ui/mockups/` for UI tasks.
- Your global `~/.claude/CLAUDE.md`:
  - stay inside `/mnt/git/DebtBot`;
  - use the built-in tools before writing scripts;
  - run noisy commands through `node ~/.claude/tools/run.mjs`;
  - never use `pkill -f`.

## Git
- Work in a new worktree at `/mnt/git/DebtBot/.claude/worktrees/task-NN`, on a new branch `task/NN-<name>`.
  - Base the branch on the **local** branch `web-ui`, not on `main` or `origin/main`.
  - For example, from `/mnt/git/DebtBot`: `git worktree add -b task/NN-<name> .claude/worktrees/task-NN web-ui`, then EnterWorktree with that path.
- Commit as you go and push your branch to origin at the end.
- Don't touch `main` or `web-ui`, don't open PRs and don't merge. The orchestrator merges your branch into `web-ui`.
- Migrations are created only with `dotnet ef migrations add <Name> --project ../DebtBot.DB`, run from `DebtBot/`. Never write migrations by hand.

## Machine limits (important)
- The machine has 4 cores and 5.4 GB of RAM. Only about 7 GB of disk is free (95% used), and other projects' containers share the machine. Those containers aren't yours to stop.
- Run one heavy thing at a time, and check `free -m` and `df -h /` before Docker builds.
- After a Docker build:
  - remove dangling images (`docker image prune -f`) and the build cache you created (`docker builder prune -f`);
  - delete `bin`, `obj` and `node_modules` you no longer need when the disk runs low;
  - run `dotnet build-server shutdown` after builds.
- The installed SDK is .NET 10.0.115, with runtime 10 only. The projects target net8.0:
  - `dotnet build` works.
  - To run tests or the app locally, set `DOTNET_ROLL_FORWARD=Major`, or use the `mcr.microsoft.com/dotnet/sdk:8.0` container.
  - The real runtime check is `docker compose`. The image is built from `DebtBot/Dockerfile`, with Postgres (`db`) and RabbitMQ (`rabbit`).
- Node is 22.22, and `google-chrome` is installed for headless screenshots.

## Product decisions (from the user)
- **Users:** there are no password-only users. Every user is a Telegram identity, and some have never started the bot; those are placeholders created from @mentions. A password is only an extra way to sign in for an existing user.
- **Duplicates:** there should be one user per Telegram id. Don't add a DB unique constraint, because existing data may already have duplicates. Instead, admins get merge suggestions (same Telegram id, or the same Telegram username ignoring case) on the Merge users screen.
- **Sign-in:** the Telegram Login Widget only. No Mini App, and no bot sign-in links.
- **Exchange:** an exchange is created as two **draft** bills and accepted with a separate finalize step, the same as Telegram's `/ExchangeDebt` with its Finalize button.
- **Admin page:** required.
- **UI hosting:** the API serves the UI from `wwwroot`. It's built in a Node stage of `DebtBot/Dockerfile`, and `wwwroot` isn't committed.
- **Style:**
  - the home-family style: the nopds/abook sidebar shell, flat cards and pills, Inter, and nopds CSS tokens on Tailwind 4;
  - references: `/mnt/git/nopds/src/Nopds.Web/ClientApp/src/index.css` and `/mnt/git/abook/src/abook-ui/src/components/Sidebar.tsx`;
  - the palette is Lime on graphite, as in `notes/web-ui-palette.md`;
  - don't invent new colours or layouts.

## Mockups first (UI tasks)
**Mockups first, every UI view.** Before implementing any view:
1. Make a static HTML mockup with the real tokens, realistic DebtBot data, light and dark, desktop (1280 px) and phone (390 px) widths.
2. Render it to PNG with headless Chrome.
3. Send the PNGs with **SendUserFile (display: render)** and ask the user what to change.
4. Iterate until the user approves that view.
5. Save the approved PNGs and HTML under `DebtBot.Doc/web-ui/mockups/<view>/` and commit them. Only then implement the view.

Several views can be shown in one round. Wait for the user's answers, because the user follows the session through Remote Control. After implementing, screenshot the real view at the same widths in both themes and send those too.

## Quality bar
- Every endpoint has `[Authorize]` and checks access: `IBillService.HasAccess` for reads, and creator or admin for changes. Never trust user ids from the request body for "self".
- Keep changes focused. Don't reformat files you don't otherwise touch. The repo has CRLF and BOM files, so keep their line endings.
- Tests: `DebtBot.Tests` (xUnit, added in T1) must pass. Add tests for new logic.

## Finishing
When you're done:
1. Write `DebtBot.Doc/web-ui/reports/task-NN.md` and commit it. Cover:
   - the branch and commits;
   - what was built;
   - endpoints and pages;
   - test results with numbers;
   - what was verified at runtime and what was only compiled;
   - decisions made;
   - anything left open for later tasks.
2. Push the branch.
3. End with a short report, and wake the orchestrator using the paragraph below.
4. **Don't remove yourself.** Leave your worktree, your branch and your session as they are: don't remove the worktree, delete the branch, exit, stop or archive the session, or run `claude stop` on yourself. The orchestrator may send the task back to you, and it cleans all of this up after the merge.

## Waking the orchestrator
Always do this when you finish, and also when you stop because you're blocked. The orchestrator is the session named **"DebtBot orchestrator"** (id `334ad10a`, session id `334ad10a-e54a-4cf5-bb10-dc9b00a79ca5`), with cwd `/mnt/git/DebtBot`.
1. Send your report to it with SendMessage. Find its row with ListAgents by id `334ad10a` and use that exact name and ref. Or reply to the `from` address of the orchestrator's messages to you.
2. Check that it's alive: `claude agents --json | jq '.[] | select(.id=="334ad10a")'`.
   - It counts as not running if it's missing, if its state isn't working, idle or blocked, or if the SendMessage failed.
   - Only in that case, resume it with your report as its next prompt: `~/.claude/skills/start-code-session/start.sh /mnt/git/DebtBot --resume 334ad10a-e54a-4cf5-bb10-dc9b00a79ca5 "Report from <your session name>: <your report>"`.
   - Never resume it while it's running, because that starts a copy.
