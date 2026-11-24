// Upgrade path, step 2 (since series-2 T3): after starting this branch's image on the database main made and
// upgrade-seed.mjs described. Checks that only the new migration applied, that every list main showed is unchanged
// (new fields may be added, nothing else may differ), that an old draft opens as the New bill form in the browser and
// saves with PUT Bills/{id}, and that API tokens work on the upgraded database.
import { mkdirSync, readFileSync } from 'node:fs'
import { launch } from './lib/cdp.mjs'
import { URL0, call, mint, raw, sql, waitForApp } from './lib/api.mjs'

await waitForApp()
const before = JSON.parse(readFileSync(new URL('./upgrade.json', import.meta.url)))
const { ids, bills, views } = before
const who = { anna: mint(ids.anna, 'Anna Petrova', true), boris: mint(ids.boris, 'Boris Ivanov'), clara: mint(ids.clara, 'Clara Müller') }
const results = []
const check = (name, ok, detail = '') => {
  results.push(ok)
  console.log(ok ? 'PASS' : 'FAIL', name, ok ? '' : detail)
}

const migrations = sql('select "MigrationId" from "__EFMigrationsHistory" order by 1').split('\n')
const added = migrations.filter((m) => !before.migrations.includes(m))
check('only ApiTokens applied on top of main', added.length === 1 && added[0].endsWith('_ApiTokens') && before.migrations.every((m) => migrations.includes(m)), added.join(', '))
check('the ApiTokens table exists and is empty', sql('select count(*) from "ApiTokens"') === '0')

/** `b` has every value `a` has (deeply); extra keys in `b` are new fields and fine. Returns the first difference. */
function diff(a, b, path = '') {
  if (a === null || typeof a !== 'object') return a === b ? null : `${path}: ${JSON.stringify(a)} → ${JSON.stringify(b)}`
  if (b === null || typeof b !== 'object') return `${path}: ${JSON.stringify(b)}`
  if (Array.isArray(a)) {
    if (!Array.isArray(b) || a.length !== b.length) return `${path}: ${a.length} items → ${Array.isArray(b) ? b.length : typeof b}`
    // Lists may come in another order when sort keys tie: match items by a key when it's unique in the list (an id,
    // a bill id, a person), else by position.
    const key = (x) => x?.id ?? x?.billId ?? x?.user?.id ?? x?.userId ?? null
    const keys = a.map(key)
    const unique = keys.every((k) => k !== null) && new Set(keys).size === keys.length
    for (let i = 0; i < a.length; i++) {
      const other = unique ? b.find((x) => key(x) === keys[i]) : b[i]
      const d = diff(a[i], other, `${path}[${key(a[i]) ?? i}]`)
      if (d) return d
    }
    return null
  }
  for (const k of Object.keys(a)) {
    const d = diff(a[k], b[k], `${path}.${k}`)
    if (d) return d
  }
  return null
}
const canon = (list) => [...list].sort((x, y) => JSON.stringify(x).localeCompare(JSON.stringify(y)))
for (const [k, token] of Object.entries(who)) {
  const now = {
    debts: (await call(token, 'GET', '/Debts/Own?countPerPage=100')).items,
    bills: (await call(token, 'GET', '/Bills?countPerPage=100')).items,
    spendings: (await call(token, 'GET', '/Spendings/Own?countPerPage=100')).items,
    summary: await call(token, 'GET', '/Spendings/Own/summary'),
    contacts: await call(token, 'GET', '/UserContacts/Own'),
    exchanges: (await call(token, 'GET', '/Bills/exchanges?countPerPage=100')).items,
  }
  for (const part of Object.keys(now)) {
    const a = views[k][part]
    const b = now[part]
    const d = Array.isArray(a) && a.every((x) => !x?.id) ? diff(canon(a), canon(b)) : diff(a, b)
    check(`${k}: ${part} as on main`, !d, d)
  }
}
const usersNow = (await call(who.anna, 'GET', '/Users?countPerPage=100')).items
const ud = diff(views.users, usersNow)
check('Admin › Users as on main (plus apiTokenCount 0)', !ud && usersNow.every((u) => u.apiTokenCount === 0), ud)
const draftNow = await call(who.anna, 'GET', `/Bills/${bills.draft}`)
const dd = diff(before.draft, draftNow)
check('the old draft is unchanged', !dd, dd)

// ---------- The old draft in the browser: the New bill form; remove Clara (an equal line), Save → PUT ----------
const OUT = `${process.env.TMPDIR ?? '/tmp'}/debtbot-upgrade`
mkdirSync(OUT, { recursive: true })
const b = await launch(Number(process.env.E2E_CDP_PORT ?? 9372))
try {
  await b.viewport(1280, 900)
  await b.goto(`${URL0}/login`)
  await b.waitFor('document.querySelector("input[autocomplete=username]")')
  await b.fill('input[autocomplete=username]', 'anna')
  await b.fill('input[autocomplete=current-password]', 'anna-pass-123')
  await b.click('button.btn-primary')
  await b.waitFor('location.pathname !== "/login"')
  await b.goto(`${URL0}/bills/${bills.draft}`)
  await b.waitFor('document.querySelector("main")?.innerText.includes("People on this bill") && document.querySelector("main").innerText.includes("Saved.")', 20000)
  check('the old draft opens as the form ("People on this bill", "Saved.")', true)
  await b.click('main button[aria-label^="Remove Clara"]')
  await b.waitFor('document.querySelector("main").innerText.includes("Unsaved changes")')
  await b.click('main button', 'Save')
  await b.waitFor('document.querySelector("main").innerText.includes("Saved.")', 20000)
  await b.shot(`${OUT}/upgraded-draft-1280-light.png`)
  const saved = await call(who.anna, 'GET', `/Bills/${bills.draft}`)
  const people = saved.lines[0].participants.map((p) => p.user.id).sort().join()
  check('…Save (PUT) took Clara off the line; the rest of the draft is the same', people === [ids.anna, ids.boris].sort().join() && saved.description === before.draft.description && saved.totalWithTips === before.draft.totalWithTips, people)
} catch (e) {
  check('the old draft in the browser', false, `${e.message} at ${await b.path().catch(() => '?')}`)
  await b.shot(`${OUT}/FAIL-upgrade.png`).catch(() => {})
} finally {
  await b.close()
}

// ---------- API tokens on the upgraded database ----------
const created = await call(who.boris, 'POST', '/profile/tokens', { name: 'After the upgrade', scope: 'Read', expiresInDays: 30 })
const read = await raw(created.token, 'GET', '/Debts/Own?countPerPage=100')
check('a token made after the upgrade reads Boris’s debts, as main showed them', read.status === 200 && !diff(views.boris.debts, read.body.items), `${read.status}`)
await call(who.boris, 'DELETE', `/profile/tokens/${created.id}`)

const failed = results.filter((x) => !x).length
console.log(`\nupgrade: ${results.length - failed}/${results.length} passed`)
process.exit(failed ? 1 : 0)
