# T2 report: API extension and admin API

- **Branch:** `task/02-api`, based on local `web-ui` at `b15e1fd`.
- **Commits:**
  - `0b8707b` API extension: bills (list, lines, payments, cancel, preview, exchange), profile, spendings, contacts, admin users and merge
  - `dab4f98` T2 tests; fix the BillModel ledger mapping
  - the last commit: `{message}` bodies on the two remaining plain 404s, line endings, `api.md` and this report
- **Numbering:** the series was renumbered while T2 ran. The UI tasks are now T4–T8 (the Admin/Merge screen is T8) and the end-to-end check is T9. This report uses the new numbers.

## What was built
**Data:**
- `Bill.ExchangeBillId` (nullable uuid, no FK). On both bills of an exchange it holds the id of the other bill.
- Migration `20261123171457_ExchangePair`, made with `dotnet ef migrations add ExchangePair --project ../DebtBot.DB`. The snapshot keeps its CRLF line endings.
- **Why it was needed:** `/ExchangeDebt` stored nothing that linked the two bills. The pair existed only in the Telegram button's callback data. A shared description text couldn't pair the bills reliably.
- The bot now sets the link too: `ExchangeDebtCommand` calls `SetExchangePair`.
- Exchanges made before this change have no link. `exchange/finalize` still accepts them, as long as neither bill is linked to a third bill.

**Bills (`BillsController`, `BillService`, the new `BillRules`/`ExchangeRules`, `BillActionResult`):**
- **Lists:**
  - `GET Bills` lists the bills you take part in, with the filters `withUserId`, `currencyCode` (any case), `status` and paging.
  - `GET Bills/All` is the admin list; it used to be `GET Bills`.
  - `BillListModel` gains `creator` and `exchangeBillId`. `BillModel` gains `exchangeBillId` and `ledgerRecords` (the bill detail page needs them).
- **`POST Bills` is reworked.** It used to AutoMap the body straight to the entity, which let the client set `Status` (for example `Processed`, with no ledger) and published no `EnsureBillParticipant`.
  - It now validates the body.
  - It maps to the parser models and goes through the same `Add(BillParserModel)` path as text bills.
  - The bill is always a draft.
  - The `Status` field is gone from `BillCreationModel`, and most of its fields are optional with defaults.
- **Lines and payments:**
  - `POST {id}/lines` and `POST {id}/payments` are for drafts only, for the creator or an admin.
  - They map to `BillLineParserModel`/`BillPaymentParserModel`: a participant without a part counts as 1 (equal split), and a given part is kept as is.
  - They publish `EnsureBillParticipant` for the users they add.
  - A total that equals the line sum follows the new lines; a total with tips stays as it is.
- **Finalize:** checks access (404 or 403). It also checks the draft can be finalized: lines, participants and payments above zero. Before, the background consumer would divide by zero.
- **`POST {id}/Cancel`:** creator or admin; 400 when already cancelled. Cancelling one bill of an exchange cancels the other too.
- **`GET lines/{lineId}`:** checks access to the line's bill.
- **`POST text/preview`:** returns the parser's `ValidationModel` and saves nothing.
- **`POST exchange`:**
  - checks that both amounts are above zero, the users are two different existing users, and the currency codes are 3 letters;
  - builds the bills with `ExchangeRules.BuildBills`, which is moved out of `TelegramService`, so the bot and the API build identical bills;
  - creates two drafts and links them.
- **`POST exchange/finalize`:** both bills must be visible (404) and changeable by you (403), drafts, and each other's pair. It then finalizes both, like `FinalizeExchangeCommand`.

**Debts, spendings, profile, contacts:**
- **`DebtsController`:** class-level `[Authorize]`. `GET Own` returns the whole `PagingResult`.
- **New `SpendingsController`:** `GET Own`, paged. `SpendingModel` gains `date` and `isCanceled`, and `BudgetService` now includes `User`, which used to come back null.
- **New `ProfileController`** (`api/v1/profile`):
  - `GET` returns the self view.
  - `PUT` replaces display name, phone and email, validated, and nothing else.
  - `UsersController.GET own` is removed.
