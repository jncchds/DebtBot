# DebtBot REST API for the web UI

This covers everything the UI tasks (T4–T8) call, as of the end of the series (T9). Sign-in endpoints (`api/v1/auth/*`) are described in `web-sign-in.md`; their shapes are repeated briefly at the end.

## Conventions
- **Base:** `/api/v1`. Every endpoint below needs `Authorization: Bearer <accessToken>` unless marked otherwise. No token → **401**.
- **"Self"** is always the user in the token. No endpoint takes the caller's own id from the body or query.
- **JSON:** camelCase; enums go out and come in as **strings**. Dates are ISO 8601 UTC. Amounts are JSON numbers (decimals, e.g. `30.0000`).
- **Errors:** `{ "message": string, "errors": string[] }` where `message` joins `errors` with `; `.
  - **400** validation or wrong state; **403** you can see it but may not change it; **404** missing, or you may not see it.
  - A **403 from the Admin policy** (non-admin calling an admin endpoint) and framework 401s have an empty body.
- **Access rules:**
  - **See a bill** (`HasAccess`): you're its creator, a payer or a line participant — or an admin.
  - **Read a bill** (`CanRead`, since T7b; `GET Bills/{id}` and `GET Bills/lines/{lineId}` only): what `HasAccess` allows, or you have a **confirmed** subscription to anyone taking part in it (creator, payer, line participant, or in its participants list).
  - **Change a bill** (lines, payments, finalize, cancel, exchange accept): creator or admin. Someone without `HasAccess` gets 404 (that includes a subscriber who can only read it); a participant who isn't the creator gets 403.
  - **Someone else's lists (`ofUserId`, since T7b):** `GET Debts/Own`, `GET Bills`, `GET Spendings/Own` and `GET Spendings/Own/summary` take `ofUserId?`. Left out or your own id: your lists, unchanged. Someone you have a **confirmed** subscription to (`mySubscription = Confirmed`): their lists, exactly as they would get them. Anything else (pending, none, the other direction): **403** `{ message }`. Admins aren't special here.
- **Sorting (since T5):** paged lists take `sortBy=<Field>` and `sortOrder=Asc|Desc` next to their filters; see `mockups/shell/README.md`, "List views".
- **Paging:** `?pageNumber=0&countPerPage=N` (page numbers start at 0; without `countPerPage` everything comes in one page). Paged responses are a `PagingResult<T>`.
  - Since T9 the page is read first and `totalCount` is kept consistent with it: without `countPerPage` it's one query; a short page is the last page, so the total follows from it; a full page is counted separately but never reported below what it shows. Data can still change between two requests.

```ts
interface PagingResult<T> {
  countPerPage: number | null; pageNumber: number; totalCount: number; lastPageNumber: number;
  isFirstPage: boolean; isLastPage: boolean; moreThenFivePassed: boolean; moreThenFiveLeft: boolean;
  items: T[];
}
```

## Enums
```ts
type ProcessingState = "Draft" | "Ready" | "Processed" | "Cancelled"; // Ready = finalized, ledger being written
type UserRole = "Regular" | "Admin";
type SubscriptionState = "None" | "Pending" | "Confirmed";
type MergeReason = "SameTelegramId" | "SameTelegramUserName";
```

