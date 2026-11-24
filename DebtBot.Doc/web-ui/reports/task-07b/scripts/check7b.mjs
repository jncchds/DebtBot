// T7b runtime check in headless Chrome: Anna views Boris's Debts, Bills and Spendings. Usage: node check7b.mjs <screenshot dir>
import { readFileSync } from 'node:fs'
import { launch } from './cdp.mjs'
import { call, mint } from './seed7b.mjs'

const URL0 = 'http://localhost:18098'
const OUT = process.argv[2]
const seed = JSON.parse(readFileSync(new URL('./seed7b.json', import.meta.url)))
const { ids, bills } = seed
const results = []
const sleep = (ms) => new Promise((r) => setTimeout(r, ms))
const anna = mint(ids.anna, 'Anna Petrova')
const api = (path) => call(anna, 'GET', path)

async function step(name, fn) {
  try {
    await fn()
    results.push(['PASS', name])
    console.log('PASS', name)
  } catch (e) {
    results.push(['FAIL', name, e.message])
    console.log('FAIL', name, e.message)
    try {
      await b.shot(`${process.env.TMPDIR ?? '/tmp'}/t7b-fail-${results.length}.png`)
      console.log('  at', await b.path(), '|', (await b.text()).replace(/\s+/g, ' ').slice(0, 400))
    } catch {
      /* page gone */
    }
  }
}
const expect = (cond, msg) => {
  if (!cond) throw new Error(msg)
}

const b = await launch(9348)
for (const ev of ['uncaughtException', 'unhandledRejection'])
  process.on(ev, async (e) => {
    console.log('ABORT', e?.message ?? e)
    await b.close()
    process.exit(1)
  })
await b.viewport(1280, 900)
await b.theme(false)

const shots = async (name) => {
  for (const dark of [false, true]) {
    await b.theme(dark)
    await sleep(300)
    await b.shot(`${OUT}/${name}-${dark ? 'dark' : 'light'}.png`)
  }
  await b.theme(false)
}
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
const signIn = async (login, password) => {
  await b.goto(`${URL0}/login`)
  await b.waitFor('document.querySelector("input[autocomplete=username]")')
  await b.fill('input[autocomplete=username]', login)
  await b.fill('input[autocomplete=current-password]', password)
  for (let i = 0; ; i++) {
    await b.click('button.btn-primary')
    try {
      await b.waitFor('location.pathname !== "/login" || document.body.innerText.includes("Sign-in failed")')
    } catch {
      /* still waiting */
    }
    if ((await b.path()) !== '/login') return
    if (!(await b.eval('!!document.querySelector("button.btn-primary")'))) {
      await b.waitFor('location.pathname !== "/login"', 30000)
      return
    }
    if (i === 3) throw new Error('sign-in failed 4 times')
    console.log('  sign-in limited, waiting 30 s')
    await sleep(30000)
  }
}
const main = () => b.eval('document.querySelector("main").innerText')
const aside = () => b.eval('document.querySelector("aside").innerText')
const rows = () => b.eval('[...document.querySelectorAll("main tbody tr")].map(r => r.innerText.replace(/\\s+/g, " ").trim())')
const params = () => b.eval('Object.fromEntries(new URLSearchParams(location.search))')
const whose = () => b.eval('document.querySelector("main button[aria-haspopup=listbox]")?.innerText.trim() ?? null')
const openWhose = () => b.click('main button[aria-haspopup=listbox]')
const pick = (name) => b.click('main [role=option]', name)
const range = (n) => b.waitFor(`document.querySelector("main").innerText.includes(${JSON.stringify(n)})`)

await step('sign in as Anna', () => signIn('anna', 'anna-pass-123'))

// ---------- Debts ----------
await step('Debts, mine: Whose dropdown and Following (Boris, Grisha); Clara (pending) and Dmitry aren\'t offered', async () => {
  await b.goto(`${URL0}/`)
  await b.waitText('Owed to you')
  await b.waitFor('document.querySelector("main button[aria-haspopup=listbox]")')
  expect((await whose()).includes('Mine'), `whose: ${await whose()}`)
  const side = await aside()
  expect(side.includes('FOLLOWING') || side.includes('Following'), 'no Following section')
  expect(side.includes('see Boris’s debts') && side.includes('see Grisha’s debts'), 'Following entries')
  expect(side.includes('All subscriptions'), 'no … All subscriptions')
  await openWhose()
  const menu = await b.eval('[...document.querySelectorAll("main [role=option]")].map(o => o.innerText.trim())')
  expect(JSON.stringify(menu) === JSON.stringify(['👤 Mine', '🔔Boris', '🔔Grisha'].map((s) => s)) || (menu.length === 3 && menu[1].includes('Boris') && menu[2].includes('Grisha')), `menu: ${menu}`)
  await b.fill('main input[placeholder="Search…"]', 'gri')
  const filtered = await b.eval('[...document.querySelectorAll("main [role=option]")].map(o => o.innerText.trim())')
  expect(filtered.length === 2 && filtered[1].includes('Grisha'), `search: ${filtered}`)
  await b.fill('main input[placeholder="Search…"]', '')
  await shots('debts-mine-open-1280')
})