- **`UserContactsController`:**
  - `GET Own` returns `ContactModel[]`, which includes subscription states for the T7 Contacts page, and `[]` when there are none.
  - `PUT Own/{id}` renames a contact; `DELETE Own/{id}` removes one.
  - `POST Own/by-telegram?username=` uses `FindOrAddUser`, which creates a placeholder like bills do, then `EnsureContact`. `EnsureContact` is now an `IUserContactService` method, and `EnsureContactConsumer` delegates to it.

**Admin (`UsersController`):**
- **`GET Users`:** paged, with `search` (display name, Telegram username or login; contains, ignoring case) and `role`.
  - Each row is an `AdminUserModel`: no hash, but `hasPassword`, `billCount`, `contactCount` and `firstBillDate`.
  - The old version returned the full list, or 404 when it was empty.
- **`GET Users/merge/suggestions`:**
  - `MergeSuggestions.Build` groups users by Telegram id and by normalized username (trimmed, leading `@` stripped, lower case), and returns one entry per pair with its reasons.
  - The keeper order is: Telegram id with the bot enabled, then login or password, then more bills, then older, then the id.
- **`POST Users/merge`:**
  - takes a JSON body `{oldUserId, newUserId}`; the query parameters still work;
  - returns 400 for the same id twice, for a missing id, or for merging away your own signed-in account;
  - returns 404 for an unknown id;
  - returns the kept user's row.
- **`UserRepository.MergeUsers` is rewritten.** The whole merge, including loading the two users, now runs inside the transaction. "What happens on merge" below has the details.
- `POST Users/{id}/password` (T1) is unchanged.

**Errors:** new and changed endpoints return `{ message, errors }` through `DebtBotControllerBase.ErrorResult`/`FromResult`. The 404 for a bill you can't see uses the same body.

## What happens on merge (for the T8 Merge screen)
**Before T2,** the merge would fail with a constraint violation, or leave orphan rows that blocked deleting the old user, in these cases:
- `BillPayments`, `BillLineParticipants`, `BillParticipants` and `Spendings` have the user in their primary key, so a shared bill made the `UPDATE` hit a PK violation and the merge failed;
- the same happened with `LedgerRecords` when both users had a record with the same counterpart in the same bill;
- old↔new ledger records became debts to oneself;
- duplicate contact and subscription rows were left pointing at the old user. Deleting the old user then either failed on a `Restrict` FK (spendings and ledger records use `Restrict`) or cascade-deleted the leftovers.

**Now:**
- **Moved to the kept user:** bills created, payments, line participations, bill participations, ledger records, spendings (budgets), contacts both ways and subscriptions both ways.
- **Combined when both users have the same key:**
  - payment amounts and line parts are added;
  - spendings add amount, payment amount and portion;
  - ledger records with the same counterpart in the same bill add their amounts;
  - for contacts, the kept user's link and name stay;
  - for subscriptions, the kept user's row stays, confirmed if either was.
- **Deleted:** ledger records, contact links and subscriptions between the two users, since they would point a user at itself. Their mutual debt disappears.
- **Profile fields:** as before, the kept user's values win and the old values only fill gaps.
- **Web sign-in:**
  - if the kept user has no login and no password, it takes the old user's login and password hash;
  - otherwise the kept user's stay and the old ones are dropped;
  - the old row's login is cleared and saved first, so `IX_Users_Login` holds at every statement.
- **Refresh tokens:** the old user's tokens are deleted. The old user's access tokens are still valid until they expire (≤ 60 min) but point at a user id that no longer exists.
- **The old user row:** it's deleted at the end, and nothing references it any more. The change tracker is cleared at the start, because the bulk updates bypass it.

## Endpoints
`api.md` has the full list, with shapes and access rules. The new and changed endpoints:

| Endpoint | Access |
|---|---|
| `GET Bills` | user, filters and paging |
| `GET Bills/All` | Admin |
| `POST Bills` | user, draft only |
| `POST Bills/{id}/lines` | creator or admin, draft only |
| `POST Bills/{id}/payments` | creator or admin, draft only |
| `GET Bills/lines/{lineId}` | can see the bill |
| `POST Bills/{id}/Finalize` | creator or admin |
| `POST Bills/{id}/Cancel` | creator or admin |
| `POST Bills/text/preview` | user |
| `POST Bills/exchange` | user |
| `POST Bills/exchange/finalize` | creator or admin of both bills |
| `GET Debts/Own` | user, returns the full `PagingResult` |
| `GET Spendings/Own` | user |
| `GET profile` / `PUT profile` | user (self) |
| `GET UserContacts/Own` | user, returns `[]` when empty |
| `PUT UserContacts/Own/{id}` / `DELETE UserContacts/Own/{id}` | user, own contacts |
| `POST UserContacts/Own/by-telegram` | user |
| `GET Users` | Admin, search, role and paging |
| `GET Users/merge/suggestions` | Admin |
| `POST Users/merge` | Admin |
| ~~`GET Users/own`~~ | removed |

The routes `Bills/{id}`, `Users/{id}` and the bill sub-routes now have `:guid` constraints.

## Tests
**120 passed, 0 failed** (the 52 from T1 plus 68 new). They ran inside `mcr.microsoft.com/dotnet/sdk:8.0` on a copy of the tree: `dotnet test DebtBot.Tests/DebtBot.Tests.csproj`.

**`DOTNET_ROLL_FORWARD=Major` on the host .NET 10 runtime doesn't work for the new tests.** AutoMapper 12 fails to build its maps on .NET 10 with "Incorrect number of arguments supplied for lambda invocation". The T1 tests don't build an AutoMapper configuration and still pass that way. T3, the .NET upgrade, will meet this: AutoMapper needs updating with it.

- **New package:** `Microsoft.EntityFrameworkCore.Sqlite` 8.0.0 for tests. SQLite runs the transactions, unique indexes and `ExecuteUpdate`/`ExecuteDelete` that EF InMemory can't.
- **`SqliteFixture`:** the real repositories and services over in-memory SQLite. It gives `ModifiedAt` a default value, because SQLite has no rowversion.

**The tests:**
- **`BillRulesTests`:**
  - equal split (no part → 1);
  - split by parts;
  - payment mapping;
  - creation defaults (upper-case currency, payment currency, total = sum of lines, date);
  - line, payment and currency validation;
  - finalize preconditions.
- **`ExchangeRulesTests`:**
  - amounts above zero (4 cases);
  - two different users, and both present;
  - currency codes;
  - the bills mirror `/ExchangeDebt` (currencies, totals, line participant, payer, description and rate);
  - pair validation (linked, legacy unlinked, linked elsewhere, the same bill, not a draft).
- **`BillServiceTests`** (SQLite):
  - a draft created with equal split; unknown users rejected;
  - lines by parts with the total following, and `EnsureBillParticipant` published; payments;
  - drafts only;
  - **finalize:** a non-participant gets NotFound, a participant gets Forbidden, an unknown id gets NotFound, and the creator and an admin can, publishing `BillFinalized`; an incomplete draft is rejected;
  - **cancel:** the same access rules, and already-cancelled is Invalid;
  - line access;
  - **exchange:** two linked drafts with the right payments and currencies; zero or negative amounts, the same user and an unknown user are rejected;
  - **exchange finalize:** finalizes both bills, and a second accept is Invalid; a stranger gets NotFound, a participant Forbidden, an admin Ok; a non-pair is Invalid;
  - cancelling one exchange bill cancels both.
- **`MergeSuggestionsTests`:**
  - username normalization (6 cases);
  - same Telegram id;
  - usernames differing only in case or `@`;
  - both reasons give one pair;
  - unrelated users aren't suggested;
  - each keeper rule: Telegram id plus bot, Telegram id without the bot doesn't count, login or password, more bills, older.
- **`UserRepositoryTests`** (SQLite):
  - **merge** moves the login and password and deletes the old user's refresh tokens, but not the kept user's;
  - the kept user's login and password win, and the old login can be used again afterwards;
  - same and unknown ids;
  - **a full merge:**
    - bill creator moved;
    - payments, parts and spendings combined;
    - ledger records combined, and the self record deleted;
    - contacts deduplicated, and the self link deleted;
    - subscriptions combined (confirmed) and the self one deleted;
    - the old row gone;
  - **admin list:** search ignoring case on name, username and login; role filter; counts; `hasPassword`; paging; no `*Hash` property on the models;
  - suggestions from the database;
  - **profile `PUT`:** a JSON body with `telegramId`, `telegramUserName`, `login` and `role` changes only name, phone and email (the Telegram id is unchanged); validation; clearing fields;
  - **contacts:** an empty list, `EnsureContact` is idempotent, subscription states both ways, rename, remove.