## Shared shapes
```ts
interface UserDisplay { id: string; displayName: string; telegramId: number | null; telegramUserName: string | null }

interface LedgerRecord { creditorUser: UserDisplay; debtorUser: UserDisplay; amount: number; currencyCode: string }

interface BillLineParticipant { billLineId: string; user: UserDisplay; part: number; userDisplayName: string }
interface BillLine { id: string; billId: string; itemDescription: string; subtotal: number; participants: BillLineParticipant[] }
interface BillPayment { user: UserDisplay; amount: number }

interface Spending {
  description: string; amount: number; currencyCode: string;          // in the bill currency
  paymentAmount: number; paymentCurrencyCode: string;                 // in the payment currency
  portion: number;                                                    // 0..1 share of the bill
  billId: string; date: string; isCanceled: boolean; user: UserDisplay;
}

interface Bill {                       // GET Bills/{id}, and the bill returned by lines/payments
  id: string; creator: UserDisplay;
  currencyCode: string; paymentCurrencyCode: string; description: string; date: string;
  status: ProcessingState; totalWithTips: number;
  exchangeBillId: string | null;       // set on both bills of an exchange: the id of the other bill
  lines: BillLine[]; payments: BillPayment[]; spendings: Spending[]; ledgerRecords: LedgerRecord[];
}

interface BillListItem {               // GET Bills, GET Bills/All
  id: string; currencyCode: string; paymentCurrencyCode: string; description: string; date: string;
  status: ProcessingState; totalWithTips: number;
  creator: UserDisplay; exchangeBillId: string | null;
  ledgerRecords: LedgerRecord[];
  participantIds: string[];            // since T7b: everyone in the bill's participants list
  participantCount: number;            // since T8: how many (creator, payers and line participants)
}

interface Debt { creditorUser: UserDisplay; debtorUser: UserDisplay; amount: number; currencyCode: string }
```

`Debt` is seen from the creditor's side: `amount > 0` means `debtorUser` owes `creditorUser`; `amount < 0` means `creditorUser` owes `debtorUser` `-amount`. In `Debts/Own`, `creditorUser` is always you (since T5: no subscribed users' balances, and no settled zero balances), and `debtorUser.displayName` is the name in your contact list. The bot's `/Debts` still includes subscriptions.
With `ofUserId` (since T7b), `creditorUser` is that person and `debtorUser` carries each counterpart's **own** display name (not anybody's contact name); sorting by `Person` uses that name.

## Bills (`/Bills`)

| Endpoint | Access | Request | Response |
|---|---|---|---|
| `GET Bills` | any user | query: `ofUserId?` (since T7b: someone you're subscribed to, confirmed; else 403), `withUserId?`, `currencyCode?` (payment currency, any case), `status?`, `createdByMe?`, `dateFrom?`, `dateTo?` (`yyyy-mm-dd`, whole days, UTC), `search?` (description contains, ignoring case), `sortBy?` (`Date` default, `Description`, `Total`, `Status`), `sortOrder?` (`Asc`, `Desc` default), paging | `PagingResult<BillListItem>` — bills you take part in (the bill's participants list: creator, payers and line participants, written with the bill since T9, so a new bill is listed at once). `withUserId` (since T5) matches a ledger record between you, or a not yet finalized bill you're both in. With `ofUserId`, "you" in `withUserId` and `createdByMe` is that person. |
| `GET Bills/Own` | any user | paging | `PagingResult<Bill>` — bills you created |
| `GET Bills/All` | **Admin** | since T8: the `GET Bills` query without `ofUserId` and `createdByMe` (ignored), plus `creatorId?`; `withUserId` is anyone taking part (the participants list) | `PagingResult<BillListItem>` — every bill (before T8: `PagingResult<Bill>`, paging only) |
| `GET Bills/{id}` | read the bill (`CanRead`) | – | `Bill`, or 404 |
| `POST Bills` | any user (becomes creator) | `BillCreation` | `string` (the new bill id, JSON string). Always a **draft**. |
| `POST Bills/{id}/lines` | change, **draft only** | `BillLineCreation[]` (at least one) | `Bill` |
| `POST Bills/{id}/payments` | change, **draft only** | `BillPaymentCreation[]` (at least one) | `Bill` |
| `GET Bills/lines/{lineId}` | read the line's bill (`CanRead`) | – | `BillLine` |
| `POST Bills/{id}/Finalize` | change | – | 200, empty. The ledger is written in the background (status `Ready`, then `Processed`). |
| `POST Bills/{id}/Cancel` | change | – | 200, empty. Background; status becomes `Cancelled`, ledger and spendings are marked cancelled. For an exchange bill, its other bill is cancelled too. 400 if already cancelled. |
| `POST Bills/text/preview` | any user | `text/plain` body, the bot's bill text format (see `POST Bills/text` in Swagger) | always 200: `{ result: BillParser, errors: string[], isValid: boolean }`. Nothing is saved. |
| `POST Bills/text?createDrafted=bool` | any user | `text/plain` | `string` bill id; 400 with `string[]` errors (unchanged, pre-existing) |
| `POST Bills/exchange` | any user (becomes creator) | `ExchangeCreation` | `Exchange` — two **draft** bills |
| `GET Bills/exchanges` | any user | query: `withUserId?` (the other side), `currencyCode?` (either side, any case), `status?` (`Draft` = waiting to accept, `Processed` = accepted, includes `Ready`, `Cancelled`), `sortBy?` (`Date` default, `Amount` = what "from" gives), `sortOrder?` (`Desc` default), paging | `PagingResult<ExchangeListItem>` (since T6): exchanges you created or are one of the two people in, **one row per pair**. When you're in it, "from" is you. |
| `POST Bills/exchange/finalize` | change, on **both** bills | `Exchange` | `Exchange`. Finalizes both. |
| `POST Bills/import/{creatorTelegramUserName}` | **Admin** | Excel file | unchanged |

