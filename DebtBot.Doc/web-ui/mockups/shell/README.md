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
| Debts | `/` |
| Bills | `/bills?with=&status=&currency=&page=` |
| Bill | `/bills/<id>` |
| New bill | `/bills/new`, `/bills/new/paste` |
| Exchange | `/exchange` |
| Spendings | `/spendings?page=` |
| Contacts | `/contacts`, `/contacts/requests` |
| Profile | `/profile`, `/profile/password`, `/profile/telegram` |
| Admin | `/admin/users?search=`, `/admin/merge`, `/admin/bills` |
| Login | `/login` (it returns you to the page you came from) |