## Runtime check (docker compose, Development)
**How it was run:**
- My own compose file in the job's tmp folder (project `debtbot-t2`) builds `DebtBot/Dockerfile` from the worktree, a Release build;
- it sets a JWT key, the API on host port 18090, a named DB volume and no bot token.
- On the first start the app came up before Postgres was ready and logged "Error migrating database". After one restart of the app container, both `WebAuth` and `ExchangePair` were applied. That's pre-existing startup behaviour; compose has no healthcheck.

**First admin token:** as in T1. The default admin row comes from `DEFAULT_USER_TELEGRAM_ID`. A small script minted an HS256 JWT with the compose key and the same claims as `IdentityService`. With it the admin set a password through `PUT auth/password`. Every later token came from `POST auth/login`. Anna, Boris and Stranger were created with `POST Users`, given passwords with `POST Users/{id}/password`, and signed in.

**Bill from the form endpoints:**
- `POST Bills` (Pizza 30, equal split Anna/Boris, Anna paid 30) → 200 with the id.
- `POST lines` (Wine 20, parts 1:3) → 200, total 50. `POST payments` (Anna 50) → 200.
- Lines by Boris → **403**; payments by Stranger → **404**.
- `GET Bills/{id}`: Boris → 200, Stranger → **404**.
- `GET lines/{id}`: Boris → 200, Stranger → **404**.
- **Finalize:** Stranger → **404**, Boris → **403**, Anna → 200, then status `Processed`, with the ledger and spendings.
- Lines on the processed bill → 400.
- `Debts/Own` (Anna) → `PagingResult`, Bob (Anna's name for Boris) owes 30 EUR: 15 for pizza and 15 for wine.
- `Spendings/Own` (Boris) → 30 EUR, portion 0.6, with `date` and `isCanceled`.
- `GET Bills?withUserId=…&currencyCode=eur&status=Processed` → 1 bill.
- `Bills/All`: Boris → 403, admin → 200.

**Text preview:** a valid text → 200 with `isValid: true` and the parsed lines; bad text → 200 with errors; nothing was saved (`Bills/Own` count unchanged).

**Exchange:**
- The same user twice → 400; a zero amount → 400.
- Valid: Anna 100 EUR ↔ Boris 430 PLN → two ids.
- `GET Bills?status=Draft` shows both, `Draft`, with `exchangeBillId` pointing at each other and `creator` Anna.
- **Finalize:** Stranger → **404**, Boris → **403**, a bill paired with a non-pair → 400, Anna → 200.
- Both bills then `Processed`. Ledger: Boris owes Anna 100 EUR, and Anna owes Boris 430 PLN. `Debts/Own` became 130 EUR and −430 PLN.

**Cancel:**
- A Taxi bill of 12 was finalized, and debts went to 142 EUR.
- **Cancel:** Stranger → **404**, Boris → **403**, Anna → 200, then status `Cancelled` and debts back to 130 EUR. A second cancel → 400.

**Profile:**
- `GET` returns `login` and `hasPassword: true`.
- A `PUT` with `telegramId`, `telegramUserName` and `login` in the body changed only name, phone and email.
- A bad email → 400. `GET Users/own` → 404 (removed).

**Contacts:**
- Stranger `GET Own` → `[]`.
- `by-telegram @boris_t2` → the contact; `by-telegram newbie_t2` → a new placeholder.
- Rename → 200 "Bob"; deleting the placeholder → 204.

**Admin:**
- `GET Users` as Boris → 403.
- `?search=ANNA`, `?search=t2&countPerPage=2` (5 total, paged) and `?role=Admin` all work.
- No `passwordHash` in any response.

**Merge:**
- Seeded duplicates: "Anna dup" (username `ANNA_T2`, matching `@anna_t2`), and "Carl" plus "Carl web" (the same Telegram id 7001, "Carl web" with login and password).
- **`merge/suggestions`** → two pairs:
  - keep Anna, merge Anna dup, reason `SameTelegramUserName`;
  - keep Carl web, merge Carl, reason `SameTelegramId`, because the login rule wins.
  - As Boris → 403.
- Same id → 400; unknown id → 404.
- **A merge against the suggestion,** old = Carl web, new = Carl:
  - → 200, Carl now has `login: carl.web, hasPassword: true`;
  - Carl web's old refresh cookie → **401**, with 0 refresh-token rows left;
  - `POST auth/login carl.web` → 200 with `user.id` = **Carl's id**;
  - `GET Users/{carl web}` → 404.
- The query-parameter form merged Anna dup into Anna → 200. Afterwards the suggestions list was `[]`.

After the change to the 404 bodies, I rebuilt only the app and checked `GET Bills/{unknown}` and `GET Users/{unknown}` → 404 `{message, errors}`, and `swagger.json` → 200.

**Clean-up:**
- `docker compose down -v --rmi all`, which removed the stack, the volume and the built image;
- removed the pulled postgres, rabbitmq and sdk:8.0 images and the test NuGet volume;
- ran image and builder prune, and `dotnet build-server shutdown`.

**Only unit-tested, not run live:** the Telegram bot side (`/ExchangeDebt` setting the link, `EnsureContactConsumer` delegating, the `AddPayments` fix), since there's no bot token.

## Decisions
- **The exchange link** is a nullable `ExchangeBillId` on `Bill`, set on both bills. It has no FK, to keep it the smallest working change.
- **Who accepts an exchange:** the creator or an admin (the "change" rule). The counterparty can see the draft but can't accept it. The Telegram button worked the same way in practice, since it only appeared in the creator's chat. The UI shows Accept only when `creator.id` is the user or the user is an admin.
- **Cancelling one bill of an exchange** cancels the pair.
- **Plain 404 vs 403:** if you can't see a bill you get 404; if you can see it but didn't create it you get 403.
- **Merge keeper "older"** uses the earliest bill date, because `User` has no creation timestamp; `ModifiedAt` is a rowversion.
- **Merge login transfer works on the pair:** the old login and hash move only if the kept user has neither. Moving just the hash would pair the kept user's login with the other user's password.
- **Merging away the admin's own signed-in account** is refused with 400.
- **`BillCreationModel`:** `Status` is removed and the fields are optional with defaults. Creation always gives a draft; to finalize straight away, the UI calls Finalize after Create.
- **`GET UserContacts/Own`** returns the new `ContactModel` (with subscription states) instead of `UserModel`. Contact phone and email are no longer included. Nothing outside the repo used it.
- **`POST Bills` returns the id** as a JSON string, the same as `POST Bills/text`.
- **Pre-existing bug fixed:** `BillService.AddPayments(parser)`, the bot's `/AddPayments`, opened a transaction and then called a repository method that opens another. Nested `BeginTransaction` throws in EF, so the service-level one is removed.

## Open for later tasks
- **T3 (.NET upgrade):** AutoMapper 12 doesn't work on the .NET 10 runtime (see Tests), so update it with the framework. It also has NU1903 advisories.
- **T4–T8 (UI):** write `types.ts` from `api.md`.
  - Finalize and cancel are asynchronous: refetch after a short delay, or poll until the status is no longer `Ready`.
  - The user picker is contacts plus yourself.
- **T8 (Merge screen):** suggestions carry everything needed for display (counts, login, bot, first bill). A manual merge picker can use `GET Users?search=`.
- **Not touched, pre-existing:**
  - `DELETE Users/{id}` never works: the repository's check is inverted (`if (user != null) return false`), and it would hit `Restrict` FKs anyway;
  - `PUT Users/{id}` can still change the Telegram id (admin only);
  - the bot's `/FinalizeBill`, `/CancelBill` and the Finalize button have no creator check;
  - `FindUser` by username uses `ILike`, so `_` in a username acts as a wildcard;
  - the subscription endpoints accept any user id.
- **Compose:** the app doesn't wait for Postgres. A healthcheck or retry would help T9.