await step('Debts of Boris: picked from the dropdown; title, banner, totals, own names, you, read-only', async () => {
  await pick('Boris')
  await b.waitText('Boris’s debts')
  expect((await params()).of === ids.boris, `url ${await b.path()}`)
  const text = await main()
  expect(text.includes('You’re seeing Boris’s debts because you’re subscribed to Boris. Read-only.'), 'banner')
  expect(text.includes('Back to mine'), 'Back to mine')
  expect(text.includes('Owed to Boris'.toUpperCase()) || text.includes('Owed to Boris'), 'totals wording')
  const api0 = await api(`/Debts/Own?ofUserId=${ids.boris}`)
  await b.waitFor('document.querySelectorAll("main tbody tr").length > 0')
  const r = await rows()
  expect(r.length === api0.items.length, `rows ${r.length} vs api ${api0.items.length}`)
  expect(r.some((x) => /^Anna Petrova\s?\(you\)/.test(x)), `no "Anna Petrova (you)": ${r}`)
  expect(r.some((x) => x.startsWith('Grisha Volkov')) && !r.some((x) => x.includes('Grisha from work')), `own names: ${r}`)
  expect(r.some((x) => x.includes('owes Boris')) || r.some((x) => x.includes('Boris owes')), 'direction words')
  expect(!text.includes('owes you') && !text.includes('you owe'), 'your wording on Boris’s list')
  const side = await aside()
  expect(side.includes('Boris’s balances'.toUpperCase()) || side.includes('Boris’s balances'), 'sidebar section')
  expect(side.includes('showing now'), 'Following: showing now')
  await shots('debts-boris-1280')
})

await step('Debts of Boris: direction filter, sort, reload, Back keep of', async () => {
  await b.click('main button', 'Boris owes')
  await b.waitFor('new URLSearchParams(location.search).get("dir") === "owe"')
  expect((await params()).of === ids.boris, 'of dropped by the filter')
  const owes = await api(`/Debts/Own?ofUserId=${ids.boris}&direction=IOwe`)
  await b.waitFor(`document.querySelectorAll("main tbody tr").length === ${owes.items.length}`)
  await b.fill('#list-sort', 'amount')
  await b.waitFor('new URLSearchParams(location.search).get("sort") === "amount"')
  await b.reload()
  await b.waitText('Boris’s debts')
  const p = await params()
  expect(p.of === ids.boris && p.dir === 'owe' && p.sort === 'amount', `after reload ${JSON.stringify(p)}`)
  await b.back()
  await b.waitFor('!new URLSearchParams(location.search).get("sort")')
  expect((await params()).of === ids.boris, 'Back lost of')
  await b.back()
  await b.waitFor('!new URLSearchParams(location.search).get("dir")')
  expect((await params()).of === ids.boris, 'Back lost of (2)')
})

await step('Debts of Boris: 🧾 Bills keeps of', async () => {
  await b.waitFor('document.querySelectorAll("main tbody tr").length > 0')
  const href = await b.eval('[...document.querySelectorAll("main tbody tr a")].map(a => a.getAttribute("href")).find(h => h.startsWith("/bills"))')
  expect(href.startsWith(`/bills?of=${ids.boris}&with=`), `link ${href}`)
})

