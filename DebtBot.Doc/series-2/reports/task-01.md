# T1 report: the creator leaves a bill's people; a draft is the New bill form

- **Branch:** `task/01-creator-leave`, based on the local `series-2` at `7e3147c`.
- **Commits:**
  - `02f7f17`: the approved mockups (`series-2/mockups/creator-leave/`);
  - `eb8f757`: backend `PUT Bills/{id}` and the xUnit tests;
  - `b2e7b3c`: the web UI (lock rule, shared bill form, the draft as the form, ✎ Edit line), with vitest;
  - `66ea293`: `api.md` and the shell URL map;
  - the last commit: this report, the runtime check script and screenshots, and the e2e draft flow update.

## The scope grew (user decisions during the mockup rounds)
The brief was "the creator can leave and rejoin a bill's people". The user widened it in four mockup rounds; the orchestrator recorded the changes in `plan.md`.
1. **Lock rule** (via the orchestrator): the same for everyone. A person shows • only while they **paid**, are on a **by-parts** line or are the **only person on a line**. Removing someone takes them off every equal-split line, and those lines are split among the people left.
2. **Round 2:** no extra "Add yourself" section and no warning about equal lines. You add yourself back from the People search ("Anna Petrova (you)").
3. **Round 3:** "we need endpoint/endpoints to edit the line: add/remove people, change the ratios", then "can we do all the edits on the UI and apply them in the end via save button?". The user chose: **a draft opens in the same form as New bill**, edited locally and saved with one `PUT Bills/{id}`. The mockups show the real page, each section once.
4. **Round 4:** approved.

## What was built
### Backend
- **`PUT Bills/{id}`** (`BillsController.ReplaceDraftAsync` → `BillService.ReplaceDraftAsync` → `BillRepository.ReplaceDraft`). The body is `BillCreation` and it returns `Bill`. The orchestrator's six points:
  1. **Still a draft, checked inside the transaction.** The first statement is a conditional `UPDATE … WHERE Id = @id AND Status = Draft AND ExchangeBillId IS NULL` (`ExecuteUpdate`), which also takes the row lock.
     - If a finalize got there first, nothing is updated and the result is **409**.
     - If the replace gets there first, a finalize waits for the commit and then finalizes the new content.
     - The service also returns 409 when the bill it loaded isn't a draft.
  2. **Access:** 404 without access (as `GET` and the other actions), 403 for a participant who isn't the creator or an admin.
  3. **Atomic:** the fields, line participants, lines and payments are replaced in one transaction.
  4. **`BillParticipants` are rebuilt:** the creator always stays; people on the new lines and payments are added; everyone else is removed. An admin who saves stays out, and the creator stays the creator.
  5. **Exchange drafts are refused (400):** "An exchange can't be edited; cancel it and make a new one". Editing one half would break the mirrored pair, and I found no safe rule for editing both halves together.
  6. **Validation is the same as `POST Bills`** (`BillRules.ValidateBill` and unknown users). An empty `date` keeps the bill's date rather than "now".
- `BillActionStatus.Conflict` maps to 409 in `DebtBotControllerBase.FromResult`.
- **Scope 6 (creator on no line and no payment):** no backend change was needed, which a test confirms. The bill stays in the creator's `GET Bills` (as a `BillParticipant`), the creator can add lines, finalize and cancel, and finalizing gives them no ledger record and no spending.
  - Caveat: the bot's `forceSponsor` finalize makes the creator the sponsor even then. That's unchanged and not used by the web.

### Web UI (`DebtBot/ClientApp/src`)
- **`components/bill/BillForm.tsx`** is the New bill form taken out of `NewBill.tsx`: Bill, People on this bill, Lines (✎ edit, ✕ remove), Payments (✕) and the preview. The caller owns the state and the actions card.
  - New bill and the draft page both use it.
  - Editors are in the URL: `<base>/add-line`, `<base>/add-payment`, and the new `<base>/edit-line/<n>` (1-based), where `<base>` is `/bills/new` or `/bills/<id>`.
- **`lib/newBill.ts`:**
  - `lockedPeople(f)`: the new rule, with no `meId`;
  - `removePerson`, `withLine`;
  - `fromBill(bill)`: a saved draft as the form;
  - `draftChanged`: ignores order, and treats an equal split as the same whatever its parts;
  - `loadDraft`, `draftKey`, `StoredDraft`;
  - `formEditor`.
