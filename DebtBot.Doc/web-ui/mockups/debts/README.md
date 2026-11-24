# Debts (approved 2026-11-23)

`debts.html` (query: `theme=dark`, `phone=1|open`, `state=main|filtered|empty`) and the PNGs are the spec for 💸 Debts (`/`).

- Two total cards (owed to you / you owe, per currency), then the shared list pattern (`../common/list.js`, described in `../shell/README.md`).
- One row per person and currency, with a 🧾 Bills button (`/bills?with=<id>&currency=<ccy>`).
- **After approval (user, round 4):** the mockup's 🔁 Exchange / settle button was dropped. An exchange swaps currencies between two people; it doesn't settle a balance. The person filter is a type-to-search box rather than a select, because contact lists can be long.
- Filters: person, currency, direction (all / owed to you / you owe). Sort: person, amount, currency.
- Only your own balances: no "people you follow" (user, round 2).
- No New bill or Bills buttons on the page, since they're in the sidebar (user, round 3).
- Sidebar sub-section: Balances, one entry per person, opening `/bills?with=<id>`.
