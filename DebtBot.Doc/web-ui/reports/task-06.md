# T6 report: New bill and Exchange

- **Branch:** `task/06-ui-newbill-exchange`, based on local `web-ui` at `873cdf2`.
- **Commits:**
  - `8734993`: the approved mockups (`mockups/new-bill/`, `mockups/exchange/`), the URL map;
  - `de2ac82`: `GET Bills/exchanges` with tests and `api.md`;
  - `34996c2`: the New bill and Exchange pages, the shared exchange components, the sub-sections;
  - the last commit: a phone-card fix, docs, this report, the screenshots and the check scripts.

## Mockups (approved by the user, 4 rounds)
- **Round 1:** New bill form (people first, lines, payments, a preview of who owes whom, validation), paste text (preview, errors, the format help), Exchange (form, the draft after Create with Accept and Cancel, the list).
- **Round 2:** "on exchange I sometimes want to specify exchange rate instead of the second amount": the second side gets an **Amount | Rate** switch.
- **Round 3:** "let the first one be exchange rate also, but then switch the second to amount": either side can be the rate, only one at a time.
- **Paid in another currency:** the user asked for an example and asked whether the API has a third currency. It doesn't: a bill has a bill currency and a payment currency, and debts are always in the payment currency. The user remembered a planned "debts currency" choice. It exists only as an unmerged 2024 commit on `origin/feature-currency_selection` ("ChargeInPaymentCurrency"), so it's out of scope (see "Open").
- **Round 4:**
  - "tables don't fit the narrow screen and most elements are cropped from sides": on phones the grid no longer lets content overflow, and the per-person table becomes one block per person;
  - "exchange rate both ways mustn't be cropped or replaced with ... as it defeats the purpose": rates are always shown both ways and in full (form, banner, list rows, phone cards), and the sidebar amounts wrap;
  - "'Both ways' is a bad caption": the caption is now "Exchange rate".
- Then "looks good".

## Backend
**`GET Bills/exchanges`** (`[FromQuery] ExchangeListQuery`) returns `PagingResult<ExchangeListItem>`, one row per pair:
- **Fields:** `forwardBillId`, `backwardBillId`, `date`, `status`, `description`, `creator`, `fromUser`/`fromAmount`/`fromCurrencyCode`, `toUser`/`toAmount`/`toCurrencyCode`, `rate` (= from / to).
- **One bill describes the whole deal:** its payer hands over the payment, and its line's person hands back the subtotal.
  - The row is built from the bill you pay in, so when you're one of the two people, **"from" is you**.
  - If you only created it (an admin making one for others), it's built from the bill with the smaller id.
- **Access:** the same as `GET Bills` (creator, payer or line participant). It's checked on the bills themselves, not on `BillParticipants`, which is filled in the background just after creation, so a new exchange shows up at once.
- **Filters and sort:**
  - `withUserId` (the other side);
  - `currencyCode` (either side, any case);
  - `status` (`Draft`; `Processed` includes `Ready`; `Cancelled`);
  - `sortBy` (`Date`, `Amount` = what "from" gives) and `sortOrder`. `ExchangeSortField` is in `ListSorting.cs`.
- Sorting ends with the id, so pages are stable. The amount is ordered through a `(double)` cast, as in T5.
- **Only linked pairs** (`exchangeBillId`) are listed. Exchanges made by `/ExchangeDebt` before T2 added the link don't appear there; they still appear in `GET Bills`.
- **No migration.** No other backend changes.
- **Tests:** `ExchangeListTests` adds 5:
  - one row per pair from each side;
  - a creator who isn't in the exchange sees it once;
  - ordinary bills are left out;
  - filters, including `Processed` with `Ready`;
  - both sorts, with stable paging.
- **Checked live on Postgres:** the `Guid <` comparison translates, a bad `sortBy` gives 400, and no token gives 401.

