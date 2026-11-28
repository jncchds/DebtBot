# DebtBot REST API for the web UI

This covers everything the UI tasks (T4–T8) call, as of the end of the series (T9). Sign-in endpoints (`api/v1/auth/*`) are described in `web-sign-in.md`; their shapes are repeated briefly at the end.

## Conventions
- **Base:** `/api/v1`. Every endpoint below needs `Authorization: Bearer <accessToken>` unless marked otherwise: a web session's JWT, or a personal API token `dbt_…` (series 2, see "API tokens" below). No token, or an unknown, expired or revoked one → **401**.
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
| `PUT Bills/{id}` | change, **draft only** (since series-2 T1) | `BillCreation` | `Bill`. Replaces the draft's fields, lines and payments in one transaction, validated like `POST Bills`; an empty `date` keeps the bill's date, and the creator stays the creator (also when an admin saves). The bill's participants list is rebuilt: the creator always stays, people no longer on a line or payment are removed. 409 when the bill is no longer a draft (checked again inside the transaction, so a finalize that won the race isn't overwritten); 400 for an exchange bill (cancel it and make a new one). The web's draft page saves with it. |
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
- **The creator doesn't have to take part** (series-2 T1): a bill whose creator is on no line and no payment is still theirs (in their lists, theirs to edit, finalize and cancel); finalizing gives them no debts and no spendings.
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
  receiptOpenIn: ReceiptOpenIn;    // receipts T1: where a read receipt opens on the web
}
type ReceiptOpenIn = "Form" | "Text"; // default Form
interface ProfileUpdate { displayName: string; phone?: string | null; email?: string | null; receiptOpenIn?: ReceiptOpenIn | null }
```
- `PUT` replaces all three fields. An empty or missing phone or email clears it.
- `receiptOpenIn` (receipts T1) is stored on the user, so it follows them across devices. Left out or null keeps the current value, so the older Profile form doesn't reset it; any other value than `Form` or `Text` is 400.
- Limits: display name 1–100 characters, phone ≤ 32, email ≤ 254 and must contain `@`.
- Telegram fields, login and role are never changed here. Extra fields in the body are ignored.
- Login and password are changed with `PUT auth/password`.
- `GET Users/own` (admin-only, old) is removed.

## Receipts (receipts series, T1, T2)
A receipt photo becomes a bill draft: the New bill form's fields, plus the same bill in the bill text format. Nothing is saved as a bill, and the image and the model's raw answer are never stored. The feature is on only when `DebtBot:Llm:ApiKey` is set (`GET auth/config` → `receiptParse`), or with the fake provider in Development.

| Endpoint | Who | Body / query | Returns |
|---|---|---|---|
| `POST Bills/receipt` | any user; **not a read-only API token** (403, the global scope check) | `multipart/form-data`, one file field `image`: JPEG, PNG or WebP (checked by content), at most 5 MB | `ReceiptDraft`, kept 15 minutes |
| `GET Bills/receipt/{id}` | the draft's owner (a read-only token may read it) | – | `ReceiptDraft`; **404** `{ code: "notFound" }` for anyone else, an unknown id, or after 15 minutes |

```ts
interface ReceiptDraft {
  id: string;                    // for GET Bills/receipt/{id} and the web prefill /bills/new?receipt=<id>
  description: string;           // the merchant (or "Receipt"), plus the date when no time is printed
  date: string | null;           // as printed, treated as UTC; 12:00 when only a date is printed
  currencyCode: string | null;   // ISO 4217, or null when unknown or ambiguous (see warnings)
  totalWithTips: number | null;  // the printed total, plus tips or service charge printed outside it; null if none printed
  lines: { description: string; subtotal: number }[]; // the line totals, always above zero (discounts are folded in)
  warnings: { code: string; params: Record<string, string>; message: string }[]; // amounts in params are invariant strings, e.g. "41.50"
  model: string;                 // the model that answered
  text: string;                  // the bill text format, with you as the stand-in payer and line participant
  expiresAt: string;
}
```
- **`text`:** description; the total and currency (`XXX` when unknown); one payment `<total> <you>`; each line as `<name>`, `<subtotal>`, `1 <you>`. "You" is your Telegram username as stored (usually `@name`), else your Telegram id. It parses on `POST Bills/text/preview` (tested for every fixture). The people and payers have to be changed before saving.
- **Lines** arrive in printed order, at most 100, names on one line and at most 200 characters, `×<quantity>` added when the quantity isn't 1. Amounts are rounded to the currency's minor units and never "fixed".
- **Discounts** (receipts T2): a negative line never reaches the draft, the text or the form, and the lines' sum (so the total) is unchanged:
  - one right under an item is subtracted from that item (`discountFolded`);
  - one with no item above it, or that reads as a whole-receipt discount (loyalty, coupon, card, bonus, promo, voucher… in several languages, or a plain "Discount" after the last item), is spread over all lines in proportion to their amounts, rounded to the currency's minor units, with the remainder on the largest line (`discountSpread`). So is one that would take its item to zero or below;
  - discounts as big as all the lines together are left out (`discountSkipped`, and then `linesTotalMismatch`).
- **`message`** on each warning is its English wording, the same one the bot sends (`ReceiptWarningText`); the web can show it as is.
- **Warnings** (codes and params):

| Code | Params | Meaning |
|---|---|---|
| `linesTotalMismatch` | `linesSum`, `total`, `difference` | the lines don't add up to the total (less tips, service and tax printed inside it) within 1% |
| `currencyAmbiguous` | `symbol` | `$`, `¥` or `kr` fits several currencies and your last bill's currency isn't one of them: `currencyCode` is null |
| `currencyFromLastBill` | `symbol`, `currency` | an ambiguous sign was read as the currency of your latest bill |
| `currencyMissing` | `symbol`? | no currency could be read |
| `totalMissing` | – | no total printed; `totalWithTips` is null |
| `tipsAdded` / `serviceChargeAdded` | `amount` | printed outside the total, so added to `totalWithTips` |
| `discountFolded` | `name`, `item`, `amount` | the discount `name` was taken off the item above it, `item` |
| `discountSpread` | `name`, `amount` | a whole-receipt discount (or one as big as its item) spread over all lines |
| `discountSkipped` | `name`, `amount` | a discount as big as all the lines, left out |
| `negativeAmountIgnored` | `field` | a negative total, tips, service charge or tax, ignored |
| `itemSkipped` | `name` | an item without a readable amount, left out |
| `tooManyItems` | `count`, `kept` | over 100 items; the first 100 are kept |
| `nameTruncated` | `line` | a name cut to 200 characters |
| `dateUnreadable` | `value` | the printed date couldn't be read |

- **Errors** are `{ code, message }` (`message` in English, the same as the bot's):

| Status | `code` | When |
|---|---|---|
| 400 | `noImage` | no `image` file, or not a multipart form |
| 413 | `tooLarge` | the image is over 5 MB, or the request over 5 MB + 64 KB |
| 415 | `unsupportedType` | not JPEG, PNG or WebP |
| 415 | `heicNotSupported` | a HEIC/HEIF photo (the web client converts to JPEG first) |
| 422 | `notAReceipt` | the model says it isn't a receipt |
| 422 | `unreadable` | the answer wasn't valid JSON for the schema, twice |
| 429 | `rateLimited` | also `window: "hour" \| "day"`, `retryAfterSeconds`, and a `Retry-After` header |
| 502 | `upstreamError` | the AI service failed (5xx twice, or an error a retry won't fix) |
| 503 | `receiptUnavailable` | the feature is off (no API key) |
| 503 | `busy` | the AI service is busy or out of quota (its 429 or 402) |
| 504 | `timeout` | no answer in `Llm:TimeoutSeconds` (45 s), twice |

- **Limits:** per user, `Llm:UserHourlyLimit` (10) in a rolling hour and `Llm:UserDailyLimit` (30) in a rolling day, counted together for the web, API tokens and the bot. Every call that reaches the model counts, failed or not; calls refused before that (feature off, bad image, read-only token) don't. The counts are in memory, so a restart resets them. API tokens also keep their per-token limit.
- **Retries:** the server asks the model at most twice: once more on invalid JSON, an answer off the schema, a 5xx or a timeout. Busy (429/402) and not-a-receipt aren't retried.

## API tokens (series 2, T2)
Personal access tokens let a script or an app call the API as the user, with every access rule above applying unchanged.

- **Format:** `dbt_` plus 43 characters (32 random bytes, base64url). Only the SHA-256 hash is stored, and the secret is returned once, by create. Lists show `prefix` (the 8 characters after `dbt_`) as `dbt_a1b2c3d4…`.
- **Header:** `Authorization: Bearer dbt_…`, the same header as a session. The default authentication scheme picks by the value: `dbt_…` goes to the token handler, anything else to JWT.
- **Who you are:** the token's user, with the same claims as their JWT (`nameidentifier`, `name`, `Admin` for admins), read from the database on every request, so a role change, a rename or a merge applies at once. API-token requests also carry `auth_method=pat`, `pat_scope` and `pat_id`.
- **Scopes:** `Read` gets **403** `{ message: "This API token is read-only." }` on anything but GET, HEAD and OPTIONS, for every endpoint. `ReadWrite` can do whatever the user can.
- **JWT only** (a token gets **403** `{ message: "API tokens can't be used here. Sign in to the web app instead." }`, whatever its scope or role): `GET/POST/DELETE profile/tokens`, `PUT auth/password`, `POST auth/refresh`, `POST auth/logout`, `POST Users/{id}/password`, `POST Users/{id}/tokens/revoke-all`. Telegram sign-in (`POST auth/telegram`) takes no bearer, so nothing else is affected. In code: `[JwtOnly]`.
- **Expiry:** 30, 90 or 365 days, or never. **Limit:** 20 active (not expired, not revoked) tokens per user (`DebtBot:ApiTokens:MaxActivePerUser`).
- **Rate limit:** a fixed window per token, 120 requests a minute (`DebtBot:ApiTokens:RateLimitPerMinute`; 0 turns it off). Over it: **429** `{ message }` with `Retry-After`. Session (JWT) traffic isn't limited.
- **Last used:** `lastUsedAt` is written at most once a minute per token.
- **Errors:** 401 for an unknown, expired or revoked token or a deleted user (no body); 403 as above; 429 for the rate limit.

| Endpoint | Access | Request | Response |
|---|---|---|---|
| `GET profile/tokens` | any user, JWT only | – | `ApiToken[]`: active first, then expired, then revoked; newest first within each |
| `POST profile/tokens` | any user, JWT only | `ApiTokenCreation` | `ApiTokenCreated` (the secret, this once); 400 `{ message }` for a bad name, scope or expiry; 409 at 20 active tokens |
| `DELETE profile/tokens/{id}` | own tokens, JWT only | – | 204, also when already revoked (the first `revokedAt` stays); 404 for someone else's or an unknown id |
| `POST Users/{id}/tokens/revoke-all` | **Admin**, JWT only | – | `{ revoked: number }` (active tokens revoked); 404 for an unknown user |

```ts
type ApiTokenScope = 'Read' | 'ReadWrite'
interface ApiToken {
  id: string; name: string; prefix: string; scope: ApiTokenScope;
  createdAt: string; lastUsedAt: string | null; expiresAt: string | null; revokedAt: string | null;
  status: 'Active' | 'Expired' | 'Revoked';
}
interface ApiTokenCreated extends ApiToken { token: string }   // dbt_…
interface ApiTokenCreation { name: string; scope: ApiTokenScope; expiresInDays: 30 | 90 | 365 | null }   // name 1–100 characters
```
- Admins never see other users' tokens: `GET Users` carries only `apiTokenCount`.
- **Swagger** (`/swagger`, served in every environment): "Authorize" takes a session token or a `dbt_…` token the same way.

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
  apiTokenCount: number;            // active API tokens (series 2); never the tokens themselves
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
- **API tokens** (series 2): the old user's tokens are deleted with it, so they stop working at once. They are not moved to the kept user.
- **Sessions:** the old user's refresh tokens are deleted. Access tokens already issued to the old user still pass signature checks until they expire (60 min), but they point at a user id that no longer exists.
- **The old user row is deleted.** Nothing references it afterwards.

## Auth (`/auth`, from T1)
```ts
interface SessionUser { id: string; displayName: string; telegramUserName: string | null; isAdmin: boolean }
interface Session { user: SessionUser; accessToken: string; accessTokenExpiresAt: string }
```
`GET auth/config` → `{ telegramBotUsername: string | null, receiptParse: boolean, receiptModel: string | null }` (anonymous; `receiptParse` and `receiptModel`, the model for the privacy note, since receipts T1) · `POST auth/telegram` (widget payload) → `Session` · `POST auth/login { login, password }` → `Session` · `POST auth/refresh` (cookie) → `Session` · `POST auth/logout` → 204 · `GET auth/me` → `SessionUser` · `PUT auth/password { login?, currentPassword?, newPassword? }` → 204. Errors here are `{ message }` only. Details are in `web-sign-in.md`.
