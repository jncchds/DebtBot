// T7 runtime check in headless Chrome. Usage: node check7.mjs <screenshot dir>
import { readFileSync } from 'node:fs'
import { launch } from './cdp.mjs'
import { mint } from './seed7.mjs'

const URL0 = 'http://localhost:18097'
const API = `${URL0}/api/v1`
const OUT = process.argv[2]
const seed = JSON.parse(readFileSync(new URL('./seed7.json', import.meta.url)))
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
    try {
      await b.shot(`${process.env.TMPDIR ?? '/tmp'}/fail-${results.length}.png`)
      console.log('  at', await b.path(), '|', (await b.text()).replace(/\s+/g, ' ').slice(0, 300))
    } catch {
      /* page gone */
    }
  }
}
const expect = (cond, msg) => {
  if (!cond) throw new Error(msg)
}

const tokenFor = (login, password) =>
  fetch(`${API}/auth/login`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ login, password }) })
    .then((r) => r.json())
    .then((s) => s.accessToken)
const anna = await tokenFor('anna', 'anna-pass-123')
const dmitryToken = await tokenFor('dmitry', 'dmitry-pass-123')
const get = (token, path) => fetch(API + path, { headers: { Authorization: `Bearer ${token}` } }).then((r) => r.json())

const b = await launch(9347)
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
  await b.eval('fetch("/api/v1/auth/logout", { method: "POST" })')
  await sleep(300)
  await b.goto(`${URL0}/login`)
  await b.waitFor('document.querySelector("input[autocomplete=username]")')
  await b.fill('input[autocomplete=username]', login)
  await b.fill('input[autocomplete=current-password]', password)
  // The auth endpoints allow 10 requests a minute per IP; this check signs in often, so wait out the limit.
  for (let i = 0; ; i++) {
    await b.click('button.btn-primary')
    try {
      await b.waitFor('location.pathname !== "/login" || document.body.innerText.includes("Sign-in failed")')
    } catch {
      /* still waiting */
    }
    if ((await b.path()) !== '/login') return
    if (i === 3) throw new Error('sign-in failed 4 times')
    console.log('  sign-in limited, waiting 30 s')
    await sleep(30000)
  }
}
/** The number on the 🔔 Subscriptions nav button, 0 when there's no badge. */
const badge = () => b.eval(`(() => { const a = [...document.querySelectorAll('nav a')].find(x => x.getAttribute('href') === '/subscriptions'); const s = a && [...a.querySelectorAll('span')].find(x => /^\\d+$/.test(x.innerText.trim())); return s ? Number(s.innerText) : 0 })()`)
const waitBadge = async (n) => {
  for (let i = 0; i < 40; i++) {
    if ((await badge()) === n) return
    await sleep(250)
  }
  throw new Error(`badge ${await badge()}, expected ${n}`)
}
const firstRowText = () => b.eval('(document.querySelector("main tbody tr") || {}).innerText || ""')

await step('sign in as Anna', () => signIn('anna', 'anna-pass-123'))

// ---------- Spendings ----------
await step('Spendings: default list, newest first, cancelled hidden; This month in the sidebar matches the API', async () => {
  const api = await get(anna, '/Spendings/Own?countPerPage=20')
  await b.goto(`${URL0}/spendings`)
  await b.waitText(`1–20 of ${api.totalCount}`)
  const t = await b.text()
  expect(!t.includes('Concert tickets'), 'cancelled spending shown by default')
  expect(t.includes('33 % of the bill') && t.includes('paid as 74.42 EUR'), 'no share or paid-as line')
  expect((await firstRowText()).includes(api.items[0].description), `first row isn't ${api.items[0].description}`)
  const month = await get(anna, '/Spendings/Own/summary?dateFrom=2026-11-01&dateTo=2026-11-30')
  expect(t.includes('THIS MONTH') || t.includes('This month'), 'no This month section')
  for (const m of month) expect(t.includes(`November · ${m.count} spending`), `no "${m.currencyCode}: November · ${m.count}" in the sidebar`)
  await shots('spendings-1280')
})

