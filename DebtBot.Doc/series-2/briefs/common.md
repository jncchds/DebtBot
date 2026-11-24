# Rules for every DebtBot series-2 worker session

This task is one of a series (T1 to Tn) of series-2 tasks. An orchestrator session runs them one at a time, and each task gets its own worker session. Read this file before your brief, and follow it for the whole task.

## Read first
- `DebtBot.Doc/series-2/plan.md`: the agreed plan, the user's decisions and the task list.
- `DebtBot.Doc/series-2/notes/`: the palette tokens and how this work is run.
- `DebtBot.Doc/series-2/reports/`: reports from the earlier tasks, covering what exists, the decisions made and anything left open.
- Approved mockups in `DebtBot.Doc/series-2/mockups/` for UI tasks.
- The finished web-ui series (merged to main in PR #34), in `DebtBot.Doc/web-ui/`: `api.md`, `web-sign-in.md`, `reports/task-09.md` (the final state), `e2e/README.md` (the reusable end-to-end check), and `mockups/shell/README.md` (URL map, list pattern, shared components). Its mockups are the style reference for new views.
- Your global `~/.claude/CLAUDE.md`:
  - stay inside `/mnt/git/DebtBot`;
  - use the built-in tools before writing scripts;
  - run noisy commands through `node ~/.claude/tools/run.mjs`;
  - never use `pkill -f`.

## Git
- Work in a new worktree at `/mnt/git/DebtBot/.claude/worktrees/task-NN`, on a new branch `task/NN-<name>`.
  - Base the branch on the **local** branch `series-2`, not on `main` or `origin/main`.
  - For example, from `/mnt/git/DebtBot`: `git worktree add -b task/NN-<name> .claude/worktrees/task-NN series-2`, then EnterWorktree with that path.
- Commit as you go and push your branch to origin at the end.
- Don't touch `main` or `series-2`, don't open PRs and don't merge. The orchestrator merges your branch into `series-2`.
- Migrations are created only with `dotnet ef migrations add <Name> --project ../DebtBot.DB`, run from `DebtBot/`. Never write migrations by hand.

## Machine limits (important)
- The machine has 4 cores and 5.4 GB of RAM. About 15 GB of disk is free (2026-11-24), and other projects' containers share the machine. Those containers aren't yours to stop.
- Run one heavy thing at a time, and check `free -m` and `df -h /` before Docker builds.
- After a Docker build:
  - remove dangling images (`docker image prune -f`) and the build cache you created (`docker builder prune -f`);
  - delete `bin`, `obj` and `node_modules` you no longer need when the disk runs low;
  - run `dotnet build-server shutdown` after builds.
- The projects target net11.0 (since T3). SDK 11 (11.0.100, runtimes 11.0.0) is installed user-local in `~/.dotnet`; the system SDK 10 in `/usr/lib/dotnet` can't build net11:
  - put it first in each command: `PATH=~/.dotnet:$PATH DOTNET_ROOT=~/.dotnet dotnet build DebtBot.sln` (and the same for `dotnet test DebtBot.Tests/DebtBot.Tests.csproj`). Don't edit shell profiles.
  - `dotnet ef` needs dotnet-ef 11. The global tool in `~/.dotnet/tools` stays on 10 for other projects, so install 11 into your job's tmp folder: `dotnet tool install dotnet-ef --version 11.0.0 --tool-path <tmp>/eftool`, then run `<tmp>/eftool/dotnet-ef` (with the same `PATH`/`DOTNET_ROOT`) from `DebtBot/`.
  - No `DOTNET_ROLL_FORWARD` and no sdk:8.0 container are needed any more.
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
5. Save the approved PNGs and HTML under `DebtBot.Doc/series-2/mockups/<view>/` and commit them. Only then implement the view.

**Every view has its own URL** (the user's rule, from T4). Back and Forward, reload, bookmarks and new tabs must all work. Filters, paging, tabs and modes go in the URL, never only in component state. The URL map and the sidebar sub-sections are in `mockups/shell/README.md`; follow them, and update the README if your task adds a URL.

Several views can be shown in one round. Wait for the user's answers, because the user follows the session through Remote Control. After implementing, screenshot the real view at the same widths in both themes and send those too.

## Quality bar
- Every endpoint has `[Authorize]` and checks access: `IBillService.HasAccess` for reads, and creator or admin for changes. Never trust user ids from the request body for "self".
- Keep changes focused. Don't reformat files you don't otherwise touch. Line endings are LF everywhere (`.gitattributes`, since T3b); `*.bat`/`*.cmd` are CRLF. Some files start with a BOM: keep it.
- Tests: `DebtBot.Tests` (xUnit, added in T1) must pass. Add tests for new logic.

## Finishing
When you're done:
1. Write `DebtBot.Doc/series-2/reports/task-NN.md` and commit it. Cover:
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
Always do this when you finish, and also when you stop because you're blocked. The orchestrator is the session named **"DebtBot orchestrator 2"** (id `645e1162`, session id `645e1162-a1e5-44ec-8c96-df03ffb46bbe`), with cwd `/mnt/git/DebtBot`.
1. Send your report to it with SendMessage. Find its row with ListAgents by id `645e1162` and use that exact name and ref. Or reply to the `from` address of the orchestrator's messages to you.
2. Check that it's alive: `claude agents --json | jq '.[] | select(.id=="645e1162")'`.
   - It counts as not running if it's missing, if its state isn't working, idle or blocked, or if the SendMessage failed.
   - Only in that case, resume it with your report as its next prompt: `~/.claude/skills/start-code-session/start.sh /mnt/git/DebtBot --resume 645e1162-a1e5-44ec-8c96-df03ffb46bbe "Report from <your session name>: <your report>"`.
   - Never resume it while it's running, because that starts a copy.
