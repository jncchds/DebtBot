// Upgrade path, step 2: after starting the web-ui image on the database seeded by upgrade-seed.mjs. Checks that the
// migrations applied, that Anna's debts and bills are what main showed, and that LinkOldExchanges linked exactly the
// certain /ExchangeDebt pairs (and left the ambiguous ones alone).
import { readFileSync } from 'node:fs'
import { call, mint, sql, waitForApp } from './lib/api.mjs'

await waitForApp()
const before = JSON.parse(readFileSync(new URL('./upgrade.json', import.meta.url)))
const anna = mint(before.ids.anna, 'Anna Petrova')
const results = []
const check = (name, ok, detail = '') => {
  results.push(ok)
  console.log(ok ? 'PASS' : 'FAIL', name, ok ? '' : detail)
}

const migrations = sql('select "MigrationId" from "__EFMigrationsHistory" order by 1').split('\n')
check('all migrations applied, LinkOldExchanges last', migrations.at(-1).endsWith('_LinkOldExchanges'), migrations.slice(-3).join(', '))

/** A debt as "<other person> <currency> <amount>", whatever the model's shape on main or now. */
const norm = (d) => {
  const userIds = []
  const walk = (o) => {
    for (const [k, v] of Object.entries(o ?? {})) {
      if (v && typeof v === 'object') walk(v)
      else if (/id$/i.test(k) && typeof v === 'string' && v !== before.ids.anna) userIds.push(v)
    }
  }
  walk(d)
  const amount = Number(d.amount ?? d.Amount)
  return `${userIds.sort().join('+')} ${d.currencyCode ?? d.CurrencyCode} ${amount.toFixed(2)}`
}
const debtsNow = (await call(anna, 'GET', '/Debts/Own?countPerPage=100')).items
const a = before.debts.map(norm).sort()
const b = debtsNow.map(norm).sort()
check('Anna’s debts are the same as on main', JSON.stringify(a) === JSON.stringify(b), `\n  main: ${a.join('; ')}\n  now:  ${b.join('; ')}`)

const billsNow = (await call(anna, 'GET', '/Bills?countPerPage=100')).items.map((x) => x.id)
const missing = before.billIds.filter((id) => !billsNow.includes(id))
check('every bill main listed for Anna is still listed', missing.length === 0, `missing ${missing.length}`)

const pairOf = (id) => sql(`select coalesce("ExchangeBillId"::text, '') from "Bills" where "Id" = '${id}'`)
for (const [name, [f, w]] of [['accepted', before.exAccepted], ['draft', before.exDraft]])
  check(`old ${name} exchange linked both ways`, pairOf(f) === w && pairOf(w) === f, `${pairOf(f)} / ${pairOf(w)}`)
check('the ambiguous lookalikes are left unlinked', before.ambiguous.every((id) => pairOf(id) === ''), before.ambiguous.map(pairOf).join(','))

const ex = (await call(anna, 'GET', '/Bills/exchanges?countPerPage=50')).items
const accepted = ex.find((x) => [x.forwardBillId, x.backwardBillId].includes(before.exAccepted[0]))
const draft = ex.find((x) => [x.forwardBillId, x.backwardBillId].includes(before.exDraft[0]))
check(
  'GET Bills/exchanges shows both old exchanges the right way round',
  ex.length === 2 &&
    accepted?.status === 'Processed' &&
    accepted.fromAmount === 100 &&
    accepted.fromCurrencyCode === 'EUR' &&
    accepted.toAmount === 430 &&
    accepted.toCurrencyCode === 'PLN' &&
    draft?.status === 'Draft' &&
    draft.fromAmount === 50 &&
    draft.toCurrencyCode === 'EUR',
  JSON.stringify(ex.map((x) => [x.status, x.fromAmount, x.fromCurrencyCode, x.toAmount, x.toCurrencyCode])),
)

const failed = results.filter((x) => !x).length
console.log(`\nupgrade: ${results.length - failed}/${results.length} passed`)
process.exit(failed ? 1 : 0)
