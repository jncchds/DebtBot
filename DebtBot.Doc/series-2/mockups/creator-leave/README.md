# Creator leaves a bill's people; a draft is the New bill form (approved 2026-11-24, round 4)

`creator-leave.html` (query: `theme=dark`, `phone=1`, `state=newbill-leave|newbill-left|bill-leave|bill-left`) and the PNGs are the spec. `scripts/render.mjs` renders them.

- **Lock rule (user):** the same for everyone, the creator included. A person on the bill shows • (can't be removed) only while they **paid**, are on a **by-parts** line (unequal parts) or are the **only person on a line**. Otherwise they get ×. Removing someone also takes them off every equal-split line, and those lines are split among the people left. There's no warning about it (user, round 2).
- **The creator** can take themselves off like anyone else and add themselves back from the People search ("Anna Petrova (you)"). There's no extra "Add yourself" section (user, round 2). They still own the bill: it stays in their lists, and they edit, finalize and cancel it.
- **A draft opens as the New bill form** (user, round 3): the header (title, Draft, Cancel bill), then Bill, People on this bill, Lines (✎ edit, ✕ remove), Payments (✕), Who owes whom (preview) and the actions. All edits stay local (kept in the tab) until **Save** (`PUT Bills/{id}` replaces the draft) or **✓ Save and finalize**. **Discard changes** goes back to the saved draft. With no changes, the buttons are Save (disabled) and ✓ Finalize.
- **✎ Edit line** opens the line editor in place of the line, prefilled (item, subtotal, split, people and parts). "Save line" changes the form only.
- Each section appears once (user, round 3): the PNGs show the real page, not a sheet of states.

| PNG | What |
|---|---|
| `newbill-leave-*` | New bill: you're only on equal lines, so your chip has ×; Boris paid (•), Dmitry is on a by-parts line (•). |
| `newbill-left-*` | The same after removing yourself: the equal lines go on without you, the preview has no "you", and the search offers you back. |
| `bill-leave-*` | A draft opened as the form, with no changes. |
| `bill-left-*` | The draft after removing yourself, with ✎ Edit line open on Tiramisu; unsaved changes. |
