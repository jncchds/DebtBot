# Receipt UI (approved 2026-11-28, round 3)

`receipt.html` (query: `theme=dark`, `phone=1`, `state=form-receipt|form-busy|paste-error|form-replace|form-filled|paste-filled|profile`) and the PNGs are the spec. `scripts/render.mjs` renders them. The data is the `supermarket-discount` fixture after the server's discount folding (4 lines, 12.06 EUR).

## Decisions (user)
- **No Form/Text switch in the panel** (round 1): a receipt read on New bill fills the form, one read on Paste text fills the text. The choice only matters where there's no page yet.
- **The Profile setting is "New bill opens: Form / Paste text"** (round 2, round 3). It decides where ➕ New bill in the sidebar goes (`/bills/new` or `/bills/new/paste`; the URLs themselves don't change) and where the bot's link `/bills/new?receipt=<id>` ends up (Paste text with the receipt's text when the setting is Paste text). It's stored as `receiptOpenIn` (`GET/PUT profile`), and it's shown always, not only when receipts are on.
- **Replace** keeps the people on the bill (the new lines get all of them ticked) and replaces the description, date, currency, total, lines and payments (the old payments are removed).
- The Profile card has its own Save, like Details.

## Views
| State | URL | What |
|---|---|---|
| `form-receipt` | `/bills/new/receipt` | the "🧾 From a receipt" panel above the form: picker and drop zone (`accept="image/*"`, `capture` on phones), what it fills, the privacy note with `receiptModel` every time |
| `form-busy` | `/bills/new/receipt` | reading (up to 45 seconds), the file name and size, Cancel |
| `paste-error` | `/bills/new/paste/receipt` | the API's `message` in red; the picker stays to try again; the page is untouched |
| `form-replace` | `/bills/new/receipt` | the form has content: "Replace the form?" Replace / Keep my form |
| `form-filled` | `/bills/new` | prefilled; the warnings (`warning.message`) above the lines, each dismissable; "🧾 From a receipt" in the title row |
| `paste-filled` | `/bills/new/paste?preview=1` | the draft's `text`, "Filled from a receipt. People and payers are you for now; change them before saving.", the warnings, the usual preview |
| `profile` | `/profile` | the "➕ New bill" card under Details |

The panel and the button show only when `GET auth/config` has `receiptParse: true`. ✕ closes the panel (back to the page's own URL).
