Task T1 (creator leaves a bill's people) of the DebtBot series-2 series. An orchestrator session runs the series one task at a time, with one session per task.

First, read `/mnt/git/DebtBot/DebtBot.Doc/series-2/briefs/common.md` and follow it throughout. To read it before your worktree exists, use `git -C /mnt/git/DebtBot show series-2:DebtBot.Doc/series-2/briefs/common.md`.

- Worktree: `/mnt/git/DebtBot/.claude/worktrees/task-01`
- Branch: `task/01-creator-leave`, based on the local `series-2` branch.
- Read: `DebtBot.Doc/series-2/plan.md` section 1 and "User decisions"; `DebtBot.Doc/web-ui/reports/task-05.md` and `task-06.md` (PeoplePicker, New bill); `DebtBot.Doc/web-ui/mockups/new-bill/` and `mockups/bill/` (the approved views you change); `mockups/shell/README.md`.

## Goal
On the New bill form and on a draft bill, the creator can take themselves off the bill's people and put themselves back, like anyone else. The creator still owns the bill.

## What exists
- `ClientApp/src/lib/newBill.ts`: `emptyNewBill(meId)` starts with you on the bill (keep that); `lockedPeople(f, meId)` always locks you.
- `ClientApp/src/pages/Bill.tsx`: `billPeople(bill, meId)` always adds the **viewer** and locks everyone in it; extra people live in `?people=`.
- `PeoplePicker` locks the people in `locked` (• instead of ×). `PaymentEditor` preselects `isMe` as the payer.
- Backend: nothing requires the creator on a line or a payment; `BillRepository.ensureParticipants` always adds the creator as a `BillParticipant` (that's ownership and list membership: keep it). Confirm this with a test rather than changing it.

## Scope
1. **Lock rule (user decision):** you are locked only while you are on a line or a payment, the same as everyone. Once you're off all of them, your chip gets × and you can remove yourself. You can add yourself back from the search box ("Anna (you)").
2. **New bill:** `lockedPeople` stops adding `meId`. Removing yourself survives a reload (the form is in sessionStorage already).
3. **Draft bill page:** the people list is the people on lines and payments, plus you **only if you haven't removed yourself**, plus `?people=` extras. Removing yourself must live in the URL (common.md: every mode in the URL), for example a `people` value that marks you out; pick something clean and document it in `mockups/shell/README.md`. Also make the draft page base "you" on the bill's creator rather than on any viewer, so an admin editing someone else's draft isn't added; say in the report what you chose.
4. **Editors:** with you off the bill, `PaymentEditor` must not preselect you, and `LineEditor` must not tick you. Check the New bill "who owes whom" preview with no creator on the bill.
5. **Empty states:** if removing yourself leaves no people, the picker and editors must still behave (hint text, no crash).
6. **Server check:** create and finalize a bill whose creator is on no line and no payment through the API; the creator gets no debts, still sees the bill in `GET Bills` and can edit, finalize and cancel it. Add an xUnit test for that if the existing tests don't cover it. Only change the backend if this fails.

## Mockups first
Mockup the two changed cards (New bill "People on this bill" and the draft bill people card) in the states: you locked (on a line), you removable, you removed (with "Add yourself" visible in search or as a hint). Light and dark, 1280 and 390 px, from the approved new-bill and bill mockups. Get approval, commit under `DebtBot.Doc/series-2/mockups/creator-leave/`, then build.

## Tests
- vitest for `lockedPeople`, the draft page's people list and the URL value, and the editor defaults with no "me".
- xUnit as in scope 6. All existing tests pass (198 .NET, 113 vitest at the end of web-ui).

## Runtime check
`docker compose` (see common.md): sign in, make a New bill with yourself removed (others pay and share), save as draft, add yourself back on the draft and remove again, finalize; check the debts. Screenshot the real views in both themes at both widths. Bring it down and clean up.

## Docs
`reports/task-01.md`; update `mockups/shell/README.md` for the URL value; a line in `DebtBot.Doc/web-ui/api.md` only if an endpoint changed.

Send 3 or 4 screenshots of the built views with SendUserFile (display: render).

When you're done, follow "Finishing" and "Waking the orchestrator" in common.md.
