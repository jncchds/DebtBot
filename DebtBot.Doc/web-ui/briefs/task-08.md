Task T8 (UI: Admin, with Users, Merge users and All bills) of the DebtBot web-ui series. An orchestrator session runs the series one task at a time, with one session per task.

First, read `/mnt/git/DebtBot/DebtBot.Doc/web-ui/briefs/common.md` and follow it throughout. To read it before your worktree exists, use `git -C /mnt/git/DebtBot show web-ui:DebtBot.Doc/web-ui/briefs/common.md`.

- Worktree: `/mnt/git/DebtBot/.claude/worktrees/task-08`
- Branch: `task/08-ui-admin`, based on the local `web-ui` branch (T1 to T7b are merged into it).
- Read:
  - **the spec:** `mockups/admin-users/`, `mockups/admin-merge/` and `mockups/admin-bills/`, with their READMEs and PNGs;
  - `reports/mockups-78.md`, the "T8" backend needs and the shell additions;
  - `api.md`, the Admin users section and Bills;
  - `reports/task-02.md`, on the merge: what moves and what's deleted;
  - the reports `task-05.md`, `task-07.md` and `task-07b.md`, for the conventions and shared components;
  - `DebtBot/ClientApp/README.md`.

## Mockups are already approved
Build exactly what those READMEs and PNGs show. **Don't run a new mockup round.** Ask the user only if the spec is unclear, and record the answer in that README.

## Scope
1. **Backend** (with tests, and `api.md` updated):
   - `POST Users/{id}/Role` refuses to change your own role (400). Check that `Regular` works for removing admin.
   - `GET Bills/All` takes `BillListQuery`'s filters and sort plus `creatorId`, and returns `PagingResult<BillListItem>` with `participantCount`. Admin only.
   - Anything else the READMEs list as a backend need.
2. **Pages**, all admin-only (non-admins get the 404 page, as T4 set up):
   - **Users** (`/admin/users?…`):
     - the table with search and a role filter;
     - "Login & password" Set/Change, with its own URL (`/admin/users/<id>/password`, as the spec says);
     - the role button, disabled for yourself;
     - delete for placeholders only, with the 409 "merge instead" leading to `/admin/merge?merge=<id>`;
     - the "possible duplicate" marker from merge suggestions.
   - **Merge users** (`/admin/merge?…`):
     - the suggestions, showing keep → merge, the reasons and the counts;
     - one-click merge with a confirmation that states what moves and what's deleted;
     - swapping the keeper;
     - the manual picker with two searches over all users;
     - `?swap`, `?keep` and `?merge` in the URL, as the spec says;
     - the sidebar badge with the suggestion count.
   - **All bills** (`/admin/bills?…`): the shared list pattern with the spec's filters ("Created by", plus the Bills filters without "Created by me"), read-only rows linking to `/bills/<id>`.
   - Sidebar sub-sections and shell README updates, as in `reports/mockups-78.md`.
3. **Checks:**
   - `npm run build`, `npm run lint` and `npm test` pass, with new tests for the admin list specs and the merge URL state;
   - `dotnet build` and all the .NET tests pass.
4. **Runtime check:**
   - cold `docker compose up --build`;
   - seed an admin, regular users, a placeholder, and two duplicates (the same Telegram id; and the same username differing in case or `@`);
   - in headless Chrome:
     - search and filter Users;
     - set a password for a user, who then signs in with it;
     - make someone admin and remove it again, and your own role is disabled;
     - delete a stray placeholder, and get a 409 for a user with data that links to Merge;
     - merge a suggested pair, with the badge count dropping and the merged user signing in with their password;
     - a manual merge;
     - All bills filters, sort and paging;
     - a non-admin gets 404 on `/admin/*` and 403 on the API;
     - phone layout.
   - Take screenshots in light and dark at 1280 px and 390 px, and send them with SendUserFile.
   - Bring the stack down and clean up.

When you're done, follow "Finishing" and "Waking the orchestrator" in common.md. Put the screenshots under `DebtBot.Doc/web-ui/reports/task-08/`.
