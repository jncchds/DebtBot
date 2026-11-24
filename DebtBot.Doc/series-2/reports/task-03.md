# T3 report: end-to-end check, e2e stability, polish, docs

- **Branch:** `task/03-e2e`, based on the local `series-2` at `996518c`.
- **Commits:**
  - `922ad2e`: `finalizeBlockers` removed;
  - `eb0ca59`: the e2e stability fixes, the series-2 API and browser checks, the polish, and the README settings;
  - `09a6a04`: the upgrade path from `main` (e7a93a3), the JWT-only check with an admin token, and the first drafts of the e2e README and the PR description;
  - `1727811`: `waitFor` reports its last result, and removing the Chrome profile can't fail a run;
  - the last commits: the upgrade-check matching fix, this report, the final numbers in `pr-description.md`, and the screenshots (`reports/task-03/screenshots/`).

## 1. e2e stability
- **Bus before seeding.** The new `e2e/wait-bus.sh` waits for "Bus started" in the app's log, for up to 4 minutes. `run.sh` and `upgrade.sh` call it before they seed, after both starts in the upgrade's case. The bus connected after 6 to 25 s in my runs. No bill was left in Processing in any run.
- **Finalize and accept.** `check.mjs` now uses `seeStatus`:
  - it polls `GET Bills/{id}` until the status is Processed, for up to 90 s;
  - then it looks for "Finalized" or "Accepted" on the page.

  The page polls for only 30 s and then says "still processing", by design (`usePollWindow`). So when the background work took longer, the check reloads the page. This applies to the draft's Save and finalize, the exchange's Accept and the new creator-leaves Finalize. No run needed the reload.
- **The "Loading…" flake (Admin Users at 390 px in T1 and T2), and the root cause I found.**
  - **What I found.** `lib/cdp.mjs` gave Chrome a throwaway profile in `/tmp/chrome-*` (100–200 MB each) and never removed it. On this machine `/tmp` is a 2.7 GB **tmpfs**. When I started, 27 leftover profiles from earlier e2e runs (dated today, 03:31 to 19:52) filled it to the quota.
  - **What it did here.** My first run hit it: Claude Code's own temp writes failed with EDQUOT. In that run, Contacts "Add" sent no request at all: the server log has no `POST UserContacts/Own/by-telegram?username=newbie_e2e`, and the button stayed disabled. That's the same shape as T1's and T2's "Loading…" flake, a request that never settles.
  - **The link to T1 and T2 isn't proven.** I couldn't reproduce their exact failure. It's a strong explanation: every run added 100–200 MB to the same tmpfs, and those flakes came late in a day of runs.
  - **Fix.** `close()` stops Chrome, waits for it, and removes the profile; so does an exit handler, when a script dies without `close()`. I removed the 27 dead profiles (only `/tmp/chrome-*` dirs that no running process used). In the clean runs no profile was left behind. 264 small `com.google.Chrome.chrome_url_fetcher_*` files in `/tmp` remain; they're tiny and belong to every Chrome run.
  - **The page isn't the cause.** Admin Users itself is fine: `RequireAuth` shows "Loading…" only until the session refresh settles, and the refresh has no retry loop that could hang.
- **Diagnostics for the next flake.**
  - A failed step now lists the requests still in flight (CDP Network events).
  - `waitFor` reports how many times it tried and its last result or error.
