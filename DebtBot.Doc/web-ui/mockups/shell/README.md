# Shell (approved 2026-11-23)

The HTML and PNGs here are the spec for the app shell. `sections.html` shows the sidebar on each page.

- **Above the divider:** every page, in this order: 💸 Debts, 🧾 Bills, ➕ New bill, 🔁 Exchange, 📊 Spendings, 👥 Contacts, 🔔 Subscriptions, 👤 Profile, 🛠 Admin. Admin is shown to admins only. New bill stays up here because it's used often.
- **Badges:** 🔔 Subscriptions shows a count badge (accent pill) with the number of people waiting for you to confirm (T7; none at 0). Badges are hidden on the collapsed rail and shown in the open sidebar or drawer.
- **Below the divider:** each page has its own sub-section, in the style of abeat's song list (section title, then entries with a title and a muted subtitle, then "… All x"):

| Page | Sub-section | Built in |
|---|---|---|
| Debts | Balances per person (green "owes you", red "you owe"); each opens `/bills?with=<id>`. Then **Following** (below) | T5, T7b |
| Bills / a bill | Drafts, Recent bills (last 5), … All bills. Then **Following** | T5, T7b |
| New bill | Mode: Form / Paste text; Continue a draft. The ➕ New bill entry above the divider goes to Form or Paste text, as Profile's "New bill opens" says (receipts T3) | T4 (modes), T6 (drafts) |
| Exchange | Waiting to accept, Recent exchanges | T6 |
| Spendings | This month (one entry per bill currency, opening that month's spendings in it), … All spendings. Then **Following** | T7, T7b |
| Contacts | Recently added (the 3 newest), … All contacts | T7 |
| Subscriptions | Summary: Requests, Get your bills and debts, Your subscriptions (each scrolls to its card; none highlighted) | T7 |
| Profile | Sections: Details (with the "➕ New bill" card since receipts T3), Login & password, Telegram, API tokens | T4, pages T7, API tokens series 2 T2 |
| Admin | Users, Merge users (badge with the suggestion count, `GET Users/merge/suggestions`; none at 0), All bills | T4 (links), T8 (badge) |

- **Following (T7b, `mockups/following/`)** on Debts, Bills and Spendings: up to 5 people you have a confirmed subscription to (🔔, your name for them, "see <Name>'s debts|bills|spendings", or "showing now" highlighted for the one you're viewing), then "… All subscriptions" → `/subscriptions`. Hidden when you have none. While you view someone (`?of=`), the page's own section shows their data instead ("<Name>'s balances", "<Name>'s drafts" / "<Name>'s recent bills", "<Name> this month"), without its "… All x".
- **Phone:** a 48 px icon rail. ☰ opens the full sidebar, sub-section included, as an overlay drawer with a backdrop. It closes after navigating.
- **Every view has its own URL** (the user's rule), so Back and Forward, reload, bookmarks and new tabs all work. Filters, paging and tabs live in the URL, not in component state.

| View | URL |
|---|---|
| Debts | `/?of=&with=&currency=&dir=owed\|owe&sort=person\|amount\|currency&order=asc\|desc&page=` |
| Bills | `/bills?of=&q=&with=&currency=&from=&to=&status=draft\|processing\|finalized\|cancelled&mine=1&sort=date\|description\|total\|status&order=&page=` |
| Bill | `/bills/<id>`. A draft you can change is the New bill form (series-2 T1, `series-2/mockups/creator-leave/`), with its editors at `/bills/<id>/add-line`, `/bills/<id>/add-payment` and `/bills/<id>/edit-line/<n>` (1-based). Unsaved changes are kept in sessionStorage (per bill, with the saved version they're based on) until Save. The `?people=` list of T5 is gone. |
| New bill | `/bills/new`, `/bills/new/add-line`, `/bills/new/add-payment`, `/bills/new/edit-line/<n>` (series-2 T1; the form itself is kept in sessionStorage), `/bills/new/paste` (`?preview=1` once previewed). Receipts T3 (`receipts/mockups/receipt/`): `/bills/new/receipt` and `/bills/new/paste/receipt` open 🧾 From a receipt above the form or the text (only when `receiptParse` is on; ✕ goes back to the page's own URL); the bot's link `/bills/new?receipt=<id>` fills the form, or moves on to `/bills/new/paste?receipt=<id>` when Profile says Paste text, and `receipt` leaves the URL once it's loaded |
| Exchange | `/exchange?with=<id>&rate=give\|get&draft=<billId>&person=&currency=&status=waiting\|accepted\|cancelled&sort=date\|amount&order=&page=` (`with`, `rate` and `draft` are the form; the rest is the list) |
| Spendings | `/spendings?of=&currency=&from=&to=&cancelled=1&sort=date\|amount&order=asc\|desc&page=` |
| Contacts | `/contacts?q=` (the search; typing replaces the history entry) |
| Subscriptions | `/subscriptions` (`#requests`, `#theirs`, `#yours` scroll to a card); the old `/contacts/requests` redirects here |
| Profile | `/profile`, `/profile/password`, `/profile/telegram`, `/profile/tokens?open=<id>` (the open row's actions), `/profile/tokens/new`, `/profile/tokens/created` (the shown-once secret, in memory only: a reload goes to `/profile/tokens`) (series 2, T2) |
| Admin | `/admin/users?search=&role=admin\|regular&page=&open=<id>` (`open`: the row whose ▾ actions are shown, series 2 T2), `/admin/users/<id>/password` (the Login & password dialog over the list, keeping its query), `/admin/merge?swap=<mergeId>,…&keep=<id>&merge=<id>`, `/admin/bills?q=&by=&with=&currency=&from=&to=&status=&sort=&order=&page=` (T8). Non-admins get the 404 page on all of them. |
| Login | `/login` (it returns you to the page you came from) |

`of=<userId>` (T7b) on Debts, Bills and Spendings shows someone you have a confirmed subscription to, read-only (`mockups/following/`). It isn't a list filter: filters, sort and paging keep it, and changing it (the "Whose" dropdown, Back to mine, a Following entry) drops the filters and goes to page 1, as a new history entry. Links from someone's list carry it on (`/bills?of=&with=&currency=` from their Debts). A bill opened from their list is `/bills/<id>`, read-only unless you're its creator or an admin.

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
- `lib/lists.ts`: each list's spec and its `xxxQuery(state)` that maps URL state to the API query (`PAGE_SIZE` = 20): debts, bills, exchanges (T6) and spendings (T7).
- `components/list/ListView.tsx`: `<ListView spec state onChange filters sorts columns card rowKey rowHref? rowHighlight? items total pageSize loading fetching error empty filteredEmpty tableMinWidth? />`. `columns` are `{ key, header, sort?, align?, width?, cell }`; `sorts` are `{ value, label, asc, desc }` (the direction labels).
- `components/list/filters.tsx`: `FilterPerson` (type to search; use it for people, never a select of all contacts), `FilterSelect`, `FilterChoice` (pills, first option `''` = all), `FilterFlag`, `FilterText` (commits after a pause, on Enter or blur), `FilterDate`, `FilterCurrency` (3 letters, suggestions).
- `components/people/PersonSearch.tsx` (an autocomplete combobox over `usePeople()`) and `PeoplePicker.tsx` (chips, plus the search box on its own line).
- `components/list/Pager.tsx`.

**API convention** (backend, server-side sort and filter):
- paging as before: `pageNumber` (0-based) and `countPerPage`;
- `sortBy=<Field>` (an enum, e.g. `BillSortField`, `DebtSortField`) and `sortOrder=Asc|Desc` (`SortOrder`), all in `DebtBot.Models.Enums/ListSorting.cs`;
- filters as named query parameters, bound to one query class (`[FromQuery] BillListQuery`, `DebtListQuery`), with currency codes normalised to upper case in the controller;
- the repository orders by the field, then by a unique key (the id), so pages are stable;
- decimals are compared and ordered through a `(double)` cast so the SQLite tests behave like Postgres;
- an invalid enum value gives the framework's 400.

