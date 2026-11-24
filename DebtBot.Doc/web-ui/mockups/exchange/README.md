# Exchange (approved 2026-11-24)

`exchange.html` (query: `theme=dark`, `phone=1`, `state=main|rate|rate-first|created|errors`) and the PNGs are the spec for `/exchange`.

An exchange is a currency exchange between two people: a forward and a backward bill, so the amounts swap currencies. It is never called "settling". It is created as two **drafts** and accepted with a separate step (`POST Bills/exchange/finalize`), like the bot's `/ExchangeDebt` and its Finalize button.

## New exchange
- **With:** the person search (`PersonSearch`), one person; `?with=<userId>` prefills it.
- **You give** and **<Name> gives you**, each an amount and a currency, with ⇄ Swap between them.
- **Amount | Rate (user, rounds 2–3):** either side can be typed as a rate instead of an amount ("1 EUR = 4.30 PLN", ⇅ Flip types it the other way round). Only one side at a time: choosing Rate on one side switches the other to Amount. The worked-out amount is shown ("That's 430.00 PLN", rounded to 0.01) and sent, because the API takes two amounts. Which side is a rate is in the URL (`rate=give|get`).
- **Exchange rate:** always both directions, in full, never cut off or shortened (user, round 4).
- Optional description (defaults to "Exchange").
- Create makes the two drafts.

## After Create
A banner with who gives whom what, the rate both ways, links to both bills, ✓ Accept and Cancel. Its URL is `/exchange?draft=<billId>`, so a reload keeps it. The same wording and component are used for the draft exchange on the Bills list and the Bill page.

## Exchanges list
The shared list pattern (`ListView`). Filters: person, currency, status (Waiting to accept / Accepted / Cancelled). Sort: date, amount (the amount you give). Columns: date, with, you give, you get, rate (both ways), status, Accept (for its creator or an admin; otherwise "<name> accepts"). Phone cards show both rates too.

## Sidebar
Waiting to accept, Recent exchanges. The amounts wrap instead of being cut off.
