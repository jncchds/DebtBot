# Subscriptions (approved 2026-11-24)

`subscriptions.html` (query: `theme=dark`, `phone=1|open`, `state=main|empty`) and the PNGs are the spec for 🔔 Subscriptions (T7).

A subscription is the bot's Telegram notification about someone's new bills and debts. Both sides have to agree: one asks, the other confirms. The web UI still never shows other people's debts (T5's "no following"); this page only manages who the bot notifies.

## Sidebar and URL
- **A new top-level page** in the sidebar, right after 👥 Contacts: **🔔 Subscriptions** → `/subscriptions`. Nav order is now: Debts, Bills, New bill, Exchange, Spendings, Contacts, **Subscriptions**, Profile, Admin.
- **Badge:** the nav button shows a count badge (`count` style: accent pill, as planned for Admin's merge badge) with the number of contacts whose `theirSubscription = Pending`. No badge at 0. Hidden on the collapsed phone rail, shown in the open drawer.
- No query parameters: the page is small and not paged.

## Page
Title "Subscriptions", subtitle "The bot can tell someone in Telegram about your new bills and debts, and tell you about theirs. Both sides have to agree." Then three cards, each a list of rows (name bold, muted @username, actions on the right). All built from `GET UserContacts/Own` (`mySubscription`, `theirSubscription`).

| Card (title — muted hint) | Who | Right side |
|---|---|---|
| **Requests** — people asking for your bills and debts | `theirSubscription = Pending` | ✓ Confirm (primary, `subscription/confirm`), Decline (secondary, `subscription/decline`) |
| **Get your bills and debts** — people you confirmed | `theirSubscription = Confirmed` | success pill "Confirmed", Stop (ghost, `subscription/decline`) |
| **Your subscriptions** — whose bills and debts you get | `mySubscription` `Confirmed` or `Pending` | Confirmed: success pill "🔔 You get theirs" + Stop; Pending: muted pill "⏳ Waiting for <name>" + Withdraw (both the new `subscription/cancel`) |

- Empty cards: "No requests." / "Nobody yet." / "None yet. Use 🔔 Ask for their bills on Contacts."
- Asking is done from the Contacts list ("🔔 Ask for their bills", `subscription/request`).
- After any action, refetch contacts (the badge, Contacts and this page share the query). Errors from the API show as `alert-danger` above the cards.

## Sidebar sub-section
**Summary**, three plain entries (none highlighted, so it doesn't look like one request is selected), each scrolling to its card:
- 🔔 Requests — "1 waiting for you" / "nobody is waiting";
- 📤 Get your bills and debts — "1 person" / "nobody";
- 📥 Your subscriptions — "1 confirmed · 1 waiting" / "none".

## User decisions (rounds)
- During Contacts: subscriptions belong in the web UI; add Stop / Withdraw for your own subscriptions.
- Profile round: the Requests entries in the Contacts sub-section looked like one selected request, and the page was unreachable when there were no requests. So subscriptions get **their own sidebar button after Contacts**, called **Subscriptions**, about **bills and debts**. Approved in its first round.

## Backend needs (T7)
- New `POST UserContacts/subscription/cancel?contactId=` (see `../contacts/README.md`).
- Check the wording against what the bot really sends to subscribers (bills, and debts via `/Debts` with subscriptions); adjust the subtitle if it differs.
