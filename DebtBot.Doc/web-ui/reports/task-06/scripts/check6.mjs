// T6 runtime check in headless Chrome. Usage: node check6.mjs <screenshot dir>
import { readFileSync } from 'node:fs'
import { launch } from './cdp.mjs'

const URL0 = 'http://localhost:18096'
const API = `${URL0}/api/v1`
const OUT = process.argv[2]
const seed = JSON.parse(readFileSync(new URL('./seed6.json', import.meta.url)))
const ids = seed.ids
const results = []
const sleep = (ms) => new Promise((r) => setTimeout(r, ms))

async function step(name, fn) {
  try {
    await fn()
    results.push(['PASS', name])
    console.log('PASS', name)
  } catch (e) {
    results.push(['FAIL', name, e.message])
    console.log('FAIL', name, e.message)
  }
}
const expect = (cond, msg) => {
  if (!cond) throw new Error(msg)
}

// The API as Anna, to check balances.
const token = await fetch(`${API}/auth/login`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ login: 'anna', password: 'anna-pass-123' }) }).then((r) => r.json()).then((s) => s.accessToken)
const get = (path) => fetch(API + path, { headers: { Authorization: `Bearer ${token}` } }).then((r) => r.json())
/** Anna's balance with someone in a currency: > 0 they owe Anna. */
async function balance(who, currency) {
  const d = await get('/Debts/Own')
  return d.items.find((x) => x.debtorUser.id === ids[who] && x.currencyCode === currency)?.amount ?? 0
}
async function waitBalance(who, currency, expected) {
  for (let i = 0; i < 40; i++) {
    const v = await balance(who, currency)
    if (Math.abs(v - expected) < 0.005) return v
    await sleep(500)
  }
  throw new Error(`${who} ${currency}: expected ${expected}, got ${await balance(who, currency)}`)
}
const round2 = (n) => Math.round(n * 100) / 100

const b = await launch(9341)
for (const ev of ['uncaughtException', 'unhandledRejection'])
  process.on(ev, async (e) => {
    console.log('ABORT', e?.message ?? e)
    await b.close()
    process.exit(1)
  })
await b.viewport(1280, 900)
await b.theme(false)

/** Fills the input whose <label> text starts with `label` (inside `scope`). */
async function fillLabel(label, value, scope = 'main') {
  const sel = await b.eval(`(() => { const l = [...document.querySelectorAll(${JSON.stringify(scope + ' label')})].find(x => x.innerText.trim().startsWith(${JSON.stringify(label)})); return l ? '#' + CSS.escape(l.htmlFor) : null })()`)
  if (!sel) throw new Error(`no label ${label}`)
  await b.fill(sel, value)
}
async function pick(inputSelector, typed, name) {
  await b.eval(`document.querySelector(${JSON.stringify(inputSelector)}).focus()`)
  await b.fill(inputSelector, typed)
  await b.waitFor('document.querySelector("main [role=option]")')
  await b.click('main [role=option]', name)
}
const shots = async (name, opts = {}) => {
  for (const dark of opts.themes ?? [false, true]) {
    await b.theme(dark)
    await sleep(300)
    await b.shot(`${OUT}/${name}-${dark ? 'dark' : 'light'}.png`)
  }
  await b.theme(false)
}
/** Phone width, as a phone user: no sidebar choice saved from the desktop runs, so it starts as the icon rail. */
const phone = async () => {
  await b.eval('localStorage.removeItem("debtbot.sidebar")')
  await b.viewport(390, 844, true)
  await b.reload()
}
const desktop = async () => {
  await b.eval('localStorage.removeItem("debtbot.sidebar")')
  await b.viewport(1280, 900)
  await b.reload()
}
const noOverflow = async () => {
  const w = await b.eval('document.documentElement.scrollWidth')
  const vw = await b.eval('window.innerWidth')
  const wide = await b.eval(`[...document.querySelectorAll('main *')].filter(e => e.getBoundingClientRect().right > window.innerWidth + 1).map(e => e.tagName + '.' + (e.className?.toString?.() ?? '').slice(0, 40)).slice(0, 3)`)
  expect(w <= vw && wide.length === 0, `wider than the screen: ${w} > ${vw} ${JSON.stringify(wide)}`)
}

