# New bill (approved 2026-11-24)

`new-bill.html` (query: `theme=dark`, `phone=1`, `state=form|form-addline|form-errors|form-fx|paste|paste-errors|paste-preview`) and the PNGs are the spec for `/bills/new` and `/bills/new/paste`.

## Form (`/bills/new`)
- **Bill:** description (several lines; the first is the title), date and time, bill currency, "Paid in" (payment currency, the bill currency when empty), total with tips (empty = the lines' total). A hint under the total shows "Lines x + tips y".
- **People on this bill:** `PeoplePicker`, the search box on its own line. It starts with you. Anyone on a line or a payment can't be removed (•).
- **Lines:** the added lines with chips and ✕. "＋ Add line" opens the reused `LineEditor` (`/bills/new/add-line`), which offers only the bill's people.
- **Payments:** the reused `PaymentEditor` (`/bills/new/add-payment`), "who paid" from the bill's people only. When the payment currency differs, a note shows the rate the payments imply, both ways ("960.00 PLN paid as 225.00 EUR · 1 EUR = 4.2667 PLN · 1 PLN = 0.2344 EUR").
- **Who owes whom (preview):** worked out like the server's finalize: each share = subtotal × part / parts × payments total / lines total, in the payment currency; the biggest payer is owed by everyone else. A table (person, share, [share in the payment currency], paid, balance) when wide, one block per person on a phone. It says the server does the real calculation.
- **Actions:** "Save as draft" (needs a valid currency) and "✓ Create and finalize" (also needs a line and a payment), plus "Kept in this tab until you create the bill" and Clear form. Errors show under the fields and in a summary above the buttons.
- **Phone (user, round 4):** nothing may be wider than the screen; tables become stacked rows.

## Paste text (`/bills/new/paste`)
- The text box, "👁 Preview" (`POST Bills/text/preview`), and a Format help block from the bot's `/AddBill` format.
- **Preview:** the parsed bill (title, total, paid in, tips, lines, payments). Each name shows who it matches among your contacts (`@boris_m → Boris`) or "new person". Parse errors show in a red block.
- Save as draft / Create and finalize appear once the text is valid.

## Sidebar
Mode: Form, Paste text. Continue a draft: your recent drafts that aren't exchanges.

## Not in scope (user, round 4)
- A third "debts currency" (count the debts in either the bill or the payment currency) was planned once: `origin/feature-currency_selection` has an unmerged "ChargeInPaymentCurrency" commit. Today debts are always in the payment currency.
