# Web sign-in

Every web user is a Telegram identity. There are two ways to sign in:

- **Telegram Login Widget:** the user signs in with Telegram, and the user record is found or created the same way the bot does it.
- **Login and password:** optional. A user sets them for themselves, or an admin sets them. There is no open sign-up.

## Sessions
- A session has two parts:
  - a short-lived **access token**, a JWT that lasts 60 minutes by default (`JwtConfiguration:LifeTime`). The SPA keeps it in memory and sends it as `Authorization: Bearer <token>`;
  - a **refresh token** in the httpOnly cookie `debtbot_refresh`, set with `SameSite=Strict` and `Path=/api/v1/auth`. It is `Secure` everywhere except in Development, and lasts 30 days by default (`JwtConfiguration:RefreshTokenLifetimeDays`).
- Only the SHA-256 hash of a refresh token is stored, in the `RefreshTokens` table.
- Every refresh rotates the token. If a token that was already rotated is presented again, the whole chain that came from it is revoked, and that browser has to sign in again.
- When a user changes their own password, their other web sessions are signed out. When an admin sets a user's password, all of that user's web sessions are signed out.

## Endpoints (`api/v1/auth`)
| Endpoint | Auth | Notes |
|---|---|---|
| `GET config` | anonymous | `{ telegramBotUsername }`. It is null when no bot token is configured or Telegram can't be reached, and the UI then hides the widget. |
| `POST telegram` | anonymous, rate-limited | Takes the widget payload with its own names: `id`, `first_name`, `last_name`, `username`, `photo_url`, `auth_date`, `hash`. Returns a session. |
| `POST login` | anonymous, rate-limited | `{ login, password }` → session. A wrong login and a wrong password both get the same 401. After 5 failures in 15 minutes, that login gets 429 for a while. |
| `POST refresh` | the cookie | Returns a new session and rotates the cookie. Returns 401 when there's no valid session. |
| `POST logout` | anonymous | Revokes the cookie's token and clears the cookie. Returns 204. |
| `GET me` | bearer | `{ id, displayName, telegramUserName, isAdmin }` |
| `PUT password` | bearer | `{ login?, currentPassword?, newPassword? }`. See the rules below. Returns 204, 400 with `{ message }`, or 409 when the login is taken. |

A session is `{ user: { id, displayName, telegramUserName, isAdmin }, accessToken, accessTokenExpiresAt }`.

The admin endpoint `POST api/v1/Users/{id}/password` takes `{ login?, password }` and sets any user's login and password without needing the current one. It needs the Admin role.

**Password rules:**
- Logins are 3–64 characters from `a-z 0-9 . _ - @`. They are stored lowercase and must be unique.
- Passwords are at least 8 characters.
- The first time, both a login and a password are needed. After that, any change needs the current password.
- To change only the login, send `login` and `currentPassword` and leave out `newPassword`.

`POST login` and `POST telegram` are limited to 10 requests per minute per client IP. Behind a reverse proxy, all clients share the proxy's IP, because forwarded headers aren't set up yet.

## Settings
| Setting (env var) | Default | |
|---|---|---|
| `DebtBot_DebtBot__JwtConfiguration__Key` | a public value in appsettings.json | **Set it in production.** Use a random string of at least 32 characters, for example from `openssl rand -base64 48`. The app logs a warning while the default is used, and won't start with a key shorter than 32 bytes. |
| `DebtBot_DebtBot__JwtConfiguration__LifeTime` | 60 | Access token lifetime in minutes. |
| `DebtBot_DebtBot__JwtConfiguration__RefreshTokenLifetimeDays` | 30 | Refresh token lifetime in days. |
| `DebtBot_DebtBot__Telegram__BotToken` | – | When it's empty, the bot doesn't poll Telegram, `config` returns no bot username, and `POST telegram` is rejected. |

## Telegram Login Widget setup
1. In BotFather, send `/setdomain`, pick the bot, and enter the domain the web UI is served from, for example `debts.example.com`. The widget only works on that domain.
2. Serve the UI over **HTTPS**. Telegram requires it, and the refresh cookie is `Secure` outside Development.
3. The UI builds the widget from `GET api/v1/auth/config` and posts the widget's callback data to `POST api/v1/auth/telegram`.

The hash check follows https://core.telegram.org/widgets/login#checking-authorization:
- the data-check-string is the fields that are present, as sorted `key=value` lines joined with `\n`;
- the HMAC-SHA256 is keyed with `SHA256(bot token)` and compared in constant time;
- `auth_date` must be less than 24 hours old.

## `/Bearer` in Telegram
`/Bearer` replies with an access token for the sender. Only an admin can get a token for someone else, with `/Bearer @user`. For anyone else, the mention is ignored.
