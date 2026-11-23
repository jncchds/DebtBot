Task T5 (UI: Debts, Bills list, Bill detail) of the DebtBot web-ui series. An orchestrator session runs the series one task at a time, with one session per task.

First, read `/mnt/git/DebtBot/DebtBot.Doc/web-ui/briefs/common.md` and follow it throughout, especially "Mockups first" and the URL rule. To read it before your worktree exists, use `git -C /mnt/git/DebtBot show web-ui:DebtBot.Doc/web-ui/briefs/common.md`.

- Worktree: `/mnt/git/DebtBot/.claude/worktrees/task-05`
- Branch: `task/05-ui-debts-bills`, based on the local `web-ui` branch (T1 to T4 are merged into it).
- Read:
  - `plan.md`, "Step 2", the Debts, Bills and Bill detail rows;
  - `mockups/shell/README.md` (the URL map and the sidebar sub-sections), plus the approved `mockups/shell/` and `mockups/login/` for the look;
  - `api.md`, which you use to extend `api/types.ts`;
  - the reports `task-02.md` (finalize and cancel are asynchronous; exchange pairing via `ExchangeBillId`) and `task-04.md` (the ClientApp structure, `components/subnav/index.tsx`, conventions, i18n);
  - `DebtBot/ClientApp/README.md`.

## Goal
The three core views are real:
- 💸 Debts (home);
- 🧾 Bills, with filters and paging;
- the Bill detail page, with draft actions.

Their sidebar sub-sections are real too. Each one works at 1280 px and 390 px, in light and dark, and every state has its own URL.

## Mockups first
Make mockups of all three views in one round, with realistic DebtBot data in several currencies, and send them with SendUserFile (display: render). Iterate until the user approves each view, then commit them under `mockups/debts/`, `mockups/bills/` and `mockups/bill/`. Show these states:
1. **Debts** (`/`):
   - balances per person and currency: "you owe" in danger, "owed to you" in success;
   - per person, the buttons "Bills with them" (`/bills?with=<id>`) and "Exchange / settle" (`/exchange?with=<id>`; T6 builds that page, so in T5 it's just a link);
   - the empty state (no debts);
   - the sidebar sub-section: balances per person, as in the shell README.
2. **Bills** (`/bills?with=&status=&currency=&page=`, plus a "created by me" filter if the API supports it):
   - filters (person from contacts, currency, status), with status pills;
   - paging;
   - draft exchanges are marked as an exchange pair, with an Accept button (`exchange/finalize`);
   - the empty and filtered-empty states;
   - sub-section: Drafts, Recent bills (last 5), … All bills.
3. **Bill detail** (`/bills/<id>`):
   - header (description, date, creator, status pill, currencies, tips);
   - lines with participants and parts;
   - payments;
   - ledger records (who owes whom what, after finalize);
   - for a **draft**, if you're the creator or an admin: add line, add payment, Finalize, Cancel;
   - the processing state after finalize or cancel;
   - an exchange pair shows its other half and Accept;
   - 404 or no-access.

## Scope
1. **Pages** replacing the placeholders in `App.tsx`: Debts, Bills and Bill detail, as in the approved mockups.
2. **Sidebar sub-sections** for Debts and Bills in `components/subnav/index.tsx`, as in the shell README.
3. **URL state:**
   - filters, page, and any tab or dialog that deserves its own URL (for example `/bills/<id>/add-line`, if you use a separate view) live in the URL;
   - Back and Forward and reload restore them;
   - update `mockups/shell/README.md` if you add URLs.
4. **Draft actions:**
   - add line (split equally or by parts between contacts plus yourself), add payment, Finalize, Cancel, using the structured `lines` and `payments` endpoints from T2;
   - build the line and payment editors as **reusable components**, because T6's New bill form uses them too;
   - finalize and cancel are asynchronous (T2): show a processing state, and refetch or poll until the status leaves `Ready`, with a sensible limit;
   - only show the actions the API will allow, but still handle a 403 or 404 gracefully.
5. **Money:**
   - format amounts per currency with `Intl.NumberFormat`, in English for now, going through i18n;
   - never do money maths in floats in the UI: display what the API returns, and send decimals as the API expects (check `api.md`).
6. **Types:** extend `api/types.ts` from `api.md` for bills, lines, payments, ledger, debts, contacts (picker) and paging. Enums are strings.
7. **i18n:** every string goes in `locales/en/*.json`, and the check script passes.
8. **Code splitting:** add route-level `lazy()` now that there are real pages, as the T4 report suggests.
9. **Checks:**
   - `npm run build`, `npm run lint` and `npm test` pass;
   - add vitest tests for the URL-state helpers and the amount formatting, at least;
   - `dotnet build` and the 154 .NET tests still pass (SDK 11, as in common.md).
10. **Runtime check:**
    - cold `docker compose up --build`;
    - seed realistic data through the API, as in T2 and T3: two or three users with passwords, contacts, finalized bills in two currencies, a draft, a draft exchange;
    - then, in headless Chrome:
      - Debts shows the right balances;
      - the Bills filters and paging survive reload and Back/Forward;
      - a bill detail renders;
      - a draft gets a line and a payment and is finalized, and Debts updates;
      - cancel works;
      - a draft exchange is accepted from the list;
      - a non-participant gets the no-access view;
      - phone layout and drawer.
    - Take screenshots of the real views in light and dark at 1280 px and 390 px, and send them with SendUserFile.
    - Bring the stack down and clean up as in common.md.

When you're done, follow "Finishing" and "Waking the orchestrator" in common.md. Put the screenshots under `DebtBot.Doc/web-ui/reports/task-05/`, and list their paths in your report.
