# T2 report: receipt bot

- **Branch:** `task/02-receipt-bot`, based on the local `receipts` at `4390563`.
- **Commits:**
  - `bc60917`: discount folding, warning wording, the photo handler, the chat state table and migration, the link button, tests and docs;
  - `2e9746a`: the Development-only bot update path and the runtime check script;
  - the last commit: the script fix after the first run, and this report.

## What was built
### Discounts folded into lines (`DebtBot/Receipts/ReceiptDiscounts.cs`)
The normaliser calls `ReceiptDiscounts.Fold` after naming the lines, so the web, the bot and the text all get it. No negative line reaches the draft, the text or the form, and the lines' sum (so the receipt's total) never changes.
- **An item discount** (a negative line right under an item) is subtracted from that item: `discountFolded` (`name`, `item`, `amount`).
- **A whole-receipt discount** is spread over all lines in proportion to their amounts, rounded to the currency's minor units, with the rounding remainder on the largest line (the first of equals): `discountSpread` (`name`, `amount`). A discount counts as whole-receipt when:
  - there's no item above it; or
  - its name has a whole-receipt word: loyal, coupon, voucher, promo, member, club, card, bonus, point, reward, cashback, gift, (sub)total, order, receipt, bill, check, and the Russian, German, French, Italian and Spanish ones (`лояль`, `карт`, `бонус`, `купон`, `итог`, `чек`, `Gutschein`, `fidélité`, `cupón`…); or
  - it's a plain "Discount" (in several languages, with at most a number or a %) after the last item, when there are at least two items.
- **A discount that would take its item to zero or below** is spread instead.
- **Spreading keeps every line at one minor unit or more.** Discounts as big as all the lines together can't be spread. They're left out with `discountSkipped` (`name`, `amount`), and `linesTotalMismatch` then says so. Nothing is "fixed".
- `discountLine` is gone. Several discounts are handled in order: item ones first, then all the whole-receipt ones spread together, with one warning each.
- **The `supermarket-discount` fixture** (loyalty −1.00 at the end) now gives 1.19, 2.30, 5.16 and 3.41 (12.06), and its text round-trips through `ParserService`. The round-trip theory for every fixture now also asserts that every line is above zero.

### Warning wording, in one place
`ReceiptWarningText.Describe(warning, currency)` words every code in English. The normaliser stores the result on each warning as **`message`** (a new field on `ReceiptWarning`, so it's in the API's `warnings[]`). The bot sends that text, and **T3 can show `warning.message` as is**, so the web and the bot say the same thing. Amounts get the currency code when it's known. Examples:
- `The lines add up to 41.50 EUR, but the total is 45.00 EUR (off by 3.50 EUR).`
- `The discount “Loyalty discount” (1.00 EUR) was spread over all lines.`
- `The discount “Discount” (2.00 EUR) was taken off “Pizza”.`
- `“$” read as CAD, the currency of your last bill.`

The error messages were already shared: `ReceiptErrors.Message`, which is the API's `message` too.

### The photo handler (`DebtBot/Telegram/ReceiptPhotoHandler.cs`)
`UpdateHandler.BotOnMessageReceived` calls it after `Actualize`, before command parsing. It returns false when the message isn't its business, and then the usual handling runs.
- **What it reads:**
  - in a private chat, any photo, or a document whose MIME type is `image/*`. JPEG, PNG and WebP are read; the service checks the content, so a HEIC file gets the HEIC hint and a GIF gets `unsupportedType`;
  - in a group or supergroup, only when the caption mentions the bot: a `mention` entity equal to `@<bot username>` (any case, the username from `TelegramBotInfoProvider`), or a `text_mention` of the bot's id. Every other group photo returns false at once: nothing is downloaded, nothing reaches the model, nothing is sent;
  - an edited message with a photo is ignored (an edited caption would otherwise be read and paid for again).
