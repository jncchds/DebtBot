# T5 report: Debts, Bills, Bill detail

- **Branch:** `task/05-ui-debts-bills`, based on local `web-ui` at `453ac15`.
- **Commits:**
  - `f15b731`: the approved mockups;
  - `f65978c`: backend sort and filters;
  - `5bf86b2`: the pages and the shared list pattern;
  - the last commit: fixes from the runtime check, the docs, this report and the screenshots.

## Mockups (approved by the user)
Three rounds; the spec is in `mockups/debts/`, `mockups/bills/`, `mockups/bill/` and `mockups/common/` (the shared CSS, `shell.js` and `list.js`).

**Round 1:** first drafts of all three views. After it:
- the user asked me to use the old UI branches on the remote as a mild reference;
- from `origin/react_ui` I took per-currency "Bills" links, the grouping by person and a Bills subtitle that names the filters;
- `origin/react-ui-vibe` typed lines and payments as raw JSON, which I didn't copy.

**Round 2.** The user's decisions:
- **No following.** The web never shows subscribed users' balances. The subscription endpoints are effectively admin-only, the user said.
- **Paged lists get server-side "sort by a field"** and filters for the fields where they make sense.
- **One list pattern for Bills, Debts, Spendings and Exchanges:** a header with filters and Sort by, then paged results, a table when the width allows and cards on phones.

**Round 3:** "remove all bill button from the pages, it's available on the left panel". So the pages have no New bill or Bills buttons. The per-row 🧾 Bills on Debts stays, because it's a filtered link. The user said not to send new mockups and to start building, so the round-3 PNGs were re-rendered and committed without another review.

The Bill detail mockup wasn't changed after round 1.

## Backend (sort and filters)
**`GET Bills`** binds `[FromQuery] BillListQuery`.
- **New parameters:**
  - `createdByMe`;
  - `dateFrom` and `dateTo` (whole days, UTC);
  - `search` (description contains, ignoring case);
  - `sortBy` (`Date`, `Description`, `Total`, `Status`) and `sortOrder` (`Asc`, `Desc`).
- **`withUserId`** now also matches a bill that has no ledger yet (a draft, or one still finalizing) when you're both in it. Before, drafts never showed up under "Bills with them".
- **The bot keeps the old meaning** (ledger only) through the old repository overload (`WithIncludesUnfinalized = false`).

**`GET Debts/Own`** binds `[FromQuery] DebtListQuery`, through a new `DebtService.GetOwn`.
- It returns only **your own** balances, without subscribed users' and without settled (zero) ones.
- **Parameters:**
  - `withUserId` and `currencyCode`;
  - `direction` (`All`, `OwedToMe`, `IOwe`);
  - `sortBy` (`Person` = your contact name, `Amount` signed, `Currency`) and `sortOrder`.
- The bot's `GetForUser` (with subscriptions) is unchanged.

**Shared:**
- the enums `SortOrder`, `BillSortField`, `DebtSortField` and `DebtDirection` are in `DebtBot.Models.Enums/ListSorting.cs`;
- every sort ends with a unique key, so paging is stable;
- decimals are compared and ordered through a `(double)` cast, because SQLite (in the tests) compares them as text; Postgres gives the same result;
- an invalid enum gives the framework's 400 (checked live).

The convention for T6 and T7 is written down in `mockups/shell/README.md`, "List views". `api.md` is updated.

No migration was needed.

## Frontend (`DebtBot/ClientApp/src`)
**The shared list pattern (for T6 and T7 too):**
- `components/list/ListView.tsx`:
  - a header with the filters, Sort by, a direction toggle and Clear filters;
  - a table when the list itself is at least `tableMinWidth` wide (ResizeObserver), cards below that;
  - on narrow widths the filters fold under "🔍 Filters (n)";
  - `Pager`, with "Page x of y" when narrow;
  - an empty state, a filtered-empty state (with Clear filters) and a past-the-end page.
- `components/list/filters.tsx`: `FilterSelect`, `FilterChoice`, `FilterFlag`, `FilterText` (debounced), `FilterDate` and `FilterCurrency`.
- **URL state:**
  - `lib/listState.ts` holds the spec, parse/serialize, the changes (`withFilter`, `withSort`, `withPage`, …) and `pageWindow`;
  - `lib/useListState.ts` is the hook;
  - `lib/lists.ts` has each list's spec and its URL-to-API query mapping (`debtsSpec`/`debtsQuery`, `billsSpec`/`billsQuery`; T6/T7 add theirs here).
- Defaults are left out of the URL. A filter or sort change goes back to page 1 and is a new history entry.
- The component API is in `mockups/shell/README.md`, "List views".

**Pages** (route-level `lazy()`):
- **💸 Debts (`/`):**
  - two total cards (owed to you / you owe, per currency, from all your balances);
  - the list: person, currency, direction, amount, with 🧾 Bills (`/bills?with=&currency=`) and 🔁 Exchange / settle (`/exchange?with=`);
  - filters: person, currency, direction. Sort: person, amount, currency;
  - an empty "You're all settled" state.
