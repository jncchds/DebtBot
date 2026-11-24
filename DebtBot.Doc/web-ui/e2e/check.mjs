// The browser check, in headless Chrome over the DevTools protocol, after seed.mjs (and api-check.mjs).
// 1. A tour of every page as admin (Anna), regular user (Boris) and subscriber (Clara), at 1280 and 390 px, light and
//    dark: the page loads its data, nothing is wider than the screen, the console stays clean; a screenshot of each.
// 2. Flows: sign-in/refresh/sign-out, Bills and the way back to the list, a draft's actions, New bill (form and
//    paste), Exchange, Contacts, Subscriptions, Profile, Admin (Users, Merge, All bills), Following.
// Usage: node check.mjs <screenshot dir> [--no-tour] [--no-flows]
import { mkdirSync } from 'node:fs'
import { launch } from './lib/cdp.mjs'
import { URL0, call, loadSeed, mint, sleep } from './lib/api.mjs'

const OUT = process.argv[2] ?? `${process.env.TMPDIR ?? '/tmp'}/debtbot-e2e`
const TOUR = !process.argv.includes('--no-tour')
const FLOWS = !process.argv.includes('--no-flows')
mkdirSync(OUT, { recursive: true })
const { ids, bills } = loadSeed()
const tok = { anna: mint(ids.anna, 'Anna Petrova', true), boris: mint(ids.boris, 'Boris Ivanov'), clara: mint(ids.clara, 'Clara Müller') }
const results = []

const b = await launch(Number(process.env.E2E_CDP_PORT ?? 9371))
for (const ev of ['uncaughtException', 'unhandledRejection'])
  process.on(ev, async (e) => {
    console.log('ABORT', e?.stack ?? e)
    await b.close()
    process.exit(1)
  })

