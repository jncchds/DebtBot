# T3 report: upgrade to .NET 11 (and the pre-existing bug fixes, 5b)

- **Branch:** `task/03-net11`, based on local `web-ui` at `6fbffbe`.
- **Commits:**
  - `0479d4f` Upgrade to .NET 11 and latest free packages
  - `cf216c5` Telegram.Bot 22 tests; compose waits for healthy Postgres and RabbitMQ
  - `337030a` Fix pre-existing bugs found in T2 (5b)
  - `abf4678` DELETE Users: a user's own contact list doesn't block the delete
  - the last commit: this report

## What was done
**SDK and target framework:**
- SDK 11.0.100 (runtimes 11.0.0) is installed user-local in `~/.dotnet` with `dotnet-install.sh --channel 11.0`. The system SDK 10 in `/usr/lib/dotnet` is untouched, and no shell profile was edited.
- All five projects in `DebtBot.sln` (`DebtBot`, `DebtBot.DB`, `DebtBot.Models`, `DebtBot.Models.Enums`, `DebtBot.Tests`) target `net11.0`.
- `DebtBot/Dockerfile` uses `mcr.microsoft.com/dotnet/aspnet:11.0` and `sdk:11.0`.
- `.github/workflows/publish.yml` builds the Dockerfile with Buildx and pins no SDK, so nothing changed there. It now gets SDK 11 through the Dockerfile.
- **No `global.json`.** `net11.0` already needs SDK 11: the system SDK 10 stops with NETSDK1045, which says exactly that. A `global.json` would add no protection, and it would block a later SDK 12 until someone edits it.
- **dotnet-ef:** the global tool in `~/.dotnet/tools` stays on 10.0.12, because other projects on this machine use it with the system SDK 10. For a moment I updated it to 11 and then put it back. For this task, dotnet-ef 11.0.0 was installed into the job's tmp folder (`--tool-path`). `common.md` now tells later workers to do the same.

**Packages (old → new):**

| Package | Project | Old | New | Note |
|---|---|---|---|---|
| Microsoft.AspNetCore.Authentication.JwtBearer | DebtBot | 8.0.1 | 11.0.0 | |
| Microsoft.EntityFrameworkCore.Design | DebtBot, DebtBot.DB | 8.0.0 | 11.0.0 | |
| Npgsql.EntityFrameworkCore.PostgreSQL | DebtBot.DB | 8.0.0 | 11.0.0 | 11.0.0 is out |
| Microsoft.EntityFrameworkCore.InMemory | Tests | 8.0.0 | 11.0.0 | |
| Microsoft.EntityFrameworkCore.Sqlite | Tests | 8.0.0 | 11.0.0 | |
| System.Threading.RateLimiting | DebtBot | 8.0.0 | **removed** | in-box in the shared framework since .NET 7 |
| AutoMapper | DebtBot | 12.0.1 | 14.0.0 | the last version below 15 (15+ is commercial). Works on .NET 11: maps build, all tests pass. |
| AutoMapper.Extensions.Microsoft.DependencyInjection | DebtBot | 12.0.1 | **removed** | merged into AutoMapper 13+. `AddAutoMapper(assemblies)` in `Program.cs` compiles unchanged against AutoMapper's own extension. |
| AutoMapper.Collection | DebtBot | 9.0.0 | 11.0.0 | 11.0.0 needs AutoMapper `[14.0.0, 15.0.0)`; 12+ needs AutoMapper 15 |
| MassTransit.RabbitMQ | DebtBot | 8.1.3 | 8.5.12 | latest 8.x (9 is commercial) |
| Telegram.Bot | DebtBot | 19.0.0 | 22.10.5 | latest |
| Swashbuckle.AspNetCore | DebtBot | 6.4.0 | 11.0.1 | latest; brings Microsoft.OpenApi 3.x |
| Swashbuckle.AspNetCore.Filters | DebtBot | 8.0.0 | 11.0.0 | latest; still referenced but unused (its only use is commented out in `Program.cs`) |
| Scrutor | DebtBot | 4.2.2 | 7.0.0 | |
| Polly | DebtBot | 8.2.1 | 8.9.1 | |
| EPPlus | DebtBot | 7.0.9 | 7.7.4 | latest 7.x. EPPlus 8 makes `ExcelPackage.LicenseContext` obsolete (it throws) in favour of `ExcelPackage.License.Set…`, so 7.x keeps the existing `LicenseContext.NonCommercial` setup and the same licence model. |
| Microsoft.VisualStudio.Azure.Containers.Tools.Targets | DebtBot | 1.19.4 | 1.23.0 | |
| xunit | Tests | 2.9.3 | 2.9.3 | already the latest v2; v3 is a different package (`xunit.v3`) |
| xunit.runner.visualstudio | Tests | 2.8.2 | 4.0.1 | runs the xunit v2 tests (154 found and passed) |
| Microsoft.NET.Test.Sdk | Tests | 17.14.1 | 18.12.0 | |
| NSubstitute | Tests | 5.1.0 | 6.2.0 | |