await step('Spendings: show cancelled, sort by amount, paging; reload and Back keep them', async () => {
  await b.click('main label', 'Show cancelled')
  await b.waitFor('location.search.includes("cancelled=1")')
  await b.waitText('Concert tickets')
  expect((await b.text()).includes('Cancelled'), 'no Cancelled pill')
  await b.fill('#list-sort', 'amount')
  await b.waitFor('location.search.includes("sort=amount")')
  const api = await get(anna, '/Spendings/Own?includeCancelled=true&sortBy=Amount&countPerPage=20')
  await b.waitFor(`(document.querySelector("main tbody tr") || {}).innerText?.includes(${JSON.stringify(api.items[0].description)})`)
  await b.click('main nav[aria-label] button', '2')
  await b.waitFor('location.search.includes("page=2")')
  await b.waitText(`21–${api.totalCount} of ${api.totalCount}`)
  await b.reload()
  await b.waitText(`21–${api.totalCount} of ${api.totalCount}`)
  expect((await b.path()) === '/spendings?cancelled=1&sort=amount&page=2', `path ${await b.path()}`)
  await b.back()
  expect((await b.path()) === '/spendings?cancelled=1&sort=amount', `after Back ${await b.path()}`)
  await b.back()
  expect((await b.path()) === '/spendings?cancelled=1', `after Back ${await b.path()}`)
  await shots('spendings-cancelled-1280')
})

await step('Spendings: This month entry filters by currency and month; filters can match nothing', async () => {
  await b.goto(`${URL0}/spendings`)
  await b.waitText('This month'.toUpperCase())
  await b.click('nav a', 'EUR')
  await b.waitFor('location.search.includes("currency=EUR") && location.search.includes("from=2026-11-01") && location.search.includes("to=2026-11-30")')
  const month = (await get(anna, '/Spendings/Own/summary?dateFrom=2026-11-01&dateTo=2026-11-30')).find((m) => m.currencyCode === 'EUR')
  await b.waitText(`1–${month.count} of ${month.count}`)
  await b.goto(`${URL0}/spendings?currency=USD&from=2026-12-01`)
  await b.waitText('No spendings match these filters')
  await shots('spendings-filtered-empty-1280')
  await b.click('main button', 'Clear filters')
  await b.waitFor('location.search === ""')
})

// ---------- Contacts ----------
await step('Contacts: add an existing contact, a placeholder and a bad username', async () => {
  await b.goto(`${URL0}/contacts`)
  await b.waitText('Add by Telegram username')
  expect((await b.text()).includes('Dmitry wants notifications about your bills and debts.'), 'no request banner')
  await shots('contacts-1280')
  await b.fill('#add-username', '@Boris_IV')
  await b.click('main form button[type=submit]')
  await b.waitText('Boris is already in your contacts.')
  await b.fill('#add-username', 'ab')
  await b.click('main form button[type=submit]')
  await b.waitText("That isn't a Telegram username")
  await b.fill('#add-username', 'grandpa_vlad')
  await b.click('main form button[type=submit]')
  await b.waitText("@grandpa_vlad added. They haven't started the bot yet")
  expect((await b.eval('document.querySelector("#add-username").value')) === '', 'input not cleared')
  const hl = await b.eval('[...document.querySelectorAll("main tbody tr")].find(r => r.className.includes("bg-accent-faint"))?.innerText ?? ""')
  expect(hl.includes('grandpa_vlad') && hl.includes('never started the bot'), `highlighted row: ${hl}`)
  await b.waitFor('document.querySelector("nav").innerText.includes("today · never started the bot")')
  await shots('contacts-added-1280')
})

await step('Contacts: search in the URL, kept over a reload', async () => {
  await b.fill('main input[type=search]', 'ivan')
  await b.waitFor('location.search === "?q=ivan"')
  await b.waitText('1 of 5')
  const rows = await b.eval('[...document.querySelectorAll("main tbody tr")].map(r => r.innerText)')
  expect(rows.length === 1 && rows[0].includes('Boris'), `rows ${JSON.stringify(rows)}`)
  await b.reload()
  await b.waitText('1 of 5')
  expect((await b.eval('document.querySelector("main input[type=search]").value')) === 'ivan', 'search box lost on reload')
  await b.fill('main input[type=search]', 'zzz')
  await b.waitText('No contacts match')
  await shots('contacts-search-empty-1280')
  await b.fill('main input[type=search]', '')
  await b.waitFor('location.search === ""')
})