- **🧾 Bills (`/bills`):**
  - filters: search, person (contacts), currency, from, to, status pills, created by me;
  - sort: date, description, total, status;
  - a "Your balance" column, from the ledger records;
  - an exchange pair is folded into one row (`pairExchanges`) with ✓ Accept, for its creator or an admin;
  - a subtitle that names the filters.
- **Bill (`/bills/<id>`, `/bills/<id>/add-line`, `/bills/<id>/add-payment`):**
  - the header, the facts (total, paid in, tips, people), lines with chips (equal or ×parts), payments, and who owes whom (crossed out when cancelled);
  - **for a draft, the creator or an admin gets:**
    - Finalize, disabled with the API's reason (`finalizeBlockers`);
    - Cancel, with an inline confirmation;
    - the editors at their own URLs;
  - **an exchange half:** a banner with who gives whom what, a link to the other half, and ✓ Accept exchange. It has no line or payment editing;
  - **not found and no access** are one view (both 404);
  - **a 403** shows "Only the bill's creator or an admin can change it."
- **Background actions:** Finalize, Cancel and Accept show a "Finalizing…" or "Cancelling…" banner and poll every second until the status changes. A bill opened while `Ready` polls too. After 30 s it says "This is taking longer than usual." with a Refresh button. Bills and debts queries are invalidated after every change.

**Reusable editors for T6's New bill form:**
- `components/bill/LineEditor.tsx` (`people`, `currency`, `onSubmit(BillLineCreation)`, `onCancel`, `busy`, `error`, `hint`, `title`, `submitLabel`):
  - item, subtotal, split equally or by parts;
  - people from contacts plus yourself (`lib/usePeople.ts`).
- `components/bill/PaymentEditor.tsx`: the same props, with `onSubmit(BillPaymentCreation)`.
- Both only build the request object; the caller sends it.

**Sidebar sub-sections** (`components/subnav/money.tsx`):
- **Debts:** Balances, one entry per person, coloured, opening `/bills?with=<id>`.
- **Bills and a bill:**
  - Drafts (an exchange once);
  - Recent bills, the last 5 non-drafts, with exchanges folded;
  - … All bills.

