# T7b report: Following (subscribed people's Debts, Bills and Spendings)

- **Branch:** `task/07b-following`, based on local `web-ui` at `9f12826` (T1 to T7 merged).
- **Commits:**
  - `4dec47e`: backend (`ofUserId`, bill read access), tests, `api.md`;
  - `f4bb9ae`: the UI (`?of=`, the Whose dropdown, read-only mode, Following), vitest tests, the shell README;
  - the last commit: the no-access sidebar fix, the runtime-check scripts, the screenshots, the following README's "Built (T7b)" and this report.
- **No mockup round:** built from the approved `mockups/following/`. Nothing in the spec was unclear enough to ask the user. What I decided while building is under "Decisions" and in that README's "Built (T7b)".

## Backend
- **`ofUserId`** on `GET Debts/Own`, `GET Bills`, `GET Spendings/Own` and `GET Spendings/Own/summary`:
  - left out, or your own id: your lists, unchanged;
  - a user you have a **confirmed** subscription to: their lists, exactly as they'd get them;
  - anything else (pending, none, or only their subscription to you): **403** `{ message }`. Admins aren't special.
  - The check is one helper, `DebtBotControllerBase.ListOwner`, backed by the new `IUserContactService.HasConfirmedSubscription`. "You" always comes from the token.
