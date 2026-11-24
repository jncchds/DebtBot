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
      const stuck = b.pending()
      if (stuck.length) console.log('  requests still pending:', stuck.join(', '))
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
/**
 * Polls the API until the bill has `status` (finalizing runs in the background, through RabbitMQ, and can take a
 * while on a loaded machine), up to `seconds`. Then the page has to show it too: callers wait for the text after.
 */
async function waitStatus(id, status, seconds = 90) {
  let now
  for (let i = 0; i < seconds; i++) {
    now = (await call(tok.anna, 'GET', `/Bills/${id}`)).status
    if (now === status) return
    await sleep(1000)
  }
  throw new Error(`bill ${id} is ${now} after ${seconds} s, not ${status}`)
}
/**
 * After a finalize or accept: waits for the API status, then for `text` on the page. The page polls for 30 s and then
 * says "still processing" (by design), so when the background work took longer it needs a reload to show the result.
 */
async function seeStatus(id, status, text) {
  await waitStatus(id, status)
  const shown = await b.waitText(text, 5000).catch(() => false)
  if (!shown) {
    console.log(`  ${text} not shown after the poll window; reloading`)
    await b.reload()
    await b.waitText(text, 20000)
  }
}
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
    ['bill-draft-edit-line', `/bills/${bills.draft}/edit-line/1`, 'Save line'],
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
    ['profile-tokens', '/profile/tokens', 'No tokens yet'],
    ['profile-tokens-new', '/profile/tokens/new', 'Create token'],
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
    ['profile-tokens', '/profile/tokens', 'No tokens yet'],
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

  // Since series-2 T1 a draft is the New bill form: the line and payment stay in the form until Save and finalize.
  await step('A draft: add a line and a payment (each with its own URL), then save and finalize', async () => {
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
    await loaded('Unsaved changes')
    await b.waitFor('[...document.querySelectorAll("main button")].some(x => x.innerText.includes("Save and finalize") && !x.disabled)')
    await shots('flow-bill-draft')
    await b.click('main button', 'Save and finalize')
    await seeStatus(bills.draft, 'Processed', 'Finalized')
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

  // ----- Series 2, T1: anyone can leave a bill's people, including you; a draft is the New bill form -----
  /** The chips of "People on this bill": [name, removable]. */
  const chips = () =>
    b.eval(`[...([...document.querySelectorAll('main section')].find(s => s.innerText.includes('People on this bill'))?.querySelectorAll('span.inline-flex') ?? [])]
      .map(c => [c.firstChild.textContent, !!c.querySelector('button')])`)
  const chip = async (name) => (await chips()).find(([n]) => n.startsWith(name))
  const lineText = (item) => b.eval(`[...document.querySelectorAll('main li')].find(l => l.querySelector('b')?.innerText === ${JSON.stringify(item)})?.innerText ?? ''`)
  const editLine = async (base, item, n) => {
    await b.click(`main a[aria-label="Edit ${item}"]`)
    await b.waitFor(`location.pathname === "${base}/edit-line/${n}"`)
    await loaded('Save line')
  }
  let leftId

  await step('New bill: locks (payer, by parts) and ✎ Edit line; you leave the bill, the preview has no "you"', async () => {
    await b.eval('sessionStorage.clear()')
    await b.goto(`${URL0}/bills/new`)
    await loaded('People on this bill')
    await fillLabel('Description', 'Dinner without me')
    await fillLabel('Bill currency', 'eur')
    await pick('main input[placeholder^="Add a person"]', 'boris i', 'Boris Ivanov')
    await pick('main input[placeholder^="Add a person"]', 'dmi', 'Dmitry K.')
    await b.click('main a', 'Add line')
    await b.waitFor('location.pathname === "/bills/new/add-line"')
    await fillLabel('Item', 'Pasta', 'main form')
    await fillLabel('Subtotal', '60', 'main form')
    await b.click('main form button[type=submit]')
    await b.waitFor('location.pathname === "/bills/new"')
    expect((await lineText('Pasta')).includes('Anna'), 'you are not on the new line')
    expect((await chip('Anna'))?.[1], 'on an equal line only, your chip should be removable')
    // A payer is locked.
    await b.click('main a', 'Add payment')
    await b.waitFor('location.pathname === "/bills/new/add-payment"')
    expect((await b.eval('document.querySelector("main form select").value')) === ids.anna, 'you are not the preselected payer')
    await fillLabel('Who paid', ids.boris, 'main form')
    await fillLabel('Amount', '60', 'main form')
    await b.click('main form button[type=submit]')
    await b.waitFor('location.pathname === "/bills/new"')
    expect((await chip('Boris'))?.[1] === false, 'the payer is removable')
    // ✎ Edit line on New bill: by parts locks everyone on the line; back to equal unlocks them.
    await editLine('/bills/new', 'Pasta', 1)
    await b.click('main form button[aria-pressed]', 'By parts')
    await b.fill('main form input[aria-label="Part for Dmitry K."]', '2')
    await b.click('main form button[type=submit]')
    await b.waitFor('location.pathname === "/bills/new"')
    expect((await lineText('Pasta')).includes('by parts'), `Pasta isn't by parts: ${await lineText('Pasta')}`)
    expect((await chip('Dmitry'))?.[1] === false && (await chip('Anna'))?.[1] === false, `a by-parts line doesn't lock: ${JSON.stringify(await chips())}`)
    await editLine('/bills/new', 'Pasta', 1)
    await b.click('main form button[aria-pressed]', 'Equally')
    await b.click('main form button[type=submit]')
    await b.waitFor('location.pathname === "/bills/new"')
    expect((await chip('Dmitry'))?.[1] === true, 'equal again, Dmitry should be removable')
    // You leave: off Pasta, and the preview is only about the others.
    await b.click('main button[aria-label^="Remove Anna"]')
    await sleep(200)
    expect(!(await chip('Anna')), 'you are still on the bill')
    expect(!(await lineText('Pasta')).includes('Anna'), 'you are still on Pasta')
    const m = await main()
    expect(m.includes('Dmitry K. owes Boris Ivanov') && !/owes you|You owe/.test(m), 'the preview should be "Dmitry K. owes Boris Ivanov" only')
    await b.reload()
    await loaded('People on this bill')
    expect(!(await chip('Anna')), 'a reload brought you back')
    await b.click('main a', 'Add payment')
    await b.waitFor('location.pathname === "/bills/new/add-payment"')
    expect((await b.eval('document.querySelector("main form select").value')) === ids.boris, 'without you, the first person should be the preselected payer')
    await b.click('main form button', 'Cancel')
    await b.waitFor('location.pathname === "/bills/new"')
  })

  await step('…save as draft: it opens as the form, without you, and stays in your Bills', async () => {
    await b.click('main button', 'Save as draft')
    await b.waitFor('/^\\/bills\\/[0-9a-f-]{36}$/.test(location.pathname)', 20000)
    leftId = (await b.path()).split('/')[2]
    await loaded('Saved.')
    expect(!(await chip('Anna')), 'the draft shows you on the bill')
    const bill = await call(tok.anna, 'GET', `/Bills/${leftId}`)
    expect(bill.creator.id === ids.anna && bill.lines[0].participants.every((p) => p.user.id !== ids.anna), 'API: not yours, or you are on the line')
    expect((await call(tok.anna, 'GET', '/Bills?status=Draft&countPerPage=100')).items.some((x) => x.id === leftId), 'the draft is not in your Bills')
    await shots('flow-draft-form')
  })

  await step('…on the draft: add yourself back from the search, tick yourself in ✎ Edit line, reload, Save (PUT)', async () => {
    await pick('main input[placeholder^="Add a person"]', 'ann', 'Anna Petrova (you)')
    expect(await chip('Anna'), 'not added back')
    expect((await main()).includes('Saved.'), 'adding a person alone shows unsaved changes')
    await editLine(`/bills/${leftId}`, 'Pasta', 1)
    await shots('flow-draft-edit-line')
    await b.eval(`[...document.querySelectorAll('main form li')].find(l => l.innerText.includes('Anna')).querySelector('input[type=checkbox]').click()`)
    await b.click('main form button[type=submit]')
    await b.waitFor(`location.pathname === "/bills/${leftId}"`)
    await loaded('Unsaved changes')
    await b.reload()
    await loaded('Unsaved changes')
    expect((await lineText('Pasta')).includes('Anna'), 'the edit was lost on reload')
    await b.click('main button', 'Save')
    await loaded('Saved.')
    const bill = await call(tok.anna, 'GET', `/Bills/${leftId}`)
    expect(bill.lines[0].participants.some((p) => p.user.id === ids.anna), 'you are not on the saved line')
  })

  await step('…remove yourself again (an equal line), Save, Finalize: Dmitry owes Boris 30, nothing for you', async () => {
    expect((await chip('Anna'))?.[1], 'your chip is locked on an equal line')
    await b.click('main button[aria-label^="Remove Anna"]')
    await loaded('Unsaved changes')
    await b.click('main button', 'Save')
    await loaded('Saved.')
    let bill = await call(tok.anna, 'GET', `/Bills/${leftId}`)
    expect(bill.lines[0].participants.map((p) => p.user.id).sort().join() === [ids.boris, ids.dmitry].sort().join(), 'Pasta is not Boris and Dmitry')
    await b.click('main button', 'Finalize')
    await seeStatus(leftId, 'Processed', 'Finalized')
    bill = await call(tok.anna, 'GET', `/Bills/${leftId}`)
    const r = bill.ledgerRecords
    expect(r.length === 1 && r[0].debtorUser.id === ids.dmitry && r[0].creditorUser.id === ids.boris && r[0].amount === 30, `ledger ${JSON.stringify(r)}`)
    expect(bill.spendings.every((s) => (s.user?.id ?? s.userId) !== ids.anna), 'you have a spending')
    expect((await call(tok.anna, 'GET', '/Bills?countPerPage=100')).items.some((x) => x.id === leftId), 'the bill left your Bills')
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
    await seeStatus(new URLSearchParams(new URL(await b.eval('location.href')).search).get('draft'), 'Processed', 'Accepted')
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

  // ----- Series 2, T2: Profile › API tokens and Admin › Users revoke-all (the API side is in api-check.mjs) -----
  const tokenStatus = async (token, path = '/Debts/Own') => (await fetch(`${URL0}/api/v1${path}`, { headers: { Authorization: `Bearer ${token}` } })).status
  /** Clicks the ▾ of the visible row (table or card) that shows `name`. */
  const openRow = (name) =>
    b.eval(`(() => { const el = [...document.querySelectorAll('main button[aria-label]')].find(e => e.getAttribute('aria-label').startsWith('Actions: ' + ${JSON.stringify(name)}) && e.offsetParent); if (!el) return false; el.click(); return true })()`)
  const stripEl = 'document.querySelector("main tbody td[colspan]")'
  async function newToken(name, scope, expiry) {
    await b.goto(`${URL0}/profile/tokens`)
    await loaded('Your tokens')
    await b.click('main a', 'New token')
    await b.waitFor('location.pathname === "/profile/tokens/new"')
    await b.fill('#tok-name', name)
    await b.click('main label', scope)
    await b.click('main [role=radio]', expiry)
    await b.click('main button[type=submit]', 'Create token')
    await b.waitFor('location.pathname === "/profile/tokens/created"')
    const secret = await b.eval('document.querySelector("[data-testid=token-secret]")?.innerText.trim()')
    expect(/^dbt_[A-Za-z0-9_-]{43}$/.test(secret ?? ''), `secret shown: ${secret}`)
    const leaked = await b.eval(`[location.href, JSON.stringify({...localStorage}), JSON.stringify({...sessionStorage})].some(s => s.includes(${JSON.stringify(secret)}))`)
    expect(!leaked, 'the secret is in the URL or storage')
    return secret
  }
  let readToken, writeToken

  await step('Profile › API tokens: empty, the sidebar entry; New token preselects Read only and 90 days; a name is needed', async () => {
    await b.goto(`${URL0}/profile/tokens`)
    await loaded('No tokens yet')
    await b.waitFor('[...document.querySelectorAll("aside a")].some(a => a.innerText.includes("API tokens") && a.className.includes("text-accent"))')
    await b.click('main a', 'New token')
    await b.waitFor('location.pathname === "/profile/tokens/new"')
    expect(await b.eval('[...document.querySelectorAll("main input[type=radio]")].find(r => r.checked)?.closest("label").innerText.startsWith("Read only")'), 'Read only preselected')
    expect(await b.eval('document.querySelector("main [role=radio][aria-checked=true]")?.innerText === "90 days"'), '90 days preselected')
    await b.click('main button[type=submit]', 'Create token')
    await loaded('Enter a name (1–100 characters).')
  })

  await step('…a read-only token: shown once with the warning and curl, it reads; a reload of /created goes to the list', async () => {
    readToken = await newToken('Home Assistant', 'Read only', '90 days')
    const text = await main()
    expect(text.includes("You won't see it again") && text.includes(`curl -H "Authorization: Bearer ${readToken}"`) && text.includes('/api/v1/Debts/Own'), 'warning and curl example')
    await shots('flow-token-created')
    expect((await tokenStatus(readToken)) === 200, 'the new token can’t read')
    await b.reload()
    await b.waitFor('location.pathname === "/profile/tokens"')
    await loaded('Home Assistant')
    expect(!(await main()).includes(readToken), 'the secret is still shown after a reload')
    expect((await main()).includes(`dbt_${readToken.slice(4, 12)}…`), 'the list doesn’t show the prefix')
  })

  await step('…a read-and-write token that never expires; Done; revoke it from its ▾ strip (?open=): 401', async () => {
    writeToken = await newToken('Budget script', 'Read and write', 'Never')
    await b.click('main a', 'Done')
    await b.waitFor('location.pathname === "/profile/tokens"')
    await loaded('Budget script')
    const list = await call(tok.anna, 'GET', '/profile/tokens')
    const rw = list.find((x) => x.name === 'Budget script')
    const ro = list.find((x) => x.name === 'Home Assistant')
    expect(list.length === 2 && rw.scope === 'ReadWrite' && rw.expiresAt === null && ro.scope === 'Read', 'API list')
    expect(Math.round((Date.parse(ro.expiresAt) - Date.parse(ro.createdAt)) / 86400000) === 90, 'read-only isn’t 90 days')
    expect((await tokenStatus(writeToken)) === 200, 'the read-and-write token can’t read')
    expect(await openRow('Budget script'), 'no ▾ for Budget script')
    await b.waitFor(`location.search.includes("open=${rw.id}") && ${stripEl}`)
    await shots('flow-tokens-strip')
    await b.click('main tbody td[colspan] button', 'Revoke')
    await b.waitText('This can')
    await b.click('main button.btn-danger', 'Revoke')
    await b.waitFor(`!location.search.includes("open=") && [...document.querySelectorAll("main tbody tr")].some(r => r.innerText.includes("Budget script") && r.innerText.includes("Revoked"))`)
    expect((await tokenStatus(writeToken)) === 401, 'the revoked token still works')
    expect((await tokenStatus(readToken)) === 200, 'the other token stopped working')
  })

  await step('Admin › Users: Boris’s token count, ▾ (?open=) Revoke all; reload keeps the strip, Back closes it', async () => {
    const b1 = await call(tok.boris, 'POST', '/profile/tokens', { name: 'Boris phone', scope: 'Read', expiresInDays: 365 })
    await call(tok.boris, 'POST', '/profile/tokens', { name: 'Boris laptop', scope: 'ReadWrite', expiresInDays: null })
    const borisRow = `[...document.querySelectorAll("main tbody tr")].find(r => r.innerText.includes("Boris Ivanov") && !r.querySelector("td[colspan]"))`
    await b.goto(`${URL0}/admin/users?search=boris`)
    await b.waitFor(`${borisRow}?.querySelectorAll("td")[7]?.innerText.trim() === "2"`, 20000)
    expect(await openRow('Boris Ivanov'), 'no ▾ for Boris')
    await b.waitFor(`location.search.includes("open=${ids.boris}") && ${stripEl}?.innerText.includes("Revoke all 2 API tokens")`)
    await b.reload()
    await b.waitFor(`${stripEl}?.innerText.includes("Revoke all 2 API tokens")`, 20000)
    await b.click('main tbody td[colspan] button', 'Revoke all 2 API tokens')
    await b.waitText('Scripts and apps using them stop working at once')
    await b.click('main button.btn-danger', 'Revoke all')
    await b.waitFor(`${borisRow}?.querySelectorAll("td")[7]?.innerText.trim() === "0"`)
    expect((await tokenStatus(b1.token)) === 401, 'Boris’s token still works')
    await b.waitFor(`location.search.includes("open=") && ${stripEl}`)
    await b.back()
    await b.waitFor(`!location.search.includes("open=") && !${stripEl}`)
  })

  await step('Swagger: Authorize with the read-only token, then GET /api/v1/profile answers 200', async () => {
    await b.goto(`${URL0}/swagger/index.html`)
    await b.waitFor('document.querySelector("button.authorize")', 30000)
    await b.eval('document.querySelector("button.authorize").click()')
    await b.waitFor('document.querySelector(".modal-ux input")')
    const hint = await b.eval('document.querySelector(".modal-ux").innerText')
    expect(hint.includes('dbt_') && hint.includes('Profile'), `the hint: ${hint.slice(0, 200)}`)
    await b.fill('.modal-ux input', readToken)
    await b.eval('document.querySelector(".modal-ux button.authorize").click()')
    await sleep(300)
    await b.eval('document.querySelector(".modal-ux button.btn-done, .modal-ux .close-modal").click()')
    const op = '#operations-Profile-get_api_v1_profile'
    await b.waitFor(`document.querySelector("${op}")`)
    await sleep(500)
    // Open the operation with its arrow button (a click on the summary isn't picked up from script).
    await b.eval(`document.querySelector("${op}.is-open") || document.querySelector("${op} .opblock-control-arrow").click()`)
    await b.waitFor(`document.querySelector("${op} .try-out__btn")`)
    await b.eval(`document.querySelector("${op} .try-out__btn").click()`)
    await b.waitFor(`document.querySelector("${op} .execute")`)
    await b.eval(`document.querySelector("${op} .execute").click()`)
    await b.waitFor(`document.querySelector("${op} .live-responses-table tbody .response-col_status")`, 20000)
    const code = await b.eval(`document.querySelector("${op} .live-responses-table tbody .response-col_status").innerText.trim()`)
    expect(code === '200', `Swagger response ${code}`)
  }, { allow: [/swagger/i] })

  await step('Admin › Users: no delete for admins, delete a stray placeholder', async () => {
    await b.goto(`${URL0}/admin/users`)
    await loaded('Clara Müller')
    // Rows show values only; ▾ opens the row's action strip (series 2, T2), the next table row.
    const strip = `document.querySelector("main tbody tr td[colspan]")`
    const stripHas = (label) => b.eval(`!![...(${strip}?.querySelectorAll('a,button') ?? [])].some(e => e.innerText.includes(${JSON.stringify(label)}))`)
    await clickIn('Dmitry K.', 'Actions')
    await b.waitFor(`location.search.includes("open=") && ${strip}`)
    expect((await stripHas('Remove admin')) === true, 'Dmitry’s strip has Remove admin')
    expect((await stripHas('Delete')) === false, 'Dmitry (admin) has a delete button')
    await clickIn('@grandpa_vlad', 'Actions')
    await b.waitFor(`${strip}?.innerText.includes("Set login and password")`)
    expect((await stripHas('Delete')) === true, 'no delete for the stray placeholder')
    await b.click('main tbody td[colspan] button', 'Delete')
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
