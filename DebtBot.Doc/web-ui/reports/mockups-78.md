# Mockups for T7 and T8 (session "DebtBot mockups T7-T8", b83a20d8)

Branch `task/mockups-78` (worktree `.claude/worktrees/mockups-78`), based on `web-ui`; `web-ui` 54f973d (T6) merged in at 993c0d7. Mockups only, no app code. All views approved by the user on 2026-11-24.

Commits: `21328d0` Spendings · `53c6e3e` Contacts · `ae32d6c` Subscriptions + Profile (Contacts updated, shell nav) · `9110f97` Admin Users · `7c80097` Admin Merge · `1a78269` Following (subscriptions on Debts/Bills/Spendings) · `700c8a2` Admin All bills · this report.

Every folder under `DebtBot.Doc/web-ui/mockups/` has the HTML, light/dark PNGs at 1280 and 390 px with the states, and a `README.md` that is the worker's full spec.

## Views

| View | Folder | URLs | Sidebar sub-section | Task |
|---|---|---|---|---|
| 📊 Spendings | `spendings/` | `/spendings?currency=&from=&to=&cancelled=1&sort=date\|amount&order=&page=` | This month (a total per bill currency), … All spendings | T7 |
| 👥 Contacts | `contacts/` | `/contacts?q=` | Recently added (3 newest), … All contacts | T7 |
| 🔔 Subscriptions (new page) | `subscriptions/` | `/subscriptions` | Summary (Requests / Get your bills and debts / Your subscriptions, with counts) | T7 |
| 👤 Profile | `profile/` | `/profile`, `/profile/password`, `/profile/telegram` | Sections: Details, Login & password, Telegram (as T4) | T7 |
| Subscriptions on Debts, Bills, Spendings | `following/` | `of=<userId>` on `/`, `/bills`, `/spendings` | each page's section shows the viewed person's data; new "Following" section (≤5, … All subscriptions) | T7b |
| 🛠 Admin › Users | `admin-users/` | `/admin/users?search=&role=admin\|regular&page=`, `/admin/users/<id>/password` (dialog) | Admin: Users, Merge users (badge), All bills | T8 |
| 🛠 Admin › Merge users | `admin-merge/` | `/admin/merge?swap=<id>,…&keep=<id>&merge=<id>` | same, badge = suggestion count | T8 |
| 🛠 Admin › All bills | `admin-bills/` | `/admin/bills?q=&by=&with=&currency=&from=&to=&status=&sort=&order=&page=` | same | T8 |

## The user's decisions
- **Spendings:** the share is always a whole percentage of the bill, never a fraction. Approved as shown: currency filter and monthly totals by **bill currency**; cancelled hidden by default (faded with a pill when shown); monthly totals in the sidebar only.
- **Subscriptions belong in the web UI.** Stop and Withdraw for your own subscriptions were added (new endpoint). Contacts get an "added on" date (for Recently added).
- **Subscriptions became their own top-level page**, 🔔 Subscriptions after 👥 Contacts, with a count badge of pending requests: the Contacts sub-section entries looked like one selected request, and the page was unreachable with no requests. Wording: notifications about **bills and debts**.
- **Subscriptions in the earlier pages (reverses T5's "no following"):** Debts, Bills and Spendings get a read-only view of someone you're subscribed to, chosen with a **"Whose" dropdown** (not pills: the list may be big), with a banner and "Back to mine". Planned by the orchestrator as T7b.
- **Profile:** approved as shown.
- **Admin › Users:** "Sign-in" was a bad caption: the column is **Login & password** and its Set/Change button lives in that column; the Make/Remove admin button lives in the **Role** column; the last column has only 🗑 (placeholders). Sort by name only.
- **Admin › Merge users** and **All bills:** approved as shown.

## Backend needs

**T7**
- `GET Spendings/Own`: `[FromQuery] SpendingListQuery` (`currencyCode` = bill currency, `dateFrom`, `dateTo`, `includeCancelled` default false, `sortBy` `Date|Amount`, `sortOrder`).
- New `GET Spendings/Own/summary?dateFrom=&dateTo=` → `{ currencyCode, amount, count }[]` (non-cancelled).
- `Contact.addedAt` (a column on the contact link; migration via `dotnet ef`).
- New `POST UserContacts/subscription/cancel?contactId=`: removes your own subscription (pending or confirmed).

**T7b (Following)**
- `ofUserId` on `Debts/Own`, `Bills`, `Spendings/Own` (+ summary), allowed only with a confirmed subscription (or self), else 403.
- Read access to a bill (`GET Bills/{id}`, lines) for confirmed subscribers of any participant; changes stay creator-or-admin.
- Counterparts by their own display names; tests for confirmed / pending / none.

**T8**
- `POST Users/{id}/Role` should refuse changing your own role (the UI disables it); check `Regular` works for removing admin.
- `GET Bills/All`: the `BillListQuery` filters and sort plus `creatorId`, returning `PagingResult<BillListItem>` with `participantCount`.
- Users and Merge need nothing new (the "possible duplicate" marker and the badge come from `GET Users/merge/suggestions`).

## Additions for `mockups/shell/README.md` (not edited here)
- Nav order: Debts, Bills, New bill, Exchange, Spendings, Contacts, **🔔 Subscriptions**, Profile, Admin. Nav buttons may carry a `count` badge (Subscriptions: pending requests; hidden on the collapsed phone rail). `mockups/common/shell.js` already has it (`MOCK.badges`).
- Sub-sections: Spendings "This month, … All spendings"; Contacts "Recently added, … All contacts" (no Requests); **Subscriptions "Summary"**; Debts/Bills/Spendings get "Following" (T7b).
- URL map:
  - Spendings `/spendings?currency=&from=&to=&cancelled=1&sort=date|amount&order=&page=` (+ `of=` in T7b)
  - Contacts `/contacts?q=` (no `/contacts/requests`)
  - Subscriptions `/subscriptions`
  - Debts and Bills `+ of=<userId>` (T7b)
  - Admin `/admin/users?search=&role=&page=`, `/admin/users/<id>/password`, `/admin/merge?swap=&keep=&merge=`, `/admin/bills?q=&by=&with=&currency=&from=&to=&status=&sort=&order=&page=`

## Notes
- The orchestrator already merged up to 1a78269 into `web-ui` and started T7 from the four T7 READMEs; T7b (Following) is planned after T7.
- Mockups follow T6's rule that nothing on a phone is wider than the screen (all lists become cards at 390 px).
- Rendering: headless Chrome, `file://…/<view>.html?<query>` at the listed window sizes.
