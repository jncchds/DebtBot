# T7 report: Spendings, Contacts, Subscriptions, Profile

- **Branch:** `task/07-ui-contacts-profile`, based on local `web-ui` at `36458bb` (the approved T7 mockups were already merged).
- **Commits:**
  - `fa2cd21`: backend (spendings filters, sort and summary; contact `addedAt` with its migration; `subscription/cancel`), tests, `api.md`;
  - `ac4702b`: the four pages, the sub-sections and the 🔔 nav badge;
  - the last commit: check scripts, screenshots, the shell and mockup READMEs, the ClientApp README and this report.
- **No mockup round:** built from the approved READMEs and PNGs in `mockups/spendings/`, `contacts/`, `subscriptions/` and `profile/`. Nothing in them was unclear enough to ask the user. What I decided while building is under "Decisions" and in each README's new "Built (T7)" section.

## Backend
- **`GET Spendings/Own`** binds `[FromQuery] SpendingListQuery`:
  - `currencyCode` (bill currency, any case);
  - `dateFrom` and `dateTo` (whole days, UTC);
  - `includeCancelled` (default `false`, so cancelled spendings are now hidden unless asked for);
  - `sortBy` (`SpendingSortField`: `Date`, `Amount`) and `sortOrder` (`Desc` by default);
  - paging.
  - Sorting ends with the bill id, so pages are stable. The amount sort uses the `(double)` cast, as T5 does.
  - The bot's `/Spendings` keeps its old list (every spending, newest first) through the old overload.
- **New `GET Spendings/Own/summary?dateFrom=&dateTo=`** returns `[{ currencyCode, amount, count }]` over non-cancelled spendings, ordered by currency. The sums are done in memory, so the decimals stay exact.
- **The query's dates are treated as UTC** (`DateTime.SpecifyKind`). A date bound from the query string has no kind, and Npgsql refuses to send that as `timestamptz`.
- **`Contact.addedAt`:**
  - a new nullable column `UserContactsLinks.AddedAt`, from the migration `ContactAddedAt` (made with dotnet-ef 11);
  - set whenever a link is created: `EnsureContact` (by-telegram and bills) and the older `AddContact`. A merge keeps the date.
  - **Existing rows stay `null`.** Their real date is unknown, and the migration time would make every old contact look "added today" under Recently added. The UI sorts them last and shows no date.
- **`by-telegram`** already trimmed the `@` and matched the stored `@name` ignoring case. New tests check `boris_iv`, `@boris_iv` and `  @Boris_IV ` against a stored `@boris_iv`.
- **New `POST UserContacts/subscription/cancel?contactId=`** removes your own subscription to them, pending or confirmed. It returns 404 `{ message }` when there's none, and "you" always comes from the token.
- **Subscription wording checked:** the bot notifies subscribers about new bills (`BillProcessedConsumer`), and `/Debts` includes subscriptions, so "bills and debts" stays.
- **`api.md`** is updated.

## Frontend
**Pages:**
- **📊 Spendings (`/spendings?currency=&from=&to=&cancelled=1&sort=date|amount&order=&page=`):**
  - the shared `ListView`, with `spendingsSpec`/`spendingsQuery` in `lib/lists.ts`;
  - columns: date, the bill link with "NN % of the bill", the amount (with "paid as …" when the payment currency differs), and a Cancelled pill. Cancelled rows are faded and struck through;
  - phone cards and both empty states, as in the mockup;
  - the currency suggestions come from the all-time summary.
- **👥 Contacts (`/contacts?q=`):**
  - the request banner (one or "and N others"), with Review linking to `/subscriptions`;
  - the add card: a placeholder message, "already in your contacts", the username checked in the browser, and the API's message for any other 400;
  - search in the URL, replacing the history entry as you type;
  - a table at 1024 px and wider, cards below;
  - inline rename (Enter saves, Esc cancels) and the inline remove confirmation;
  - the notifications cell: you get theirs, asked, the Ask button, or "—" for placeholders.
- **🔔 Subscriptions (`/subscriptions`):**
  - three cards (Requests, Get your bills and debts, Your subscriptions) with Confirm, Decline, Stop and Withdraw;
  - errors show in `alert-danger`.