await step('sign in as Anna', async () => {
  await b.goto(`${URL0}/login`)
  await b.waitFor('document.querySelector("input[autocomplete=username]")')
  await b.fill('input[autocomplete=username]', 'anna')
  await b.fill('input[autocomplete=current-password]', 'anna-pass-123')
  await b.click('button.btn-primary')
  await b.waitFor('location.pathname !== "/login"')
})

const borisEur0 = await balance('boris', 'EUR')
const dmitryEur0 = await balance('dmitry', 'EUR')
let formBillId

await step('New bill form: people by autocomplete, lines equal and by parts, payment; kept over a reload', async () => {
  await b.goto(`${URL0}/bills/new`)
  await b.waitText('People on this bill')
  expect((await b.text()).includes('CONTINUE A DRAFT') && (await b.text()).includes('Bowling night'), 'no Continue a draft with the seeded draft')
  await fillLabel('Description', 'Dinner at Molto\nBirthday of Boris')
  await fillLabel('Bill currency', 'eur')
  await fillLabel('Total with tips', '84')
  await pick('main input[placeholder^="Add a person"]', 'bor', 'Boris')
  await pick('main input[placeholder^="Add a person"]', 'dmi', 'Dmitry')
  // Line 1, equal split for all three.
  await b.click('main a', 'Add line')
  await b.waitFor('location.pathname === "/bills/new/add-line"')
  const offered = await b.eval('[...document.querySelectorAll("main form li")].map(li => li.innerText.trim())')
  expect(offered.length === 3 && offered.some((x) => x.includes('Boris')) && !offered.some((x) => x.includes('Clara')), `line offers ${JSON.stringify(offered)}`)
  await fillLabel('Item', 'Pasta', 'main form')
  await fillLabel('Subtotal', '48', 'main form')
  await b.click('main form button[type=submit]')
  await b.waitFor('location.pathname === "/bills/new"')
  // Line 2, by parts for Boris and Dmitry.
  await b.click('main a', 'Add line')
  await b.waitFor('document.querySelector("main form")')
  await fillLabel('Item', 'Tiramisu', 'main form')
  await fillLabel('Subtotal', '28', 'main form')
  await b.click('main form button', 'By parts')
  await b.eval('[...document.querySelectorAll("main form li")].find(li => li.innerText.includes("Anna")).querySelector("input[type=checkbox]").click()')
  await sleep(200)
  await b.fill('main form input[aria-label="Part for Boris"]', '2')
  await b.click('main form button[type=submit]')
  await b.waitFor('location.pathname === "/bills/new"')
  await b.reload()
  await b.waitText('Tiramisu')
  const t = await b.text()
  expect(t.includes('Pasta') && t.includes('split equally') && t.includes('by parts') && t.includes('Boris ×2') && t.includes('Dmitry ×1'), 'lines lost on reload')
  expect(t.includes('Lines 76.00 EUR + tips 8.00 EUR'), 'no tips hint')
})

await step('New bill form: payment, preview, validation', async () => {
  await b.click('main a', 'Add payment')
  await b.waitFor('location.pathname === "/bills/new/add-payment"')
  await shots('newbill-addpayment-1280', { themes: [false] })
  await fillLabel('Amount', '84', 'main form')
  await b.click('main form button[type=submit]')
  await b.waitFor('location.pathname === "/bills/new"')
  await b.waitText('Boris owes you')
  const t = await b.text()
  expect(t.includes('38.32 EUR') && t.includes('28.00 EUR') && t.includes('+66.32'), 'preview amounts')
  // Validation: a bad currency blocks saving and shows the errors.
  await fillLabel('Bill currency', 'EU')
  await b.click('main button', 'Save as draft')
  await b.waitText('Can’t save yet')
  expect((await b.text()).includes('Use a 3-letter code'), 'no field error')
  expect((await b.path()) === '/bills/new', 'saved anyway')
  await shots('newbill-errors-1280', { themes: [false] })
  await fillLabel('Bill currency', 'EUR')
})
await shots('newbill-form-1280')