await step('Contacts: rename inline (Enter saves) and remove with the inline confirmation', async () => {
  const row = (name) => `[...document.querySelectorAll("main tbody tr")].find(r => r.innerText.includes(${JSON.stringify(name)}))`
  await b.eval(`${row('Mum')}.querySelector("button[title=Rename]").click()`)
  await b.waitFor('document.querySelector("main tbody input")')
  await b.fill('main tbody input', 'Mama')
  await b.eval('document.querySelector("main tbody form").requestSubmit()')
  await b.waitFor(`${row('Mama')} && !document.querySelector("main tbody input")`)
  const c = await get(anna, '/UserContacts/Own')
  expect(c.find((x) => x.id === ids.mum)?.displayName === 'Mama', 'rename not saved')
  await b.eval(`${row('grandpa_vlad')}.querySelector("button[title=Remove]").click()`)
  await b.waitText('Remove @grandpa_vlad from your contacts?')
  await shots('contacts-remove-1280')
  await b.click('main tbody button.btn-danger', 'Remove')
  await b.waitFor(`!${row('grandpa_vlad')}`)
  const after = await get(anna, '/UserContacts/Own')
  expect(!after.some((x) => x.telegramUserName === '@grandpa_vlad'), 'still a contact')
})

await step('Contacts: ask for Dmitry’s bills', async () => {
  const row = `[...document.querySelectorAll("main tbody tr")].find(r => r.innerText.includes("Dmitry"))`
  await b.eval(`${row}.querySelector("button.btn-secondary").click()`)
  await b.waitFor(`${row}.innerText.includes("Asked, waiting")`)
  const c = await get(anna, '/UserContacts/Own')
  expect(c.find((x) => x.id === ids.dmitry)?.mySubscription === 'Pending', 'not pending')
})

// ---------- Subscriptions ----------
await step('Subscriptions as Anna: badge 1, Review from the banner, confirm Dmitry → badge gone', async () => {
  await b.goto(`${URL0}/contacts`)
  await b.waitText('Review')
  await waitBadge(1)
  await b.click('main a', 'Review')
  await b.waitFor('location.pathname === "/subscriptions"')
  await b.waitText('people asking for your bills and debts')
  const t = await b.text()
  expect(t.includes('1 waiting for you') && t.includes('1 confirmed · 2 waiting'), 'summary sub-section wrong')
  await shots('subscriptions-1280')
  await b.click('main #requests button', 'Confirm')
  await waitBadge(0)
  await b.waitFor('document.querySelector("#theirs").innerText.includes("Dmitry")')
  expect((await b.text()).includes('No requests.'), 'requests card not empty')
})

await step('Subscriptions as Dmitry: confirm, decline, stop both ways and withdraw, with the badge count', async () => {
  await signIn('dmitry', 'dmitry-pass-123')
  await b.goto(`${URL0}/subscriptions`)
  await b.waitText('people asking for your bills and debts')
  // Clara asked in the seed; Anna asked from Contacts.
  await waitBadge(2)
  await shots('subscriptions-dmitry-1280')
  const card = (id, name) => `[...document.querySelectorAll("#${id} li")].find(li => li.innerText.includes(${JSON.stringify(name)}))`
  await b.eval(`${card('requests', 'Anna')}.querySelector("button.btn-primary").click()`)
  await waitBadge(1)
  await b.eval(`${card('requests', 'Clara')}.querySelector("button.btn-secondary").click()`)
  await waitBadge(0)
  await b.waitFor(`!${card('requests', 'Clara')}`)
  await b.eval(`${card('theirs', 'Boris')}.querySelector("button").click()`)
  await b.waitFor(`!${card('theirs', 'Boris')}`)
  await b.eval(`${card('yours', 'Boris')}.querySelector("button").click()`)
  await b.waitFor(`!${card('yours', 'Boris')}`)
  expect(await b.eval(`${card('yours', 'Clara')}.innerText.includes("Waiting for")`), 'no pending Clara')
  await b.eval(`${card('yours', 'Clara')}.querySelector("button").click()`)
  // Only Anna's is left: she confirmed Dmitry's request earlier in this run.
  await b.waitFor(`!${card('yours', 'Clara')} && ${card('yours', 'Anna')}.innerText.includes("You get theirs")`)
  const c = await get(dmitryToken, '/UserContacts/Own')
  const s = Object.fromEntries(c.map((x) => [x.telegramUserName, `${x.mySubscription}/${x.theirSubscription}`]))
  expect(s['@anna_p'] === 'Confirmed/Confirmed' && s['@boris_iv'] === 'None/None' && s['@clara_m'] === 'None/None', `API states ${JSON.stringify(s)}`)
  await shots('subscriptions-dmitry-after-1280')
})