No packages were added.

**AutoMapper NU1903 is not fixed.** AutoMapper 14.0.0 still has GHSA-6vhm-j9h7-f9gh, GHSA-gpwp-5797-mjff and GHSA-rvv3-g6hj-g44x (high). AutoMapper 12 had the same three. No free version fixes them: the fixed versions are 15.1.1+ and 16.x, which are commercial. The NuGet audit in SDK 10+ also checks transitive packages, so the warning now shows on `DebtBot.Tests` as well.

**Code changes for the new versions:**
- **Telegram.Bot 22:**
  - `IUpdateHandler.HandlePollingErrorAsync` → `HandleErrorAsync(…, HandleErrorSource, …)`;
  - `SendTextMessageAsync` → `SendMessage`: `replyToMessageId` becomes `ReplyParameters`, `IReplyMarkup` becomes `ReplyMarkup`, and parse mode and thread id are passed by name;
  - `EditMessageTextAsync` → `EditMessageText`;
  - `AnswerCallbackQueryAsync` → `AnswerCallbackQuery`;
  - `GetMeAsync` → `GetMe` (`ReceiverServiceBase` and `TelegramBotInfoProvider`);
  - `ReceiverOptions.ThrowPendingUpdates` → `DropPendingUpdates`.
  - The rest compiled unchanged: `Message`, `Update`, `CallbackQuery`, entities, the `ReceiveAsync` polling and `TelegramBotClientOptions`. No-token mode, `/Bearer`, `TelegramBotInfoProvider` and the exchange/finalize callback data are unchanged.
- **Swashbuckle 11 / Microsoft.OpenApi 3:** `Microsoft.OpenApi.Models` → `Microsoft.OpenApi`.
  - The security requirement is now `AddSecurityRequirement(document => …)` with `OpenApiSecuritySchemeReference("Bearer", document)`.
  - `RawTextRequestOperationFilter` uses `JsonSchemaType.String` and an `IOpenApiMediaType` dictionary.
  - The HTTP bearer scheme, the global requirement, `text/plain` on `POST Bills/text` and the string enums were all checked in the live `swagger.json` (now `openapi: 3.0.4`).
- **Scrutor 7:** `FromCallingAssembly()` no longer exists, so it's now `FromAssemblyOf<Program>()`. It's the same assembly.
- **Microsoft.IdentityModel 8.x:** the `IsNullOrEmpty()` collection extension that `BillService` and `BillRepository` picked up from `Microsoft.IdentityModel.Tokens` isn't public any more. It's replaced with `list is { Count: > 0 }`.
- **MassTransit 8.5** has nullable annotations. The RabbitMQ user and password from config get `!`, so the behaviour is unchanged and there are no new warnings.
- `DebtBot.csproj` gained `InternalsVisibleTo DebtBot.Tests`, so the tests can check `ProcessedMessage.ObjectType`/`ObjectId` (the reply-header parsing).

