# Admin › Merge users (approved 2026-11-24)

`admin-merge.html` (query: `theme=dark`, `phone=1|open`, `state=main|confirm|swapped|done|manual-typing|manual|manual-confirm|empty`) and the PNGs are the spec for 🛠 Admin › Merge users (T8). Admin only.

## URL
`/admin/merge?swap=<mergeId>,<mergeId>&keep=<id>&merge=<id>`
- `swap` lists the suggestions shown swapped (by the suggested merge user's id), so a swap survives reload and Back undoes it.
- `keep` and `merge` fill the manual picker (Users › Delete's 409 "🔀 Merge…" opens `/admin/merge?merge=<id>`).
- The open confirmation is not in the URL (like the other inline confirmations).

## Sidebar
Sub-section **Admin** (as T4): the 🔀 Merge users entry carries a `count` badge with the number of suggestions (`GET Users/merge/suggestions`, length); no badge at 0. The same query feeds the "possible duplicate" marker on Users.

## Page
Title "Merge users", subtitle "Pairs that look like the same person: the same Telegram id, or the same username ignoring case. Merging moves everything onto the kept user."

1. **Suggestions (N)**, one card per `MergeSuggestion`:
   - top row: one accent pill per reason (`SameTelegramId` → "Same Telegram id", `SameTelegramUserName` → "Same username"), right: muted "suggested keeper on the left" ("on top" on phones);
   - two user boxes, **✓ Keep** (accent border and tint) ← **✕ Merge away** (dashed border). Stacked with ↑ on phones. Each shows the name, then Telegram (`@username · id` or "no id"), Bot (started / placeholder), Login (`login` · 🔑 password), Bills (`billCount · since <firstBillDate>`), Contacts (`contactCount`);
   - actions: **🔀 Merge…** (primary), **⇄ Swap** (secondary). Swapped: the boxes trade places, a danger pill "Swapped: against the suggestion" replaces the hint, and "Back to the suggestion" appears.
2. **Confirmation** (inside the card, danger tint), built from the two users:
   - heading "Merge <old> into <kept>? This can't be undone.";
   - **Moves to <kept>:** <old>'s N bills, payments and shares, debts, spendings, contacts and subscriptions. Rows on the same bill are added together.
   - **Deleted:** any debts, contact links and subscriptions between the two of them, and the <old> account itself.
   - **Profile:** <kept>'s name, Telegram and contact details win; <old>'s only fill gaps.
   - **Sign-in:** "<kept> keeps their login and password; <old>'s are dropped." or, when the kept user has neither, "<kept> has none, so they take <old>'s login and password, if any." Plus "<old> is signed out of the web app."
   - **🔀 Merge** (danger) / Cancel → `POST Users/merge { oldUserId, newUserId }`.
3. **After a merge:** `alert-info` at the top "✅ Merged **<old>** into **<kept>**. Their bills and debts are now <kept>'s."; suggestions and the badge refetch. 400 (e.g. merging away your own account) shows the API message in `alert-danger` inside the card.
4. **Merge any two users** card (hint "for duplicates the suggestions miss", hidden on phones): ✓ Keep and ✕ Merge away search boxes (autocomplete over `GET Users?search=`, showing name, @username, placeholder, bill count). Chosen users show as chips with ✕, then the same two boxes, Swap, and the same confirmation. Merge… is disabled with "Pick two different users." until two different users are chosen.
5. **Empty:** ✅ "No duplicates found", "No two users share a Telegram id or username." The manual picker stays.

## User decisions (rounds)
- Approved in round 1, as shown.

## API
`GET Users/merge/suggestions`, `GET Users?search=` (picker), `POST Users/merge`.

## Backend needs (T8)
None.

## Built (T8)
- `pages/AdminMerge.tsx`, `components/admin/UserSearch.tsx`, `lib/admin.ts` (URL state: `parseMergeUrl`, `mergeSearch`, `withSwap`, `withManual`, `withManualSwapped`, `afterMerge`).
- Swap and Back to the suggestion are new history entries; picking or clearing a user in the manual picker too. After a merge the merged-away user is dropped from `swap`, `keep` and `merge` (replacing the history entry), and the page scrolls to the top for the ✅ message.
- `swap` is keyed by the suggested merge user's id, as the spec says: in a group of three where one user is the suggested merge user of two pairs, swapping one swaps both.
- The manual picker excludes the user chosen on the other side; its user boxes come from the suggestion rows when they're there, otherwise from `GET Users/{id}` plus a search by name.
- A merge refetches every query (users, suggestions and badge, bills, debts, contacts).