- **The draft page (`pages/Bill.tsx`):** a draft the viewer can change (creator or admin; not an exchange; not processing) renders `DraftForm`. Everything else keeps the read-only view, which has lost its old edit bits.
  - **Header:** title, Draft, Cancel bill.
  - **Actions card:** "Saved." or "Unsaved changes, kept in this tab.", plus Discard changes, Save (`PUT`), and ✓ Finalize, which becomes "Save and finalize" when there are changes.
  - **Unsaved changes** are kept in sessionStorage per bill, together with the saved version they're based on. If the bill was saved again elsewhere, the page starts from the server's version.
  - **The picker** offers your contacts plus everyone on the saved bill, so an admin can put the creator back.
- **"You" on a draft:** the draft's people are its creator (only while they're on a line or payment, or while the draft is empty), then everyone on a line or payment. So "you" is the bill's creator, not the viewer: an admin editing someone else's draft isn't added.
- **`?people=` is gone:** people who are on nothing aren't saved. While unsaved they live in the tab's form, which is where the user's rule put form state (New bill already worked this way).
- **Editors:**
  - `LineEditor` takes `initial` for ✎ Edit line ("Edit line" / "Save line");
  - the defaults live in `lib/editors.ts` (`lineStart`, `defaultPayer`): you're preselected as payer and ticked on a new line only while you're on the bill;
  - `PaymentEditor` shows "Add people to the bill first." when the bill has no people.
- **Removed:** `useAddLines` and `useAddPayments` (the web no longer uses `POST Bills/{id}/lines|payments`; the endpoints stay for the API and the bot), and the locale key `people.onBillHint`. Hint and tooltip texts now state the new rule.

## Endpoints and pages
- New: `PUT Bills/{id}` (documented in `web-ui/api.md`, with a note that the creator doesn't have to take part).
- New URLs: `/bills/new/edit-line/<n>` and `/bills/<id>/edit-line/<n>`. `/bills/<id>/add-line|add-payment` now open the form's editors. `web-ui/mockups/shell/README.md` is updated.

## Tests
- **.NET:** 205 passed (198 before, plus 7 in `CreatorLeaveTests`):
  - the creator on nothing (lists, edit, finalize through the real `BillFinalizedConsumer`, no debts or spendings, cancel);
  - replace (all fields, parts, participants rebuilt, the date kept);
  - access (404 / 403 / admin keeps the creator);
  - validation;
  - a finalized bill is a conflict;
  - the repository refuses a bill that stopped being a draft after the service check;
  - an exchange is refused.
- **vitest:** 122 passed (113 before; the old lock test was replaced):
  - the lock rule, `removePerson` with the preview without you, `withLine`;
  - `fromBill`, `draftChanged` (order and equal parts), `loadDraft`;
  - the editor defaults with and without you, and `formEditor`.
- `npm run typecheck` and `npm run lint` are clean.

## Runtime check (docker compose, built from this branch)
- **`scripts/t1check.mjs`, on the e2e stack after `seed.mjs`: 6/6 passed.**
  - **New bill:** Anna adds Boris and Dmitry and a Pasta line (you're ticked and preselected as payer). Boris pays. Your chip has ×; removing it takes you off Pasta, and the preview says "Dmitry K. owes Boris Ivanov" with no "you". A reload keeps it, and the payment editor then preselects Boris.
  - **Save as draft:** the draft opens as the form without you. The API shows you off the line, and the draft is in your `GET Bills`.
  - **Add yourself back** from the search ("Saved." stays, since people alone aren't saved), then tick yourself in ✎ Edit line (`/edit-line/1`). "Unsaved changes" survives a reload. Save → the API has you on Pasta.
  - **Remove yourself again** (×, equal line) and Save → Pasta is Boris and Dmitry; a reload keeps it.
  - **Finalize:** Processed, one ledger record (Dmitry owes Boris 30 EUR), no spending for you, still in your Bills.
- **Full e2e (`web-ui/e2e/run.sh --no-build`, fresh database):** API check passed, browser 74/74.
  - I changed the draft flow in `check.mjs` to the form ("Unsaved changes" → "Save and finalize").
  - The first run had one failure (Admin › Users stuck on "Loading…", a timing flake on the loaded machine). A clean rerun passed everything.
- **Screenshots:** `reports/task-01/screenshots/`, in 1280 and 390, light and dark: `newbill-you-left`, `draft-form`, `draft-edit-line`, `finalized`.
- The stack is down, the volumes are removed and the build cache is pruned.

## Left open
- `lib/bills.ts` `finalizeBlockers` is now only used by its tests (the draft form uses `checkNewBill`). It's kept for now.
- **Exchange drafts can't be edited** (400); they can only be accepted or cancelled, as before.
- **People who are on nothing aren't saved on the server**, so after Save they stay only in this tab's form. That matches "on the bill means on a line or a payment".
- **For T3:** the e2e draft flow now uses the form. A creator-leaves flow could be folded into `check.mjs` from `t1check.mjs`.