**Breaking changes reviewed (ASP.NET Core and EF Core 9, 10 and 11):**
- **EF 9: `Migrate()` throws on pending model changes.** The model matches the snapshot (see below), and migrations ran on startup with no warning.
- **EF 9: migration lock.** `Migrate()` now locks `__EFMigrationsHistory`; it shows in the startup log and is harmless.
- **EF 10: `ExecuteUpdate` takes setter lambdas in a new form.** The existing `SetProperty` lambdas compile unchanged, and the merge and cancel tests that use them pass on SQLite and live.
- **EF Core 10/Npgsql: client-generated `Guid` keys are now UUIDv7** (`01a209f8-…`). Old rows keep their v4 ids. Nothing sorts by id except a tiebreak in the admin users list.
- **C# 14 first-class spans: `array.Contains` in queries.** EF 11 translates it; the list filters and merge queries ran live.
- **ASP.NET Core 9/10/11:** nothing here uses the changed areas: cookie-auth redirects, `WebHostBuilder`, `IActionContextAccessor`, `KnownNetworks` or `WithOpenApi`. The rate limiter, JWT bearer, static files and fallback, and `JsonStringEnumConverter` behave as before.
- **Snapshot format:** EF 11 writes snapshots in a new format (file-scoped namespace, `LastMigrationId`, no BOM). Since no migration was added, the existing snapshot is kept byte for byte.

**Migration check:** `dotnet ef migrations add Net11Check --project ../DebtBot.DB` from `DebtBot/` produced an empty `Up`/`Down`. Its only snapshot changes were EF 11's new format and `ProductVersion`. I removed it with `dotnet ef migrations remove --offline` (without `--offline` it tries to reach the DB). The remove rewrote the snapshot in EF 11's format, so I restored the original file with `git checkout`. The model is identical either way.

**Compose (open from T2):** `docker-compose.yml` now has healthchecks:
- `db`: `pg_isready -h 127.0.0.1 -U postgres`. Over TCP, so it isn't fooled by the socket-only server that runs during first-run init.
- `rabbit`: `rabbitmq-diagnostics -q ping`.
- The app's `depends_on` uses `condition: service_healthy` for both. A cold `up` (fresh volume, no images) now migrates on the first try, with no "Error migrating database".

## 5b: pre-existing bugs from T2 (added by the user)
- **Bot creator check:** the rule is the API's, creator or admin.
  - `/FinalizeBill` and `/CancelBill` check it, as commands and as buttons. So does the exchange Finalize button, on both bills.
  - It goes through the new `IBillService.CanTelegramUserChangeAsync(billId, telegramUserId)`, which finds the user by Telegram id and calls `CanChange`.
  - Everyone else gets "Only the bill's creator or an admin can do this": as the callback answer for buttons, or as a chat message for commands. Nothing is finalized or cancelled.
  - An unknown bill id still goes down the old path and its old error message.
- **Subscription endpoints:** request, confirm and decline already took "self" from the token. T2's actual problem was that `contactId` could be anything. Now:
  - `request`: 400 for yourself, 404 for an unknown user. Before, an unknown id hit the FK and returned 500.
  - `confirm` and `decline`: 404 `{ message }` when there's no such request or subscription. The service methods return `bool`; the bot ignores the result as before.
  - The admin `subscription/add` still takes ids.
- **`DELETE Users/{id}`:**
  - The inverted check is fixed, and it now returns `DeleteUserResult`: 200 deleted, 404 unknown, 400 for your own account.
  - It's **409** `{ message: "… Merge them into another user instead." }` while the user has bills (created, paid, line participant or participant), ledger records or spendings, or is in **someone else's** contacts.
  - Bills are included because their creator FK is `Cascade`, so deleting a creator would wipe the bill for everyone in it. Ledger records and spendings have `Restrict` FKs. The FKs are unchanged.
  - The user's **own** contact list, subscriptions and refresh tokens cascade. In the live check, a placeholder made by `by-telegram` got a link back to its creator, and that first version of the rule made it undeletable (fixed in `abf4678`).