- **The feature is off** (no key): a private chat gets `Reading receipts isn't set up on this server.`, and a group gets nothing.
- **"Start the bot first":** the bot had no such message, so I added one. A sender with no user row, or with `TelegramBotEnabled` false (never started, or `/Stop`), gets `Start the bot first: open a private chat with the bot and send /start, then send the photo again.` Nothing is downloaded.
- **The download:**
  - the largest photo size (by width × height) is fetched with `getFile` and `DownloadFile` into a `MemoryStream`, never to disk;
  - a size over `Llm:MaxImageBytes`, either from the update or from `getFile`, gets `tooLarge` without a download;
  - a failed download gets "The photo couldn't be downloaded from Telegram. Try sending it again." The log has only the exception type.
- **The service:** `IReceiptService.ParseAsync(user.Id, new ReceiptInput(bytes, fileName ?? caption))`. That's T1's entry point, with the same limiter, drafts and errors. The file name or caption is only the fake client's fixture hint; `OpenRouterClient` ignores it.

### The reply (HTML)
The reply answers the photo (`reply_to_message_id`, and the topic in forum groups). It has:
1. **The bill in a `<pre>` block:** `/AddBill`, a newline, then the draft's `text`. I chose a code block over plain text because Telegram's clients show a **Copy** button on `<pre>` blocks and copy exactly the block, newlines included. Plain text would mean copying the hint and the note too. The text is HTML-escaped (`&`, `<`, `>`).
2. `People and payers are you for now; change them before sending.`
3. The warnings, at most two lines: `⚠️ <message>`, and on the second line `(+N more)` when there are more than two.
4. The privacy line, in italics (see below).
- **The button** is **"Open in the web form"**, a URL button to `<WebUrl>/bills/new?receipt=<draft id>`.
  - **`DebtBot:WebUrl`** (env `DebtBot_DebtBot__WebUrl`) is new, empty by default. With it empty there's no button.
  - **Links Telegram refuses** are left out up front: not http(s), `localhost`, a loopback IP, or a host name without a dot (`http://debtbot:8080`). LAN IPs and paths are kept: `https://example.com/debts/` gives `https://example.com/debts/bills/new?receipt=…`.
  - **As a backstop**, `SendOrUpdateTelegramMessage` catches a 400 from Telegram on a message with link buttons and resends it without them, so the reply is never lost.
  - `InlineButtonRecord` gained an optional `url`.
- **Too long:** a bill that doesn't fit Telegram's 4096 characters (about 100 lines) is replaced by "The bill is too long for one Telegram message. Open it in the web form, or split it into several bills.", with the button. It couldn't be sent back as one message anyway.
- **Errors** get `ReceiptErrors.Message`, the web's text, for every code. `rateLimited` adds when to try again: `… You can read the next one in 42 minutes.` (rounded up; minutes under an hour, else hours).

### The privacy note, once per chat
- **The state** is a new table, **`TelegramChatStates`** (`ChatId` bigint PK, `ReceiptNoteShownAt` timestamp, nullable). The migration `20261128143732_TelegramChatStates` was made with dotnet-ef 11.
- **The first reply after the model read a photo** in a chat carries the full note, the spec's wording with the model name: *"The receipt is sent to an AI service (OpenRouter, model fake/receipt-fixtures) to read it. Don't send receipts with personal data you don't want to share."* Later replies carry the footer *"Read by an AI service (OpenRouter, fake/receipt-fixtures)."*
- **What uses it up:** success, and the errors that come after the model was called (`notAReceipt`, `unreadable`, `busy`, `timeout`, `upstreamError`). Errors before the model (wrong type, too large, rate limited, off) don't use it up and don't show it.
- **A race** (two photos in the same chat at once) is handled: the unique key fails, and that reply shows the footer.

### Help
`/Format` ends with "Or send a photo of a receipt (in a group, mention the bot in the caption): the bot answers with this text filled in, ready to edit and send back." It's the only place that lists what the bot understands.

### Development-only update path (`Controllers/DevTelegramController.cs`)
`POST api/v1/dev/telegram/update` runs a Bot API update through the real `UpdateHandler` without Telegram. It's admin-only, 404 outside Development, and hidden from Swagger.
- **The input:** a multipart form with `update` (the JSON, as Telegram sends it) and an optional `file`.
- **What's faked:** a small in-memory bot client serves `getFile` and the download from `file`. The Telegram messages that the update handler and the photo handler send are returned as `replies` instead of sent, along with the `downloads`.
- **What's real:** commands like `/AddBill` and `/start` run with their real services and publish to the bus as usual. Sending to Telegram fails without a token, which is logged.
- **For T4:** use this path for the bot run with the real key.