// ---------- helpers ----------
const expect = (cond, msg) => {
  if (!cond) throw new Error(msg)
}
/** Console noise that is expected: the SPA asks for a refresh on load and gets 401 when signed out. */
const allowed = (p) => /status of 401/.test(p) || /auth\/refresh/.test(p)
async function step(name, fn, { allow = [] } = {}) {
  b.takeProblems()
  try {
    await fn()
    const problems = b.takeProblems().filter((p) => !allowed(p) && !allow.some((re) => re.test(p)))
    expect(problems.length === 0, `console: ${problems.join(' | ')}`)
    results.push(['PASS', name])
    console.log('PASS', name)
  } catch (e) {
    results.push(['FAIL', name, e.message])
    console.log('FAIL', name, '—', e.message)
    try {
      await b.shot(`${OUT}/FAIL-${results.length}.png`)
      console.log('  at', await b.path(), '|', (await b.text()).replace(/\s+/g, ' ').slice(0, 400))
    } catch {
      /* page gone */
    }
  }
}
const W = { desktop: [1280, 900], phone: [390, 844] }
let width = 'desktop'
async function size(w) {
  width = w
  await b.eval('try { localStorage.removeItem("debtbot.sidebar") } catch {}').catch(() => {})
  await b.viewport(...W[w], w === 'phone')
}
async function shots(name) {
  for (const dark of [false, true]) {
    await b.theme(dark)
    await sleep(250)
    await b.shot(`${OUT}/${name}-${W[width][0]}-${dark ? 'dark' : 'light'}.png`)
  }
  await b.theme(false)
}
const noOverflow = async () => {
  const w = await b.eval('document.documentElement.scrollWidth')
  const vw = await b.eval('window.innerWidth')
  const wide = await b.eval(
    `[...document.querySelectorAll('main *')].filter(e => e.getBoundingClientRect().right > window.innerWidth + 1 && !e.closest('[data-scroll-x]')).map(e => e.tagName + '.' + (e.className?.toString?.() ?? '').slice(0, 40)).slice(0, 3)`,
  )
  expect(w <= vw && wide.length === 0, `wider than the screen: ${w} > ${vw} ${JSON.stringify(wide)}`)
  // A table clipped by its card (overflow hidden) doesn't widen the page, so check it on its own.
  const clipped = await b.eval(`[...document.querySelectorAll('main table')].filter(t => t.scrollWidth > t.parentElement.clientWidth + 1).map(t => t.scrollWidth + ' > ' + t.parentElement.clientWidth)`)
  expect(clipped.length === 0, `a table is wider than its card: ${clipped.join(', ')}`)
}
const main = () => b.eval('document.querySelector("main")?.innerText ?? ""')
const h1 = () => b.eval('document.querySelector("main h1")?.innerText ?? ""')
const loaded = (text) => b.waitFor(`document.querySelector("main") && !document.querySelector("main [aria-busy=true]") && document.querySelector("main").innerText.includes(${JSON.stringify(text)})`, 20000)
async function signIn(login, password) {
  await b.goto(`${URL0}/login`)
  await b.waitFor('document.querySelector("input[autocomplete=username]") || location.pathname !== "/login"')
  if ((await b.path()) !== '/login') await signOut()
  for (let i = 0; ; i++) {
    await b.fill('input[autocomplete=username]', login)
    await b.fill('input[autocomplete=current-password]', password)
    await b.click('button.btn-primary')
    await b.waitFor('location.pathname !== "/login" || document.querySelector("[role=alert]")').catch(() => {})
    if ((await b.path()) !== '/login') return
    if (i === 3) throw new Error(`sign-in as ${login} failed 4 times: ${await main()}`)
    console.log('  sign-in refused (rate limit?), waiting 30 s')
    await sleep(30000)
  }
}
async function signOut() {
  if (width === 'phone') await size('desktop')
  await b.goto(`${URL0}/profile`)
  await b.waitFor('document.querySelector("aside")')
  await b.click('aside button', 'Sign out')
  await b.waitFor('location.pathname === "/login"')
}
/** Clicks a link or button whose text (or title/aria-label) includes `label`, inside the row or card including `row`. */
const clickIn = (row, label) =>
  b
    .eval(
      `(() => { const r = [...document.querySelectorAll('main tbody tr, main li, main section')].find(e => e.innerText.includes(${JSON.stringify(row)})); if (!r) return 'no row';
    const el = [...r.querySelectorAll('a,button')].find(e => (e.innerText + ' ' + (e.title || '') + ' ' + (e.getAttribute('aria-label') || '')).includes(${JSON.stringify(label)})); if (!el) return 'no button'; if (el.disabled) return 'disabled'; el.click(); return 'ok' })()`,
    )
    .then(async (r) => {
      expect(r === 'ok', `${row} / ${label}: ${r}`)
      await sleep(300)
    })
const hasIn = (row, label) =>
  b.eval(
    `(() => { const r = [...document.querySelectorAll('main tbody tr, main li')].find(e => e.innerText.includes(${JSON.stringify(row)})); if (!r) return null;
    return [...r.querySelectorAll('a,button')].some(e => (e.innerText + ' ' + (e.title || '') + ' ' + (e.getAttribute('aria-label') || '')).includes(${JSON.stringify(label)})) })()`,
  )
async function pick(inputSelector, typed, name) {
  await b.eval(`document.querySelector(${JSON.stringify(inputSelector)}).focus()`)
  await b.fill(inputSelector, typed)
  await b.waitFor(`[...document.querySelectorAll('main [role=option]')].some(o => o.innerText.includes(${JSON.stringify(name)}))`)
  await b.click('main [role=option]', name)
}
const fillLabel = (label, value, scope = 'main') =>
  b
    .eval(
      `(() => { const l = [...document.querySelectorAll(${JSON.stringify(scope + ' label')})].find(x => x.innerText.trim().startsWith(${JSON.stringify(label)})); if (!l) return null;
    const el = l.htmlFor ? document.getElementById(l.htmlFor) : l.querySelector('input,textarea,select'); if (!el) return null; el.setAttribute('data-e2e', '1'); return true })()`,
    )
    .then(async (ok) => {
      expect(ok, `no field ${label}`)
      await b.fill('[data-e2e="1"]', value)
      await b.eval('document.querySelector("[data-e2e=\\"1\\"]").removeAttribute("data-e2e")')
    })