await step('Save as draft, then finalize; Debts updated', async () => {
  await b.click('main button', 'Save as draft')
  await b.waitFor('/^\\/bills\\/[0-9a-f-]{36}$/.test(location.pathname)')
  formBillId = (await b.path()).split('/')[2]
  // The old page stays on screen while the lazy Bill page loads: wait for the bill's own title.
  await b.waitFor('document.querySelector("main h1")?.innerText === "Dinner at Molto"')
  expect((await b.text()).includes('Draft'), 'not a draft')
  await b.click('main button', 'Finalize')
  await b.waitText('Finalized', 30000)
  await waitBalance('boris', 'EUR', round2(borisEur0 + 38.32))
  await waitBalance('dmitry', 'EUR', round2(dmitryEur0 + 28))
  // The form was cleared.
  await b.goto(`${URL0}/bills/new`)
  await b.waitText('People on this bill')
  expect(!(await b.text()).includes('Tiramisu'), 'form not cleared after creating')
})

await step('Create and finalize directly, in another payment currency', async () => {
  const claraUsd0 = await balance('clara', 'USD')
  await b.goto(`${URL0}/bills/new`)
  await b.waitText('People on this bill')
  await fillLabel('Description', 'Coffee beans')
  await fillLabel('Bill currency', 'PLN')
  await fillLabel('Paid in', 'USD')
  await pick('main input[placeholder^="Add a person"]', 'cla', 'Clara')
  await b.click('main a', 'Add line')
  await b.waitFor('document.querySelector("main form")')
  await fillLabel('Item', 'Beans', 'main form')
  await fillLabel('Subtotal', '86', 'main form')
  await b.click('main form button[type=submit]')
  await b.waitFor('location.pathname === "/bills/new"')
  await b.click('main a', 'Add payment')
  await b.waitFor('document.querySelector("main form select")')
  await b.fill('main form select', ids.clara)
  await fillLabel('Amount', '20', 'main form')
  await b.click('main form button[type=submit]')
  await b.waitFor('location.pathname === "/bills/new"')
  await b.waitText('1 USD = 4.3000 PLN')
  const t = await b.text()
  expect(t.includes('You owe Clara') && t.includes('10.00 USD'), 'fx preview')
  await shots('newbill-fx-1280')
  await phone()
  await b.reload()
  await b.waitText('1 USD = 4.3000 PLN')
  await noOverflow()
  await shots('newbill-fx-390')
  await desktop()
  await b.click('main button', 'Create and finalize')
  await b.waitFor('/^\\/bills\\/[0-9a-f-]{36}$/.test(location.pathname)', 20000)
  await b.waitText('Finalized', 30000)
  await waitBalance('clara', 'USD', round2(claraUsd0 - 10))
})

await step('Paste text: preview with an error, fix it, create a draft', async () => {
  const bad = 'Taxi home\n\n30\nEUR\n\n30 @anna_p\n\nTaxi\nthirty\n1 @anna_p\n1 @boris_iv\n1 @kate_new'
  await b.goto(`${URL0}/bills/new/paste`)
  await b.waitText('Bill text')
  await b.fill('main textarea', bad)
  await b.click('main button', 'Preview')
  await b.waitText('1 problem')
  expect((await b.path()).includes('preview=1'), 'preview not in the URL')
  expect((await b.text()).includes('Failed to parse line subtotal'), 'no parser error')
  await shots('paste-errors-1280', { themes: [false] })
  await b.fill('main textarea', bad.replace('thirty', '30'))
  await b.click('main button', 'Preview')
  await b.waitText('Looks good')
  const t = await b.text()
  expect(t.includes('Boris') && t.includes('new person'), 'names not matched')
  await b.reload()
  await b.waitText('Looks good')
  await shots('paste-preview-1280')
  await b.click('main button', 'Save as draft')
  await b.waitFor('document.querySelector("main h1")?.innerText === "Taxi home"')
  expect((await b.text()).includes('Draft'), 'not a draft')
})