// ---------- Profile ----------
await step('Profile details: validation in the browser, save, the sidebar name updates', async () => {
  await signIn('anna', 'anna-pass-123')
  await b.goto(`${URL0}/profile`)
  await b.waitFor('document.querySelector("#p-name")?.value === "Anna Petrova"')
  await b.fill('#p-name', '')
  await b.fill('#p-email', 'anna.example.com')
  await b.click('main button[type=submit]')
  await b.waitText('Enter a name (1–100 characters).')
  expect((await b.text()).includes('An email needs an @.'), 'no email error')
  await shots('profile-details-error-1280')
  await b.fill('#p-name', 'Anna P.')
  await b.fill('#p-email', 'anna.p@example.com')
  await b.click('main button[type=submit]')
  await b.waitText('✓ Saved')
  await b.waitFor('document.querySelector("aside").innerText.includes("Sign out (Anna P.)")')
  const p = await get(anna, '/profile')
  expect(p.displayName === 'Anna P.' && p.email === 'anna.p@example.com', 'not saved')
  await b.fill('#p-name', 'Anna Petrova')
  await b.click('main button[type=submit]')
  await b.waitFor('document.querySelector("aside").innerText.includes("Sign out (Anna Petrova)")')
  await shots('profile-details-1280')
})

await step('Login & password as Anna: 409 for a taken login, the API message for a wrong current password', async () => {
  await b.goto(`${URL0}/profile/password`)
  await b.waitText('Change login or password')
  expect((await b.text()).includes('Password set'), 'no Password set pill')
  await b.fill('#pw-login', 'boris')
  await b.fill('#pw-current', 'anna-pass-123')
  await b.click('main button[type=submit]')
  await b.waitText('The login “boris” is taken. Pick another one.')
  expect(await b.eval('document.querySelector("#pw-login").getAttribute("aria-invalid") === "true"'), 'login field not marked')
  await shots('profile-password-taken-1280')
  await b.fill('#pw-login', 'anna')
  await b.fill('#pw-current', 'wrong-pass')
  await b.click('main button[type=submit]')
  await b.waitText('The current password is incorrect.')
})