## Endpoints and pages
- **New:** `POST api/v1/dev/telegram/update` (Development only).
- **Changed:** `warnings[]` in `ReceiptDraft` gains `message`, and the three discount codes replace `discountLine` (`api.md`, "Receipts").
- No pages.

## Tests
**.NET: 425 passed, 0 failed** (360 before, plus 65).
- **`ReceiptDiscountTests`** (17):
  - an item discount;
  - a whole-receipt coupon spread with exact cents (9.66, 9.67, 9.67);
  - whole-receipt words under an item, in 5 languages;
  - a discount before any item; a plain "Discount 10%" after the last item;
  - a discount bigger than its item, and one equal to it;
  - several discounts (one folded, two spread);
  - JPY (0 minor units, with the remainder) and KWD (3);
  - a discount as big as all the lines (skipped, with a mismatch); a 0.01 line kept at 0.01;
  - a folded draft round-trips through `ParserService`.
- **`ReceiptNormalizerTests`:** the supermarket fixture is folded, with its warning's `message`. Every fixture's text round-trips with lines above zero.
- **`ReceiptBotTests`** (46, fake Telegram updates and the fake LLM on the real receipt service, limiter and draft store):
  - a private photo: the `<pre>` text, the hint, the note, the button URL, the largest size downloaded;
  - the button's draft is 200 for the sender on `GET Bills/receipt/{id}` and 404 for another user;
  - no button without `WebUrl`; 10 URL cases (localhost, 127.0.0.1, a bare host, ftp, garbage, slashes, a path, a LAN IP);
  - the supermarket reply has no `-1.00` and has the spread warning;
  - **the copied text, pasted back** with its entities, goes through `TelegramService.ProcessMessage` and `ParseBill`, and `BillService.Add` makes the bill (12.06, the folded lines, Anna as creator);
  - an image document is read; a PDF isn't ours;
  - groups: no mention, a plain `Group`, or another user mentioned are ignored, with no download or model call; `@Debt_Bot` (any case) and a `text_mention` are read;
  - the note once per chat, then the footer, another chat gets its own, kept in the DB; a GIF before it doesn't use it up;
  - never started and unknown senders get "Start the bot first"; with no key, one line in private and silence in a group; an edited caption is ignored;
  - `notAReceipt`, `unreadable`, `busy`, `timeout` and `upstreamError` get the web's messages, plus the note; every `ReceiptError` has its message;
  - too large from the update (not downloaded) and from `getFile`; HEIC and GIF documents;
  - **the limit shared with the web:** a limit of 3 an hour; one web POST and two bot photos pass, the third bot photo is refused with "…in 1 hour" and never reaches the model; Boris is unaffected;
  - the retry durations; two warning lines with "+N more"; HTML escaping; the 4096 limit;
  - the `UpdateHandler` routes a photo to the handler and skips command parsing.
- **`TelegramBotApiTests`** (2): a link button is sent as a URL; a refused link button is dropped and the message resent.

## Runtime check (docker compose, built from this branch)
- **The stack:** the e2e stack (`docker-compose.yml`, the override, `web-ui/e2e/compose.e2e.yml`) plus a throwaway file with `Llm__Provider=Fake`, `UserHourlyLimit=6`, `UserDailyLimit=20` and `WebUrl=https://debts.example.com`.
- **`reports/task-02/scripts/t2check.mjs`: 24/24 passed** on a fresh database. The first run had 21/24: two checks compared Postgres's `12.0600` as text, and I miscounted the limit (the ignored group photo correctly didn't count). I fixed the script and reran it on a fresh volume:
  - the `TelegramChatStates` migration is applied; the dev path is 403 for a non-admin;
  - `/start` as a fake update turns the bot on for the sender;
  - a private photo (`supermarket-discount`): one reply to the photo, the largest size downloaded, the `<pre>` bill, no negative line, the hint, the warning, the privacy note, and the button. The draft is 200 for the sender and 404 for Boris;
  - the second photo gets the footer, and the chat's state is in the table;
  - a group photo without a mention gets nothing and no download; with a `text_mention` of the bot it's read, with that chat's own note;
  - **the reply's bill, sent back as a text update with its entities, made the bill:** `FreshMart 2026-11-21`, 12.06 EUR, created by the sender, lines 1.19, 2.30, 3.41 and 5.16;
  - `notAReceipt` gets the web's message;
  - **the shared limit:** 4 bot reads, then a web read (the 5th), then a bot read (the 6th). The 7th in the bot gets "…You can read the next one in 1 hour." and the web gets 429;
  - a sender who never started the bot gets "Start the bot first", with no download.
