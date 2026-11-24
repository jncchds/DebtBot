Task T7b (Following: subscribed people's Debts, Bills and Spendings) of the DebtBot web-ui series. An orchestrator session runs the series one task at a time, with one session per task.

First, read `/mnt/git/DebtBot/DebtBot.Doc/web-ui/briefs/common.md` and follow it throughout. To read it before your worktree exists, use `git -C /mnt/git/DebtBot show web-ui:DebtBot.Doc/web-ui/briefs/common.md`.

- Worktree: `/mnt/git/DebtBot/.claude/worktrees/task-07b`
- Branch: `task/07b-following`, based on the local `web-ui` branch (T1 to T7 are merged into it).
- Read:
  - **the spec: `mockups/following/README.md`**, plus `following.html` and the PNGs;
  - `reports/mockups-78.md`, the "T7b" backend needs;
  - `mockups/subscriptions/README.md`, for what a confirmed subscription is;
  - the `plan.md` decisions, noting that this **reverses T5's "no following"**;
  - `api.md`;
  - the reports `task-05.md` and `task-07.md`, for the list pattern, `lib/lists.ts`, the Spendings page and the subscription endpoints;
  - `DebtBot/ClientApp/README.md`.

## Mockups are already approved
Build exactly what `mockups/following/` shows. **Don't run a new mockup round.** Ask the user only if the spec is unclear, and record the answer in that README.

## Scope
1. **Backend:**
   - `Debts/Own`, `GET Bills`, `Spendings/Own` and `Spendings/Own/summary` accept `ofUserId`:
     - allowed for yourself, or when you have a **confirmed** subscription to that user (`mySubscription = Confirmed`);
     - otherwise 403. Pending and none are both 403. Admins aren't special here, unless the spec says so.
   - The results are the subscribed person's data, seen from their side. Counterparts are shown by **their own display names**, not your contact names, because you may not know those people.
   - **Bill read access:** `GET Bills/{id}` and its lines are also readable by confirmed subscribers of any participant. `HasAccess` (or wherever reads are checked) changes for reads only. **Changes stay creator-or-admin**: finalize, cancel, lines, payments and exchange accept stay as they are.
   - Tests: confirmed, pending and none on every endpoint; `ofUserId` = self; the bill read for a subscriber; a subscriber still gets 403 or 404 on every change endpoint.
   - Update `api.md`.
2. **UI**, as in the spec:
   - `?of=<userId>` on `/`, `/bills` and `/spendings`;
   - the "Whose" dropdown: "👤 Mine", a search box, and every confirmed subscription. It's hidden when you have none;
   - the read-only mode: title, banner, "Back to mine", the totals wording, the Bills "<Name>'s balance" column and the "you're on it" pill, and no Accept or other actions;
   - the sidebar sections showing the viewed person's data;
   - the new "Following" section: up to 5 people, then "… All subscriptions";
   - `of` survives filters, sort and paging, and Back/Forward;
   - a bill opened from someone else's list shows read-only.
   - Update `mockups/shell/README.md` (the URL map and sub-sections).
3. **Checks:**
   - `npm run build`, `npm run lint` and `npm test` pass, with tests for the `of` URL state and the read-only switches;
   - `dotnet build` and all the .NET tests pass.
4. **Runtime check:**
   - cold `docker compose up --build`;
   - seed: Anna is subscribed (confirmed) to Boris, pending to Clara, and has nothing with Dmitry;
   - in headless Chrome:
     - Anna views Boris' Debts, Bills and Spendings, with filters, sort, paging and reload;
     - she opens one of Boris' bills read-only;
     - she sees no actions;
     - `?of=Clara` and `?of=Dmitry` give a clean no-access view;
     - curl confirms 403 on the API for pending and none, and 403 or 404 on change endpoints for the subscriber;
     - phone layout.
   - Take screenshots in light and dark at 1280 px and 390 px, and send them with SendUserFile.
   - Bring the stack down and clean up.

When you're done, follow "Finishing" and "Waking the orchestrator" in common.md. Put the screenshots under `DebtBot.Doc/web-ui/reports/task-07b/`.