- **Debts of someone else** (`DebtService.GetOwn(..., ownNames: true)`): each counterpart comes back under their **own** display name, not the owner's contact name. Sorting by `Person` uses that name too.
- **Bills of someone else:** `createdByMe` and `withUserId` are meant from their side.
- **`participantIds`** on `GET Bills` items (from the bill's participants list), so the UI can mark "you're on it", drafts included.
- **Bill read access:** the new `IBillService.CanRead` is `HasAccess`, or a confirmed subscription to anyone on the bill (creator, payer, line participant, or its participants list). It's used only by `GET Bills/{id}` and `GET Bills/lines/{lineId}`.
  - Every change (lines, payments, finalize, cancel, exchange accept) still goes through `HasAccess` plus creator-or-admin, so a subscriber gets **404** there.
- **`api.md`** is updated (access rules, the four endpoints, `participantIds`, Debt names).
- No migration.

## Frontend
- **URL:** `?of=<userId>` on `/`, `/bills` and `/spendings` (`lib/following.ts`).
  - It isn't a list filter. `listSearch` already keeps unknown parameters, so filters, sort, paging, Clear filters, reload and Back/Forward keep it.
  - Switching person (the dropdown, Back to mine, a Following entry) drops the filters and goes to page 1, as a new history entry.
  - Links from someone's list carry it on: 🧾 Bills on their Debts → `/bills?of=&with=&currency=`, and their sidebar entries.
- **"Whose" dropdown** (`components/following/Following.tsx`):
  - shows 👤 Mine, a search box (focused), "Subscribed to", and every confirmed subscription A–Z by your name for them;
  - the list scrolls, and it uses the `on` style when viewing someone;
  - Enter picks the first match; Esc or a click outside closes it;
  - it's hidden when you have no subscriptions.
- **Read-only mode** (title, subtitle, the `alert-info` banner with Back to mine):
  - **Debts:** "Owed to Boris" / "Boris owes" totals, rows and direction filter; people under their own names; you as "Anna Petrova (you)".
  - **Bills:**
    - a "Boris's balance" column ("owed to Boris" / "Boris owes");
    - "Created by Boris";
    - the muted "you're on it" pill;
    - no ✓ Accept, including on his draft exchange;
    - the person filter searches his counterparts.
  - **Spendings:** his list, his currency suggestions and empty-state wording.
- **No access** (403/404 for `of`): your page's title, the dropdown, and the error box with Back to mine. The sidebar then shows only Following.
- **A bill opened from someone's list** is `/bills/<id>`. It was already read-only for anyone who isn't its creator or an admin, so it needed no change. The check confirms no Finalize, Cancel, editors or people picker, on a finalized bill and on his draft.
- **Sidebar:**
  - each page's own section shows the viewed person's data: "Boris's balances", "Boris's drafts" / "Boris's recent bills", "Boris this month", without "… All x";
  - the new **Following** section (up to 5, "showing now" highlighted, then "… All subscriptions") is on all three pages and hidden with nobody.
- **Strings:** a new `following` group.
- **Shell README:** the URL map (`of=` on the three lists, and what changing it does) and the sub-sections (Following).

## Checks
- `npm run build`: OK. `npm run lint`: 0 warnings, 0 errors (i18n: 549 keys).
- `npm test`: **87 passed** in 11 files (77 before, plus 10 in `lib/following.test.ts`), covering:
  - parsing `of` (your own id, junk);
  - `withOf` and `ofSearch`;
  - `of` surviving every list change;
  - `of` not being a filter;
  - the followed list, its search and naming;
  - the read-only switches (no list actions, whose balance, "you're on it").
- `dotnet build DebtBot.sln`: 0 errors.
- `dotnet test` (SDK 11): **181 passed**, 0 failed (174 before, plus 7 in `FollowingTests`), covering:
  - Debts of a confirmed subscription, with own names, filters and sort;
  - `ofUserId` = self and left out unchanged;
  - 403 for pending and none on all four endpoints, for an admin too, and for the reverse direction;
  - Bills of Boris with `participantIds`, `createdByMe` and `withUserId` from his side;
  - Spendings and the summary;
  - bill and line reads for a subscriber (and 404 for pending, none, or someone else's subscription);
  - a read via the participants list only;
  - 404 on finalize, cancel, lines, payments and exchange accept for a subscriber, with nothing changed.

## Runtime check (docker compose, cold build)
- **Setup:** the real `docker-compose.yml` plus `reports/task-07b/scripts/t7b-compose.yml` (project `debtbot-t7b`, API on 18098). The final run was a cold build (no image, build cache pruned) from a clean volume (`scripts/rerun.sh`).
- **Seed** (`seed7b.mjs`, through the API):
  - Anna is a **regular** user (the default admin is someone else), so the change checks mean something;
  - Anna is subscribed to Boris and Grisha (confirmed), to Clara (pending), and has nothing with Dmitry;
  - Boris has 28 bills (2 pages), including bills by Anna and by Grisha, a draft and a draft exchange, plus 5 balances and 25 spendings;
  - Boris calls Grisha "Grisha from work", and Anna must see "Grisha Volkov".
- **curl (`curl7b.sh`), 25/25 as expected:**
  - 200 for Boris and 403 for Clara and Dmitry on all four endpoints;
  - 200 for `ofUserId` = self;
  - 200 for Boris's bill (one Anna isn't on) and its line;
  - 404 for Clara's and Dmitry's bills;
  - 404 for Anna on Finalize, Cancel, lines and payments on Boris's draft, on Cancel of his finalized bill, and on accepting his exchange.
- **Headless Chrome (`check7b.mjs`), 13/13 passed:**
  - **Your Debts:** the dropdown (Mine, Boris, Grisha; the search), and Following.
  - **Boris's Debts**, picked from the dropdown:
    - the title, banner and totals wording;
    - rows match the API, with "Anna Petrova (you)", "Grisha Volkov" (not his contact name) and the direction words;
    - the "Boris's balances" sidebar section, with "showing now".
    - The direction filter, sort, reload and Back keep `of`.
    - 🧾 Bills keeps `of`.
  - **Boris's Bills:**
    - "1–20 of 28", then page 2 and a reload;
    - the "Boris's balance" column, "Created by Boris", and the "you're on it" pills;
    - no Accept, and no bills he isn't on;
    - "Boris's drafts" in the sidebar.
    - The status filter, "Created by Boris" and sort, with Back and Forward.
  - **Read-only bills:** Boris's bill without Anna, opened read-only; his draft opened from the sidebar with no actions.
  - **Boris's Spendings:** picked from the dropdown; the count matches the API; the banner and "Boris this month" (no "… All spendings"); the EUR filter, then a reload.
  - **No access:** `?of=Clara` on `/` and `/bills`, and `?of=Dmitry` on `/spendings` and `/`, each give the error box with Back to mine, no rows, and only Following in the sidebar. Back to mine works from there and from the banner.
  - **Phone (390):**
    - cards, and nothing wider than the screen on all three pages;
    - the dropdown menu fits the screen;
    - the drawer shows Following.
- **Screenshots** of the real app, light and dark (`reports/task-07b/`):
  - Debts: `debts-mine-open-1280`, `debts-boris-1280`, `debts-boris-390`, `debts-boris-390-open`;
  - Bills: `bills-boris-1280`, `bills-boris-390`;
  - Bill: `bill-boris-readonly-1280`, `bill-boris-draft-1280`;
  - Spendings: `spendings-boris-1280`, `spendings-boris-390`, `spendings-boris-390-drawer`;
  - No access: `noaccess-clara-1280`.
- **Found and fixed at runtime:** the no-access view showed an empty "Clara's balances" sidebar heading. The sidebar now shows only Following there, and the check asserts it.
- **Clean-up:**
  - `down -v`;
  - removed `debtbot-t7b:local` and the pulled `postgres:14.1-alpine` and `rabbitmq:3`;
  - ran image and builder prune;
  - removed `wwwroot`, `bin` and `obj`;
  - ran `dotnet build-server shutdown`.
  - About 15 GB is free. `ClientApp/node_modules` is kept in the worktree.
- **Not verified live:** the Telegram widget sign-in (Anna signs in with a password).

## Decisions
- The viewed person is named by **your** contact name (dropdown, title, banner, Following). **Their counterparts** on Debts use their own names. On Bills, "by …" uses the same names as your own Bills ("by you", your contact names), as in the mockup.
- Admins get no special `ofUserId` access (the brief), though an admin can still open any single bill, as before.
- The dropdown's list scrolls instead of the mockup's "… 3 more" line, because the spec text says it scrolls.
- Bill read access also counts the participants list (`BillParticipants`), because that's what `GET Bills` lists. Otherwise a bill in Boris's list could 404 when opened.
- 403 and 404 on an `of` list both show the no-access view.

## Open for later tasks
- None needed for T8. T9 can reuse `reports/task-07b/scripts/` (`rerun.sh` runs from the repo root).
- The bill page's "← Bills" link goes to your own `/bills`, not back to the list you came from; the browser's Back does.
