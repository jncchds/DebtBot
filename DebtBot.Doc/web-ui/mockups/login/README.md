# Login (approved 2026-11-23)

- **Card:** 💸 DebtBot, then Login and Password fields and Sign in. Below "or" is the Telegram Login Widget, followed by a hint.
- **Telegram widget:** the blue button is a stand-in for Telegram's own iframe, whose colours are Telegram's. When `GET auth/config` returns no `telegramBotUsername`, the widget and "or" are hidden, and the hint changes to "ask an admin".
- **Errors:** any failure shows one message in the danger style: "Sign-in failed. Check your login and password, or try again later."
- **Theme:** the page uses the last theme chosen in this browser, and system when none was chosen. It's applied before the first paint. The page has no theme toggle of its own.
