# Profile (approved 2026-11-24)

`profile.html` (query: `theme=dark`, `phone=1|open`, `state=details|details-error|details-saved|password-first|password-change|password-taken|password-short|password-saved|telegram|telegram-off`) and the PNGs are the spec for 👤 Profile (T7).

## URLs and sub-section
The sub-section T4 built stays as it is: **Sections** — 🪪 Details "Name, phone, email" (`/profile`), 🔑 Login & password "Web sign-in" (`/profile/password`), ✈️ Telegram "Account and bot" (`/profile/telegram`). The current one is highlighted. Each is its own page with one card (max width ~34rem).

## Details — `/profile`
- Title "Profile", subtitle "How you appear to people you share bills with."
- Card "🪪 Details": Display name (help: "Contacts see their own name for you; this is the default."), Phone (placeholder "optional"), Email (placeholder "optional"). Save (primary) and Reset (ghost, back to the loaded values).
- `GET profile` to load, `PUT profile { displayName, phone, email }` to save (empty phone or email clears it).
- Validation shows under each field in danger colour with a danger border, checked in the browser first and from the API's 400 `errors`: "Enter a name (1–100 characters).", "An email needs an @.", phone ≤ 32, email ≤ 254.
- After saving: "✓ Saved" (success colour) next to Save; the sidebar's sign-out label ("Sign out (Name)") updates with the new name.

## Login & password — `/profile/password`
Title "Login & password", subtitle "An extra way to sign in to the web app, besides Telegram. Optional." `PUT auth/password { login?, currentPassword?, newPassword? }`.
- **First time** (`login` null and `hasPassword` false): card "🔑 Set a login and password", `alert-info` "You sign in with Telegram now. Add a login and password to also sign in without it.", then Login (help "3–64 characters: a–z, 0–9, . _ - @. Stored in lower case."), Password (help "At least 8 characters."), Repeat password, button "Set login and password". Sends `login` and `newPassword`.
- **Later:** card "🔑 Change login or password" with a success pill "Password set" in the header; Login (prefilled, help "Change it here, or leave it as it is."), Current password (help "Needed for any change."), New password (placeholder "leave empty to keep it"), Repeat new password, "Save changes". Login only: send `login` + `currentPassword`; password: add `newPassword`.
- **Errors:**
  - 409 → `alert-danger` above the fields "The login “anna” is taken. Pick another one." and the login field marked;
  - too short (checked before sending) → "At least 8 characters." under New password; repeat mismatch → "The passwords don't match." under the repeat field;
  - a wrong current password or other 400 → the API's `message` in `alert-danger`.
- After saving: "✓ Saved. Use the new login and password next time." and the form clears the password fields.

## Telegram — `/profile/telegram`
Title "Telegram", subtitle "Your account is your Telegram identity. It can't be changed here."
- Card "✈️ Account": Username (`@name`), Telegram id (plain digits), Role (accent pill "Admin" or muted "Regular"). Read-only, a two-column list (stacked on phones).
- Card "🤖 The bot" with a pill in the header from `telegramBotEnabled`:
  - **Started** (success pill): "You've started @Bot. It messages you about new bills and the notifications you've agreed to." and a secondary "Open @Bot ↗" button.
  - **Not started** (danger pill): "You haven't started @Bot yet, so it can't send you anything, such as notifications about new bills." Steps: 1. Open t.me/Bot in Telegram. 2. Press **Start** (or send `/start`). 3. Come back and reload this page. Primary "Open @Bot ↗".
  - The bot name and link come from `GET auth/config` (`telegramBotUsername`); if it's null, leave out the button and the link and say "the bot".

## User decisions (rounds)
- Approved in round 1, as shown. (In the same round the user moved subscriptions to their own sidebar page; see `../subscriptions/README.md`.)

## Backend needs
None: `GET`/`PUT profile`, `PUT auth/password` and `GET auth/config` cover it.

## Built (T7)
Built as specified. After saving Details, the session is refreshed so "Sign out (Name)" shows the new name. A 409 marks the login field and shows the alert; a wrong current password shows the API's message.
