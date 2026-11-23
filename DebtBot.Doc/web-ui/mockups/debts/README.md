# Debts (approved 2026-11-23)

`debts.html` (query: `theme=dark`, `phone=1|open`, `state=main|filtered|empty`) and the PNGs are the spec for 💸 Debts (`/`).

- Two total cards (owed to you / you owe, per currency), then the shared list pattern (`../common/list.js`, described in `../shell/README.md`).
- One row per person and currency, with the buttons 🧾 Bills (`/bills?with=<id>&currency=<ccy>`) and 🔁 Exchange / settle (`/exchange?with=<id>`).
- Filters: person, currency, direction (all / owed to you / you owe). Sort: person, amount, currency.
- Only your own balances: no "people you follow" (user, round 2).
- No New bill or Bills buttons on the page, since they're in the sidebar (user, round 3).
- Sidebar sub-section: Balances, one entry per person, opening `/bills?with=<id>`.