**Money:**
- `lib/money.ts` works in integer units of 1/10000 (decimal(10,4)), with `parseAmount` (up to 4 decimals) and `formatNumber` (Intl, the currency's own digits);
- `lib/useFormat.ts`: `money()` goes through i18n (`money.amount`). Dates follow the mockups ("18 Nov 2026, 20:40", en-GB for English);
- tips, balances and totals are never summed in floats;
- amounts are sent exactly as typed.

**Other:**
- `api/types.ts` gains the bill, line, payment, ledger, debt, contact, exchange and list-query types;
- `api/bills.ts` holds the queries and mutations;
- new i18n groups: `list`, `money`, `debts`, `bills`, `bill`, `editor`, plus `common` and `subnav` additions. 240 keys; the check passes.

## Checks
- `npm run build`: OK. The main chunk shrank from 365 kB to 246 kB (gzip 77 kB). Debts is 5 kB, Bills 7 kB and Bill 16 kB, plus shared chunks.
- `npm run lint`: 0 warnings, 0 errors.
- `npm test`: **34 passed** in 4 files:
  - client (8);
  - `listState` (parse, clean, round-trip, changes, `pageWindow`);
  - money (units, parsing, formatting);
  - bill helpers (balance, tips, people, finalize blockers, exchange pairing, handles).
- `dotnet build DebtBot.sln`: 0 errors.
- `dotnet test`: **159 passed**, 0 failed (154 before, plus 5 in `ListQueryTests`: bill sorts, bill filters, the "with" meaning for the web and the bot, stable paging, and own debts filters, sorts and paging over a SQLite copy of the debts view). SDK 11.

## Runtime check (docker compose, cold build)
**Setup:**
- the real `docker-compose.yml` plus an override in the job's tmp folder:
  - project `debtbot-t5`, image `debtbot-t5:local`, API on 18095;
  - Development, a JWT key, the default admin from `DEFAULT_USER_TELEGRAM_ID`, a named DB volume;
- run from a clean volume.

**Seed, all through the API:**
- a minted admin JWT (as in T1–T4) set Anna's login and name;
- `POST Users` created Boris, Clara and Dmitry with Telegram usernames, and they got passwords;
- Anna added them with `by-telegram` and renamed them; "Mum" is a placeholder;
- bills: 24 finalized in EUR, PLN, USD and PLN-paid-in-EUR, with equal and by-parts lines; 2 drafts (one with tips); 1 draft exchange (100 EUR ⇄ 430 PLN).

**Headless Chrome over CDP: 17/17 checks passed.**
- **Debts:** the right balances: Boris 20.00 EUR, Dmitry 111.65 EUR and 31.70 PLN, Clara 66.38 USD, Mum 40.00 EUR. It's a table at 1280 and has the Balances sub-section.
- **Debts URL state:** a filter (`dir=owe`) and a sort (`sort=amount`) go to the URL and survive a reload; Back undoes them one at a time.
- **Bills paging:** page 2 survives a reload and Back/Forward ("21–28 of 28").
- **Bills filters:**
  - `currency=USD`, then `status=draft` gives the filtered-empty state;
  - that survives a reload, Back returns to `?currency=USD`, and Clear filters works;
  - search (`q=pizza`), `mine=1` and `sort=total` (the first row is the largest) work.
- **Exchange pair in the list:** the draft exchange is one row with ⇄ and Accept. Accepting it from the list finalizes both bills, and Debts then shows Boris 120.00 EUR and 430.00 PLN.
- **Bill detail:** a finalized bill renders lines, parts (×3), payments and "Boris owes you 30.00 EUR", with the Drafts and Recent sub-section. An exchange half shows the deal, the other half and Accept exchange.
- **Draft flow:**
  - Finalize is disabled with "Add a payment to finalize";
  - Add line goes to `/bills/<id>/add-line` (it survives a reload), and Lemonade 4 EUR for Anna and Clara is added;
  - Add payment goes to `/add-payment`, and 84 EUR is added;
  - Finalize shows Finalizing…, then Finalized;
  - Debts then gives Boris **151.50 EUR** (120 + 30 × 84/80), Dmitry 143.15 EUR and Clara 2.10 EUR, checked through the API too.
- **Cancel:** Cancel bill, then Yes, cancel, and the bill becomes Cancelled.
- **Phone (390):** cards with no table, "Page 1 of 2", and the folded filters. ☰ opens the drawer with Balances, and it closes after navigating.
- **Clara (non-participant):** a bill she isn't on, and an unknown id, both show "Bill not found". On a bill she's on but didn't create, she sees "You owe …" and no actions.
- **API:** `sortBy=Nope` gives 400, and `Debts/Own` without a token gives 401.

**Screenshots** (the real app) are in `DebtBot.Doc/web-ui/reports/task-05/`:
- `debts-1280-light.png`, `debts-1280-dark.png`, `debts-390-light.png`, `debts-390-dark.png`, `debts-390-dark-drawer.png`;
- `bills-1280-light.png`, `bills-1280-dark.png`, `bills-drafts-1280-light.png`, `bills-390-light.png`, `bills-390-dark.png`, `bills-390-filters-dark.png`;
- `bill-1280-light.png`, `bill-1280-dark.png`, `bill-390-light.png`, `bill-exchange-1280-light.png`, `bill-draft-addline-1280-light.png`, `bill-draft-addline-1280-dark.png`, `bill-notfound-1280-light.png`.

They were taken after the flows ran, so some drafts are already finalized or cancelled in later shots.

**Clean-up:**
- `down -v`;
- removed `debtbot-t5:local` and the pulled `postgres:14.1-alpine` and `rabbitmq:3`;
- ran image and builder prune;
- removed the local `wwwroot`;
- about 17 GB of disk is free;
- `ClientApp/node_modules` is kept for the next UI task.

**Not verified live:**
- the "taking longer than usual" state (it needs a stuck consumer);
- a 403 shown from a real action (the UI hides actions it can't do; the error path is only compiled);
- the bills list's Accept for an admin who isn't the creator.

## Decisions
- **"Bills with them"** includes not yet finalized bills you're both in (web only; the bot unchanged).
- **The Bills total** counts bills, so a page with a folded exchange pair shows one row fewer than "1–n of N" says.
- **Currency filters** are a 3-letter input with suggestions (from your balances). There's no endpoint listing currencies, and a select would miss bills in currencies without a current balance.
- **The person filter on Bills** uses your contacts. On Debts it uses the people in your balances.
- **Dates** follow the mockups (day first, 24-hour) through en-GB formatting while the UI language is English.
- **The editors** are inline sections with their own URLs, not dialogs. Leaving one (Cancel, or after adding) uses `replace`, so Back doesn't reopen it.
- **Polling** is 1 s, for up to 30 s, for our own action or for a bill opened while `Ready`.

## Open for later tasks
- **T6 and T7:** use the list pattern (see "List views" in `mockups/shell/README.md`). Add `exchangesSpec` and `spendingsSpec` to `lib/lists.ts`, and backend `sortBy`/`sortOrder` plus filters on their endpoints, following `ListSorting.cs`.
- **T6:** the New bill form should reuse `LineEditor` and `PaymentEditor` and `usePeople`. The Exchange page takes `?with=<userId>` from the Debts rows.
- **Seeding note for later checks:** Telegram usernames are stored with a leading "@" (the bot's convention), so `POST Users` needs `telegramUserName: "@name"` for `by-telegram` to find the user. Otherwise it creates a placeholder.
- **A pre-existing quirk:** the shell's theme button shows "◐" (system) as "◖" in headless Chrome's font. Not changed.