- **The app log** has no receipt content or image data (0 matches for the fixture's items or base64).
- **Clean-up:** the stack is down with its volumes; the image (`jncchds/debtbot:latest`) is removed; dangling images and the build cache (2.4 GB) are pruned; `dotnet build-server shutdown` was run. Disk: 6.5 GB free afterwards (4.4 GB at the lowest).
- **Not run for real:** sending to Telegram (no token). The URL button's refusal handling is unit-tested against a fake 400, not against Telegram.

### The bot's reply (the `supermarket-discount` fixture, from the runtime check)
```html
<pre>/AddBill
FreshMart 2026-11-21

12.06
EUR

12.06 @anna_t2

Milk 1L
1.19
1 @anna_t2

Rye bread
2.30
1 @anna_t2

Gouda cheese
5.16
1 @anna_t2

Apples ×1.235
3.41
1 @anna_t2</pre>
People and payers are you for now; change them before sending.

⚠️ The discount “Loyalty discount” (1.00 EUR) was spread over all lines.

<i>The receipt is sent to an AI service (OpenRouter, model fake/receipt-fixtures) to read it. Don't send receipts with personal data you don't want to share.</i>
```
Button: `[[{"text":"Open in the web form","url":"https://debts.example.com/bills/new?receipt=<draft id>"}]]`

## Decisions
- **A code block (`<pre>`), not plain text,** for the bill: Telegram's Copy button copies only the block, exactly.
- **`/AddBill` is the block's first line,** so the copied text is a complete message that makes the bill. In a group, plain `/AddBill` works as today.
- **The warning wording lives on the server** (`ReceiptWarningText`, sent as `message`), rather than in a shared resource file, so the web shows exactly the bot's sentences.
- **"Whole-receipt" heuristics:** a word list, plus a plain "Discount" after the last item. The model's prompt already asks it to fold item discounts itself, so a negative line at the end is more likely the receipt's.
- **Spread discounts are applied together** (one proportional pass over their sum), with one warning each.
- **"Start the bot first"** is new wording; there was no existing message. It uses `TelegramBotEnabled`.
- **Edited messages with photos are ignored**, so they aren't read and charged twice.
- **The privacy note is used up only by a reply after the model saw the photo,** so a refused GIF doesn't hide it.
- **The dev update path** lives in the app, gated by Development and admin, so T4 can drive the real-key bot run the same way.

## For T3 (UI)
- **Show `warning.message`** for warnings. The codes stay for logic: `discountFolded`, `discountSpread` and `discountSkipped` replace `discountLine`, with params in `api.md`.
- **Lines are always above zero** now, so the form can take a draft as it is.
- **The bot's button opens `/bills/new?receipt=<id>`.** The page reads `GET api/v1/Bills/receipt/{id}`, owner only, for 15 minutes; after that, 404 `notFound`. Someone opening the link on another account gets the 404, so word it ("This receipt has expired or isn't yours.").
- **The error messages** are the API's `message` (the same as the bot's).

## Left open
- **For T4:**
  - drive the bot with the real key through `POST api/v1/dev/telegram/update`, with a real photo as `file` and a photo update whose caption is empty;
  - check how the real model names discounts against the word list.
- **`WebUrl` in prod** is the user's to set (`DebtBot_DebtBot__WebUrl`). It isn't in `docker-compose.yml`.
- **The username mention** in groups needs `getMe` to have worked (`TelegramBotInfoProvider`). If Telegram is unreachable at that moment, `@bot` mentions are missed until the retry (5 minutes); a `text_mention` still works.