- **One unexplained failure.** Run 2 failed the first tour page, admin Debts at 1280 px: the wait timed out after 20 s. Its FAIL screenshot shows the page fully loaded, and no request was pending.
  - This was before the `waitFor` diagnostics existed, so I can't tell whether the wait's eval was throwing or the page loaded just as the wait ran out. In run 4's app log, the slowest requests were `auth/refresh` and `auth/logout`, at about 2.2–2.4 s; nothing else took over 2 s.
  - The same run aborted at its very end on `rmSync` (ENOTEMPTY: Chrome's helpers were still writing). That's fixed with retries, and a failed removal now only prints a line.

## 2. e2e coverage (T1's and T2's checks folded in)
The e2e set stays in **`DebtBot.Doc/web-ui/e2e/`**: its README now says it covers both series. `t1check.mjs` and `t2check.mjs` stay in `series-2/reports/` as records. `compose.e2e.yml` sets `DebtBot_DebtBot__ApiTokens__RateLimitPerMinute=30` so the 429 check is quick.

**`api-check.mjs`: 75 checks (50 before).**
- **T1:**
  - a draft Anna makes without herself is in her Bills;
  - `PUT Bills/{id}` gives 200 for the creator (back on the line) and for an admin who isn't the creator (Anna off again and still the creator);
  - 403 for a participant, 404 without access, 409 on a finalized bill, 400 on an exchange draft;
  - finalized without the creator: Dmitry owes Boris 30, nothing for Anna, and the bill is still in her Bills.
- **T2** (as Grisha, so Anna's token list starts empty for the browser check):
  - the secret is returned once and never listed;
  - both scopes read; read-only gets 403 "read-only" on POST, PUT and Cancel, while read-and-write gets 200;
  - an admin's read-and-write token gets 403 on all 8 JWT-only endpoints. An admin's token is needed so the two admin endpoints get past the admin policy. My first version used Grisha's token, and those two answered with the admin-policy 403 instead;
  - an admin's token is admin, a regular one isn't;
  - `DELETE` gives 204, the revoked token then 401 and the other 200; someone else's token gives 404; no token or an unknown one gives 401;
  - the **31st request in a minute is 429 with `Retry-After`**, while another token and a session still work;
  - `apiTokenCount` is 2 without prefixes; revoke-all is 403 for a regular user and `{ revoked: 2 }` for an admin, after which the tokens give 401; an unknown user gives 404.

**`check.mjs`: 91 steps (74 before).**
- **Tour:** 3 more pages at both widths, in light and dark: admin `bill-draft-edit-line`, `profile-tokens` and `profile-tokens-new`, and the regular user's `profile-tokens`.
- **Flows:** 9 new ones (the Profile › API tokens part is two steps).
  1. **New bill:**
     - you're ticked on a new line and preselected as payer; Boris pays, and his chip is locked;
     - ✎ Edit line on New bill: By parts with Dmitry ×2 locks Dmitry and you, and Equally unlocks them;
     - you leave, and the preview is only "Dmitry K. owes Boris Ivanov";
     - a reload keeps it, and Boris is then the preselected payer.
  2. **Save as draft:** it opens as the form without you, is yours in the API, and is in your Bills.
  3. **On the draft:** add yourself back from the search ("Saved." stays), tick yourself in ✎ Edit line (`/bills/<id>/edit-line/1`), reload (the changes are kept), then Save (PUT).
  4. **Remove yourself again** and Save, then **Finalize:** one record, Dmitry owes Boris 30, no spending for you, and the bill is still in your Bills.
  5. **Profile › API tokens:**
     - empty, with the sidebar entry highlighted; Read only and 90 days preselected; an empty name gives its error;
     - a read-only token: the secret, warning and curl are shown, the secret is kept out of the URL and storage, the token reads, a reload of `/created` goes to the list, and the list shows only the prefix.
  6. **A read-and-write token that never expires**, then Done: the API list is right. Revoke it from its ▾ strip (`?open=<id>`): it then gives 401, and the other token still gives 200.
  7. **Admin › Users:**
     - Boris has 2 tokens;
     - ▾ puts `?open=<borisId>` in the URL, and a reload keeps the strip;
     - "Revoke all 2 API tokens" → confirm → the count is 0 and his token gives 401;
     - Back closes the strip.
  8. **Swagger:** Authorize with the read-only token, then `GET /api/v1/profile` answers 200.

## 3. Upgrade from main
`main` is now `e7a93a3` (the web-ui merge), so T9's upgrade scripts, which seeded the pre-web-ui main the way the bot made data, no longer fit. I rewrote them for this step. T9's version is in git history at `e7a93a3`, under the same file names.
- **`upgrade.sh`:**
  1. main's image (`debtbot-main:local`, built from `git archive e7a93a3`) on a fresh database;
  2. wait for the bus;
  3. **`seed.mjs`** through main's API (the same e2e data set);
  4. `upgrade-seed.mjs` records what main shows: for Anna, Boris and Clara their debts, bills, spendings, spending summary, contacts and exchanges, plus Admin › Users, the old draft and the applied migrations;
  5. this branch's image on the same database;
  6. wait for the bus;
  7. `upgrade-check.mjs`.
- **`upgrade-check.mjs`:**
  - **only** `ApiTokens` applied on top of main, and the table is empty;
  - every recorded list is the same as on main: every value main returned is equal, and new fields may be added;
  - Admin › Users is the same, plus `apiTokenCount: 0`; the old draft is unchanged;
  - in the browser, the old draft opens as the form ("People on this bill", "Saved."); removing Clara and Save (PUT) takes her off the line, and the rest stays;
  - a token made after the upgrade reads Boris's debts as main showed them.
- `compose.main.yml` lost its placeholder bot token, because the web-ui main runs without Telegram settings.
- **Result:** **25/25** (the second upgrade run). The first run had 22/25: my `diff` matched spendings by `user.id`, which every spending of a person shares. It now matches by a key only when that key is unique in the list (an id, a bill id, a person), and by position otherwise. In both runs nothing in the data had changed.

## 4. Open points from T1 and T2
- **`finalizeBlockers`** was used only by its tests (the draft form uses `checkNewBill`). I removed it, its test, and the two locale keys only it used (`bill.blockers.noParticipants`, `notDraft`). vitest has 129 tests (130 − 1).
- **Docs agree on the URLs and endpoints.** I checked the README, `api.md` and the shell README against the controllers' routes and `App.tsx`:
  - `PUT Bills/{id}`, `profile/tokens` (GET/POST/DELETE), `Users/{id}/tokens/revoke-all`, the JWT-only list and the 429;
  - `/bills/new/edit-line/<n>`, `/bills/<id>/edit-line/<n>`, `/profile/tokens?open=`, `/profile/tokens/new|created` and `/admin/users?open=`.

  They agree. The README gained the two `DebtBot_DebtBot__ApiTokens__*` settings (missing before). Its feature list now says a draft is edited in the same form as a new bill and that Profile has API tokens.

## 5. Polish (both themes, 1280 and 390 px)
I looked at New bill, the draft form, Edit line, Profile › API tokens (list, strip, new, created) and Admin › Users (cards and strip).
- **Lines in the bill form (New bill and the draft):** the people chips were inside the item column. Next to the amount, ✎ and ✕, that column was about 120 px wide on a phone, so "Dinner at Molto" wrapped and every chip took its own line. The chips are now a full-width row under the line, and "split equally" doesn't break in the middle. Desktop looks the same apart from the chips spanning the row.
- **People search placeholder:** "Add a person: type a name or @username…" was cut off at 390 px ("…@userna"). It's now "Add a person: name or @username…", which fits.
- The rest needed nothing: tokens list and strip, New token, created, and Admin Users cards with ▾.

## Tests
- **.NET:** **231 passed**, 0 failed. No backend code changed in this task.
- **vitest:** **129 passed** (130 − the removed `finalizeBlockers` test). `npm run typecheck` and `npm run lint` are clean.

## Runtime check
Fresh database each time, with `run.sh`. Run 1 rebuilt the image from this branch; the later runs used `--no-build`, on the same client code.

| Run | API | Browser | Notes |
|---|---|---|---|
| 0 | 50/50 | 73/74 | Before my changes, with /tmp full: Contacts "Add" sent no request (see 1) |
| 1 | 74/75 | 91/91 | The JWT-only check used a regular user's token (a check bug, fixed) |
| 2 | 75/75 | 90/91 + abort | The admin Debts wait timed out with the page loaded (see 1); the profile removal aborted at the end (fixed) |
| **3** | **75/75** | **91/91** | |
| **4** | **75/75** | **91/91** | |
| **5** | **75/75** | **91/91** | |

- **Runs 3, 4 and 5 are three fresh runs in a row with the final scripts.** No step needed the finalize reload. No Chrome profile was left in `/tmp`.
- **Upgrade run:** see 3.
- **Clean-up:** the stack is down with its volumes. I removed the images this task built or pulled (`jncchds/debtbot:latest`, `debtbot-main:local`, `postgres:14.1-alpine`, `rabbitmq:3`, `rabbitmq:3-management`), pruned the build cache (2.9 GB) and dangling images, deleted `bin`/`obj`/`node_modules`/`wwwroot` and main's source copy, and ran `dotnet build-server shutdown`. No Chrome profile is left in `/tmp`.

## Decisions
- The e2e set stays in `web-ui/e2e/` (moving it would only churn paths); its README covers both series.
- The upgrade check now starts from the current `main`; T9's bot-data version is in history.
- The e2e stack's token rate limit is 30 a minute.
- I didn't change the page's 30 s poll window (a product behaviour). The check reloads instead.

## Left open
- **Run 2's Debts wait:** not explained. The new diagnostics will show the cause if it happens again.
- The deferred items in the PR description's follow-ups:
  - exchange drafts can't be edited;
  - other lists still have inline buttons;
  - tokens aren't purged.