let draftId
await step('Exchange: rate on the second side, create the drafts, accept; balances through the API', async () => {
  const eur0 = await balance('boris', 'EUR')
  const pln0 = await balance('boris', 'PLN')
  await b.goto(`${URL0}/exchange?with=${ids.boris}`)
  await b.waitText('New exchange')
  expect((await b.text()).includes('WAITING TO ACCEPT'), 'no sidebar section')
  await b.fill('main input[aria-label^="Amount: You give"]', '100')
  await b.fill('main input[aria-label^="Currency: You give"]', 'eur')
  await b.eval('[...document.querySelectorAll("main [role=group]")][1].querySelectorAll("button")[1].click()')
  await b.waitFor('location.search.includes("rate=get")')
  await b.waitFor('document.querySelector(\'main input[aria-label="Rate"]\')')
  await b.fill('main input[aria-label="Rate"]', '4.3')
  await b.fill('main input[aria-label^="Currency: Boris"]', 'PLN')
  await b.waitText('430.00 PLN')
  const t = await b.text()
  expect(t.includes('1 EUR = 4.3000 PLN') && t.includes('1 PLN = 0.2326 EUR'), 'rate both ways')
  await shots('exchange-rate-1280', { themes: [false] })
  await b.click('main button', 'Create exchange')
  await b.waitFor('location.search.includes("draft=")')
  draftId = new URLSearchParams((await b.path()).split('?')[1]).get('draft')
  await b.waitText('Exchange with Boris.')
  expect((await b.text()).includes('You give Boris 100.00 EUR, Boris gives you 430.00 PLN.'), 'deal text')
  await shots('exchange-created-1280')
  await b.click('main button', 'Accept')
  await b.waitText('Accepted', 30000)
  await waitBalance('boris', 'EUR', round2(eur0 + 100))
  await waitBalance('boris', 'PLN', round2(pln0 - 430))
})

await step('Exchange: rate on the first side; the mode and amounts survive a reload', async () => {
  await b.goto(`${URL0}/exchange?with=${ids.clara}`)
  await b.waitText('New exchange')
  await b.fill('main input[aria-label^="Amount: Clara"]', '50')
  await b.fill('main input[aria-label^="Currency: Clara"]', 'USD')
  await b.eval('[...document.querySelectorAll("main [role=group]")][0].querySelectorAll("button")[1].click()')
  await b.waitFor('location.search.includes("rate=give")')
  await b.waitFor('document.querySelector(\'main input[aria-label="Rate"]\')')
  await b.fill('main input[aria-label="Rate"]', '1.087')
  await b.fill('main input[aria-label^="Currency: You give"]', 'EUR')
  await b.waitText('46.00 EUR')
  await b.reload()
  await b.waitText('46.00 EUR')
  expect((await b.text()).includes('1 EUR = 1.0870 USD'), 'rate lost')
  await phone()
  await b.reload()
  await b.waitText('46.00 EUR')
  await noOverflow()
  await shots('exchange-ratefirst-390')
  await desktop()
})

await step('Exchange list: filters, sort and paging survive reload and Back/Forward', async () => {
  const all = await get('/Bills/exchanges?countPerPage=20')
  await b.goto(`${URL0}/exchange`)
  await b.waitText(`1–20 of ${all.totalCount}`)
  await b.click('main nav button', '2')
  await b.waitFor('location.search.includes("page=2")')
  await b.reload()
  await b.waitText(`21–${all.totalCount} of ${all.totalCount}`)
  await b.back()
  await b.waitText(`1–20 of ${all.totalCount}`)
  await b.forward()
  await b.waitText(`21–${all.totalCount}`)
  // Status filter, person filter, sort.
  await b.goto(`${URL0}/exchange`)
  await b.waitText('1–20')
  await b.click('main button', 'Accepted')
  await b.waitFor('location.search.includes("status=accepted")')
  const acc = await get('/Bills/exchanges?status=Processed')
  await b.waitText(`of ${acc.totalCount}`)
  await pick('main input[placeholder="Everyone"]', 'cla', 'Clara')
  await b.waitFor('location.search.includes("person=")')
  const accClara = await get(`/Bills/exchanges?status=Processed&withUserId=${ids.clara}`)
  await b.waitFor(`document.querySelectorAll("main table tbody tr").length === ${accClara.items.length}`)
  await b.fill('#list-sort', 'amount')
  await b.waitFor('location.search.includes("sort=amount")')
  await b.reload()
  await b.waitFor('document.querySelector("main table tbody tr")')
  const sorted = await get(`/Bills/exchanges?status=Processed&withUserId=${ids.clara}&sortBy=Amount`)
  const firstRow = await b.eval('document.querySelector("main table tbody tr").innerText')
  expect(firstRow.includes(`${sorted.items[0].fromAmount.toFixed(2)} EUR`), `first row ${firstRow}`)
  await b.back()
  expect(!(await b.path()).includes('sort=amount') && (await b.path()).includes('person='), `Back: ${await b.path()}`)
  await b.back()
  expect(!(await b.path()).includes('person='), `Back twice: ${await b.path()}`)
})

