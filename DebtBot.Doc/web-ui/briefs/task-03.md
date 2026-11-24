Task T3 (upgrade to .NET 11) of the DebtBot web-ui series. An orchestrator session runs the series one task at a time, with one session per task.

First, read `/mnt/git/DebtBot/DebtBot.Doc/web-ui/briefs/common.md` and follow it throughout. To read it before your worktree exists, use `git -C /mnt/git/DebtBot show web-ui:DebtBot.Doc/web-ui/briefs/common.md`.

- Worktree: `/mnt/git/DebtBot/.claude/worktrees/task-03`
- Branch: `task/03-net11`, based on the local `web-ui` branch (T1 and T2 are merged into it).
- Read:
  - `DebtBot.Doc/web-ui/reports/task-01.md` and `task-02.md`, especially "Tests" and "Open for later tasks" in task-02;
  - the T3 line of the task series in `plan.md`.

## Goal
Every project builds, tests and runs on .NET 11. Packages are on their latest versions that are still free. The API and the bot behave exactly as before. No new features.

## Scope
1. **SDK:**
   - Install the .NET 11 SDK user-local into `~/.dotnet` with `dotnet-install.sh` (`--channel 11.0`). Leave the system SDK 10 in `/usr/lib/dotnet` alone.
   - Use it by putting `~/.dotnet` first in `PATH` and setting `DOTNET_ROOT=~/.dotnet` in your commands. Don't edit shell profiles.
   - Add a `global.json` only if it helps, with `rollForward: latestFeature`. Explain your choice in the report.
2. **Target framework:**
   - Change every project (`DebtBot`, `DebtBot.DB`, `DebtBot.Models`, `DebtBot.Tests` and any others in `DebtBot.sln`) to `net11.0`.
   - In `DebtBot/Dockerfile`, use `mcr.microsoft.com/dotnet/aspnet:11.0` and `sdk:11.0`.
   - Check the GitHub workflows in `.github/workflows/` for a pinned SDK, and update it if there is one.
3. **Packages:** use the latest free versions. These are the user's decisions:
   - Microsoft packages (JwtBearer, EF Core, EF Design, EF InMemory, `System.Threading.RateLimiting` or drop it if it's in-box), and `Npgsql.EntityFrameworkCore.PostgreSQL`, go to 11.x. If Npgsql 11 isn't out yet, use the newest version that supports EF Core 11, and say so.
   - **AutoMapper:**
     - use the newest version **below 15**, since 15 and later are commercial;
     - it must work on .NET 11, because AutoMapper 12 breaks on .NET 10+ with "Incorrect number of arguments supplied for lambda invocation";
     - fix the NU1903 advisory if a free version fixes it; otherwise list it in the report;
     - `AutoMapper.Extensions.Microsoft.DependencyInjection` merges into AutoMapper 13+, and `AutoMapper.Collection` needs a matching version. Adapt the registration in `Program.cs`.
   - **MassTransit** stays on the latest **8.x**, since 9 is commercial.
   - **Telegram.Bot** goes to the latest version.
     - This is a big API change from 19: `SendTextMessageAsync` becomes `SendMessage`, the `Update` and `Message` types change, polling changes, and so on.
     - Adapt all bot code under `DebtBot/Telegram/` and keep the behaviour the same, including `TelegramBotInfoProvider`, the no-token mode from T1, `/Bearer`, and the exchange and finalize buttons.
   - **Others:** Swashbuckle (the latest version that works on net11; keep the HTTP bearer scheme and the string enums), Scrutor, Polly, EPPlus (stay on the same licence model and keep the existing `LicenseContext` setup; don't move to a version that needs a paid key), xunit, the test SDK, NSubstitute, and the SQLite and InMemory test packages.
   - Add no new packages unless a replacement is required, and explain any that you add.
4. **Breaking changes:**
   - Go through the ASP.NET Core and EF Core 9, 10 and 11 breaking-change lists that affect this code.
   - Make sure the existing migrations still apply on a fresh DB and on a DB at the T2 state.
   - Run `dotnet ef migrations add Net11Check --project ../DebtBot.DB` from `DebtBot/`, and confirm that it comes out empty.
     - If it isn't empty, find out why. Only keep a real migration if the model changed on purpose; otherwise remove the check migration with `dotnet ef migrations remove`.
   - Keep the warning count from growing. Fix new warnings that the upgrade causes, but don't take on the existing CS8618 backlog.
5. **Compose (open from T2):** add a Postgres healthcheck in `docker-compose.yml` and make the app wait for it with `depends_on: condition: service_healthy`, so a cold `up` starts cleanly. Do RabbitMQ the same way if it's simple.
5b. **Fix the bugs T2 found that were already there** (added at the user's request, 2026-11-23). Each fix gets a test:
   - **Bot creator check:** the bot's `/FinalizeBill`, `/CancelBill` and the Finalize button (including the exchange finalize) only work for the bill's creator or an admin, the same rule as the API. Everyone else gets a short refusal message.
   - **Subscription endpoints** in `UserContactsController`: "self" comes from the token, never from a user id in the request. Admin-only endpoints keep taking ids.
   - **`DELETE Users/{id}`:** fix the inverted check in the repository. When the user still has bills, ledger records or contacts that `Restrict` foreign keys protect, return 409 with `{ message }` that points to merging instead. Don't change the FKs.
   - **`PUT Users/{id}`** (admin) can't change the Telegram id any more; ignore that field or return 400. The product rule is that a Telegram id is never editable.
   - **User lookup by Telegram username:** escape `_` and `%` in the `ILike` pattern (or compare lowercased values exactly), so `_` no longer acts as a wildcard.
6. **Tests:**
   - All 120 tests pass on .NET 11 directly, without `DOTNET_ROLL_FORWARD` or the sdk:8.0 container.
   - Add tests only where the Telegram.Bot upgrade changes how the code works, such as the callback and button parsing for the exchange finalize.
7. **Runtime check:**
   - `docker compose up --build` on a cold start.
   - The migrations apply.
   - Smoke-test with curl:
     - `auth/config`;
     - login with a password (set one as T1 and T2 did);
     - refresh;
     - `GET Bills`, then create, finalize and check `Debts/Own`;
     - an exchange as drafts, then finalize;
     - admin `GET Users`.
   - Also confirm that the bot starts in no-token mode without errors.
   - Bring the stack down and clean up: remove the old 8.0 images you pulled, the build cache and dangling images, as in common.md.
8. **Docs:**
   - Update the SDK lines in `briefs/common.md`'s "Machine limits", in your branch, to say how later workers use SDK 11 from `~/.dotnet`.
   - Update `README`s or any docs that name .NET 8.
   - The report lists every package with its old and new version.

There are no screenshots for this task, since there's no UI.

When you're done, follow "Finishing" and "Waking the orchestrator" in common.md.