- **`PUT Users/{id}`** (admin):
  - 400 "The Telegram id can't be changed." when the body's `telegramId` differs from the user's, including setting one on a placeholder.
  - The same id or `null` is fine, so a client can send the model back.
  - 404 for an unknown id. It used to return 200.
  - The bot's own `ActualizeTelegramUser` still sets the Telegram id through the repository: that's how placeholders get linked when someone starts the bot.
- **Username lookup:** `FindUser` now compares lowercased values exactly instead of `ILike`, for the Telegram username, phone, email, the query string and the contact display name.
  - For inputs without `_`/`%`, ILike without wildcards was the same case-insensitive match.
  - So `@annaxk` no longer matches `@anna_k`: a bill or contact for `@annaxk` gets its own placeholder, as it should. `@ANNA_K` still finds `@anna_k`.
  - Email and display names had the same wildcard bug (`a_b@x` matched `axb@x`), so they're fixed in the same function.
  - The admin list's `search` (a deliberate contains search) is untouched.

## Tests
**154 passed, 0 failed** on .NET 11 directly: `PATH=~/.dotnet:$PATH DOTNET_ROOT=~/.dotnet dotnet test DebtBot.Tests/DebtBot.Tests.csproj`. That's the 120 from T1/T2, all passing unchanged, plus 34 new ones. No `DOTNET_ROLL_FORWARD` and no sdk:8.0 container.

**New, for the Telegram.Bot 22 code paths (`TelegramBotApiTests`, 9):** the bot client is a substitute that records the `SendRequest` requests.
- **A new message:**
  - with an inline keyboard → a `SendMessageRequest` with chat, text, Html parse mode, `MessageThreadId`, `ReplyParameters.MessageId` and the inline markup (callback data kept), the inline keyboard winning over the reply keyboard as before;
  - with only a reply keyboard → `ReplyKeyboardMarkup` with resize.
- **A message with an id** → `EditMessageTextRequest`, with its parse mode and inline markup.
- **The exchange Finalize callback data** fits Telegram's 64-byte limit.
- **The exchange Finalize button through `UpdateHandler`:** both bills finalized in order, and the callback answered (`AnswerCallbackQueryRequest`) with no text. A failed bill reports to the chat.
- **An unknown callback** is answered with "command not found".
- **Message routing:** case-insensitive, and the sender is actualized.
- **A raw Bot API update JSON,** deserialized with `JsonBotAPI.Options` and run through `TelegramService.ProcessMessage`: command, `mention` and `text_mention` entities, and the bot's reply header (bill type and id).

**New, for 5b (`PreexistingBugFixTests.cs`, 25):**
- **SQLite:**
  - **delete:**
    - a placeholder is deleted, with its subscriptions and tokens;
    - an unknown id is NotFound;
    - a bill creator is InUse;
    - ledger records alone are InUse;
    - being in someone's contacts is InUse, while your own contact list goes with you;
  - **username lookup:**
    - `_`/`%` aren't wildcards, and case is still ignored (also in the query string);
    - `FindOrAddUser` makes a new placeholder for a look-alike username;
    - contact display names match exactly, ignoring case;
  - confirm and decline return false without a request;
  - `CanTelegramUserChangeAsync`: creator yes, participant no, admin yes, unknown Telegram user no, unknown bill yes.
- **Controllers** (substitutes and a claims principal):
  - **PUT:** a different Telegram id → 400 and nothing updated; the same or no id → 200; setting an id on a placeholder → 400; unknown → 404;
  - **DELETE:** 200, 409 (mentions merging) and 404; yourself → 400;
  - **subscription request:** the token user is used; self → 400; unknown → 404;
  - **confirm/decline:** the token user, 404 without a request.
- **Bot commands:**
  - Finalize button and command: the creator finalizes; anyone else is refused, with no finalize and a refusal message for the command;
  - Cancel button and command: the same;
  - the exchange Finalize button: refused if either bill isn't yours, and nothing is finalized.