- **👤 Profile:**
  - Details: browser and API validation under each field, Reset, and "✓ Saved". The session is refreshed, so "Sign out (Name)" updates;
  - Login & password: first-time and change modes, too short, mismatch, a 409 alert with the login field marked, the API's message otherwise, and the password fields cleared after saving;
  - Telegram: the account details and the bot card, started or not, with the bot link from `auth/config`.

**Sidebar:**
- the 🔔 Subscriptions nav entry sits after Contacts, with the pending-request badge (hidden on the collapsed rail);
- sub-sections (`components/subnav/people.tsx`):
  - Spendings: This month, one entry per bill currency, each opening that month in that currency, then … All spendings;
  - Contacts: Recently added, then … All contacts;
  - Subscriptions: a Summary that scrolls to each card.
- `/contacts/requests` now redirects to `/subscriptions`.

**Code:**
- `lib/contacts.ts`: search, sorting, recent contacts, subscription groups, the badge count and the month range;
- `lib/profile.ts`: the form checks and request mapping;
- `api/people.ts`: the contact, subscription, profile and password mutations; `api/bills.ts` gains `useSpendings` and `useSpendingSummary`, and spendings are refetched after bill changes;
- an `alert-info` utility in `index.css`;
- the i18n check now also reads `<Trans i18nKey=…>`.
- **Strings:** new groups `spendings`, `contacts`, `subscriptions` and `profile`, plus additions to `nav`, `subnav` and `pages`.

