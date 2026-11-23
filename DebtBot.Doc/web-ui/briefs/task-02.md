Task T2 (API extension and admin API) of the DebtBot web-ui series. An orchestrator session runs the series one task at a time, with one session per task.

First, read `/mnt/git/DebtBot/DebtBot.Doc/web-ui/briefs/common.md` and follow it throughout. To read it before your worktree exists, use `git -C /mnt/git/DebtBot show web-ui:DebtBot.Doc/web-ui/briefs/common.md`.

- Worktree: `/mnt/git/DebtBot/.claude/worktrees/task-02`
- Branch: `task/02-api`, based on the local `web-ui` branch (T1 is already merged into it).
- Read:
  - `DebtBot.Doc/web-ui/plan.md`, section "Step 1 › 1b";
  - `DebtBot.Doc/web-ui/reports/task-01.md`, which covers what T1 built (auth, string enums, `DebtBot.Tests`) and what it left open for T2;
  - `DebtBot.Doc/web-ui/web-sign-in.md`.

## Goal
The REST API covers everything the UI needs (T3 to T7), with access checks on every endpoint. Admins have an API for the users list, setting passwords and merging duplicate users.

## Scope
1. **Everything in plan 1b**, in the order given there:
   - **BillsController:**
     - `GET` = bills I take part in, with filters and paging;
     - admin "all bills" moves to `GET All`;
     - an access check on `Finalize`;
     - `Cancel`;
     - structured `lines` and `payments` (draft bills only);
     - `GET lines/{lineId}`;
     - `POST text/preview`;
     - `POST exchange` as two **draft** bills, with amounts above zero and two different users;
     - `POST exchange/finalize`, mirroring `FinalizeExchangeCommand` with the same checks.
   - **DebtsController:** class-level `[Authorize]`, and `GET Own` returns the whole `PagingResult`.
   - **SpendingsController:** `GET Own`, paged.
   - **ProfileController** (`api/v1/profile`):
     - `GET` is the self view: display name, phone, email, Telegram username, `TelegramBotEnabled`, login, and `hasPassword`;
     - `PUT` changes display name, phone and email only.
     - Remove the admin-only `UsersController.GET own`, or have it delegate; nothing outside this repo uses it.
   - **UserContactsController:**
     - `GET Own` returns `[]` instead of 404;
     - `PUT Own/{contactId}` renames;
     - `DELETE Own/{contactId}`;
     - `POST Own/by-telegram?username=` reuses the `EnsureContact` flow.
     - There is no global user search.
   - New models go in `DebtBot.Models`, and AutoMapper mappings in `Mappings/`.
   - The exchange's draft bills must be recognisable as an exchange pair in `GET` bills and `GET {id}`, so the UI can list a draft exchange with an Accept button. Use whatever `/ExchangeDebt` already stores; if it stores nothing, add the smallest thing that works and explain it in the report.
2. **Admin API** (`UsersController`, Admin only):
   - **`GET` users list:**
     - paged, with a `search` filter (display name, Telegram username, login; ignoring case) and role;
     - each row shows the id, display name, Telegram id and username, role, `TelegramBotEnabled`, login, `hasPassword`, and counts that help with merge decisions (bills, contacts).
     - Never return `PasswordHash`.
   - **`POST {id}/password`** already exists (T1). Keep it.
   - **`GET merge/suggestions`:**
     - pairs of users with the same `TelegramId`, or the same Telegram username ignoring case (with or without a leading `@`);
     - each pair says why it matched, and suggests which user to keep: the one with a Telegram id and bot enabled, then the one with a login or password, then the one with more bills, then the older one.
   - **`POST merge`:**
     - keep the existing behaviour, but make it a body or keep the query, and return 400 for the same id twice and 404 for an unknown id;
     - `UserRepository.MergeUsers` must also handle what T1 added:
       - move `Login` and `PasswordHash` to the kept user if it has none (otherwise keep the kept user's);
       - revoke or delete the old user's refresh tokens;
       - keep the unique login index valid inside the transaction.
     - Check and say in the report what happens to the old user row and to any data it still owns after the merge (spendings, subscriptions, budgets). The Merge screen in T7 depends on this.
3. **Quality bar:** from common.md. Pay particular attention to:
   - Every new endpoint has `[Authorize]`.
   - Reads check `IBillService.HasAccess`, and changes check creator or admin. Ids for "self" always come from the token, never from the body.
   - Another user's token gets 403 or 404 on finalize, cancel, lines, payments and exchange/finalize.
   - Enums go out as strings (T1 set up the global converter).
4. **Tests** in `DebtBot.Tests`:
   - exchange creation rules and amounts (amounts above zero, two different users, both bills drafts);
   - exchange finalize;
   - access checks on finalize and cancel for a non-participant;
   - the structured line and payment mapping (equal split and split by parts);
   - merge suggestions (same Telegram id; username differing only in case or `@`; the suggested keeper);
   - the merge moving the login and password and revoking refresh tokens;
   - profile `PUT` not changing the Telegram id.
   - All existing tests (52) still pass.
5. **Runtime check:**
   - `docker compose`, as T1 did; its report explains how it got the first admin token in a Release image.
   - With curl:
     - create a bill from the form endpoints;
     - add lines and payments, finalize, and check `Debts/Own`;
     - text preview;
     - an exchange as drafts, then finalize;
     - cancel;
     - another user's token gets 403 or 404 on finalize and cancel;
     - the admin users list with search;
     - merge suggestions on two seeded duplicates;
     - a merge, then sign in with the merged user's password.
   - Bring the stack down and clean up, as in common.md.
6. **Docs:** add an API overview for the UI to `DebtBot.Doc/web-ui/api.md`, with each endpoint, its access rule and its request and response shapes. T3 to T7 write `types.ts` from it.

There are no screenshots for this task, since there's no UI.

When you're done, follow "Finishing" and "Waking the orchestrator" in common.md.