## Frontend
**Pages:**
- **New bill, form** (`/bills/new`, editors at `/bills/new/add-line` and `/bills/new/add-payment`):
  - description, date and time, bill currency, "Paid in", total with tips (empty = the lines' total), with a "Lines x + tips y" hint;
  - **People on this bill**: the reused `PeoplePicker`. It starts with you; people on a line or payment can't be removed;
  - lines through the reused `LineEditor` and payments through the reused `PaymentEditor`, both offering only the bill's people. Lines and payments can be removed; a second payment by the same person replaces the first, as the API does;
  - in another payment currency, a note with the rate the payments imply, both ways;
  - **Who owes whom (preview):** worked out like `BillFinalizedConsumer` (each share = subtotal × part / parts × payments total / lines total, in the payment currency; the biggest payer is owed by the rest). It's a table when the card is wide (a container query) and stacked blocks when narrow;
  - validation under the fields plus a summary. "Save as draft" needs valid fields; "Create and finalize" also needs a line and a payment, and its reason is shown;
  - the form is kept in sessionStorage, so a reload keeps it, and it's cleared once the bill exists. After creating, it goes to `/bills/<id>`. Create and finalize shows "Creating…" then "Finalizing…", and the Bill page polls while the bill is `Ready`, as in T5.
- **New bill, paste text** (`/bills/new/paste`, `?preview=1` once previewed):
  - the text box (kept in sessionStorage) and "Insert example";
  - **Preview** calls `POST Bills/text/preview`. Errors are listed, with the parser's `<pre>` shown as code;
  - the parsed bill shows each name with who it matches among your contacts (`@boris_iv → Boris`) or "new person". The matching follows `UserRepository.FindUser`: `@name` is a username, anything else is your contact name;
  - Save as draft or Create and finalize go through `POST Bills/text?createDrafted=`. If the text changed after the preview, you must preview again first;
  - the Format help is from the bot's `/AddBill` format.
- **Exchange** (`/exchange?with=&rate=give|get&draft=` plus the list's `person`, `currency`, `status`, `sort`, `order`, `page`):
  - **With:** `PersonSearch`, or `?with=<id>`;
  - **You give / <Name> gives you:** each side is Amount or Rate. A rate reads "1 EUR = x PLN", ⇅ Flip turns it round, and the worked-out amount is shown, rounded to the currency's digits, and sent. ⇄ swaps the two sides;
  - the "Exchange rate" box shows both ways;
  - Create makes the two drafts and opens `?draft=<billId>`: the banner with ✓ Accept and Cancel (with a confirmation), the "Accepting…" state and ✕ to close it;
  - **the list** uses the shared `ListView`, with `exchangesSpec`/`exchangesQuery` in `lib/lists.ts`:
    - filters: person, currency, status;
    - sort: date, or the amount you give;
    - columns: with, you give, you get, rate (both ways), status, and Accept for the creator or an admin. Anyone else sees "<name> accepts";
  - the typed amounts are kept in sessionStorage.

**Sidebar:**
- New bill: Mode (Form also covers its editors), then "Continue a draft" (your latest drafts that aren't exchanges).
- Exchange: "Waiting to accept" (opens `?draft=`) and "Recent exchanges". `SidebarEntry` gained `wrap` and `active` props.

**Consistency (brief, scope 4):**
- `components/exchange/ExchangeBits.tsx` holds `ExchangeStatusPill` ("Waiting to accept", "Accepting…", "Accepted", "Cancelled"), `RateBothWays`, `DealText` and `ExchangeBanner`.
- The Bill page's exchange banner and header pill, the Bills list's exchange rows ("✓ Accept" and the pill) and the Exchange page all use them. The old `bill.exchange*` strings are gone.

**Other:**
- **Code:** `lib/exchange.ts` (rate maths, deals) and `lib/newBill.ts` (form state, checks, request mapping, preview, storage);
- **API:** `api/types.ts` gains `BillCreation`, `ParsedBill`, `TextPreview`, `ExchangeCreation`, `ExchangeListItem`, `ExchangeListQuery` and `ExchangeSortField`; `api/bills.ts` gains `useExchanges`, `useCreateExchange`, `useCancelBill`, `useCreateBill`, `previewBillText` and `useCreateBillFromText`;
- **Strings:** new groups `newBill`, `paste` and `exchange`, plus `subnav` additions.

## Checks
- `npm run build`: OK, including the i18n check and `tsc -b`.
- `npm run lint`: 0 warnings, 0 errors.
- `npm test`: **63 passed** in 8 files (36 before, plus 27). The new tests cover:
  - exchange maths: rate parsing, rounding, both rate sides and flips, swap, problems, request mapping, deals from a bill or a list item;
  - the New bill form: request mapping, checks, locked people, payment replacement;
  - the preview: tips, by parts, another payment currency, a second payer;
  - `exchangesSpec` URL ↔ query, with the form's parameters kept;
  - `matchTextPerson`.
- `dotnet test` (SDK 11): **164 passed**, 0 failed (159 plus 5).
- `dotnet build` of the solution is part of it; 0 errors.

## Runtime check (docker compose, cold build)
- **Setup:**
  - the real `docker-compose.yml` plus `reports/task-06/scripts/t6-compose.yml` (project `debtbot-t6`, image `debtbot-t6:local`, API on 18096, Development, a JWT key, the default admin, a named DB volume);
  - each run started from a clean volume (`scripts/rerun.sh`).
- **Seed** (`scripts/seed6.mjs`, all through the API, Telegram usernames with "@"):
  - Anna (the default admin), plus Boris, Clara and Dmitry with passwords;
  - contacts and "Mum";
  - a finalized bill and a draft;
  - 22 exchanges: one made by Dmitry, 5 accepted, 1 cancelled.
- **Headless Chrome over CDP (`scripts/check6.mjs`): 14/14 passed** on the final image:
  - **Form bill:** "bor" and "dmi" picked by autocomplete; the line editor offers exactly those three people; Pasta split equally and Tiramisu by parts (Boris ×2, Dmitry ×1); the form survives a reload; payment 84 EUR; the preview gives Boris 38.32 and Dmitry 28.00; a bad currency shows the errors and doesn't save;
  - **Save as draft, then Finalize** on the bill: through the API, Boris +38.32 EUR and Dmitry +28.00 EUR. The form is cleared after creating;
  - **Create and finalize directly**, PLN paid in USD: the preview shows "1 USD = 4.3000 PLN" and "You owe Clara 10.00 USD"; the bill ends Finalized, and Clara's USD balance −10;
  - **Paste text:** an error ("Failed to parse line subtotal"), `preview=1` in the URL, then fixed; "Looks good" with `@boris_iv → Boris` and "new person"; the preview survives a reload; created as a draft;
  - **Exchange:** with `?with=Boris`, 100 EUR plus a rate of 4.3 on the second side gives 430.00 PLN, with both rates shown. Create leads to `draft=`, the banner says "You give Boris 100.00 EUR, Boris gives you 430.00 PLN.", and Accept leads to Accepted. Through the API, Boris +100 EUR and −430 PLN;
  - **Rate on the first side:** 50 USD at 1 EUR = 1.087 USD gives 46.00 EUR; `rate=give` and the amounts survive a reload;
  - **List:**
    - "1–20 of 23", then page 2;
    - a reload and Back/Forward keep it;
    - the Accepted filter matches the API's count;
    - the person filter by typing "cla";
    - sort by amount: the first row is the API's largest;
    - a reload keeps them, and Back undoes them one at a time;
  - **Accept from the list:** the waiting count drops by one;
  - **A non-admin (Boris)** sees "Anna Petrova accepts" and no Accept button;
  - **Phone (390):** cards instead of the table, and nothing wider than the screen on Exchange, New bill and Paste;
  - **The Bill page and the Bills list** use the same banner and words ("Exchange with …", "Waiting to accept", "Exchange rate").
- **Screenshots of the real app** (`reports/task-06/`):
  - New bill: `newbill-form-1280-{light,dark}`, `newbill-addpayment-1280-light`, `newbill-errors-1280-light`, `newbill-fx-1280-{light,dark}`, `newbill-fx-390-{light,dark}`;
  - Paste text: `paste-errors-1280-light`, `paste-preview-1280-{light,dark}`, `paste-390-{light,dark}`;
  - Exchange: `exchange-rate-1280-light`, `exchange-created-1280-{light,dark}`, `exchange-ratefirst-390-{light,dark}`, `exchange-list-1280-{light,dark}`, `exchange-1280-{light,dark}`, `exchange-390-{light,dark}`, `exchange-draft-390-{light,dark}`;
  - Bill page: `bill-exchange-1280-light`.
  - They were taken during the flows, so later shots show what earlier steps created or accepted.
- **Clean-up:** `down -v`; removed `debtbot-t6:local` and the pulled `postgres:14.1-alpine` and `rabbitmq:3`; ran image and builder prune; removed the local `wwwroot`; `dotnet build-server shutdown`. About 16 GB is free. `ClientApp/node_modules` is kept for the next UI task.

**Not verified live:**
- the "taking longer than usual" state;
- a 403 shown from a real action (the UI hides actions you can't do);
- Cancel from the Exchange banner (the API path is T5's, and the confirmation is only compiled);
- `?with=` pointing at someone who isn't a contact (the search box shows instead).

## Decisions
- **An exchange list row is seen from your side:** "from" is you, so "You give / You get" and Sort by amount mean what you give.
- **Rate mode:**
  - the typed rate has up to 8 decimals;
  - the worked-out amount is rounded to the currency's digits (0.01 for EUR) and sent as an amount, because the API takes two amounts;
  - when a side leaves rate mode, it keeps the amount that was worked out;
  - the currencies must differ (checked only in the browser).
- **Accepted** includes `Ready` in the filter. In the list a `Ready` pair shows "Accepting…".
- **Admins** see Accept on every draft exchange, the same as the API allows.
- **Form and drafts:** the New bill form and the paste text are kept in sessionStorage, not the URL (too large for it). The editors and the preview state are in the URL.
- **Preview names** are matched in the browser against your contacts and yourself, the way the server does. Phone, email and Telegram id matches aren't known in advance, so they show as "new person" even though the server may find them.
- **Paste-text Create and finalize** uses the existing `POST Bills/text?createDrafted=false`, which finalizes on the server.
- **Phone screenshots** are taken as a phone user (no desktop sidebar choice saved), so the drawer starts closed.

## Open for later tasks
- **A "debts currency" per bill:** count a bill's debts in its bill currency or its payment currency. The user remembers it was planned; it exists only as an unmerged commit on `origin/feature-currency_selection`. It would need a DB field, API and finalize changes. It's a product decision for the orchestrator or the user.
- **Exchanges made by `/ExchangeDebt` before the pair link** don't appear in `GET Bills/exchanges`.
- **T7:** the Spendings list can follow `exchangesSpec` the same way, and `SidebarEntry`'s `wrap` is there for amounts.
- **T9:** `reports/task-06/scripts/` holds the seed and check scripts. `rerun.sh` runs from the repo root and needs the compose override next to it.