// ---------- 1. Tour ----------
/** [name, path, text that shows the data loaded] per role. */
const tour = {
  admin: [
    ['debts', '/', 'Boris'],
    ['bills', '/bills', 'Pizza night'],
    ['bill-draft', `/bills/${bills.draft}`, 'Dinner at Molto'],
    ['bill-exchange', `/bills/${bills.exClara.forwardBillId}`, 'Exchange rate'],
    ['new-bill', '/bills/new', 'People on this bill'],
    ['new-bill-paste', '/bills/new/paste', 'Bill text'],
    ['exchange', '/exchange', 'New exchange'],
    ['spendings', '/spendings', 'Pizza night'],
    ['contacts', '/contacts', 'Mum'],
    ['subscriptions', '/subscriptions', 'Dmitry'],
    ['following', `/?of=${ids.boris}`, 'Boris'],
    ['profile', '/profile', 'Display name'],
    ['profile-password', '/profile/password', 'Change login or password'],
    ['profile-telegram', '/profile/telegram', '@anna_petrova'],
    ['admin-users', '/admin/users', 'Clara Müller'],
    ['admin-merge', '/admin/merge', 'Clara M'],
    ['admin-bills', '/admin/bills', '1–20 of'],
  ],
  regular: [
    ['debts', '/', 'Anna'],
    ['bills', '/bills', '1–20 of'],
    ['spendings', '/spendings', 'Concert tickets'],
    ['contacts', '/contacts', 'Grisha from work'],
    ['profile', '/profile', 'Display name'],
    ['admin-404', '/admin/users', 'Page not found'],
  ],
  subscriber: [
    ['debts-of-boris', `/?of=${ids.boris}`, 'Boris'],
    ['bills-of-boris', `/bills?of=${ids.boris}`, '1–20 of'],
    ['bill-of-boris', `/bills/${bills.borisDmitry}`, 'Board games #2'],
    ['spendings-of-boris', `/spendings?of=${ids.boris}`, 'Concert tickets'],
    ['no-access', `/bills?of=${ids.dmitry}`, 'Back to mine'],
  ],
}
const logins = { admin: ['anna', 'anna-pass-123'], regular: ['boris', 'boris-pass-123'], subscriber: ['clara', 'clara-pass-123'] }

if (TOUR)
  for (const role of ['admin', 'regular', 'subscriber']) {
    await size('desktop')
    await step(`tour ${role}: sign in`, () => signIn(...logins[role]))
    for (const w of ['desktop', 'phone']) {
      await size(w)
      for (const [name, path, text] of tour[role])
        await step(
          `tour ${role} ${W[w][0]}: ${name}`,
          async () => {
            await b.goto(URL0 + path)
            await loaded(text)
            await sleep(300)
            await noOverflow()
            await shots(`${role}-${name}`)
          },
          // The no-access view asks the API and gets 403 on purpose.
          { allow: name === 'no-access' ? [/status of 403/] : [] },
        )
    }
  }

