# Admin › All bills (approved 2026-11-24)

`admin-bills.html` (query: `theme=dark`, `phone=1|open`, `state=main|open|filtered|filtered-empty|empty`) and the PNGs are the spec for 🛠 Admin › All bills (T8). Admin only.

## URL
`/admin/bills?q=&by=<userId>&with=<userId>&currency=&from=&to=&status=draft|processing|finalized|cancelled&sort=date|description|total|status&order=&page=`
— the Bills URL (`mockups/shell/README.md`) without `mine`, plus `by` (the creator). Defaults left out, as every list.

## Page
Title "All bills", subtitle "Every bill in DebtBot, read-only. Open one to see it in full." (with filters on, a short summary such as "Created by Boris Ivanov."). The shared list pattern, add `adminBillsSpec` to `lib/lists.ts`:
- **Filters:** Search (description contains), **Created by** and Person (both type-to-search over **all users**, `GET Users?search=`, not your contacts), Currency, From, To, Status (pills). No "Created by me".
- **Sort:** Date (default, newest first), Description, Total, Status — as Bills.
- **Table:** Date · Bill (description bold, 🔁 for an exchange; muted "by <creator>" and the accent "Exchange pair" pill) · People (right, the number of people on the bill: creator, payers and line participants) · Total (right; an exchange pair as "100.00 EUR ⇄ 430.00 PLN", like Bills) · Status pill. No balance column, no Accept, no actions. A row opens `/bills/<id>` (admins already have full access there).
- **Cards** (phone): description + status pill; "date · by <creator> · N people" + total.
- **Empty:** 🧾 "No bills yet", "Nobody has made a bill yet."; filtered: 🔍 "No bills match these filters", "Try another creator, person, currency or status.", ✕ Clear filters.

## Sidebar sub-section
Admin (as T4) with All bills highlighted; Merge users carries its badge.

## User decisions (rounds)
- Approved in round 1, as shown.

## API and backend needs (T8)
- `GET Bills/All` (admin) takes the same `[FromQuery] BillListQuery` as `GET Bills` (`search`, `withUserId` = a participant, `currencyCode`, `status`, `dateFrom`, `dateTo`, `sortBy`, `sortOrder`, paging) **plus `creatorId?`**, without `createdByMe`, and returns `PagingResult<BillListItem>` with a new `participantCount` (today it returns full `Bill`s with paging only).
- Draft exchanges: two linked drafts show as one row, as on Bills (by `exchangeBillId`).
