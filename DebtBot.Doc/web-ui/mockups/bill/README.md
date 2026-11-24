# Bill detail (approved 2026-11-23)

`bill.html` (query: `theme=dark`, `phone=1|open`, `state=processed|draft|add-line|add-payment|processing|exchange|notfound`) and the PNGs are the spec for a bill (`/bills/<id>`).

- **Layout:**
  - header: title, status pill, date and creator, actions;
  - facts: total, paid in, tips, people;
  - lines with participant chips (equal or ×parts);
  - payments;
  - who owes whom (the ledger).
- **Draft, for the creator or an admin:**
  - Finalize, disabled with a hint until the bill can be finalized;
  - Cancel bill;
  - ＋ Add line (`/bills/<id>/add-line`) and ＋ Add payment (`/bills/<id>/add-payment`). The editors are reusable for T6's New bill form.
- **After Finalize:** a "Finalizing…" banner while the page polls.
- **Exchange half:** a banner with the other half and ✓ Accept exchange. No line or payment editing.
- **After approval (user, round 4):**
  - a draft has a "People on this bill" section above the lines: chips, plus a search box on its own line that autocompletes from your contacts and yourself;
  - the line editor ticks only those people (all of them to start with), and "who paid" lists only them, so a long contact list never becomes a long checkbox list;
  - people not yet on a line or a payment are kept in the URL (`?people=<id>,<id>`).
- **Not found and no access** are one view, because the API returns 404 for both.
