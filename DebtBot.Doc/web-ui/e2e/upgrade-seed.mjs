// Upgrade path, step 1 (since series-2 T3: from `main` after the web-ui merge, e7a93a3). Run after seed.mjs, with
// the app at the state of main. Saves what main's API shows to upgrade.json, for upgrade-check.mjs to compare after
// the upgrade: per person their debts, bills, spendings and contacts; the exchanges; the admin's user list; and the
// drafts (which open in the new form afterwards).
// (The T9 version, from the pre-web-ui main with the bot's own data, is in git history at e7a93a3.)
import { writeFileSync } from 'node:fs'
import { call, loadSeed, mint, sql, waitForApp } from './lib/api.mjs'

await waitForApp()
const { ids, bills } = loadSeed()
const who = { anna: mint(ids.anna, 'Anna Petrova', true), boris: mint(ids.boris, 'Boris Ivanov'), clara: mint(ids.clara, 'Clara Müller') }
const views = {}
for (const [k, token] of Object.entries(who)) {
  views[k] = {
    debts: (await call(token, 'GET', '/Debts/Own?countPerPage=100')).items,
    bills: (await call(token, 'GET', '/Bills?countPerPage=100')).items,
    spendings: (await call(token, 'GET', '/Spendings/Own?countPerPage=100')).items,
    summary: await call(token, 'GET', '/Spendings/Own/summary'),
    contacts: await call(token, 'GET', '/UserContacts/Own'),
    exchanges: (await call(token, 'GET', '/Bills/exchanges?countPerPage=100')).items,
  }
}
views.users = (await call(who.anna, 'GET', '/Users?countPerPage=100')).items
const draft = await call(who.anna, 'GET', `/Bills/${bills.draft}`)
const migrations = sql('select "MigrationId" from "__EFMigrationsHistory" order by 1').split('\n')
writeFileSync(new URL('./upgrade.json', import.meta.url), JSON.stringify({ ids, bills, views, draft, migrations }, null, 2))
console.log(`main: ${migrations.length} migrations (last ${migrations.at(-1)}), ${views.anna.bills.length} bills for Anna, ${views.users.length} users`)
