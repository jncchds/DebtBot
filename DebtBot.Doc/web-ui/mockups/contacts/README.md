# Contacts (approved 2026-11-24)

`contacts.html` (query: `theme=dark`, `phone=1|open`, `state=main|search|added|add-error|rename|remove|empty`) and the PNGs are the spec for 👥 Contacts (T7).

## URLs
- `/contacts?q=` — the contact list; `q` is the search text (left out when empty). Typing replaces the history entry (no entry per keystroke), like `FilterText`.
- Subscriptions moved to their own page, `/subscriptions` (see `../subscriptions/README.md`). There is no `/contacts/requests`.

## `/contacts`
Not a paged list: `GET UserContacts/Own` returns every contact, so search, sort (by your name, A–Z) and the count are done in the browser.

1. **Request banner** (`alert-info`), only when someone is waiting (a contact with `theirSubscription = Pending`): "🔔 **Dmitry** wants notifications about your bills and debts." with **Review** → `/subscriptions`. With several: "**Dmitry** and 2 others want …". Hidden while searching.
2. **Add card:** "Add by Telegram username", an `@username` input and **＋ Add** (`POST UserContacts/Own/by-telegram?username=`; the `@` is optional, the API strips it). Hint below: "Works for people who haven't started the bot yet, too."
   - Success: the input clears, the new row is highlighted (`hl`) in the list, and an `alert-info` replaces the hint: for a placeholder (`telegramBotEnabled = false`) "✅ **@name** added. They haven't started the bot yet, so they're a placeholder for now: you can put them on bills, and they'll see everything once they start the bot."; otherwise "✅ **Name** added."
   - An existing contact comes back unchanged: say "**Name** is already in your contacts." and highlight the row.
   - 400 shows the API's message in `alert-danger` under the input (the mockup's text: "That isn't a Telegram username: use 5–32 letters, digits or _.").
3. **List card:** a search row ("🔍 Search by name or @username", matches your name, their own name and the username, ignoring case and `@`; "N contacts" or "N of M" on the right, hidden on phones), then:
   - **Table** (wide): Name ↑ (your `displayName` bold; their `userDisplayName` muted below when it differs from both your name and the username) · Telegram (`telegramUserName`, plus a muted pill "never started the bot" for placeholders) · Notifications · actions (✏️ Rename, 🗑 Remove, ghost buttons).
   - **Cards** (phone): name with ✏️ 🗑 icon buttons; muted "their name · @username"; then the notification pill/button, or the "never started the bot" pill for placeholders.
   - **Notifications cell** from `mySubscription`: `Confirmed` → success pill "🔔 You get their bills"; `Pending` → muted pill "⏳ Asked, waiting"; `None` → secondary button "🔔 Ask for their bills" (`POST UserContacts/subscription/request?contactId=`); placeholders → "—" (they can't confirm).
   - **Rename:** inline on the row: an input with Save / Cancel (`PUT UserContacts/Own/{id}`, 1–100 characters; Enter saves, Esc cancels).
   - **Remove:** the row turns into an inline danger confirmation (the same pattern as Cancel bill): "Remove **Clara** from your contacts? Bills and debts with them stay; sharing a new bill adds them back." **Remove** (danger) / **Keep** (`DELETE UserContacts/Own/{id}`).
   - Search with no match: 🔍 "No contacts match", "Try part of a name or a @username."
4. **Empty** (no contacts): the add card, then 👥 "No contacts yet", "Add someone by their Telegram username above. People you share a bill with are added automatically."

## Sidebar sub-section
- **Recently added:** the 3 newest contacts by `addedAt` (👤, your name, "@username · 20 Nov"; "today · never started the bot" for a fresh placeholder); none: "Nobody yet", "Add someone by @username".
- **… All contacts** → `/contacts`.

## User decisions (rounds)
- Before round 1: **subscriptions stay in the web UI** (request, confirm, decline), with `/contacts/requests` and the Requests sub-section. This is about the bot's Telegram notifications; the web UI still never shows other people's debts (T5's "no following").
- Round 1 → 2: **add an "added on" date** to contacts (for Recently added), and **add Stop / Withdraw** for your own subscriptions (new endpoint).
- Round 2 → 3 (with the Profile round): subscriptions looked like one selected request in the Contacts sub-section, and couldn't be reached with no requests. So **🔔 Subscriptions became its own sidebar page** after Contacts (`/subscriptions`), with a count badge; Contacts lost its Requests sub-section. Wording: notifications about **bills and debts**.
- Approved in round 2; the move approved with Subscriptions.

## API
`GET UserContacts/Own`, `POST UserContacts/Own/by-telegram?username=`, `PUT UserContacts/Own/{id}`, `DELETE UserContacts/Own/{id}`, `POST UserContacts/subscription/request?contactId=` (the Ask button). Confirm, decline and cancel are on `/subscriptions`. After any change, refetch contacts (the sidebar uses the same query).

## Backend needs (T7)
- **`Contact.addedAt`** (`string`, ISO UTC): when the contact link was created. Needs a column on the user-contact link (a migration via `dotnet ef`; existing rows get the migration time or null — if null, sort them last and show no date).
- **New `POST UserContacts/subscription/cancel?contactId=`** (used on `/subscriptions`): removes *your own* subscription to `contactId`, pending or confirmed (the mirror of `decline`, which removes theirs to you). 200; 404 `{ message }` when there is none; acts for the signed-in user only.

## Built (T7)
- `addedAt` is null for contacts that existed before the migration (`ContactAddedAt`): their real date is unknown, and the migration time would make every old contact look added today. They sort last in Recently added and show no date.
- `by-telegram` matches the stored `@name` ignoring case, with or without the `@` (tests).
- A Recently added entry opens `/contacts?q=<name>`. The username is checked in the browser before sending (the mockup's message); other 400s show the API's message.
- The table shows from 1024 px of window width, cards below.
