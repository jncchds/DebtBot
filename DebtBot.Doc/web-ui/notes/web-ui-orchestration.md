---
name: web-ui-orchestration
description: "How the DebtBot web UI/API work is run — orchestrator session + one worker session per task, one branch/one PR, mockup approval per view, docs committed"
metadata:
  node_type: memory
  type: feedback
  originSessionId: b85fe091-00b4-4715-be20-d611e12ba132
  modified: 2026-11-23T17:10:31.055Z
---

On 2026-11-23 the user set up how the DebtBot API + auth + web UI work is run.

**Sessions**
- Run one orchestrator session. Each task gets its own worker session, started with a clean context through `~/.claude/skills/start-code-session/start.sh`. Tasks run one after another.
- The orchestrator is the session named "DebtBot orchestrator": id `334ad10a`, session id `334ad10a-e54a-4cf5-bb10-dc9b00a79ca5`. It took over on 2026-11-23; the first orchestrator, "DebtBot" (b85fe091), was archived.
- Every worker reports to the orchestrator with SendMessage when it finishes or is blocked.
- Workers never remove themselves: no removing their worktree or branch, no exiting, stopping or archiving their session. The orchestrator may send work back, and it cleans up after the merge (user, 2026-11-23; in common.md "Finishing").
- If the orchestrator isn't running (check `claude agents --json`), the worker resumes it with `start.sh /mnt/git/DebtBot --resume <session id> "Report from …"`. This is the same pattern as ASync's workers-wake-orchestrator.

**Git**
- One branch (`web-ui`) and one draft PR to main.
- Each worker works on `task/NN-name`, branched from `web-ui`. The orchestrator checks it, merges it into `web-ui`, pushes, and cleans up the worktree and branch.

**Views and mockups**
- Every UI view gets a mockup before it's built. The worker renders it to a PNG, sends it with SendUserFile (display: render), and iterates with the user until they approve. Only then does it implement the view.
- Approved mockups are committed as the spec.

**Docs**
- Briefs, plan, notes and mockups are committed in the repo under `DebtBot.Doc/web-ui/`.

**Product decisions**
- There are no password-only users. Every user is a Telegram identity; a password is just an extra way to sign in.
- There should be one user per Telegram id. Duplicates are shown to admins as suggestions on the Merge users screen.
- An exchange is created as a draft and accepted with a separate finalize step, the same as the Telegram button.
- An admin page is required.
- UI language (2026-11-23): English only, but every string goes through i18next like nopds, so locales can be added later. No language switch yet.
- Every view has its own URL (user, T4 mockup round 2): Back/Forward, reload and bookmarks work; filters, paging, tabs and modes live in the URL. Every page sits above the sidebar divider; below it each page has its own abeat-style sub-section. The URL map is in `mockups/shell/README.md`.
- T5 mockup round 1 (user, 2026-11-23): no "following" in the web UI. It never shows subscribed users' debts; T5 adds a web-only own-debts query, and the bot keeps its own. Paged lists get server-side sort by a field, plus filters for the fields that make sense. Bills, Debts, Spendings and Exchanges share one list pattern (header card with filters and Sort by, then paged results: a table when wide, cards on phones), built by T5 as a reusable component with URL helpers and documented in `mockups/shell/README.md`. The T6 (exchanges) and T7 (spendings) briefs must say: use the shared list pattern, with backend sort and filters.
- Exchange (user, 2026-11-23): an exchange is a currency exchange between two people (a forward and a backward bill), not "settling" a debt. Don't label it "settle". Bill participants are picked first with an autocomplete box, and lines then show only those people, because a full contact list in every line doesn't scale. T6 must reuse that.
- .NET 11 upgrade (added 2026-11-23) runs as T3, right after T2; UI tasks became T4–T8 and the end-to-end check T9. Packages go to their latest free versions (AutoMapper below 15, MassTransit 8.x; fix the AutoMapper advisory if a free fix exists), Telegram.Bot to latest with bot code adapted. The SDK 11 is installed user-local in ~/.dotnet (system SDK 10 stays).

