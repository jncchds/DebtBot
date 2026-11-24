# Spendings (approved 2026-11-24)

`spendings.html` (query: `theme=dark`, `phone=1|open`, `state=main|open|filtered|filtered-empty|empty`) and the PNGs are the spec for 📊 Spendings (T7).

## URL
`/spendings?currency=&from=&to=&cancelled=1&sort=date|amount&order=asc|desc&page=`

- Defaults are left out: no currency, no dates, cancelled hidden, `sort=date`, newest first, page 1.
- `date` starts newest first (`desc`); `amount` starts largest first (`desc`).
- As every list: invalid values fall back to the defaults; a filter or sort change goes back to page 1 and is a new history entry (`mockups/shell/README.md`, "List views").

## Page
The shared list pattern (`ListView`, see `mockups/shell/README.md`), with `spendingsSpec` and `spendingsQuery` added to `lib/lists.ts`.

- **Title** "Spendings", subtitle "Your share of every bill you take part in."
- **Filters:**
  - Currency (`FilterCurrency`): the **bill currency** (`currencyCode`).
  - From and To (`FilterDate`): whole days, as on Bills.
  - Show cancelled (`FilterFlag`, `cancelled=1`). Off by default: cancelled spendings are hidden.
- **Sort by:** Date (default, "↓ Newest first"), Amount.
- **Table** (wide):
  - Date (`d MMM`);
  - Bill: the bill description as an accent link to `/bills/<billId>`, and below it, muted, "`<portion>` % of the bill";
  - Amount (right): `amount currencyCode` in bold; when `paymentCurrencyCode` differs from `currencyCode`, a muted line "paid as `paymentAmount paymentCurrencyCode`";
  - a last column with a danger pill "Cancelled" when `isCanceled`. Cancelled rows are faded (opacity .6) and their amount is struck through.
- **Cards** (phone): line 1 the bill link and the amount; line 2 "date · NN % of the bill" and the Cancelled pill; an extra right-aligned "paid as …" line when the payment currency differs.
- **Empty states:**
  - nothing at all: 📊 "No spendings yet", "Once a bill you're on is finalized, your share of it shows up here.";
  - filters match nothing: 🔍 "No spendings match these filters", "Try another currency or date range.", ✕ Clear filters.
- Pager and the phone "🔍 Filters (n)" fold, as every list.

## Sidebar sub-section
- **This month:** one entry per bill currency, 📊, title the total (e.g. "58.00 EUR"), subtitle "November · 3 spendings". Only non-cancelled spendings dated in the current calendar month (local time). When there are none: "Nothing yet", "November · no spendings".
- **… All spendings** → `/spendings`.

## User decisions (rounds)
- Round 1: the share is **always a percentage** of the bill ("33 % of the bill"), never a fraction. Rounded to whole percent from `portion` (0..1).
- Approved as shown: the currency filter and the monthly totals use the **bill currency**; cancelled spendings are **hidden by default** and shown faded with a pill when "Show cancelled" is on; the monthly totals are **in the sidebar only**, not on the page.

## API
- `GET Spendings/Own` (paged, `countPerPage` = 20) — the list.
- `GET Spendings/Own/summary?dateFrom=&dateTo=` — the sidebar totals (new, see below).

## Backend needs (T7)
- `GET Spendings/Own` takes `[FromQuery] SpendingListQuery`: `currencyCode?` (bill currency, any case, normalised to upper), `dateFrom?`, `dateTo?` (`yyyy-mm-dd`, whole days, UTC, as Bills), `includeCancelled?` (default false), `sortBy?` (`SpendingSortField`: `Date` default, `Amount`), `sortOrder?` (`Desc` default), paging. Order by the field, then a unique key; decimals through a `(double)` cast, as the other lists.
- New `GET Spendings/Own/summary?dateFrom=&dateTo=` → `{ currencyCode: string; amount: number; count: number }[]` over non-cancelled spendings, ordered by currency. (Paging all of the month's spendings to the client would also work, but a summary keeps the sidebar to one small call.)
