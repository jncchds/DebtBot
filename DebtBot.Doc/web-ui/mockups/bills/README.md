# Bills (approved 2026-11-23)

`bills.html` (query: `theme=dark`, `phone=1|open`, `state=main|open|filtered-empty|empty`) and the PNGs are the spec for 🧾 Bills (`/bills`).

- The shared list pattern:
  - filters: search, person, currency, from, to, status, created by me;
  - sort: date, description, total, status;
  - a table on wide screens, cards on phones.
- A draft exchange shows as one highlighted row with ✓ Accept (`POST Bills/exchange/finalize`).
- "Your balance" comes from each bill's ledger records.
- No New bill button on the page (user, round 3).
- Sidebar sub-section: Drafts, Recent bills (last 5), … All bills.