**Progress**
- Orchestrator worktree: `/mnt/git/DebtBot/.claude/worktrees/web-ui` (branch `web-ui`). Draft PR: https://github.com/jncchds/DebtBot/pull/34. Docs: `DebtBot.Doc/web-ui/`.
- Workers get a short prompt that points to the brief file. A brief pasted inline that contains `git -C` gets refused by the worktree guard.
- 2026-11-23: T1 (auth) started as worker "DebtBot T1 auth", id 07d30c7c.
- 2026-11-23: orchestration handed over from b85fe091 to 334ad10a.
- 2026-11-23: T1 merged into web-ui (19ff92b). 52 tests pass. Note for T3: refresh reuse has no grace window, so the SPA must share one in-flight refresh. Orchestration was then handed to 334ad10a; the handoff is in `DebtBot.Doc/web-ui/handoff-orchestrator.md`, and T1 was told the new ids.
- 2026-11-23: T2 (API + admin API) started as worker "DebtBot T2 api", id 35c79555.
- 2026-11-23: T2 merged into web-ui (09b6243). 120 tests pass in sdk:8.0. On .NET 10 roll-forward AutoMapper 12 breaks, so T3 must update it. Open: DELETE Users/{id} never works; bot finalize/cancel commands have no creator check; compose doesn't wait for Postgres.
- 2026-11-23: T3 (.NET 11) started as worker "DebtBot T3 net11", id 5c21aa68. The user added the pre-existing bugs T2 found (bot creator check, subscription ids, DELETE Users, PUT Telegram id, ILike wildcard) to T3 (brief item 5b).
- 2026-11-23: T3 merged into web-ui (497db49). net11, SDK 11.0.100 in ~/.dotnet, 154 tests pass. AutoMapper 14 keeps 3 NU1903 advisories (no free fix). Open: paging race in ToPagingResult (count and page are separate queries).
- 2026-11-23: T4 (UI foundation) started as worker "DebtBot T4 ui foundation", id f4223192.
- 2026-11-23: I stopped T3 while the user was still giving it work (line-ending commit; question about debts by Telegram id or username without contacts). Resumed it (same id 5c21aa68) to do that on task/03b-eol. Rule: before `claude stop`, check that the worker is idle and that its transcript has no user messages after its report.
- 2026-11-23: T3b merged into web-ui (4eb8674): .gitattributes, LF everywhere (bat/cmd CRLF), BOMs kept. Branches cut before it merge with `-X renormalize`. T4 was told to merge web-ui into its branch that way. Contacts answer: debts for non-contacts already work in the bot and text API; contacts are always auto-created; the web form uses the contact picker. "Debts without contacts" would be a product decision for later.
- 2026-11-23: T4 merged into web-ui (81c26ea). ClientApp: React 19, Vite 8, react-router 8, Tailwind 4, i18next; refresh is single-flight and serialized across tabs with navigator.locks (falls back without it). 154 .NET tests and 8 vitest pass; 26/26 runtime checks. Open: no code splitting (365 kB), live Telegram widget unchecked.
- 2026-11-23: T5 (Debts, Bills, Bill detail) started as worker "DebtBot T5 debts bills", id 0c8b0b8f.
- 2026-11-23: T5 reported done (159 .NET tests, 34 vitest, 17/17 runtime) but the user gave it more changes after the report (exchange button, participants autocomplete), so the merge is on hold until its follow-up report. Note for T7: Telegram usernames are stored as "@name"; check that by-telegram accepts a name without the @.
- 2026-11-24: T5 merged into web-ui (bd35fa0) after the round-4 follow-up (no Exchange button on Debts, PeoplePicker, FilterPerson). 159 .NET tests and 36 vitest pass, 18/18 runtime checks.
- 2026-11-24: T6 (New bill, Exchange) started as worker "DebtBot T6 newbill exchange", id f2a18930.
- 2026-11-24: the user asked for a separate mockup session that gets all remaining views approved ahead of time (Spendings, Contacts, Profile for T7; Admin Users, Merge and All bills for T8), so the workers can build without waiting. Worker "DebtBot mockups T7-T8", id b83a20d8, branch task/mockups-78, brief `briefs/mockups-t7-t8.md`. Mockups only; it may run alongside a build task. The T7 and T8 briefs then say the mockups are already approved: build from them with no new mockup round, only asking the user when something is unclear.
- 2026-11-24: T6 merged into web-ui (54f973d): New bill form and paste text, Exchange page and list, GET Bills/exchanges. 164 .NET tests, 63 vitest, 14/14 runtime. User decisions (in plan.md): exchange either side can be typed as a rate; rates are shown both ways, in full, captioned "Exchange rate"; on phones nothing may be wider than the screen. Open: a third "debts currency" (bill or payment currency, from the unmerged origin/feature-currency_selection "ChargeInPaymentCurrency"), a product decision for the user; pre-link /ExchangeDebt pairs are missing from the exchanges list.
- 2026-11-24: the user decided the "debts currency" (ChargeInPaymentCurrency) stays out of this series. Note it in the PR description as a follow-up.
- 2026-11-24: T7 mockups approved (Spendings, Contacts, Profile, plus a new 🔔 Subscriptions page the user added) and merged into web-ui early (ea4cb25), with Admin Users and Merge too. Subscriptions stay in the web UI as the bot's Telegram notifications. The user may want subscriptions reflected in Debts and Bills, which would reverse "no following"; the mockup session is scoping that with them.
- 2026-11-24: T7 (Spendings, Contacts, Subscriptions, Profile) started as worker "DebtBot T7 contacts profile", id 8f091795, building from the approved READMEs with no mockup round.
- 2026-11-24: the user approved "Following" (spec `mockups/following/README.md`, merged into web-ui 99d28dc), which reverses T5's "no following": Debts, Bills and Spendings get `?of=<userId>` with a "Whose" dropdown, read-only, for people you have a confirmed subscription to, and bill read access extends to those subscribers. It runs as its own task, T7b, after T7.
- 2026-11-24: the mockup session finished: all T7, T7b and T8 views are approved and merged into web-ui (0904ff7), and the report is `reports/mockups-78.md` (backend needs split into T7, T7b and T8, plus the additions for the shell README). The T8 brief builds from `mockups/admin-users`, `admin-merge` and `admin-bills` with no mockup round.
- 2026-11-24: T7 hit the 5-hour usage limit mid-task; I resumed it with a SendMessage after the reset. T7 merged into web-ui (9ea1b8f): Spendings filters and summary, Contact.addedAt (null for old rows), subscription/cancel, and 4 pages. 174 .NET tests, 77 vitest, 15/15 runtime.
- 2026-11-24: T7b (Following) started as worker "DebtBot T7b following", id 8393fdd8.
- 2026-11-24: T7b merged into web-ui (7b15eff): ofUserId on Debts/Own, Bills and Spendings (confirmed subscription in your direction, else 403), and IBillService.CanRead for GET Bills/{id} and lines only. 181 .NET tests, 87 vitest. Open: the bill page's "← Bills" goes to your own /bills (for T9).
- 2026-11-24: T8 (Admin) started as worker "DebtBot T8 admin", id 140a0d2e.
- 2026-11-24: T8 merged into web-ui (1535d71): Role refuses your own (400), GET Bills/All with filters, and the Admin Users, Merge and All bills pages. 187 .NET tests, 108 vitest. Open: deleting an admin with no data is allowed (moved to T9).

**Why:** the user wants a clean context per task, control over how each view looks, and a durable record of the work.
**How to apply:** follow this for every task in the series. Related: [[web-ui-palette]].
