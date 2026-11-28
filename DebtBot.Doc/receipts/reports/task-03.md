# T3 report: receipt UI

- **Branch:** `task/03-receipt-ui`, based on the local `receipts` at `de886af`.
- **Commits:**
  - `20d5b10`: the approved mockups (`receipts/mockups/receipt/`);
  - `d1d0672`: the UI: the panel, the bot's link, Profile's "New bill opens", tests;
  - `14f1641`: the receipt's printed time kept as printed; the runtime check script;
  - the last commit: a long name wrapping in the Paste text preview, docs (URL map, `api.md`) and this report.

## Mockups (approved 2026-11-28, round 3)
`receipts/mockups/receipt/` (`receipt.html`, 28 PNGs, `README.md`). The user's decisions:
- **Round 1:** no Form/Text switch in the panel. A receipt read on New bill fills the form, and one read on Paste text fills the text. The choice only matters where there's no page yet.
- **Round 2:** the Profile setting also picks where ➕ New bill opens. It's now **"New bill opens: Form / Paste text"**, in a "➕ New bill" card under Details. It decides where ➕ New bill in the sidebar and the bot's link `/bills/new?receipt=<id>` take you.
- Kept as I proposed (the user didn't object): Replace keeps the people on the bill and replaces the description, date, currency, total, lines and **payments** (the old payments are removed); the Profile card has its own Save, like Details.

## What was built (`DebtBot/ClientApp/src`)
- **`lib/receipt.ts`** (pure, unit-tested):
  - `draftToForm`: the receipt's description, date, currency and total (`totalWithTips` typed into the total), with every line ticked for everyone on the bill (you when nobody is). The payment currency and the payments are cleared.
  - `formHasContent`: whether to ask "Replace the form?". People alone don't count.
  - `printedTime`: the server keeps the printed time marked UTC (T1), so the form shows the UTC fields as they are: 19:42 on the receipt is 19:42 in the form, in any time zone. Saving then stores the user's local time, as for a typed bill.
  - **Errors:**
    - `toFailure` maps the API's `{code, message}`, a status without a body (413, 504, 5xx), an abort (`cancelled`), and a broken connection. A broken connection counts as `tooLarge` when more than 5 MB was sent, else `network`.
    - `isRetryable` retries once on a 5xx, `timeout` or `network`, and never on `busy` or `receiptUnavailable` (both 503) or on a 4xx.
    - `failureText` shows the API's `message` (the bot's wording) when it sent one, else the client's wording for the code (`locales/en/receipt.json`, the same sentences as `ReceiptErrors`). For `rateLimited` it adds "You can read the next one in N minutes/hours" (rounded up, as the bot words it).
  - **Shrinking:** `shrink(file, encoder)` re-encodes as JPEG 0.8 (then 0.7 and 0.6 while it's over 1 MB), at most 1600 px on the longest side (`fitWithin`), named `<stem>.jpg` (`jpegName`). The fake provider picks its fixture by that name.
    - HEIC is converted when the browser can draw it. Otherwise it gets `heicNotSupported`.
    - Any other file the browser can't draw is sent as it is, and the server judges it.
    - `canvasEncoder` uses `createImageBitmap` (with the EXIF orientation applied) and a canvas.
  - `readReceipt`: shrink, upload, one retry.
  - `loadLinkedReceipt` returns the draft, or `notFound` on a 404.
  - Helpers: `withoutReceiptParam`, `newBillPath` (Profile → sidebar), and `fromReceiptTarget` (the button shows only with `receiptParse` and while the panel is closed).
- **`api/receipts.ts`:** `uploadReceipt` (multipart `image`, with an abort signal) and `getReceipt`.
- **`components/receipt/Receipt.tsx`:**
  - `FromReceiptButton` (the title row's "🧾 From a receipt").
  - `ReceiptPanel`:
    - a file input (`accept="image/*"`) behind "Choose a photo" ("Take or choose a photo" on phones), and a drop zone;
    - what it fills, and the privacy note with `receiptModel`, every time;
    - the busy state (name, size, "up to 45 seconds", Cancel);
    - the error, with the picker kept for another try;
    - the replace question.
  - `ReplaceQuestion` ("Replace the form?" / "Replace the text?", with a summary like "FreshMart 2026-11-21, 4 lines, 12.06 EUR").
  - `ReceiptWarnings`: `warning.message`, each with ✕.
- **`components/receipt/useReceiptLink.ts`:** handles `?receipt=<id>`.
  - On the form, with "Paste text" in Profile, it moves on to `/bills/new/paste?receipt=<id>`.
  - Otherwise it loads the draft, takes `receipt` out of the URL (replace), and hands the draft to the page, which fills it or asks first.
  - A 404 shows "This receipt has expired or isn't yours." and leaves the page alone (`ReceiptLinkNotice`).
- **`pages/NewBill.tsx`:**
  - `/bills/new/receipt` shows the panel above the form.
  - A draft fills the form (or asks first), and the page goes back to `/bills/new`.
  - The warnings show at the top of the Lines card (new `BillForm` prop `linesNotice`). They're kept in sessionStorage with the form and cleared by Clear form and by creating the bill.
- **`pages/PasteBill.tsx`:**
  - `/bills/new/paste/receipt` shows the panel.
  - A draft puts its `text` in the box and previews it (`?preview=1`), with "Filled from a receipt. People and payers are you for now; change them before saving." and the warnings above the text.
  - It asks "Replace the text?" when the box holds other text.
  - The hint goes away when the box is emptied.
- **Panel URLs without the feature:** when `receiptParse` is off, the panel URLs redirect to the page's own URL.
- **Sidebar:**
  - ➕ New bill links to `/bills/new` or `/bills/new/paste` from `profile.receiptOpenIn`, and is active on both.
  - Mode: Form is active on `/bills/new/receipt`, and Paste text on `/bills/new/paste/receipt`.
  - The Details entry's subtitle is "Name, contacts, New bill".
- **Profile** (`pages/Profile.tsx`): the "➕ New bill" card under Details, with two radio choices and Save. `newBillOpensRequest` sends the saved name, phone and email unchanged, plus `receiptOpenIn`; `PUT profile` needs the name and replaces the other two. It's shown always, not only when receipts are on (user, round 2).
- `PageTitle` gained `actionsEnd` (the button at the right end of the title row).
- Paste text's preview: a long `@username` in the "Paid" row wrapped into the amount on phones. It now wraps on its own line. This was there before T3, but receipt text with a long username shows it.

## Pages and URLs
`/bills/new/receipt` and `/bills/new/paste/receipt` are new. `/bills/new?receipt=<id>` and `/bills/new/paste?receipt=<id>` load a draft. They're in `web-ui/mockups/shell/README.md`. `web-ui/api.md` says how the web uses `receiptOpenIn` now. No endpoints changed; no backend change.

## Tests
- **vitest: 165 passed** (16 files; 129 before, plus 35 in `lib/receipt.test.ts` and 1 in `lib/profile.test.ts`). They cover:
  - mapping the draft into the form (people kept, payments cleared, no currency or total, no date);
  - the printed time, also run with `TZ=Asia/Tokyo` and `TZ=America/Los_Angeles`;
  - when to ask before replacing, and dismissing warnings;
  - every error code with and without the API's message, and the rate limit's "try again";
  - a status without a body; a broken connection as `tooLarge` or `network`;
  - what's retried; the one retry (twice in all, then the error; a retry that succeeds), and no retry for `busy`, `notAReceipt` and `rateLimited`; cancel without a retry;
  - shrinking: the size, the name stem, JPEG 0.8 and lower qualities while over 1 MB, HEIC converted or refused, other files sent as they are;
  - the link: the draft, the 404 wording, the URL clean-up;
  - the sidebar path from the setting; the button hidden with `receiptParse` false or the panel open;
  - the Profile request.
- Typecheck, oxlint and the locale check are clean (`locales OK`).
- **.NET: 425 passed, 0 failed** (unchanged; no backend change).

## Runtime check (docker compose, built from this branch)
- **The stack:** the e2e stack (`docker-compose.yml`, the override, `web-ui/e2e/compose.e2e.yml`) plus a throwaway file with `Llm__Provider=Fake` and higher per-user limits. Fresh database.
- **`reports/task-03/scripts/t3check.mjs`: 29/29 passed** in headless Chrome. The uploads are 1200×1600 PNGs drawn in the page and named after the fixtures:
  - `auth/config` has `receiptParse` and `fake/receipt-fixtures`; the button opens `/bills/new/receipt` with the privacy note and no switch;
  - **the form result** (`supermarket-discount.png`): the upload went as `supermarket-discount.jpg`, `image/jpeg`, 106 KB. The form has FreshMart, EUR, 12.06, 2026-11-21 12:00 and the four folded lines; the warning shows above the lines and can be dismissed; the panel closes;
  - **the replace question** (`cafe-eur.png`): Keep leaves the form as it was, and Replace fills in the cafe;
  - **errors** (`busy.png`, then `not-a-receipt.png`): one message each, the form kept. **Cancel** while reading goes back to the picker;
  - **the receipt's time** 19:42 is 19:42 in the form;
  - **saving a draft from the prefilled form** made a Draft bill: Corner Café, EUR, 39.50, 4 lines;
  - **the text result** (`/bills/new/paste/receipt`): `/bills/new/paste?preview=1` with the text, the hint, the warning, and "Looks good";
  - **the bot's link:** a draft from `POST Bills/receipt` (`tips-outside`), opened as `/bills/new?receipt=<id>`:
    - it fills the empty form and the URL is left as `/bills/new`;
    - a reload asks nothing;
    - opened again on the filled form, it asks first;
    - an unknown id shows "This receipt has expired or isn't yours." and leaves the form alone, with a clean URL;
  - **Profile:** "Paste text" saved (`receiptOpenIn: Text`, the name unchanged), ➕ New bill's link became `/bills/new/paste`, and the bot's link then opened the text (`decimal-comma`);
  - no console errors.
- **Screenshots:** the panel, the prefilled form with a warning, the prefilled text and the Profile setting, at 1280 and 390 px, light and dark (sent to the user). The Paste text ones were retaken after the wrap fix.
- **Clean-up:** the stack is down with its volumes; the image is removed; dangling images and the build cache are pruned; `dotnet build-server shutdown` was run.

## Decisions
- **No `capture` attribute** on the file input. The brief asks for `capture` on phones, but it makes phones open only the camera, which takes away choosing an existing photo. `accept="image/*"` already offers the camera and the gallery on iOS and Android, which matches the approved "Take or choose a photo".
- **The printed time** is shown as printed (the UTC fields of the draft's date), not converted to the browser's zone. T1 stores it "as printed, treated as UTC", so converting would shift it by the user's offset.
- **The total** is typed into "Total with tips" whenever the receipt has one, even when it equals the lines' sum, so the hint reads "Lines 12.06 + tips 0.00".
- **Retries:** `network` is retried once too (the brief calls it "a retryable error"); `busy` (503) isn't, since the server already treats it as not worth a retry.
- **`notFound`** always uses the client's wording ("This receipt has expired or isn't yours."), as the brief words it, not the API's message.
- **The Paste text page asks "Replace the text?"** when the box holds other text. The brief only names the form, but overwriting typed text without asking would lose work.
- The Profile setting is stored in the existing `receiptOpenIn` (`Form | Text`); the UI calls `Text` "Paste text".

## Left open
- **For T4:**
  - the e2e suite (`web-ui/e2e`) could take `t3check.mjs`'s steps;
  - the real model's dates and times on real receipts.
- The brief's "shown only when `receiptParse` is on" for the Profile setting no longer applies (user, round 2). The README and `api.md` say so.