**Build warnings:**
- **Before:** 120 unique (104 CS8618, 4 CS1573, 3 NU1903, 2 CS8602, 2 CS0108, 1 each CS8604, CS8603, CS8633, CS1572, CS0168).
- **After:** 122 unique. They're the same except:
  - **+3 NU1903:** the same three AutoMapper advisories, now also reported on `DebtBot.Tests`, because SDK 10+ audits transitive packages;
  - **−1 CS8604:** `UserRepository.DeleteUser` no longer passes a null user to `Remove`.
- The two new CS8604 warnings from MassTransit's annotations and one CS8602 in a new test were fixed. The CS8618 backlog wasn't touched.

## Runtime check (docker compose, Development)
**Setup:**
- The real `docker-compose.yml` with its new healthchecks, plus my override in the job's tmp folder (project `debtbot-t3`):
  - build from a given source tree, tagged `debtbot-t3:<tag>`;
  - API on host port 18090, a named DB volume and a JWT key;
  - no bot token.
- The first admin token was minted the same way as in T1/T2. After that, every token came from `POST auth/login`.

**1. A DB at the T2 state, upgraded:**
- I built the **T2 image** from `git archive web-ui` (net8, sdk/aspnet 8.0) and did a cold `up` on a fresh volume. The app waited for healthy db and rabbit, then applied all 27 migrations through `ExchangePair` on the first try.
- **Seeded:**
  - admin password, users Anna and Boris with passwords;
  - a finalized "T2 pizza" bill (40 EUR, equal split);
  - a draft "T2 exchange" (Anna 50 EUR ↔ Boris 215 PLN).
- Then I removed the T2 app container (volume kept), built the **T3 image** and started it on the same DB.
- **Startup:**
  - "No migrations were applied. The database is already up to date.", with no pending-model warning;
  - "Admin user already exists";
  - in no-token mode, "BotToken is not set …" and "No Telegram bot token is configured … not polling Telegram", with no errors;
  - the bus started and the app listens on 8080.
- **Smoke test:**
  - **sign-in:**
    - `GET auth/config` → 200 `{ telegramBotUsername: null }`;
    - a wrong password → 401 `{ message }`;
    - **login with the password set under T2** → 200;
    - `POST auth/refresh` with the T3 cookie → 200 and a new token;
  - **`GET Bills`** → the T2 bills: the processed pizza, and both exchange drafts with `exchangeBillId` set;
  - **a new bill** (taxi 30 EUR, parts 1:2, currency given as `eur`):
    - `POST Bills` → 200 id;
    - finalize as Boris → **403**;
    - finalize as Anna → 200, then `Processed` with a 20 EUR ledger record;
  - **`Debts/Own`** → Boris owes Anna 40 EUR (20 + 20);
  - **the exchange drafted under T2,** finalized by T3 via `exchange/finalize` → 200;
  - **a new exchange** (Boris 10 USD ↔ Anna 9 EUR):
    - `POST Bills/exchange` → two drafts, both listed in `?status=Draft` and linked;
    - `exchange/finalize` → 200;
  - **`Debts/Own` after both exchanges:** 99 EUR, −215 PLN, −10 USD. That's correct: 40 + 50 + 9 EUR.
  - **admin `GET Users`** → 3 users; as Anna → 403;
  - **enums** come back as strings (`role: "Admin"`);
  - **`swagger.json`** → 200, with the Bearer http scheme, the global requirement and `text/plain` on `Bills/text`.

**2. A fresh DB on T3:** `down -v`, then `up` with the T3 image → all 27 migrations applied on the first try. The seed and check scripts above passed in full on this DB too, twice: once before the 5b fixes and once after.

**3. 5b, live on Postgres:**
- **Username lookup:**
  - a user `@ab_cd` exists;
  - `by-telegram abxcd` → a **new** placeholder `@abxcd`, where ILike used to return `@ab_cd`;
  - `by-telegram AB_CD` → the existing `@ab_cd`.
