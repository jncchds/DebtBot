# Subscriptions on Debts, Bills and Spendings (approved 2026-11-24)

`following.html` (query: `view=debts|bills|spendings`, `state=mine|mine-open|boris|boris-open`, `theme=dark`, `phone=1|open`) and the PNGs are the spec for viewing someone you're subscribed to on 💸 Debts, 🧾 Bills and 📊 Spendings.

**This reverses T5's "no following" (user, 2026-11-24):** "since we've added subscriptions, we need to implement them into previous tasks also … mock up debts, spendings and bills for subscriptions". Everything else on those pages stays as approved in `../debts`, `../bills` and `../spendings`.

## URL
One extra query parameter on all three lists: `of=<userId>` (left out = mine).
- `/?of=<id>&…`, `/bills?of=<id>&…`, `/spendings?of=<id>&…`; the other filters, sort and page work the same.
- Changing `of` resets the filters and goes to page 1 (a new history entry).
- An `of` you aren't (or no longer) subscribed to: the API answers 403/404; show the normal error box with "Back to mine".

## "Whose" dropdown
Under the title (and its subtitle), before any totals or the list header card: label "Whose" and a dropdown (~13rem).
- Closed: "👤 Mine" or "🔔 <name>"; when it's someone else, the field has the `on` style (accent border and text).
- Open: a search box on top (focus), "👤 Mine", a small section title "Subscribed to", then everyone you're subscribed to (`mySubscription = Confirmed` in `GET UserContacts/Own`), A–Z by your name for them; the list scrolls. The search filters the names.
- With no subscriptions the dropdown is hidden (the page looks exactly as before).
- Phones: the same dropdown, menu as wide as needed but never wider than the screen.

## Viewing someone (`of` set)
- **Title** "<Name>'s debts" / "<Name>'s bills" / "<Name>'s spendings"; subtitles "<Name>'s balances with each person, per currency." / "Bills <Name> takes part in." / "<Name>'s share of every bill <Name> takes part in."
- **Banner** (`alert-info`) below the dropdown: "🔔 You're seeing **<Name>'s debts|bills|spendings** because you're subscribed to <Name>. Read-only." and a secondary **Back to mine** (drops `of`).
- **Debts:** totals "Owed to <Name>" / "<Name> owes"; rows are <Name>'s balances with each person (people's own display names, since they aren't your contacts; you show as "<your name> (you)"); words "owes <Name>" / "<Name> owes"; the direction filter reads All / Owed to <Name> / <Name> owes; 🧾 Bills → `/bills?of=<id>&with=<personId>&currency=<ccy>`.
- **Bills:** bills <Name> takes part in; the balance column is "<Name>'s balance" ("owed to <Name>" / "<Name> owes"); "Created by me" becomes "Created by <Name>"; bills you're also on get a muted pill "you're on it" after "by …". No ✓ Accept, no actions. A row opens `/bills/<id>`, which is read-only for you unless you're the creator or an admin (then it behaves as today).
- **Spendings:** <Name>'s share of each bill, the same columns; bill links as on Bills.
- Filters, sort, paging, empty states and phone cards are the same as on your own pages (the person filter searches <Name>'s counterparts).

## Sidebar sub-section
- Each page's own section shows the viewed person's data: "<Name>'s balances", "<Name>'s drafts" / "<Name>'s recent bills", "<Name> this month".
- A new section **Following** at the bottom on all three pages: up to 5 people you're subscribed to (🔔, name, "see <Name>'s debts|bills|spendings"; the viewed one highlighted with "showing now"), then "… All subscriptions" → `/subscriptions`. Hidden when you have no subscriptions.

## User decisions (rounds)
- Round 1: a "Whose" switch (pills) with a read-only view of someone you're subscribed to, on Debts, Bills and Spendings.
- Round 1 → 2: **a dropdown instead of pills**, because the list may be big. Approved in round 2.

## Backend needs (new follow-up, not T7's original scope)
- `GET Debts/Own`, `GET Bills`, `GET Spendings/Own` (and `Spendings/Own/summary`) accept `ofUserId?`. Allowed only when the caller has a **confirmed** subscription to that user (or is that user; admins may be allowed too). Otherwise 403. The result is exactly what that user would get without `ofUserId` (Debts: their own balances, as the web-only own-debts query of T5).
- **Bill read access for subscribers:** `IBillService.HasAccess` (or a read-only variant used by `GET Bills/{id}` and `GET Bills/lines/{lineId}`) also allows a confirmed subscriber of any participant. Changes stay creator-or-admin.
- Names in someone else's lists: return each counterpart's own display name (the viewer has no contact name for them), and flag the viewer (`user.id == me`) in the UI.
- Tests: access allowed with a confirmed subscription, denied with pending/none, and nothing changes for own lists.

## Built (T7b)
- **API:** `ofUserId` on `GET Debts/Own`, `GET Bills`, `GET Spendings/Own` and `GET Spendings/Own/summary` (`api.md`). Your own id is the same as leaving it out; pending, none or the other direction give 403; admins aren't special. Bill reads (`GET Bills/{id}`, `GET Bills/lines/{lineId}`) are open to confirmed subscribers of anyone on the bill (creator, payer, line participant or its participants list); every change still needs creator or admin, and a subscriber gets 404 there.
- **Names:** the viewed person is shown by your name for them (the dropdown, title, banner, Following). On their Debts, people come back from the API under their own display names (not their contact names, nor yours), and you show as "<your name> (you)". On their Bills, "by …" uses the same names as your own Bills ("by you", your contact names, else their own name), as in the mockup.
- **"you're on it"** comes from a new `participantIds` on `GET Bills` items (the bill's participants list), so it's right for drafts too.
- **The person filter** on their Bills searches their counterparts (from their balances), and the currency suggestions are theirs.
- **The dropdown's list scrolls** (about 18rem) instead of the mockup's "… 3 more" line, as the spec says. Enter in its search box picks the first match; Esc or a click outside closes it.
- **No access:** a 403 or 404 for `of` shows the title of your own page, the dropdown (if you have subscriptions) and the error box with Back to mine; nothing else.
- **Sidebar:** while viewing someone, "… All bills" and "… All spendings" are left out of the page's own section, as in the mockup; "… All subscriptions" ends Following.