```ts
interface BillCreation {
  currencyCode: string;            // 3 letters, stored upper case
  paymentCurrencyCode?: string;    // defaults to currencyCode
  description?: string;
  date?: string;                   // defaults to now
  totalWithTips?: number;          // > 0; defaults to the sum of line subtotals
  lines?: BillLineCreation[];
  payments?: BillPaymentCreation[];
}
interface BillLineCreation {
  itemDescription: string;
  subtotal: number;                // > 0, bill currency, without tips
  participants: { userId: string; part?: number | null }[];  // ≥ 1, each user once;
                                   // part > 0; leave it out for an equal split (counts as 1)
}
interface BillPaymentCreation { userId: string; amount: number }   // amount > 0, payment currency; each user once
```

- **Validation (400):** bad currency code, amounts or subtotals not above zero, a line without participants, a user twice in one line or in the payments, an unknown user id, a non-draft bill.
- **Payments on a user who already paid** replace that user's amount (that's how the repository has always worked).
- **Total with tips when adding lines:** if the bill's `totalWithTips` equals the sum of its current line subtotals (no tips, or an empty draft), it grows with the new lines. Otherwise it stays as it is, so a bill entered with a receipt total keeps it.
- **Finalize (400)** needs: status `Draft`, at least one line with subtotal > 0, every line with participants, at least one payment > 0, total > 0.
- **The user picker** for lines and payments uses contacts (`GET UserContacts/Own`) plus yourself (`GET profile`). There is no global user search.

### Exchange
```ts
interface ExchangeCreation {
  fromUserId: string; fromAmount: number; fromCurrencyCode: string;   // "from" hands over fromAmount
  toUserId: string;   toAmount: number;   toCurrencyCode: string;     // "to" hands back toAmount
  description?: string;            // defaults to "Exchange"
  date?: string;
}
interface Exchange { forwardBillId: string; backwardBillId: string }
interface ExchangeListItem {        // GET Bills/exchanges
  forwardBillId: string;            // the bill "from" pays in
  backwardBillId: string;           // the other bill; send both to exchange/finalize to accept
  date: string; status: ProcessingState; description: string; creator: UserDisplay;
  fromUser: UserDisplay | null; fromAmount: number; fromCurrencyCode: string;   // "from" hands this over
  toUser: UserDisplay | null;   toAmount: number;   toCurrencyCode: string;     // "to" hands this back
  rate: number;                     // fromAmount / toAmount
}
```
- **Exchanges list:** only linked pairs (`exchangeBillId`) are listed. Pairs made by `/ExchangeDebt` before the link existed are linked by the data migration `LinkOldExchanges` (T9), but only where the pairing is certain: same creator, status and description text, made within a minute, one payment and one "Exchange" line each, mirrored people, currencies and amounts, and no other bill matching either side. Anything ambiguous stays unlinked and only shows in `GET Bills`. Access is checked on the bills themselves (creator, payer or line participant), so a new exchange shows up at once.
- **Rules (400):** both amounts > 0; two different users that exist; 3-letter currency codes.
- **What it creates:** the same two bills as Telegram's `/ExchangeDebt`.
  - **Forward:** "from" pays `fromAmount fromCurrency` for "to"'s one line "Exchange" of `toAmount toCurrency`. After finalizing, "to" owes "from" `fromAmount fromCurrency`.
  - **Backward:** the mirror. "from" owes "to" `toAmount toCurrency`.
  - Each description ends with a line `Exchange <rate> FROM/TO`, where the rate is `fromAmount / toAmount`.