- **PUT:** Boris with `telegramId: 999` → 400; the same id with a new name → 200.
- **DELETE:**
  - Anna (has bills) → 409 with the merge message;
  - `@abxcd` while in Anna's contacts → 409;
  - after Anna removes the contact → 200, then `GET` → 404;
  - an unknown id → 404.
- **Subscriptions:**
  - Anna subscribing to herself → 400; to an unknown id → 404; to Boris → 200;
  - Boris confirms → 200, and Anna's contacts show `mySubscription: Confirmed`;
  - confirming without a request → 404.

**One flake seen once, pre-existing and not reproduced:** in the first T3 run, `GET Bills?status=Draft` right after creating an exchange listed only one of its two drafts. Three later tries, and both later runs, listed both. `ToPagingResult` counts first and then takes `count` rows in a separate query, with no transaction. If a background consumer changes a bill's status between the two queries, a page can come back short. T2's code was the same.

**Only unit-tested, not run live:** the bot against real Telegram (no token): sending and editing messages, callbacks, polling, and the creator-check refusals. The no-token start was checked live.

**Clean-up:**
- `docker compose down -v --rmi all` removed the stack, the volume and the built image;
- I also removed the `postgres:14.1-alpine` and `rabbitmq:3` images I pulled;
- `docker image prune -f` and `docker builder prune -f` reclaimed 4.06 GB, including the 8.0 and 11.0 SDK layers. BuildKit kept the base images in its cache, not as tagged images;
- `dotnet build-server shutdown`.
- The `postgres:16-alpine` image belongs to another project and was left alone.

## Docs
- `briefs/common.md` "Machine limits": the SDK lines now say how to use SDK 11 from `~/.dotnet` and dotnet-ef 11 from a tool path, and that roll-forward and the sdk:8.0 container are no longer needed.
- `plan.md` and `handoff-orchestrator.md`: their net8/SDK 10 lines now point to SDK 11. There's no README that names .NET 8.
- `api.md`: the subscription endpoints (400/404, self from the token), `PUT Users/{id}` (Telegram id not editable) and `DELETE Users/{id}` (409 rule).

## Decisions
- **No `global.json`** (see above).
- **EPPlus stays on 7.x:** 8.x replaces `LicenseContext` with a new licence API, so staying keeps the existing setup the brief asked for.
- **xunit stays on v2 (2.9.3):** v3 is a different package and test model, not a version bump.
- **Swashbuckle.AspNetCore.Filters is kept and bumped, although it's unused.** Removing it would be a cleanup outside this task.
- **Bot creator check** reuses `BillService.CanChange` through a Telegram-id lookup, instead of the API's `FinalizeAsync(billId, userId, isAdmin)`. The bot otherwise keeps its old behaviour; for example it doesn't add the API's finalize validation.
- **`DELETE Users` also blocks on created bills, payments and participations.** They cascade, so deleting would destroy other people's bills.
- **`FindUser` exact match covers email, phone and contact display names too,** since the same function had the same wildcard bug.
- **The snapshot stays in EF 8 format** until the next real migration rewrites it.

## Open for later tasks
- **AutoMapper NU1903** (3 high advisories): no free fix. Only AutoMapper 15+ (commercial) or replacing AutoMapper would clear it.
- **The next `dotnet ef migrations add`** will rewrite `DebtContextModelSnapshot.cs` in EF 11's format: no BOM, file-scoped namespace and `LastMigrationId`. Expect a large but harmless diff.
- **Paging race in `ToPagingResult`:** count and page are two queries (see above). The UI should tolerate a short page right after an action, or `ToPagingResult` could move into one transaction later.
- **New ids are UUIDv7.** Nothing depends on their order.
- **The T2 "Not touched" list is now done,** except the two items 5b didn't cover:
  - `POST Users` (admin) still returns 200 without the id;
  - `DELETE`/`PUT Users/{id}` have no `:guid` route constraint.
