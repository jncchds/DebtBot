Task T6 (UI: New bill and Exchange) of the DebtBot web-ui series. An orchestrator session runs the series one task at a time, with one session per task.

First, read `/mnt/git/DebtBot/DebtBot.Doc/web-ui/briefs/common.md` and follow it throughout, especially "Mockups first" and the URL rule. To read it before your worktree exists, use `git -C /mnt/git/DebtBot show web-ui:DebtBot.Doc/web-ui/briefs/common.md`.

- Worktree: `/mnt/git/DebtBot/.claude/worktrees/task-06`
- Branch: `task/06-ui-newbill-exchange`, based on the local `web-ui` branch (T1 to T5 are merged into it).
- Read:
  - `plan.md`, "Step 2": the New bill and Exchange rows, plus "Decisions made during the UI tasks";
  - `notes/web-ui-orchestration.md`: the product decisions, especially **Exchange** and the people picker;
  - `mockups/shell/README.md`: the URL map, the sub-sections, "List views", FilterPerson and PeoplePicker;
  - `mockups/bill/README.md` and the approved `mockups/debts`, `bills` and `bill` for the look;
  - `api.md`: bills, text preview, exchange;
  - `reports/task-05.md`, especially "Open for later tasks" and the Round 4 sections;
  - `DebtBot/ClientApp/README.md`.

## The user's decisions this task must follow
- **What an exchange is:** a currency exchange between two people. It creates a forward bill and a backward bill so that amounts swap currencies. It is **not** "settling" a debt, so never label it that way.
  - It's created as two **draft** bills and accepted with a separate step, the same as Telegram's `/ExchangeDebt` and its Finalize button.
- **People first:** pick the bill's participants first, in the "People on this bill" section (`PeoplePicker` with autocomplete, always on its own line). Lines and "who paid" then offer only those people. Never list every contact in every line.
- **Shared list pattern:** any paged list uses T5's `ListView`, the URL list state and a spec in `lib/lists.ts`, with **backend** `sortBy`/`sortOrder` and filters following `DebtBot.Models/Enums/ListSorting.cs`.
- **URLs:** every view, mode and tab has its own URL.

## Mockups first
Make mockups of these views in one round, with realistic data in light and dark at 1280 px and 390 px, and send them with SendUserFile (display: render). Iterate until the user approves, then commit them under `mockups/new-bill/` and `mockups/exchange/`.
1. **New bill, form mode** (`/bills/new`):
   - description, date, currency, payment currency, and tips;
   - the "People on this bill" section;
   - lines, using the reused `LineEditor`: split equally or by parts among the bill's people;
   - payments, using the reused `PaymentEditor`;
   - a live summary of who owes what, as a preview only (the server does the real calculation);
   - validation states;
   - the actions "Save as draft" and "Create and finalize".
2. **New bill, paste-text mode** (`/bills/new/paste`):
   - a textarea for the bot's bill text format;
   - **Preview** calls `POST Bills/text/preview` and shows the parsed bill and any validation errors;
   - then create as a draft, or create and finalize.
   - Show a short help block with the text format, taken from the bot's help.
3. **New bill sub-section:** the Form / Paste text modes (T4 built these), plus "Continue a draft" (your recent drafts).
4. **Exchange** (`/exchange`, optionally `?with=<userId>`):
   - the other person, picked with the person search;
   - who gives which currency: "I give X in A, they give Y in B" (or the reverse);
   - the two amounts and the computed rate, shown both ways;
   - an optional description;
   - Create creates the two drafts;
   - then the draft pair is shown with **Accept** (`exchange/finalize`) and Cancel.
   - Below that, a paged list of exchanges using the shared list pattern: waiting to accept, and accepted. Filters: person, currency, status. Sort: date and amount.
5. **Exchange sub-section:** "Waiting to accept" and "Recent exchanges".

## Scope
1. **Backend:**
   - Add an exchanges list endpoint, for example `GET Bills/exchanges`, that returns pairs (forward and backward bill, both people, both amounts and currencies, the rate, status, date). It's paged, with `sortBy` (Date, Amount), `sortOrder` and filters (`withUserId`, `currencyCode`, `status`), and has the same access rule as `GET Bills`.
   - Add tests.
   - Update `api.md`.
   - No other backend changes unless the UI needs one; if so, explain it in the report.
2. **New bill page**, both modes:
   - reuse `PeoplePicker`, `LineEditor`, `PaymentEditor`, `usePeople` and the integer-unit money helpers;
   - unsaved form state should survive a reload: put it in the URL where reasonable, or in a sessionStorage draft;
   - after creating, go to `/bills/<id>`;
   - "Create and finalize" creates the bill, then finalizes it, and shows the async processing state as T5 does.
3. **Exchange page**, the list and the sub-section, as in the approved mockups. Add `exchangesSpec` to `lib/lists.ts`.
4. **Consistency:** T5's Bills list and Bill page already show a draft exchange pair with Accept. Make sure they and the new Exchange page use the same components and wording.
5. **Types and i18n:** extend `api/types.ts`. Every string goes in the locale files, and the check script passes.
6. **Checks:**
   - `npm run build`, `npm run lint` and `npm test` pass, with new vitest tests for the form → API mapping and the rate calculation;
   - `dotnet build` and the .NET tests (159 plus yours) pass, using SDK 11 as in common.md.
7. **Runtime check:**
   - cold `docker compose up --build`, and seed through the API as T5 did (`telegramUserName` with a leading "@");
   - in headless Chrome:
     - a form bill with people picked by autocomplete, split equally and by parts, saved as a draft and then finalized, with Debts updated;
     - "Create and finalize" directly;
     - paste-text preview with an error, then fixed, then created;
     - an exchange created and then accepted, with the balances checked through the API;
     - exchange list filters, sort and paging surviving reload and Back/Forward;
     - phone layout.
   - Take screenshots of the real views in light and dark at 1280 px and 390 px, and send them with SendUserFile.
   - Bring the stack down and clean up as in common.md.

When you're done, follow "Finishing" and "Waking the orchestrator" in common.md. Put the screenshots under `DebtBot.Doc/web-ui/reports/task-06/`, and list their paths in your report.