- **Recognising a draft exchange:** both bills have `exchangeBillId` pointing at each other. In `GET Bills?status=Draft` they come back as two items. The UI should show them as one row with an **Accept** button when `creator.id` is you (or you're an admin), and call `POST Bills/exchange/finalize { forwardBillId, backwardBillId }`. Either order works for listing; for accepting, send the pair as listed.
- **Accept (exchange/finalize):**
  - both bills must be drafts and each other's pair. Bills made by `/ExchangeDebt` before this change have no link and are accepted as long as neither is linked elsewhere.
  - 404 if you can't see either bill, 403 if you aren't the creator or an admin.

## Debts (`/Debts`)

| Endpoint | Access | Response |
|---|---|---|
| `GET Debts/Own` | any user; query: `ofUserId?` (since T7b), `withUserId?`, `currencyCode?`, `direction?` (`All`, `OwedToMe`, `IOwe`), `sortBy?` (`Person` default, `Amount` signed, `Currency`), `sortOrder?` (`Asc` default, `Desc`), paging | `PagingResult<Debt>`: your own non-zero balances (since T5) |
| `GET Debts` | **Admin**, paging | `PagingResult<Debt>`, all |
| `GET Debts/{userId}` | **Admin**, paging | `PagingResult<Debt>` for that user |

Debts come from a materialized view that refreshes when the ledger changes. Right after a finalize or cancel returns, it can take a moment (the background consumer) before `Debts/Own` shows the change.

## Spendings (`/Spendings`)

| Endpoint | Access | Response |
|---|---|---|
| `GET Spendings/Own` | any user; query: `ofUserId?` (since T7b), `currencyCode?` (bill currency, any case), `dateFrom?`, `dateTo?` (`yyyy-mm-dd`, whole days, UTC), `includeCancelled?` (default `false`), `sortBy?` (`Date` default, `Amount`), `sortOrder?` (`Desc` default, `Asc`), paging | `PagingResult<Spending>`, your share of finalized bills. Since T7, spendings of cancelled bills (`isCanceled: true`) are left out unless `includeCancelled=true`. Link to `Bills/{billId}`. The bot's `/Spendings` still lists all of them, newest first. |
| `GET Spendings/Own/summary?dateFrom=&dateTo=&ofUserId=` | any user (`ofUserId` since T7b) | `SpendingSummary[]`: totals of your non-cancelled spendings per bill currency, ordered by currency (since T7) |

```ts
interface Spending {
  billId: string; description: string; date: string; isCanceled: boolean;
  amount: number; currencyCode: string;               // your share, in the bill currency
  paymentAmount: number; paymentCurrencyCode: string; // the same share in the payment currency
  portion: number;                                    // 0..1 of the bill
  user: UserDisplay;
}
interface SpendingSummary { currencyCode: string; amount: number; count: number }
```
Sorting ends with the bill id, so pages are stable; amounts are ordered through a `(double)` cast as in the other lists.

## Profile (`/profile`)

| Endpoint | Access | Request | Response |
|---|---|---|---|
| `GET profile` | any user | – | `Profile` |
| `PUT profile` | any user | `ProfileUpdate` | `Profile`; 400 `{message, errors}` |

```ts
interface Profile {
  id: string; displayName: string; phone: string | null; email: string | null;
  telegramId: number | null; telegramUserName: string | null;
  telegramBotEnabled: boolean;     // the user has started the bot
  role: UserRole; login: string | null; hasPassword: boolean;
}
interface ProfileUpdate { displayName: string; phone?: string | null; email?: string | null }
```
- `PUT` replaces all three fields. An empty or missing phone or email clears it.
- Limits: display name 1–100 characters, phone ≤ 32, email ≤ 254 and must contain `@`.
- Telegram fields, login and role are never changed here. Extra fields in the body are ignored.
- Login and password are changed with `PUT auth/password`.
- `GET Users/own` (admin-only, old) is removed.

## Contacts (`/UserContacts`)

| Endpoint | Access | Request | Response |
|---|---|---|---|
| `GET UserContacts/Own` | any user | – | `Contact[]` ordered by name; `[]` when there are none |
| `PUT UserContacts/Own/{contactId}` | any user, own contacts | `{ displayName: string }` (1–100 characters) | `Contact`; 404 if not your contact |
| `DELETE UserContacts/Own/{contactId}` | any user, own contacts | – | 204; 404 if not your contact |
| `POST UserContacts/Own/by-telegram?username=` | any user | query `username`, with or without `@`, 5–32 of `A-Z a-z 0-9 _` | `Contact`. Finds the user by Telegram username, or creates a placeholder user (as bills do for an unknown @mention). Adding an existing contact returns it unchanged. 400 for a bad username or yourself. |
| `POST UserContacts/subscription/request?contactId=` | any user | – | 200. Asks to get notifications about `contactId`'s bills. 400 for yourself, 404 for an unknown user (since T3). |
| `POST UserContacts/subscription/confirm?contactId=` | any user | – | 200. Lets `contactId` get notifications about your bills. 404 `{ message }` when they haven't asked (since T3). |
| `POST UserContacts/subscription/decline?contactId=` | any user | – | 200. Removes `contactId`'s request or subscription to you. 404 `{ message }` when there is none (since T3). |
| `POST UserContacts/subscription/cancel?contactId=` | any user | – | 200. Removes **your own** request for, or subscription to, `contactId`'s bills, pending or confirmed (the mirror of `decline`). 404 `{ message }` when there is none (since T7). |

The four subscription endpoints act for the signed-in user (from the token); only the admin's `subscription/add` takes a `userId`.
| `POST UserContacts` | any user | `UserModel` | unchanged, older add endpoint; prefer `by-telegram` |
| `GET UserContacts`, `GET UserContacts/{id}`, `POST UserContacts/{id}`, `POST UserContacts/subscription/add` | **Admin** | | unchanged |

```ts
interface Contact {
  id: string;                      // the contact's user id (use it as userId in bills)
  displayName: string;             // your name for them
  userDisplayName: string;         // their own display name
  telegramId: number | null; telegramUserName: string | null;
  telegramBotEnabled: boolean;     // false: a placeholder who never started the bot
  mySubscription: SubscriptionState;    // you get notified about their bills
  theirSubscription: SubscriptionState; // they get notified about yours; Pending = you can confirm/decline
  addedAt: string | null;          // when you added them (UTC, since T7); null for contacts added before T7
}
```

`by-telegram` matches the stored `@name` ignoring case, whether or not the query has the `@` (checked in T7's tests).

`addedAt` is set whenever a contact link is created (by-telegram, a bill, the older add endpoint). Links that existed before the T7 migration (`ContactAddedAt`) have `null`: their real date isn't known, and the migration time would make every old contact look added today. The UI sorts them last in "Recently added" and shows no date.

Removing a contact keeps bills, debts and subscriptions. A later finalized bill between you adds the contact back (`EnsureContact`).

## Admin: users (`/Users`, all **Admin**)

| Endpoint | Request | Response |
|---|---|---|
| `GET Users` | query `search?` (display name, Telegram username or login; contains, ignoring case), `role?`, paging | `PagingResult<AdminUser>` ordered by display name |
| `GET Users/{id}` | – | `UserModel` (id, displayName, telegramId, telegramUserName, phone, email, telegramBotEnabled, role) |
| `POST Users/{id}/password` | `{ login?, password }` | 204 / 400 / 404 / 409 (from T1; signs the user out everywhere) |
| `POST Users/{id}/Role?role=Admin\|Regular` | – | 200; 404 `{ message }` for an unknown id; **400 `{ message }` for your own account** (since T8). `Regular` removes admin. |
| `GET Users/merge/suggestions` | – | `MergeSuggestion[]` |
| `POST Users/merge` | `{ oldUserId, newUserId }` (the old query parameters still work) | `AdminUser` (the kept user); 400 for missing or identical ids, or for merging away your own signed-in account; 404 for an unknown id |
| `PUT Users/{id}` | `UserCreationModel` (displayName, telegramUserName, phone, email; null fields stay as they are) | 200; 404 for an unknown id; **400 when `telegramId` differs from the user's** (a Telegram id is never editable, since T3) |
| `DELETE Users/{id}` | – | 200; 404 for an unknown id; 400 for your own account; **400 `{ message }` for any admin**, with or without data: make them Regular first (since T9); **409 `{ message }` while the user has bills (created, paid or shared), ledger records or spendings, or is in someone else's contacts**: merge them instead. Their own contact list and subscriptions are deleted with them. So only stray placeholders can be deleted (since T3). |
| `POST Users` | | unchanged, older endpoint |

```ts
interface AdminUser {
  id: string; displayName: string; telegramId: number | null; telegramUserName: string | null;
  phone: string | null; email: string | null; role: UserRole; telegramBotEnabled: boolean;
  login: string | null; hasPassword: boolean;   // never the hash
  billCount: number;                // bills the user takes part in
  contactCount: number;             // entries in the user's own contact list
  firstBillDate: string | null;     // earliest bill they take part in ("older")
}
interface MergeSuggestion { keep: AdminUser; merge: AdminUser; reasons: MergeReason[] }
```

### Merge suggestions
- **Pairs:** users with the same `telegramId`, or the same Telegram username ignoring case and a leading `@`. A pair that matches both ways comes once, with both reasons.
- **Groups:** in a group of three or more, every pair is listed.
- **`keep`** is the better keeper by, in order:
  1. a Telegram id **and** the bot enabled;
  2. a login or password;
  3. more bills;
  4. older (the earlier `firstBillDate`; users without bills count as newest);
  5. the id, so the result is stable.
- To merge as suggested, send `oldUserId = merge.id` and `newUserId = keep.id`. The admin can also merge the other way, or any two users.

### What a merge does
It runs in one transaction. The old user's data moves to the kept user (`newUserId`):
- **Bills created**, payments, line participations, bill participations, ledger records, spendings (budgets), contact links both ways and notification subscriptions both ways all move to the kept user.
- **When both users have a row with the same key**, the rows are combined into the kept user's row:
  - payments in the same bill: amounts added;
  - the same line: parts added;
  - spendings on the same bill: amount, payment amount and portion added;
  - ledger records with the same counterpart in the same bill: amounts added;
  - duplicate contacts: the kept user's link (and name) stays;
  - duplicate subscriptions: the kept user's row stays, confirmed if either was.
- **Rows between the two users are deleted**, since they would point a user at itself: their ledger records with each other (so their mutual debt disappears), their contact links to each other, and their subscriptions to each other.
- **Profile:** for display name, Telegram id, Telegram username, phone and email, the kept user's value wins and the old one only fills gaps. The bot is enabled if either had it. The kept user becomes admin if either was.
- **Web sign-in:** if the kept user has neither a login nor a password, it takes the old user's login and password hash. Otherwise the kept user's stay and the old ones are dropped. The login is released before it's reassigned, so the unique index holds.
- **Sessions:** the old user's refresh tokens are deleted. Access tokens already issued to the old user still pass signature checks until they expire (60 min), but they point at a user id that no longer exists.
- **The old user row is deleted.** Nothing references it afterwards.

## Auth (`/auth`, from T1)
```ts
interface SessionUser { id: string; displayName: string; telegramUserName: string | null; isAdmin: boolean }
interface Session { user: SessionUser; accessToken: string; accessTokenExpiresAt: string }
```
`GET auth/config` → `{ telegramBotUsername: string | null }` (anonymous) · `POST auth/telegram` (widget payload) → `Session` · `POST auth/login { login, password }` → `Session` · `POST auth/refresh` (cookie) → `Session` · `POST auth/logout` → 204 · `GET auth/me` → `SessionUser` · `PUT auth/password { login?, currentPassword?, newPassword? }` → 204. Errors here are `{ message }` only. Details are in `web-sign-in.md`.
