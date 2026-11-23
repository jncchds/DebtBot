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
- **Not found and no access** are one view, because the API returns 404 for both.
