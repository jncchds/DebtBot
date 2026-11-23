# Shell (approved 2026-11-23)

The HTML and PNGs here are the spec for the app shell. `sections.html` shows the sidebar on each page.

- **Above the divider:** every page, in this order: 💸 Debts, 🧾 Bills, ➕ New bill, 🔁 Exchange, 📊 Spendings, 👥 Contacts, 👤 Profile, 🛠 Admin. Admin is shown to admins only. New bill stays up here because it's used often.
- **Below the divider:** each page has its own sub-section, in the style of abeat's song list (section title, then entries with a title and a muted subtitle, then "… All x"):

| Page | Sub-section | Built in |
|---|---|---|
| Debts | Balances per person (green "owes you", red "you owe"); each opens `/bills?with=<id>` | T5 |
| Bills / a bill | Drafts, Recent bills (last 5), … All bills | T5 |
| New bill | Mode: Form / Paste text; Continue a draft | T4 (modes), T6 (drafts) |
| Exchange | Waiting to accept, Recent exchanges | T6 |
| Spendings | This month, … All spendings | T7 |
| Contacts | Requests, Recently added | T7 |
| Profile | Details, Login & password, Telegram | T4 |
| Admin | Users, Merge users (badge with the suggestion count), All bills | T4 (links), T8 (badge) |

- **Phone:** a 48 px icon rail. ☰ opens the full sidebar, sub-section included, as an overlay drawer with a backdrop. It closes after navigating.
- **Every view has its own URL** (the user's rule), so Back and Forward, reload, bookmarks and new tabs all work. Filters, paging and tabs live in the URL, not in component state.

| View | URL |
|---|---|
| Debts | `/?with=&currency=&dir=owed\|owe&sort=person\|amount\|currency&order=asc\|desc&page=` |
| Bills | `/bills?q=&with=&currency=&from=&to=&status=draft\|processing\|finalized\|cancelled&mine=1&sort=date\|description\|total\|status&order=&page=` |
| Bill | `/bills/<id>`, `/bills/<id>/add-line`, `/bills/<id>/add-payment` |
| New bill | `/bills/new`, `/bills/new/paste` |
| Exchange | `/exchange` |
| Spendings | `/spendings?page=` |
| Contacts | `/contacts`, `/contacts/requests` |
| Profile | `/profile`, `/profile/password`, `/profile/telegram` |
| Admin | `/admin/users?search=`, `/admin/merge`, `/admin/bills` |
| Login | `/login` (it returns you to the page you came from) |

## List views (Bills, Debts, Spendings, Exchanges)

The user's rule (T5, mockup round 2): every paged list has the same shape and logic. T5 built it; T6 (exchanges) and T7 (spendings) reuse it. The approved look is in `mockups/debts/` and `mockups/bills/`.

**Look.**
- A header card holds the filters and, below them, "Sort by [field]" plus a direction button ("↓ Newest first") and "✕ Clear filters" when any filter is on.
- One page of results follows: a **table** when the list is at least `tableMinWidth` px wide (measured on the list itself), **cards** below that.
- On narrow widths the filters fold under a "🔍 Filters (n)" button and Sort stays visible.
- The pager shows "1–20 of 57" with numbered pages, or "Page 1 of 3" when narrow.
- Empty states: one for "nothing at all" and one for "filters match nothing" (with Clear filters). A page past the end offers the first page.
- No page-level New bill / Bills buttons: they're in the sidebar (user, round 3).

**URL.** Everything is in the query string, and defaults are left out (`/bills` is the default view):
- one parameter per filter, short and lower case (`with`, `currency`, `status=draft`, `mine=1`, `from=2026-11-01`);
- `sort=<field>` and `order=asc|desc` (each field has its own starting order, e.g. date starts newest first);
- `page=<n>`, 1-based;
- invalid values fall back to the defaults;
- any filter or sort change goes back to page 1 and is a new history entry, so Back undoes it.

**Code** (`DebtBot/ClientApp/src`):
- `lib/listState.ts`: `ListSpec` (`filters: { name: { kind: 'text'|'id'|'currency'|'date'|'flag'|'choice', values? } }`, `sorts: { field: 'asc'|'desc' }`, the first sort is the default), `parseListState`, `listSearch`, `withFilter`, `withoutFilters`, `withSort`, `withOrderToggled`, `withPage`, `activeFilterCount`, `pageWindow`. Unit-tested.
- `lib/useListState.ts`: `const [state, setState] = useListState(spec)` reads and writes the URL.
- `lib/lists.ts`: each list's spec and its `xxxQuery(state)` that maps URL state to the API query (`PAGE_SIZE` = 20). Add `spendingsSpec`/`exchangesSpec` here.
- `components/list/ListView.tsx`: `<ListView spec state onChange filters sorts columns card rowKey rowHref? rowHighlight? items total pageSize loading fetching error empty filteredEmpty tableMinWidth? />`. `columns` are `{ key, header, sort?, align?, width?, cell }`; `sorts` are `{ value, label, asc, desc }` (the direction labels).
- `components/list/filters.tsx`: `FilterSelect`, `FilterChoice` (pills, first option `''` = all), `FilterFlag`, `FilterText` (commits after a pause, on Enter or blur), `FilterDate`, `FilterCurrency` (3 letters, suggestions).
- `components/list/Pager.tsx`.

**API convention** (backend, server-side sort and filter):
- paging as before: `pageNumber` (0-based) and `countPerPage`;
- `sortBy=<Field>` (an enum, e.g. `BillSortField`, `DebtSortField`) and `sortOrder=Asc|Desc` (`SortOrder`), all in `DebtBot.Models.Enums/ListSorting.cs`;
- filters as named query parameters, bound to one query class (`[FromQuery] BillListQuery`, `DebtListQuery`), with currency codes normalised to upper case in the controller;
- the repository orders by the field, then by a unique key (the id), so pages are stable;
- decimals are compared and ordered through a `(double)` cast so the SQLite tests behave like Postgres;
- an invalid enum value gives the framework's 400.

