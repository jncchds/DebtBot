# Admin › Users (approved 2026-11-24)

`admin-users.html` (query: `theme=dark`, `phone=1|open`, `state=main|search|none|password|password-taken|role|delete|delete-409`) and the PNGs are the spec for 🛠 Admin › Users (T8). Admin only.

## URLs
- `/admin/users?search=&role=admin|regular&page=` — the list. Defaults left out.
- `/admin/users/<id>/password` (keeps the list's query string) — the Login & password dialog over the list. Closing it goes back to `/admin/users?…`.

## Sidebar sub-section
As T4 built it, **Admin**: 👥 Users "Search, roles, passwords", 🔀 Merge users "Duplicate suggestions" with a `count` badge (the number of merge suggestions; none at 0), 🧾 All bills "read-only".

## Page
Title "Users", subtitle "Everyone DebtBot knows: people who started the bot, and placeholders made from @mentions." The shared list pattern:
- **Filters:** Search (`FilterText`, "Name, @username or login") and Role (`FilterChoice`: All / Admin / Regular).
- **Sort:** Name A–Z only (the API orders by display name). The Sort row shows "Name" with "↑ A–Z"; no other sorts (the user didn't ask for more).
- **Table** (wide):

| Column | Content |
|---|---|
| Name ↑ | `displayName` bold, "(you)" muted for yourself. "🔀 possible duplicate" (accent, small) when the user is in any `GET Users/merge/suggestions` pair; it links to `/admin/merge`. |
| Telegram | `telegramUserName`, below it muted `telegramId` or "no Telegram id" |
| Login & password | `login` (or "—"), below it a muted pill "🔑 password set" when `hasPassword`; on the right a secondary button **✏️ Change** (login or password set) or **＋ Set** (neither) → the dialog |
| Role | accent pill "Admin" or muted "Regular"; on the right a secondary button **Make admin** / **Remove admin**. Disabled for yourself (title "You can't change your own role"). |
| Bot | success pill "started" (`telegramBotEnabled`) or muted pill "placeholder" |
| Bills, Contacts | `billCount`, `contactCount`, right-aligned |
| (last) | 🗑 (danger ghost) only for placeholders that aren't you |

- **Cards** (phone): name + role pill; "@username · id" + bot pill; "login x · 🔑 password set" (or "no login or password") + Change/Set; "N bills · N contacts" + role button + 🗑.
- **Empty:** 🔍 "No users match", "Search looks at display names, Telegram usernames and logins.", ✕ Clear filters.

## Actions
- **Login & password dialog** (`/admin/users/<id>/password`), modal card "🔑 Login & password · <name>" with ✕:
  - Login (prefilled with the current login; help "<Name> has no login yet. 3–64 characters: a–z, 0–9, . _ - @." when none), New password (help "At least 8 characters. Send it to <Name> yourself."), Repeat password; `alert-info` "Saving signs <Name> out of the web app everywhere."; Cancel / **Save**.
  - `POST Users/{id}/password { login?, password }` → 204: close, refetch. 409: `alert-danger` "The login “x” is taken. Pick another one." and the login field marked. 400: the API's message.
- **Make admin / Remove admin:** the row turns into an inline confirmation (accent tint): "Make **Clara Müller** an admin? Admins see every user and bill and can merge users." **Make admin** / Cancel (for removing: "Remove admin from **X**?" **Remove admin** / Cancel). `POST Users/{id}/Role?role=Admin|Regular`.
- **Delete:** the row turns into an inline danger confirmation: "Delete **@name**? Only stray placeholders can be deleted; their own contact list goes with them." **Delete** / Keep. `DELETE Users/{id}`.
  - **409** (has bills, ledger, spendings, or is in someone's contacts): the row shows "**@name** can't be deleted: they have bills or are in someone's contacts. Merge them into the right user instead." with **🔀 Merge…** → `/admin/merge?merge=<id>` (the manual picker with this user as the one to merge away) and Close.

## User decisions (rounds)
- Round 1 → 2: "Sign-in" was a bad caption. The column is **Login & password**, and its Set/Change button lives **in that column**; by the same logic the Make/Remove admin button moved **into the Role column**, leaving only 🗑 in the last column. The dialog says "Login & password" and saves with **Save**.
- Sorting: name only, as the API has it.
- Approved in round 2.

## API
`GET Users?search=&role=&pageNumber=&countPerPage=20`, `GET Users/merge/suggestions` (duplicate marker and the sidebar badge), `POST Users/{id}/password`, `POST Users/{id}/Role?role=`, `DELETE Users/{id}`.

## Backend needs (T8)
- Check that `POST Users/{id}/Role` accepts `Regular` (removing admin) and refuses changing your own role (the UI disables it, but the API should too).
