Mockup session for T7 and T8 of the DebtBot web-ui series. An orchestrator session runs the series. While T6 is being built, this session gets the mockups of the remaining views approved by the user ahead of time, so the T7 and T8 workers can build them without waiting for approval.

**You only make mockups and their docs. You don't write app code or backend code.**

First, read `/mnt/git/DebtBot/DebtBot.Doc/web-ui/briefs/common.md` and follow it where it applies: Git, Machine limits, Product decisions, Mockups first, the URL rule, Finishing, Don't remove yourself, and Waking the orchestrator. To read it before your worktree exists, use `git -C /mnt/git/DebtBot show web-ui:DebtBot.Doc/web-ui/briefs/common.md`.

- Worktree: `/mnt/git/DebtBot/.claude/worktrees/mockups-78`
- Branch: `task/mockups-78`, based on the local `web-ui` branch.
- Read:
  - `plan.md`, "Step 2": the Spendings, Contacts, Profile and Admin rows, plus "Decisions made during the UI tasks";
  - `notes/web-ui-orchestration.md` and `notes/web-ui-palette.md`;
  - `mockups/shell/README.md`: the URL map, the sub-sections, "List views", FilterPerson and PeoplePicker;
  - the approved mockups in `mockups/shell`, `login`, `debts`, `bills` and `bill`, with their READMEs. Build on `mockups/common/` (`mock.css`, `shell.js`, `list.js`) so the new mockups look exactly like the approved ones and the real app;
  - `api.md`: Spendings, Profile, Contacts and Admin users. Mock only what the API can do. If a view needs something the API lacks, note it as a backend need for the worker;
  - the real screens in `reports/task-04/` and `reports/task-05/`.

## How to run the rounds
- **One view at a time**, in the order below. Send its PNGs (light and dark, 1280 px and 390 px, plus the states listed) with SendUserFile (display: render). Ask what to change, and iterate until the user approves that view. Only then move to the next one.
- When a view is approved:
  1. commit its HTML and PNGs under `mockups/<view>/`;
  2. write a `README.md` there with the URLs, the states, the sidebar sub-section, the API calls each part uses, **every decision the user made in the rounds**, and any backend need. This README is the worker's spec, so make it complete;
  3. push.
- Don't edit `mockups/shell/README.md`: T6 may be editing it right now. Put new URLs and sub-section details in your view's README, and list them in your report so the orchestrator can carry them over.
- Keep each message short, and say which view and which round it is.

## Views, in order
1. **📊 Spendings** (`/spendings?…`, T7):
   - the shared list pattern: a header card with filters and Sort by, then paged results, a table when wide and cards on phones;
   - columns: date, bill (a link), description, amount and currency, cancelled marker;
   - suggest filters (currency, date range, show cancelled) and sorts (date, amount). These need backend `sortBy`/`sortOrder`, which T7 adds;
   - the empty state;
   - sub-section: "This month" (a total per currency) and "… All spendings".
2. **👥 Contacts** (`/contacts`, `/contacts/requests`, T7):
   - the list, with rename and remove (with a confirmation);
   - add by @username, including the placeholder case for someone who has never started the bot;
   - search;
   - sub-section: "Requests" and "Recently added".
   - **Ask the user** whether subscriptions (request, confirm, decline: notifications about someone's bills) belong in the web UI at all. In T5 they decided the web UI has no "following". If they say no, drop `/contacts/requests` and the Requests sub-section, and record that.
3. **👤 Profile** (`/profile`, `/profile/password`, `/profile/telegram`, T7):
   - **Details:** display name, phone, email; save, with validation errors;
   - **Login & password:** set them the first time (a login plus a password), then change them (the current password is needed), the 409 "login taken" error, and the minimum length;
   - **Telegram:** the account, read-only (username, id), and the bot's status (`TelegramBotEnabled`), with how to start the bot;
   - the sub-section, as T4 built it.
4. **🛠 Admin, Users** (`/admin/users?search=&role=&page=`, T8):
   - the table: name, Telegram, login, has password, role, bot, bills and contacts counts;
   - search and a role filter;
   - per row: set the login and password (a dialog or its own URL), and make or remove admin;
   - delete, only for stray placeholders: show the 409 "merge instead" message from the API.
5. **🛠 Admin, Merge users** (`/admin/merge`, T8):
   - the suggested pairs, showing keep → merge, the reasons (same Telegram id, same username), and the counts and logins that help decide;
   - one-click merge with a confirmation, which states what moves and what's deleted (see `api.md` and `reports/task-02.md` about the merge);
   - swapping which user is kept;
   - a manual merge picker with two user searches;
   - the empty state;
   - the sidebar badge with the suggestion count.
6. **🛠 Admin, All bills** (`/admin/bills?…`, T8): the shared list pattern over every bill, read-only, linking to `/bills/<id>`. The filters and sorts are as in Bills, plus the creator.

## Finishing
- When all six views are approved and committed:
  1. write `DebtBot.Doc/web-ui/reports/mockups-78.md`. Cover:
     - per view: the folder, the URLs and the sub-section;
     - all the user's decisions;
     - the backend needs, split into those for T7 and those for T8;
     - the additions to the shell README's URL map.
  2. commit and push.
  3. report to the orchestrator as common.md says.
- If the user stops partway through, report what's approved so far.
- Don't remove your worktree, branch or session.
