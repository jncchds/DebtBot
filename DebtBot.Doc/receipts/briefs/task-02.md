Task T2 (receipt bot) of the DebtBot receipts series. An orchestrator session runs the series one task at a time, with one session per task.

First, read `/mnt/git/DebtBot/DebtBot.Doc/receipts/briefs/common.md` and follow it throughout. To read it before your worktree exists, use `git -C /mnt/git/DebtBot show receipts:DebtBot.Doc/receipts/briefs/common.md`.

- Worktree: `/mnt/git/DebtBot/.claude/worktrees/task-02`
- Branch: `task/02-receipt-bot`, based on the local `receipts` branch.
- Read:
  - `DebtBot.Doc/series-2/receipt-parse.md`, especially "The bot", "Text format" and "Errors and fallback";
  - `DebtBot.Doc/receipts/plan.md` ("User decisions");
  - **`DebtBot.Doc/receipts/reports/task-01.md`**: the receipt service, `ReceiptErrors`, the per-user limiter, the drafts, the fake provider and the warning codes;
  - `DebtBot.Doc/web-ui/api.md` ("Receipts").

## Goal
A receipt photo sent to the bot comes back as the bill in the text format, ready to edit and send back, plus an "Open in the web form" button. It goes through the same receipt service, limits and errors as the web. Discount lines are folded into the other lines first, so the text and the form are valid as they are.

## Scope
1. **Discounts, folded into lines** (user decision, 2026-11-28). Change the normaliser in `DebtBot/Receipts/`, so both the web and the bot get it:
   - a negative line right under an item (an item discount) is subtracted from that item;
   - a negative line with no item above it, or one that reads as a whole-receipt discount (loyalty, coupon, "discount" at the end, after a subtotal), is spread over all lines in proportion to their amounts. Round to the currency's minor units and give the rounding remainder to the largest line, so the lines still sum exactly to the receipt's total;
   - if a line would end at or below zero, don't fold into it: spread that discount over all lines instead;
   - replace the `discountLine` warning with one that says what was done (for example `discountFolded` with the line name and amount, and `discountSpread` with the amount). Update `api.md`'s warning list;
   - no negative line ever reaches the draft, the text or the form. Never change the receipt's total.
2. **Photo handler**, next to the existing message handlers in `DebtBot/Telegram/`:
   - in a private chat: any photo (and an image sent as a document: jpeg, png, webp) runs `IReceiptService.ParseAsync` for that Telegram user;
   - in a group: only when the bot is mentioned in the caption (`@<botname>`); every other photo is ignored, and never sent to the model;
   - the largest photo size is downloaded with `getFile` into memory only, never to disk;
   - an unknown or never-started sender gets the bot's usual "start the bot first" handling, not a parse;
   - when the feature is off (no key), the bot says so in one line (or keeps quiet in groups).
3. **The reply:**
   - the bill in the text format from the draft (`text`), as a message the user can copy, edit and send back to make the bill, as with typed bills today. Use a code block or plain text, whichever round-trips through Telegram's copy best, and say which in the report;
   - a short hint under it: "People and payers are you for now; change them before sending";
   - the warnings worded in one or two short lines (the same wording as the web, so keep it in one place both can use, or in a shared resource);
   - an inline button **"Open in the web form"** to `<web url>/bills/new?receipt=<draft id>`. The bot needs the site's public URL: add a setting (for example `DebtBot:WebUrl`, empty by default). When it's empty, leave the button out. Telegram refuses some URLs in buttons (for example `localhost`), so handle that without failing the reply.
4. **Privacy note:** sent once per chat, the first time a photo is read there, then as a short footer line. Keep the "already shown" state durably per chat (a small table or a field, with a migration made by `dotnet ef`), not in memory.
5. **Limits and errors:** the same per-user limiter as the web (T1's entry point), and the same messages for `notAReceipt`, `unreadable`, `busy`, `timeout`, `upstreamError`, `tooLarge`, `unsupportedType`/`heicNotSupported`, `rateLimited` (with when to try again) and `receiptUnavailable`.
6. **Help:** mention photos in the bot's help or format text, wherever it lists what the bot understands.

## Tests
- The discount folding: an item discount, a whole-receipt discount spread with exact cents, a discount bigger than the line above it, several discounts, a currency with 0 and with 3 minor units; the `supermarket-discount` fixture now gives a draft with no negative lines, and its text round-trips through `ParserService`.
- The bot, with fake Telegram updates and the fake LLM client:
  - a private photo gives the text reply and the web button (and no button when `WebUrl` is empty);
  - a group photo without a mention is ignored and never reaches the receipt service; with a mention it's read;
  - the privacy note comes once per chat, then the footer;
  - each error code gives its message; the limit is shared with the web (a web call plus bot calls hit the same hourly count);
  - the draft from the button's id is returned to the same user by `GET Bills/receipt/{id}` and 404 for another.
- All existing tests still pass.

## Runtime check
Run the stack with `docker compose` and the fake provider. There is no bot token for real Telegram, so drive the photo handler with a fake update the same way the tests do (a small dev-only path, or a test host). Show that the reply text, sent back as a message, makes the bill. Bring it down and clean up afterwards (common.md).

## Docs
- `DebtBot.Doc/web-ui/api.md`: the new warning codes.
- The top-level `README.md`: the `WebUrl` setting, and photos in the bot.
- `DebtBot.Doc/receipts/reports/task-02.md`, including what T3 needs to know (the warning wording, the folded discount warnings, the button URL).

No web UI, no screenshots. Paste the bot's reply text (with a fixture) into the report.

When you're done, follow "Finishing" and "Waking the orchestrator" in common.md.
