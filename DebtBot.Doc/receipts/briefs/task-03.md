Task T3 (receipt UI) of the DebtBot receipts series. An orchestrator session runs the series one task at a time, with one session per task.

First, read `/mnt/git/DebtBot/DebtBot.Doc/receipts/briefs/common.md` and follow it throughout. To read it before your worktree exists, use `git -C /mnt/git/DebtBot show receipts:DebtBot.Doc/receipts/briefs/common.md`.

- Worktree: `/mnt/git/DebtBot/.claude/worktrees/task-03`
- Branch: `task/03-receipt-ui`, based on the local `receipts` branch.
- Read:
  - `DebtBot.Doc/series-2/receipt-parse.md`, especially "Flow (web)", "Text format" and "Errors and fallback";
  - `DebtBot.Doc/receipts/plan.md` ("User decisions");
  - **`DebtBot.Doc/receipts/reports/task-01.md` and `task-02.md`**: the endpoints, error codes, `warning.message`, the fake provider (the fixture is picked by the upload's file name), the draft URL, the discount folding;
  - `DebtBot.Doc/web-ui/api.md` ("Receipts"), `DebtBot.Doc/web-ui/mockups/shell/README.md` (URL map, shared components);
  - the approved mockups `DebtBot.Doc/web-ui/mockups/new-bill/` and `profile/`, and `DebtBot.Doc/series-2/mockups/creator-leave/` (the current New bill form) and `api-tokens/` (the Profile cards).

## Goal
On New bill and Paste text, a user can turn a receipt photo into a prefilled bill, in the form or in the text format as they choose, and the bot's "Open in the web form" link opens the prefilled form. Profile has the "Receipts open in" setting.

## Scope
1. **"From a receipt"** on New bill (`/bills/new`) and Paste text (`/bills/new/paste`), shown only when `GET auth/config` says `receiptParse: true`.
2. **The receipt panel**, with its own URL (common.md: every view has one; for example `/bills/new/receipt`, with the Form/Text choice in the query). It has:
   - a photo picker (`accept="image/*"` with `capture` on phones) and a drop zone;
   - the "Open in: Form / Text" switch, preset from the Profile setting `receiptOpenIn`, changing only this receipt;
   - the privacy note, shown every time (wording in the spec), with `receiptModel` from `auth/config` as "model X";
   - a busy state while the model reads (it can take up to 45 seconds), with Cancel.
3. **Client-side shrinking:** the longest side 1600 px, JPEG about 0.8, so it's under about 1 MB. Keep the original file name stem (the fake provider picks its fixture by it). HEIC: convert if the browser can draw it, else show the `heicNotSupported` message.
4. **Errors:** one clear message, the API's `message`, and the form is kept as it was, for every code in `api.md` (`notAReceipt`, `unreadable`, `busy`, `timeout`, `upstreamError`, `tooLarge`, `unsupportedType`, `heicNotSupported`, `rateLimited` with when to try again, `receiptUnavailable`). A network error during the upload counts as `tooLarge` when the file was large (T1 report), else as a retryable error. The client retries once on 5xx or timeout, never more. Manual entry is always there.
5. **Form result:** fills the New bill form (description, date, currency, total with tips, lines). If the form already has content, ask "Replace the form?" first. Lines arrive with every bill person ticked, as `LineEditor` does now. Warnings (`warning.message`) show above the lines, dismissable.
6. **Text result:** Paste text gets the draft's `text` and shows its usual preview, plus the hint "People and payers are you for now; change them before saving."
7. **The bot's link** `/bills/new?receipt=<id>`: loads `GET Bills/receipt/{id}` and fills the form (with the same replace question). A 404 shows "This receipt has expired or isn't yours." and leaves the form alone. After the fill, the `receipt` parameter leaves the URL so a reload doesn't fill it again.
8. **Profile:** a "Receipts open in: Form / Text" setting, saved through `PUT profile` `receiptOpenIn`, in the existing Profile pattern. Shown only when `receiptParse` is on.
9. Update `mockups/shell/README.md` with the new URLs.

## Mockups first
Before building, in one round if you can: the "From a receipt" button on New bill and on Paste text, the panel (idle with the note, busy), an error state, the prefilled form with a warning and the replace question, the prefilled text, and the Profile setting. Light and dark, 1280 and 390 px, from the approved mockups and with realistic data (for example the `supermarket-discount` fixture's folded lines). Show the real page, each section once (the user's rule from series 2), not a sheet of states. Get the user's approval, commit under `DebtBot.Doc/receipts/mockups/<view>/`, then build.

## Tests
vitest: mapping the draft into the form, the replace question, warnings, each error code's message, the one retry, the shrink keeping the file name, the `?receipt=` load (ok and 404) and the URL clean-up, the Profile setting, and the button hidden when `receiptParse` is false. All existing vitest and .NET tests still pass.

## Runtime check
Run the stack with `docker compose` and the fake provider (`DebtBot_DebtBot__Llm__Provider=Fake`, Development). In headless Chrome, upload fixture images named after the fixtures: the form result, the text result, an error (`busy`, `not-a-receipt`), the replace question, a draft link from `POST Bills/receipt` opened as `/bills/new?receipt=<id>`, and saving a draft from a prefilled form. Bring it down and clean up afterwards (common.md).

## Docs
- `mockups/shell/README.md` (URLs) and `DebtBot.Doc/web-ui/api.md` if the client relies on anything new.
- `DebtBot.Doc/receipts/reports/task-03.md`.

Screenshots: after building, send the real views at 1280 and 390 px, light and dark, with SendUserFile (display: render): the panel, the prefilled form with a warning, the prefilled text and the Profile setting.

When you're done, follow "Finishing" and "Waking the orchestrator" in common.md.