await step('Elena (Telegram sign-in, no password): first-time login and password, a change, then signing in with it', async () => {
  // Stand in for the Telegram widget: answer the SPA's refresh with a session for Elena (a token the API accepts).
  const elenaToken = mint(ids.elena, 'Elena Novak')
  let intercept = true
  await b.send('Fetch.enable', { patterns: [{ urlPattern: '*/api/v1/auth/refresh*' }] })
  b.on('Fetch.requestPaused', async ({ requestId }) => {
    if (!intercept) return b.send('Fetch.continueRequest', { requestId })
    const p = await get(elenaToken, '/profile')
    const session = { user: { id: ids.elena, displayName: p.displayName, telegramUserName: p.telegramUserName, isAdmin: false }, accessToken: elenaToken, accessTokenExpiresAt: new Date(Date.now() + 3600e3).toISOString() }
    await b.send('Fetch.fulfillRequest', { requestId, responseCode: 200, responseHeaders: [{ name: 'Content-Type', value: 'application/json' }], body: Buffer.from(JSON.stringify(session)).toString('base64') })
  })
  await b.goto(`${URL0}/profile/telegram`)
  await b.waitText('Not started')
  await b.waitText("You haven't started")
  await shots('profile-telegram-off-1280')
  await b.goto(`${URL0}/profile/password`)
  await b.waitText('Set a login and password')
  expect((await b.text()).includes('You sign in with Telegram now.'), 'no first-time info')
  await b.fill('#pw-login', 'elena')
  await b.fill('#pw-next', 'short')
  await b.fill('#pw-repeat', 'short')
  await b.click('main button[type=submit]')
  await b.waitText('At least 8 characters.')
  await b.fill('#pw-next', 'elena-pass-123')
  await b.fill('#pw-repeat', 'elena-pass-12')
  await b.click('main button[type=submit]')
  await b.waitText("The passwords don't match.")
  await shots('profile-password-first-1280')
  await b.fill('#pw-repeat', 'elena-pass-123')
  await b.click('main button[type=submit]')
  await b.waitText('✓ Saved. Use the new login and password next time.')
  await b.waitText('Change login or password')
  await b.fill('#pw-current', 'elena-pass-123')
  await b.fill('#pw-next', 'elena-pass-456')
  await b.fill('#pw-repeat', 'elena-pass-456')
  await b.click('main button[type=submit]')
  await b.waitText('✓ Saved. Use the new login and password next time.')
  expect((await b.eval('document.querySelector("#pw-current").value')) === '', 'password fields not cleared')
  intercept = false
  await b.send('Fetch.disable')
  await signIn('elena', 'elena-pass-456')
  await b.goto(`${URL0}/profile/telegram`)
  await b.waitText('Elena') // the sidebar's sign-out label
  expect((await b.text()).includes('@elena_n') && (await b.text()).includes('200005'), 'telegram account not shown')
})

await step('Telegram page as Anna (started, admin)', async () => {
  await signIn('anna', 'anna-pass-123')
  await b.goto(`${URL0}/profile/telegram`)
  await b.waitText('Started')
  expect((await b.text()).includes('Admin') && (await b.text()).includes('100001'), 'no admin pill or id')
  await shots('profile-telegram-1280')
  await b.goto(`${URL0}/profile/password`)
  await b.waitText('Change login or password')
  await shots('profile-password-1280')
})

// ---------- Phone ----------
await step('Phone (390): cards, nothing wider than the screen, drawer with the badge', async () => {
  // Put a request back for Anna, so the drawer shows the badge.
  await fetch(`${API}/UserContacts/subscription/request?contactId=${ids.anna}`, { method: 'POST', headers: { Authorization: `Bearer ${await tokenFor('clara', 'clara-pass-123')}` } })
  await b.goto(`${URL0}/spendings`)
  await phone()
  await b.waitText('of the bill')
  expect(!(await b.eval('!!document.querySelector("main table")')), 'table on a phone')
  await noOverflow()
  await shots('spendings-390')
  for (const [path, text, name] of [
    ['/contacts', 'Add by Telegram username', 'contacts-390'],
    ['/subscriptions', 'Your subscriptions', 'subscriptions-390'],
    ['/profile', 'Display name', 'profile-details-390'],
    ['/profile/password', 'Change login or password', 'profile-password-390'],
    ['/profile/telegram', 'The bot', 'profile-telegram-390'],
  ]) {
    await b.goto(URL0 + path)
    await b.waitText(text)
    await sleep(300)
    await noOverflow()
    await shots(name)
  }
  await b.goto(`${URL0}/contacts`)
  await b.waitText('Add by Telegram username')
  expect((await badge()) === 0, 'badge shown on the collapsed rail')
  await b.click('aside button', '')
  await waitBadge(1)
  await shots('contacts-390-drawer')
  await desktop()
})

await b.close()
const failed = results.filter((r) => r[0] === 'FAIL')
console.log(`\n${results.length - failed.length}/${results.length} passed`)
process.exit(failed.length ? 1 : 0)