**Shell README:** the nav order, the badge, the URL map (Spendings' full query, `/contacts?q=`, `/subscriptions`, no `/contacts/requests`) and the sub-sections are updated.

**Kept easy to extend for T7b ("following"):** the spendings list and summary hooks and services take the user from the endpoint, not from the URL spec. `BudgetService` takes a `userId`, so adding `ofUserId` is a controller change plus one parameter on the query.

## Checks
- `npm run build`: OK (the i18n check, `tsc -b`, vite).
- `npm run lint`: 0 warnings, 0 errors.
- `npm test`: **77 passed** in 10 files (63 before, plus 14), covering:
  - `spendingsSpec` (defaults, URL to query, invalid values);
  - contact search, sorting and Recently added;
  - subscription grouping and the badge count;
  - the month range;
  - the profile and password checks.
- `dotnet build DebtBot.sln`: 0 errors.
- `dotnet test` (SDK 11): **174 passed**, 0 failed (164 plus 10 in `SpendingsContactsTests`):
  - spendings defaults, filters and whole days;
  - the amount sort with stable paging;
  - the summary;
  - by-telegram with or without the `@` (3 cases);
  - a placeholder with `addedAt`;
  - `addedAt` null for old links;
  - cancel removes only your own subscription, and gives 404 when there's none.

## Runtime check (docker compose, cold build)
- **Setup:** the real `docker-compose.yml` plus `reports/task-07/scripts/t7-compose.yml` (project `debtbot-t7`, image `debtbot-t7:local`, API on 18097, Development). Each run starts from a clean volume (`scripts/rerun.sh`).
- **Seed** (`scripts/seed7.mjs`, through the API):
  - Anna (admin) plus Boris, Clara and Dmitry with passwords, and Elena with no password;
  - "Mum" as a placeholder;
  - 24 finalized bills in October and November (EUR, PLN, USD, one PLN bill paid in EUR), one of them cancelled;
  - subscriptions in every state for Anna and for Dmitry;
  - `TelegramBotEnabled` set with SQL for the real users, because only the bot's `/start` sets it.
- **Headless Chrome over CDP (`scripts/check7.mjs`): 15/15 passed:**
  - **Spendings:**
    - "1–20 of 23", newest first, with no cancelled spending;
    - the share and "paid as 74.42 EUR";
    - This month matches the summary API;
    - Show cancelled, sort by amount (the first row matches the API), page 2 ("21–24 of 24");
    - a reload keeps them, and Back undoes them one at a time;
    - the This month EUR entry gives `currency=EUR&from=2026-11-01&to=2026-11-30` with the right count;
    - a filtered-empty state, then Clear filters.
  - **Contacts:**
    - `@Boris_IV` gives "already in your contacts", `ab` gives the username message, and `grandpa_vlad` gives the placeholder message, a highlighted row and "today · never started the bot" in the sidebar;
    - `?q=ivan` shows "1 of 5" and survives a reload; a search with no match shows its empty state;
    - Mum renamed to Mama with Enter (checked through the API);
    - grandpa removed after the inline confirmation (checked through the API);
    - Ask for Dmitry's bills leads to "Asked, waiting" and `Pending` in the API.
  - **Subscriptions:**
    - **As Anna:** badge 1; Review from the banner; the Summary shows "1 waiting for you" and "1 confirmed · 2 waiting"; Confirm Dmitry takes the badge to 0.
    - **As Dmitry:** badge 2; Confirm Anna gives 1; Decline Clara gives 0; Stop Boris on both cards; Withdraw Clara. The API ends with exactly Anna ⇄ Dmitry confirmed.
  - **Profile:**
    - Details: an empty name and a bad email are both shown; saving "Anna P." updates the sidebar's "Sign out (Anna P.)" and is checked through the API; then it's set back;
    - Anna: a 409 for login `boris` (the alert, with the field marked), and "The current password is incorrect.";
    - **Elena** (Telegram-only, no password):
      - her sign-in is stood in for by answering the SPA's refresh call with a session holding a real API token, because the live widget needs HTTPS and a bot domain;
      - Telegram shows "Not started";
      - first time: too short, then a mismatch, then saved, after which the form switches to "Change login or password";
      - a change with the current password;
      - she then **signs in on `/login` with the new password**.
    - Telegram as Anna: Started, the Admin pill and the id.
  - **Phone (390):**
    - cards, and nothing wider than the screen on Spendings, Contacts, Subscriptions and all three Profile pages;
    - no badge on the collapsed rail; the badge shows in the open drawer.
- **Note on the check:** the auth endpoints allow 10 requests a minute per IP, and the check signs in often. So `signIn` waits 30 s and retries when the login page says "Sign-in failed". That's the API working as designed, not a UI fault.
- **Screenshots** of the real app, light and dark (`reports/task-07/`):
  - Spendings: `spendings-1280`, `spendings-cancelled-1280`, `spendings-filtered-empty-1280`, `spendings-390`;
  - Contacts: `contacts-1280`, `contacts-added-1280`, `contacts-remove-1280`, `contacts-search-empty-1280`, `contacts-390`, `contacts-390-drawer`;
  - Subscriptions: `subscriptions-1280` (Anna), `subscriptions-dmitry-1280`, `subscriptions-dmitry-after-1280`, `subscriptions-390`;
  - Profile: `profile-details-1280`, `profile-details-error-1280`, `profile-details-390`, `profile-password-1280`, `profile-password-taken-1280`, `profile-password-first-1280`, `profile-password-390`, `profile-telegram-1280`, `profile-telegram-off-1280`, `profile-telegram-390`.
- **Clean-up:**
  - `down -v`;
  - removed `debtbot-t7:local` and the pulled `postgres:14.1-alpine` and `rabbitmq:3` (no container used them);
  - ran image and builder prune (2.35 GB of build cache);
  - removed the local `wwwroot`;
  - ran `dotnet build-server shutdown`.
  - About 16 GB is free. `ClientApp/node_modules` is kept for the next UI task.

**Not verified live:**
- the real Telegram widget sign-in (stood in for, as above);
- the bot link when `telegramBotUsername` is set (it's null in this compose, so the page says "the bot" and shows no button; that branch is only compiled);
- a 400 from `by-telegram` itself (the browser check catches bad usernames first);
- a phone-number error from the API.

## Decisions
- **Old contacts get `addedAt = null`**, not the migration time (see Backend).
- **This month** uses the browser's calendar month, sent as `yyyy-mm-dd` days. The server treats them as UTC days, so a bill from the first or last hours of the month can fall on the other side for users far from UTC. The same is true of Bills' date filters.
- **The Contacts table** switches at the `lg` breakpoint (1024 px window), because the page isn't a `ListView`.
- **Recently added entries** open `/contacts?q=<name>`; the mockup didn't say where they go.
- **Subscriptions Summary entries** link to `#requests`, `#theirs` and `#yours`, and the page scrolls to them.
- **After saving profile details** the session is refreshed (the refresh token rotates), so the sidebar name comes from the server.

## Open for later tasks
- **T7b ("following"):** add `ofUserId` to `GET Spendings/Own` and the summary (`BudgetService` already takes the user), then `?of=` on the list specs.
- **T8:** the Admin merge badge can reuse the nav `badge` field in `Layout.tsx`.
- **T9:** `reports/task-07/scripts/` holds the seed and check; `rerun.sh` runs from the repo root.