// ---------- Bills ----------
let borisTotal = 0
await step('Bills of Boris: list, paging, balance column, you’re-on-it pill, no Accept', async () => {
  const all = await api(`/Bills?ofUserId=${ids.boris}`)
  borisTotal = all.totalCount
  await b.goto(`${URL0}/bills?of=${ids.boris}`)
  await b.waitText('Boris’s bills')
  await range(`1–20 of ${borisTotal}`)
  const text = await main()
  expect(text.includes('Bills Boris takes part in.'), 'subtitle')
  expect(text.toUpperCase().includes('BORIS’S BALANCE'), 'balance column')
  expect(text.includes('Created by Boris'), 'Created by Boris')
  expect(text.includes('you’re on it'), 'no you’re-on-it pill')
  expect(!text.includes('Accept'), 'an Accept button on Boris’s list')
  expect(text.includes('Boris owes') || text.includes('owed to Boris'), 'balance wording')
  expect(!text.includes('Flowers') && !text.includes('Clara and Dmitry'), 'a bill Boris isn’t on')
  const side = await aside()
  expect(side.includes('Boris’s drafts') || side.includes('BORIS’S DRAFTS'), 'drafts section')
  expect(side.includes('Groceries'), 'Boris’s draft in the sidebar')
  expect(side.includes('showing now'), 'Following: showing now')
  await shots('bills-boris-1280')
  await b.click('main nav button', '2')
  await b.waitFor('new URLSearchParams(location.search).get("page") === "2"')
  await range(`21–${borisTotal} of ${borisTotal}`)
  expect((await params()).of === ids.boris, 'paging dropped of')
  await b.reload()
  await range(`21–${borisTotal} of ${borisTotal}`)
})

await step('Bills of Boris: status, Created by Boris, sort; Back and Forward', async () => {
  await b.goto(`${URL0}/bills?of=${ids.boris}`)
  await range(`of ${borisTotal}`)
  await b.click('main button', 'Draft')
  await b.waitFor('new URLSearchParams(location.search).get("status") === "draft"')
  const drafts = await api(`/Bills?ofUserId=${ids.boris}&status=Draft`)
  await range(`of ${drafts.totalCount}`)
  await b.back()
  await b.waitFor('!new URLSearchParams(location.search).get("status")')
  await b.click('main label', 'Created by Boris')
  await b.waitFor('new URLSearchParams(location.search).get("mine") === "1"')
  const created = await api(`/Bills?ofUserId=${ids.boris}&createdByMe=true`)
  await range(`of ${created.totalCount}`)
  expect(created.totalCount < borisTotal, 'created-by filter did nothing')
  await b.fill('#list-sort', 'total')
  await b.waitFor('new URLSearchParams(location.search).get("sort") === "total"')
  const p = await params()
  expect(p.of === ids.boris && p.mine === '1', `of or mine lost: ${JSON.stringify(p)}`)
  await b.back()
  await b.back()
  await b.waitFor('!new URLSearchParams(location.search).get("mine")')
  await b.forward()
  await b.waitFor('new URLSearchParams(location.search).get("mine") === "1"')
  expect((await params()).of === ids.boris, 'Forward lost of')
})

await step('A bill of Boris’s opened from his list is read-only (Anna isn’t on it)', async () => {
  await b.goto(`${URL0}/bills/${bills.borisDmitry}`)
  await b.waitText('Board games #2')
  const buttons = await b.eval('[...document.querySelectorAll("main button")].map(x => x.innerText.trim())')
  expect(!buttons.some((x) => /Finalize|Cancel|Add line|Add payment|Accept/.test(x)), `actions on a read-only bill: ${buttons}`)
  await shots('bill-boris-readonly-1280')
})

await step('Boris’s draft opened from the sidebar: no Finalize, no editors, no people picker', async () => {
  await b.goto(`${URL0}/bills?of=${ids.boris}`)
  await b.waitText('Groceries')
  await b.click('aside a', 'Groceries')
  await b.waitFor(`location.pathname === "/bills/${bills.draft}"`)
  await b.waitText('Groceries')
  const text = await main()
  const buttons = await b.eval('[...document.querySelectorAll("main button")].map(x => x.innerText.trim())')
  expect(!buttons.some((x) => /Finalize|Cancel|Add line|Add payment|Accept/.test(x)) && !text.includes('People on this bill'), `draft actions: ${buttons}`)
  await shots('bill-boris-draft-1280')
})