// ---------- 2. Flows ----------
if (FLOWS) {
  await size('desktop')

  await step('sign-in: password, reload keeps the session (refresh cookie), sign-out ends it', async () => {
    await signIn('anna', 'anna-pass-123')
    await b.goto(`${URL0}/bills`)
    await loaded('Pizza night')
    await b.reload()
    await loaded('Pizza night')
    expect((await b.path()) === '/bills', 'reload lost the session')
    await signOut()
    await b.goto(`${URL0}/bills?status=draft`)
    await b.waitFor('location.pathname === "/login"')
    // Signing in returns you to the page you came from.
    await b.fill('input[autocomplete=username]', 'anna')
    await b.fill('input[autocomplete=current-password]', 'anna-pass-123')
    await b.click('button.btn-primary')
    await b.waitFor('location.pathname === "/bills"')
    expect((await b.path()).includes('status=draft'), `returned to ${await b.path()}`)
  })

  await step('Bills: filters in the URL, open a bill, "← Bills" returns to the filtered list', async () => {
    await b.goto(`${URL0}/bills?status=draft&sort=description&order=asc`)
    await loaded('Dinner at Molto')
    await b.click('main tbody a', 'Dinner at Molto')
    await b.waitFor(`location.pathname === "/bills/${bills.draft}"`)
    await b.waitFor('document.querySelector("main h1")?.innerText === "Dinner at Molto"')
    const back = await b.eval('[...document.querySelectorAll("main a")].find(a => a.innerText.includes("← Bills"))?.getAttribute("href")')
    expect(back === '/bills?status=draft&sort=description&order=asc', `back link ${back}`)
    await b.reload()
    await b.waitFor('document.querySelector("main h1")?.innerText === "Dinner at Molto"')
    await b.click('main a', '← Bills')
    await b.waitFor('location.pathname === "/bills"')
    expect((await b.path()) === '/bills?status=draft&sort=description&order=asc', `back to ${await b.path()}`)
    // From Spendings, the way back says Spendings; in a fresh tab (no history state) it's your own Bills.
    await b.goto(`${URL0}/spendings?sort=amount`)
    await b.waitFor('document.querySelector("main tbody a, main li a")')
    await b.eval('document.querySelector("main tbody a, main li a").click()')
    await b.waitFor('/^\\/bills\\/[0-9a-f-]{36}$/.test(location.pathname)')
    await b.waitFor('[...document.querySelectorAll("main a")].some(a => a.innerText.includes("← Spendings"))')
    await b.goto(`${URL0}/bills/${bills.pizza}`)
    await b.waitFor('[...document.querySelectorAll("main a")].some(a => a.innerText.includes("← Bills") && a.getAttribute("href") === "/bills")')
  })

  await step('A draft: add a line and a payment (each with its own URL), then finalize', async () => {
    await b.goto(`${URL0}/bills/${bills.draft}`)
    await b.waitFor('document.querySelector("main h1")?.innerText === "Dinner at Molto"')
    await b.click('main a', 'Add line')
    await b.waitFor(`location.pathname === "/bills/${bills.draft}/add-line"`)
    await fillLabel('Item', 'Wine', 'main form')
    await fillLabel('Subtotal', '24', 'main form')
    await b.click('main form button[type=submit]')
    await b.waitFor(`location.pathname === "/bills/${bills.draft}"`)
    await loaded('Wine')
    await b.click('main a', 'Add payment')
    await b.waitFor(`location.pathname === "/bills/${bills.draft}/add-payment"`)
    await fillLabel('Amount', '24', 'main form')
    await b.click('main form button[type=submit]')
    await b.waitFor(`location.pathname === "/bills/${bills.draft}"`)
    await b.waitFor('[...document.querySelectorAll("main button")].some(x => x.innerText.includes("Finalize") && !x.disabled)')
    await shots('flow-bill-draft')
    await b.click('main button', 'Finalize')
    await b.waitText('Finalized', 30000)
    const bill = await call(tok.anna, 'GET', `/Bills/${bills.draft}`)
    expect(bill.status === 'Processed' && bill.totalWithTips === 84, `API: ${bill.status} ${bill.totalWithTips}`)
  })

  await step('New bill, form: people, a line, a payment, save as draft', async () => {
    await b.goto(`${URL0}/bills/new`)
    await loaded('People on this bill')
    await fillLabel('Description', 'Ski pass')
    await fillLabel('Bill currency', 'eur')
    await pick('main input[placeholder^="Add a person"]', 'boris i', 'Boris Ivanov')
    await b.click('main a', 'Add line')
    await b.waitFor('location.pathname === "/bills/new/add-line"')
    await fillLabel('Item', 'Day pass', 'main form')
    await fillLabel('Subtotal', '90', 'main form')
    await b.click('main form button[type=submit]')
    await b.waitFor('location.pathname === "/bills/new"')
    await b.click('main a', 'Add payment')
    await b.waitFor('location.pathname === "/bills/new/add-payment"')
    await fillLabel('Amount', '90', 'main form')
    await b.click('main form button[type=submit]')
    await b.waitFor('location.pathname === "/bills/new"')
    await b.waitText('Boris Ivanov owes you')
    await shots('flow-new-bill')
    await b.click('main button', 'Save as draft')
    await b.waitFor('document.querySelector("main h1")?.innerText === "Ski pass"')
    expect((await main()).includes('Draft'), 'not a draft')
  })

  await step('New bill, paste text: preview, then create a draft', async () => {
    await b.goto(`${URL0}/bills/new/paste`)
    await loaded('Bill text')
    await b.fill('main textarea', 'Taxi home\n\n30\nEUR\n\n30 @anna_petrova\n\nTaxi\n30\n1 @anna_petrova\n1 @boris_iv')
    await b.click('main button', 'Preview')
    await b.waitText('Looks good')
    expect((await b.path()).includes('preview=1'), 'preview not in the URL')
    await shots('flow-paste')
    await b.click('main button', 'Save as draft')
    await b.waitFor('document.querySelector("main h1")?.innerText === "Taxi home"')
  })

  await step('Exchange: two amounts, create the drafts, accept', async () => {
    await b.goto(`${URL0}/exchange?with=${ids.grisha}`)
    await loaded('New exchange')
    await b.fill('main input[aria-label^="Amount: You give"]', '20')
    await b.fill('main input[aria-label^="Currency: You give"]', 'eur')
    await b.fill('main input[aria-label^="Amount: Grisha"]', '86')
    await b.fill('main input[aria-label^="Currency: Grisha"]', 'PLN')
    await b.waitText('1 EUR = 4.3000 PLN')
    await b.click('main button', 'Create exchange')
    await b.waitFor('location.search.includes("draft=")')
    await b.waitText('Exchange with Grisha')
    await shots('flow-exchange')
    await b.click('main button', 'Accept')
    await b.waitText('Accepted', 30000)
  })

  await step('Spendings: currency filter and the way into a bill', async () => {
    await b.goto(`${URL0}/spendings?currency=EUR`)
    await loaded('Pizza night')
    expect(!(await main()).includes('Concert tickets'), 'USD spending shown under EUR')
  })

  await step('Contacts: add a placeholder by @username, rename, remove', async () => {
    await b.goto(`${URL0}/contacts`)
    await loaded('Add by Telegram username')
    await b.fill('#add-username', 'newbie_e2e')
    await b.click('main form button[type=submit]')
    await b.waitText('@newbie_e2e added')
    const row = (name) => `[...document.querySelectorAll("main tbody tr")].find(r => r.innerText.includes(${JSON.stringify(name)}))`
    await b.eval(`${row('Mum')}.querySelector("button[title=Rename]").click()`)
    await b.waitFor('document.querySelector("main tbody input")')
    await b.fill('main tbody input', 'Mama')
    await b.eval('document.querySelector("main tbody form").requestSubmit()')
    await b.waitFor(`${row('Mama')} && !document.querySelector("main tbody input")`)
    await b.eval(`${row('newbie_e2e')}.querySelector("button[title=Remove]").click()`)
    await b.click('main tbody button.btn-danger', 'Remove')
    await b.waitFor(`!${row('newbie_e2e')}`)
    const c = await call(tok.anna, 'GET', '/UserContacts/Own')
    expect(c.some((x) => x.displayName === 'Mama') && !c.some((x) => x.telegramUserName === '@newbie_e2e'), 'API contacts')
  })

  await step('Subscriptions: confirm Dmitry’s request; the badge goes', async () => {
    await b.goto(`${URL0}/subscriptions`)
    await loaded('Dmitry')
    await b.click('main #requests button', 'Confirm')
    await b.waitFor('document.querySelector("#theirs")?.innerText.includes("Dmitry")')
    await b.waitFor(`![...document.querySelectorAll('aside a')].some(a => a.innerText.includes('Subscriptions') && /\\d/.test(a.innerText))`)
  })

  await step('Profile: save a new name, the sidebar follows; change it back', async () => {
    await b.goto(`${URL0}/profile`)
    await b.waitFor('document.querySelector("#p-name")?.value === "Anna Petrova"')
    await b.fill('#p-name', 'Anna P.')
    await b.click('main button[type=submit]')
    await b.waitText('Saved')
    await b.waitFor('document.querySelector("aside").innerText.includes("Anna P.")')
    await b.fill('#p-name', 'Anna Petrova')
    await b.click('main button[type=submit]')
    await b.waitFor('document.querySelector("aside").innerText.includes("Anna Petrova")')
  })

  await step('Admin › Users: no delete for admins, delete a stray placeholder', async () => {
    await b.goto(`${URL0}/admin/users`)
    await loaded('Clara Müller')
    expect((await hasIn('Dmitry K.', 'Delete')) === false, 'Dmitry (admin) has a delete button')
    expect((await hasIn('@grandpa_vlad', 'Delete')) === true, 'no delete for the stray placeholder')
    await clickIn('@grandpa_vlad', 'Delete')
    await b.waitText('Only stray placeholders can be deleted')
    await b.click('main button.btn-danger', 'Delete')
    await b.waitFor('![...document.querySelectorAll("main tbody tr")].some(r => r.innerText.includes("@grandpa_vlad"))')
  })

  await step('Admin › Merge: merge a suggested pair', async () => {
    await b.goto(`${URL0}/admin/merge`)
    await loaded('Suggestions')
    const before = (await call(tok.anna, 'GET', '/Users/merge/suggestions')).length
    await b.eval(`[...document.querySelectorAll('main section')[0].querySelectorAll('button')].find(x => x.innerText.includes('Merge…')).click()`)
    await b.waitText('This can’t be undone')
    await b.click('main .btn-danger', 'Merge')
    await b.waitText('Merged')
    await b.waitText(`Suggestions (${before - 1})`)
  })

  await step('Admin › All bills: a status filter, rows open the bill', async () => {
    await b.goto(`${URL0}/admin/bills?status=cancelled`)
    await loaded('Cinema tickets')
    await b.click('main tbody a', 'Cinema tickets')
    await b.waitFor(`location.pathname === "/bills/${bills.cancelled}"`)
    await b.waitFor('[...document.querySelectorAll("main a")].some(a => a.innerText.includes("← All bills"))')
  })

  await step('Following as Clara: Boris’s bills read-only; "← Bills" keeps ?of=', async () => {
    await signIn('clara', 'clara-pass-123')
    await b.goto(`${URL0}/bills?of=${ids.boris}&page=2`)
    await b.waitFor('document.querySelector("main tbody a")')
    await b.goto(`${URL0}/bills?of=${ids.boris}&q=board`)
    await loaded('Board games #2')
    await b.click('main tbody a', 'Board games #2')
    await b.waitFor(`location.pathname === "/bills/${bills.borisDmitry}"`)
    await loaded('Board games #2')
    const buttons = await b.eval('[...document.querySelectorAll("main button")].map(x => x.innerText.trim())')
    expect(!buttons.some((x) => /Finalize|Cancel|Add line|Add payment|Accept/.test(x)), `actions on a read-only bill: ${buttons}`)
    await b.click('main a', '← Bills')
    await b.waitFor('location.pathname === "/bills"')
    expect((await b.path()) === `/bills?of=${ids.boris}&q=board`, `back to ${await b.path()}`)
  })

  await step('Regular user: /admin/* is the 404 page', async () => {
    await signIn('boris', 'boris-pass-123')
    for (const p of ['/admin/users', '/admin/merge', '/admin/bills']) {
      await b.goto(URL0 + p)
      await loaded('Page not found')
    }
  })
}

await b.close()
const failed = results.filter((r) => r[0] === 'FAIL')
console.log(`\nbrowser: ${results.length - failed.length}/${results.length} passed${failed.length ? '\n' + failed.map((f) => `  FAIL ${f[1]} — ${f[2]}`).join('\n') : ''}`)
process.exit(failed.length ? 1 : 0)