await step('Exchange list: Accept from the list (Anna is an admin, so she can accept Dmitry’s too)', async () => {
  await b.goto(`${URL0}/exchange?status=waiting`)
  await b.waitFor('document.querySelector("main table tbody tr")')
  const before = (await get('/Bills/exchanges?status=Draft')).totalCount
  await b.click('main table button', 'Accept')
  await b.waitFor(`document.body.innerText.includes("of ${before - 1}") || document.querySelectorAll("main table tbody tr").length === ${before - 1}`, 30000)
})
await shots('exchange-list-1280')

await step('Phone layout: cards, folded filters, no sideways scroll', async () => {
  await phone()
  await b.goto(`${URL0}/exchange`)
  await b.waitText('Exchanges')
  await b.waitFor('!document.querySelector("main table")')
  await noOverflow()
  await shots('exchange-390')
  await b.goto(`${URL0}/bills/new`)
  await b.waitText('People on this bill')
  await noOverflow()
  await b.goto(`${URL0}/bills/new/paste`)
  await b.waitText('Bill text')
  await noOverflow()
  await desktop()
})

await step('The Bill page uses the same exchange banner and words', async () => {
  const pair = seed.exchanges[1]
  await b.goto(`${URL0}/bills/${pair.forwardBillId}`)
  await b.waitText('Exchange with')
  const t = await b.text()
  expect(t.includes('Waiting to accept') && t.includes('Exchange rate') && t.includes('Accept'), 'banner words')
  await shots('bill-exchange-1280', { themes: [false] })
  await b.goto(`${URL0}/bills?status=draft`)
  await b.waitFor('document.querySelector("main table tbody tr")')
  expect((await b.text()).includes('Waiting to accept'), 'Bills list pill')
})

await step('Screenshots: New bill and paste at 390', async () => {
  await phone()
  await b.goto(`${URL0}/bills/new/paste`)
  await b.waitText('Bill text')
  await shots('paste-390')
  await b.goto(`${URL0}/exchange?draft=${draftId}`)
  await b.waitText('Exchange with Boris.')
  await shots('exchange-draft-390')
  await desktop()
  await b.goto(`${URL0}/exchange`)
  await b.waitText('Exchanges')
  await shots('exchange-1280')
})

await step('A non-admin who didn’t create an exchange sees who accepts it', async () => {
  await b.goto(`${URL0}/login`)
  await b.eval('fetch("/api/v1/auth/logout", { method: "POST" })')
  await b.goto(`${URL0}/login`)
  await b.waitFor('document.querySelector("input[autocomplete=username]")')
  await b.fill('input[autocomplete=username]', 'boris')
  await b.fill('input[autocomplete=current-password]', 'boris-pass-123')
  await b.click('button.btn-primary')
  await b.waitFor('location.pathname !== "/login"')
  await b.goto(`${URL0}/exchange?status=waiting`)
  await b.waitText('Anna Petrova accepts')
  expect(!(await b.eval('!!document.querySelector("main table button.btn-primary")')), 'Boris has an Accept button')
})

await b.close()
const failed = results.filter((r) => r[0] === 'FAIL')
console.log(`\n${results.length - failed.length}/${results.length} passed`)
process.exit(failed.length ? 1 : 0)