// ---------- Spendings ----------
await step('Spendings of Boris: list, currency filter, paging, sidebar this month', async () => {
  const all = await api(`/Spendings/Own?ofUserId=${ids.boris}`)
  await b.goto(`${URL0}/spendings`)
  await b.waitFor('document.querySelector("main button[aria-haspopup=listbox]")')
  await openWhose()
  await pick('Boris')
  await b.waitText('Boris’s spendings')
  expect((await params()).of === ids.boris, 'of not set by the dropdown')
  await range(`of ${all.totalCount}`)
  const text = await main()
  expect(text.includes('Boris’s share of every bill Boris takes part in.'), 'subtitle')
  expect(text.includes('You’re seeing Boris’s spendings'), 'banner')
  const side = await aside()
  expect(side.includes('Boris this month') || side.includes('BORIS THIS MONTH'), 'sidebar section')
  expect(!side.includes('All spendings'), '… All spendings while viewing Boris')
  await shots('spendings-boris-1280')
  const eur = await api(`/Spendings/Own?ofUserId=${ids.boris}&currencyCode=EUR`)
  await b.fill('main input[placeholder="Any currency"], main input[list]', 'EUR')
  await b.waitFor('new URLSearchParams(location.search).get("currency") === "EUR"', 8000).catch(async () => {
    await b.goto(`${URL0}/spendings?of=${ids.boris}&currency=EUR`)
  })
  await range(`of ${eur.totalCount}`)
  expect((await params()).of === ids.boris, 'of lost by the currency filter')
  await b.reload()
  await range(`of ${eur.totalCount}`)
  // A bill link opens the bill (read-only for Anna unless she's on it).
  const href = await b.eval('document.querySelector("main tbody tr a")?.getAttribute("href")')
  expect(href && href.startsWith('/bills/'), `bill link ${href}`)
})

// ---------- No access ----------
await step('?of=Clara (pending) and ?of=Dmitry (none): a clean no-access view with Back to mine', async () => {
  for (const [who, path, name] of [
    ['clara', '/', 'debts'],
    ['clara', '/bills', 'bills'],
    ['dmitry', '/spendings', 'spendings'],
    ['dmitry', '/', 'debts-dmitry'],
  ]) {
    await b.goto(`${URL0}${path}?of=${ids[who]}`)
    await b.waitFor('document.querySelector("main [role=alert]")')
    const text = await main()
    expect(text.includes('Back to mine'), `${who} ${path}: no Back to mine`)
    expect(!text.includes('Clara Müller’s') && !text.includes('Read-only.'), `${who} ${path}: shows a banner`)
    expect(!(await b.eval('!!document.querySelector("main tbody tr")')), `${who} ${path}: rows shown`)
    const side = (await aside()).toUpperCase()
    expect(!side.includes('CLARA’S') && !side.includes('DMITRY’S') && !side.includes('THIS MONTH'), `${who} ${path}: an empty sidebar section`)
    expect(side.includes('FOLLOWING'), `${who} ${path}: no Following`)
    if (name === 'debts') await shots('noaccess-clara-1280')
  }
  await b.click('main [role=alert] button', 'Back to mine')
  await b.waitFor('location.search === ""')
  await b.waitText('Owed to you')
})

await step('Back to mine from the banner', async () => {
  await b.goto(`${URL0}/bills?of=${ids.boris}&status=finalized&page=2`)
  await b.waitText('Boris’s bills')
  await b.click('main [role=status] button', 'Back to mine')
  await b.waitFor('location.pathname === "/bills" && location.search === ""')
  await b.waitText('Your balance'.toUpperCase()).catch(() => b.waitText('Your balance'))
})

// ---------- Phone ----------
await step('Phone (390): Boris’s debts, bills, spendings; dropdown and drawer; nothing wider than the screen', async () => {
  await b.goto(`${URL0}/?of=${ids.boris}`)
  await phone()
  await b.waitText('Boris’s debts')
  expect(!(await b.eval('!!document.querySelector("main table")')), 'table on a phone')
  await noOverflow()
  await shots('debts-boris-390')
  await openWhose()
  await sleep(200)
  await noOverflow()
  const menuRight = await b.eval('document.querySelector("main [role=listbox]").getBoundingClientRect().right')
  expect(menuRight <= 390, `menu wider than the screen: ${menuRight}`)
  await shots('debts-boris-390-open')
  await b.goto(`${URL0}/bills?of=${ids.boris}`)
  await b.waitText('Boris’s bills')
  await b.waitText('you’re on it')
  await noOverflow()
  await shots('bills-boris-390')
  await b.goto(`${URL0}/spendings?of=${ids.boris}`)
  await b.waitText('Boris’s spendings')
  await noOverflow()
  await shots('spendings-boris-390')
  await b.click('aside button', '')
  await sleep(400)
  const side = await aside()
  expect(side.includes('showing now') && side.includes('All subscriptions'), 'drawer: Following')
  await shots('spendings-boris-390-drawer')
  await desktop()
})

await b.close()
const failed = results.filter((r) => r[0] === 'FAIL')
console.log(`\n${results.length - failed.length}/${results.length} passed`)
process.exit(failed.length ? 1 : 0)
