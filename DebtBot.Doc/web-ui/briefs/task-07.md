Task T7 (UI: Spendings, Contacts, Subscriptions, Profile) of the DebtBot web-ui series. An orchestrator session runs the series one task at a time, with one session per task.

First, read `/mnt/git/DebtBot/DebtBot.Doc/web-ui/briefs/common.md` and follow it throughout. To read it before your worktree exists, use `git -C /mnt/git/DebtBot show web-ui:DebtBot.Doc/web-ui/briefs/common.md`.

- Worktree: `/mnt/git/DebtBot/.claude/worktrees/task-07`
- Branch: `task/07-ui-contacts-profile`, based on the local `web-ui` branch (T1 to T6 and the approved T7 mockups are merged into it).
- Read:
  - `plan.md`, "Decisions made during the UI tasks";
  - `notes/web-ui-orchestration.md`;
  - `mockups/shell/README.md`: the URL map, the sub-sections, "List views", and the shared components;
  - **the approved mockups and their READMEs, which are your spec:** `mockups/spendings/`, `mockups/contacts/`, `mockups/subscriptions/` and `mockups/profile/`;
  - `api.md`;
  - the reports `task-05.md` and `task-06.md`, for the conventions and shared components;
  - `DebtBot/ClientApp/README.md`.

## Mockups are already approved
A separate mockup session got these four views approved by the user, and every decision is recorded in their READMEs. **Don't run a new mockup round.** Build exactly what the READMEs and PNGs show. Ask the user (with SendUserFile, if a picture helps) only when the spec is unclear or doesn't work in practice, and record the answer in that view's README.

## Scope
1. **Backend**, from the READMEs' "Backend" sections. Add tests for each, update `api.md`, and create the migration with dotnet-ef 11 as common.md says:
   - **Spendings:**
     - `GET Spendings/Own` gets the filters and sort from `mockups/spendings/README.md` (currencyCode, dateFrom/dateTo, includeCancelled, sortBy Date|Amount, sortOrder), following `ListSorting.cs`;
     - new `GET Spendings/Own/summary?dateFrom&dateTo` returns `[{ currencyCode, amount, count }]`.
   - **Contacts:**
     - `Contact.addedAt`, with a migration, plus a sensible value for existing rows (explain it);
     - check that `by-telegram` accepts a username with or without the `@` and matches the stored `@name` (T5 noted that usernames are stored with the `@`).
   - **Subscriptions:** new `POST UserContacts/subscription/cancel?contactId=`, which removes your own subscription, pending or confirmed. "Self" comes from the token.
2. **Pages**, as in the approved mockups:
   - Spendings: the shared `ListView`, plus `spendingsSpec`/`spendingsQuery` in `lib/lists.ts`;
   - Contacts: not paged, with search in the URL (`?q=`), inline rename, an inline remove confirmation, and add by @username including the placeholder message;
   - **Subscriptions:** a new top-level page and nav entry right after Contacts, with the pending-requests count badge;
   - Profile: Details, Login & password (first time and change), and Telegram.
3. **Sidebar sub-sections** for Spendings, Contacts and Subscriptions, as in their READMEs.
4. **Shell changes** (the mockup session couldn't edit the shell README):
   - nav: 🔔 Subscriptions after 👥 Contacts, with a count badge;
   - URL map: Contacts drops `/contacts/requests` and gains `?q=`; add `/subscriptions`; Spendings gets its full query;
   - sub-sections: Contacts becomes "Recently added, … All contacts", and Subscriptions gets "Summary".
   - Update `mockups/shell/README.md` and the code to match.
5. **Rules carried over:**
   - every view has its own URL;
   - on phones nothing may be wider than the screen;
   - integer-unit money helpers;
   - i18n for every string;
   - no "following" of other people's debts in the web UI, unless the orchestrator sends you a scope addition (the user is deciding that now).
6. **Checks:**
   - `npm run build`, `npm run lint` and `npm test` pass, with new tests for the new list spec, the contact search and the subscription grouping;
   - `dotnet build` and the .NET tests (164 plus yours) pass.
7. **Runtime check:**
   - cold `docker compose up --build`;
   - seed through the API as T5 and T6 did, with three users and subscriptions in every state;
   - in headless Chrome:
     - Spendings filters, sort and paging, and the "This month" sub-section;
     - add a contact (an existing one, a placeholder, a bad username), rename and remove it;
     - ask for bills from Contacts, then confirm, decline, stop and withdraw on Subscriptions as the other user, with the badge count updating;
     - Profile details save with validation, first-time login and password, then a change with the current password, a 409 for a taken login, and signing in with the new password;
     - phone layout.
   - Take screenshots of the real views in light and dark at 1280 px and 390 px, and send them with SendUserFile.
   - Bring the stack down and clean up as in common.md.

When you're done, follow "Finishing" and "Waking the orchestrator" in common.md. Put the screenshots under `DebtBot.Doc/web-ui/reports/task-07/`, and list their paths in your report.
