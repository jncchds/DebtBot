# Contacts (approved 2026-11-24)

`contacts.html` (query: `theme=dark`, `phone=1|open`, `state=main|search|added|add-error|rename|remove|empty|requests|requests-empty`) and the PNGs are the spec for 👥 Contacts (T7).

## URLs
- `/contacts?q=` — the contact list; `q` is the search text (left out when empty). Typing replaces the history entry (no entry per keystroke), like `FilterText`.
- `/contacts/requests` — notifications about bills (subscriptions).

## `/contacts`
Not a paged list: `GET UserContacts/Own` returns every contact, so search, sort (by your name, A–Z) and the count are done in the browser.

1. **Request banner** (`alert-info`), only when someone is waiting (a contact with `theirSubscription = Pending`): "🔔 **Dmitry** wants notifications about your bills." with **Review** → `/contacts/requests`. With several: "**Dmitry** and 2 others want …". Hidden while searching.
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

## `/contacts/requests`
Title "Notifications about bills", subtitle "The bot can tell someone in Telegram when you're on a new bill, and tell you about theirs. Both sides have to agree." Three cards, each a list of rows (name, muted @username, actions on the right):

| Card | Who | Actions |
|---|---|---|
| **Asking for your bills** — confirm or decline | `theirSubscription = Pending` | ✓ Confirm (`subscription/confirm`), Decline (`subscription/decline`) |
| **Get your bills** — people you confirmed | `theirSubscription = Confirmed` | pill Confirmed, Stop (`subscription/decline`) |
| **Your subscriptions** — whose bills you get | `mySubscription` `Confirmed` / `Pending` | Confirmed: pill "🔔 You get their bills" + **Stop**; Pending: pill "⏳ Waiting for <name>" + **Withdraw** (both the new `subscription/cancel`) |

Empty cards say "No requests." / "Nobody yet." / "None. Use 🔔 Ask for their bills on Contacts."

## Sidebar sub-section
- **Requests:** one entry per contact with `theirSubscription = Pending` (🔔, name, "wants your bills"), opening `/contacts/requests`; none: ✅ "No requests", "Nobody is waiting".
- **Recently added:** the 3 newest contacts by `addedAt` (👤, your name, "@username · 20 Nov"; "today · never started the bot" for a fresh placeholder); none: "Nobody yet", "Add someone by @username".
- **… All contacts** → `/contacts`.

## User decisions (rounds)
- Before round 1: **subscriptions stay in the web UI** (request, confirm, decline), with `/contacts/requests` and the Requests sub-section. This is about the bot's Telegram notifications; the web UI still never shows other people's debts (T5's "no following").
- Round 1 → 2: **add an "added on" date** to contacts (for Recently added), and **add Stop / Withdraw** for your own subscriptions (new endpoint).
- Approved in round 2.

## API
`GET UserContacts/Own`, `POST UserContacts/Own/by-telegram?username=`, `PUT UserContacts/Own/{id}`, `DELETE UserContacts/Own/{id}`, `POST UserContacts/subscription/request|confirm|decline?contactId=`, and the new `subscription/cancel` below. After any change, refetch contacts (the sidebar uses the same query).

## Backend needs (T7)
- **`Contact.addedAt`** (`string`, ISO UTC): when the contact link was created. Needs a column on the user-contact link (a migration via `dotnet ef`; existing rows get the migration time or null — if null, sort them last and show no date).
- **New `POST UserContacts/subscription/cancel?contactId=`**: removes *your own* subscription to `contactId`, pending or confirmed (the mirror of `decline`, which removes theirs to you). 200; 404 `{ message }` when there is none; acts for the signed-in user only.
