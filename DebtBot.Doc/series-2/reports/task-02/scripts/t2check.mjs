// Series 2, T2 runtime check against the e2e stack, after web-ui/e2e/seed.mjs: API tokens.
// In the UI: Anna makes a read-only and a read-and-write token (the secret shown once, never in the URL or storage);
// with fetch (as curl would): both read, only read-and-write writes, a token gets 403 on JWT-only endpoints, a revoked
// token gets 401, the per-token rate limit; Admin › Users revoke-all; Swagger "Authorize" with a token; screenshots.
// Usage: node t2check.mjs <screenshot dir>
import { mkdirSync } from 'node:fs'
import { launch } from '../../../../web-ui/e2e/lib/cdp.mjs'
import { BASE, URL0, call, loadSeed, mint, raw, sleep } from '../../../../web-ui/e2e/lib/api.mjs'

const OUT = process.argv[2] ?? `${process.env.TMPDIR ?? '/tmp'}/t2check`
mkdirSync(OUT, { recursive: true })
const { ids } = loadSeed()
const anna = mint(ids.anna, 'Anna Petrova', true)
const boris = mint(ids.boris, 'Boris Ivanov')
const results = []
const b = await launch(Number(process.env.E2E_CDP_PORT ?? 9382))

const expect = (cond, msg) => {
  if (!cond) throw new Error(msg)
}
// 401s are expected (refresh on load, revoked tokens), and Swagger UI logs its own warnings.
const allowed = (p) => /status of 40[13]/.test(p) || /auth\/refresh/.test(p) || /swagger/i.test(p)
async function step(name, fn) {
  b.takeProblems()
  try {
    await fn()
    const problems = b.takeProblems().filter((p) => !allowed(p))
    expect(problems.length === 0, `console: ${problems.join(' | ')}`)
    results.push(['PASS', name])
    console.log('PASS', name)
  } catch (e) {
    results.push(['FAIL', name, e.message])
    console.log('FAIL', name, '—', e.message)
    await b.shot(`${OUT}/FAIL-${results.length}.png`).catch(() => {})
    console.log('  at', await b.path().catch(() => '?'), '|', (await main().catch(() => '')).replace(/\s+/g, ' ').slice(0, 500))
  }
}
const W = { desktop: [1280, 900], phone: [390, 844] }
/** Both widths and themes. `reload: false` keeps the page as it is (the shown-once secret doesn't survive a reload). */
async function shots(name, { reload = true } = {}) {
  await b.eval('document.activeElement?.blur()')
  for (const w of ['desktop', 'phone']) {
    if (reload) await b.eval('try { localStorage.removeItem("debtbot.sidebar") } catch {}')
    await b.viewport(...W[w], w === 'phone')
    if (reload) {
      await b.reload()
      await b.waitFor('document.querySelector("main h1")')
    }
    await sleep(600)
    if (!reload) {
      // Without a reload the sidebar keeps its last state: open it on desktop, close the drawer on phones.
      const wide = await b.eval('document.querySelector("aside").getBoundingClientRect().width > 100')
      if (wide === (w === 'phone')) {
        await b.eval('document.querySelector("aside button").click()')
        await sleep(500)
      }
    }
    for (const dark of [false, true]) {
      await b.theme(dark)
      await sleep(300)
      await b.shot(`${OUT}/${name}-${W[w][0]}-${dark ? 'dark' : 'light'}.png`)
    }
  }
  await b.theme(false)
  await b.viewport(...W.desktop)
}
const main = () => b.eval('document.querySelector("main")?.innerText ?? ""')
const loaded = (text) => b.waitFor(`document.querySelector("main") && document.querySelector("main").innerText.includes(${JSON.stringify(text)})`, 20000)
const path = () => b.eval('location.pathname + location.search')
const status = async (token, method, p, body) => (await raw(token, method, p, body)).status
const billBody = () => ({
  currencyCode: 'EUR',
  description: 'T2 token bill',
  lines: [{ itemDescription: 'Coffee', subtotal: 8, participants: [{ userId: ids.anna }, { userId: ids.boris }] }],
  payments: [{ userId: ids.anna, amount: 8 }],
})
/** Clicks the ▾ of the row (table or card) that shows `name`. */
const openRow = (name) =>
  b.eval(`(() => { const el = [...document.querySelectorAll('main button[aria-label]')].find(e => e.getAttribute('aria-label').startsWith('Actions: ' + ${JSON.stringify(name)}) && e.offsetParent); if (!el) return false; el.click(); return true })()`)
const strip = 'document.querySelector("main tbody td[colspan]")'

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
try {
  await step('sign in as Anna', async () => {
    await b.viewport(...W.desktop)
    await b.goto(`${URL0}/login`)
    await b.waitFor('document.querySelector("input[autocomplete=username]")')
    await b.fill('input[autocomplete=username]', 'anna')
    await b.fill('input[autocomplete=current-password]', 'anna-pass-123')
    await b.click('button.btn-primary')
    await b.waitFor('location.pathname !== "/login"')
  })

  await step('Profile › API tokens: empty, the sidebar entry, New token form with Read only and 90 days preselected', async () => {
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

  await step('create a read-only token: shown once with Copy and curl; a reload of /created goes to the list', async () => {
    readToken = await newToken('Home Assistant', 'Read only', '90 days')
    const text = await main()
    expect(text.includes("You won't see it again") && text.includes(`curl -H "Authorization: Bearer ${readToken}"`) && text.includes('/api/v1/Debts/Own'), 'warning and curl example')
    await shots('tokens-created', { reload: false })
    await b.reload()
    await b.waitFor('location.pathname === "/profile/tokens"')
    await loaded('Home Assistant')
    expect(!(await main()).includes(readToken), 'the secret is gone after a reload')
    expect((await main()).includes(`dbt_${readToken.slice(4, 12)}…`), 'the list shows the prefix')
  })

  await step('create a read-and-write token that never expires; Done goes back to the list', async () => {
    writeToken = await newToken('Budget script', 'Read and write', 'Never')
    await b.click('main a', 'Done')
    await b.waitFor('location.pathname === "/profile/tokens"')
    await loaded('Budget script')
    const list = await call(anna, 'GET', '/profile/tokens')
    expect(list.length === 2 && list.every((t) => t.status === 'Active' && !('token' in t)), 'API list')
    const rw = list.find((t) => t.name === 'Budget script')
    expect(rw.scope === 'ReadWrite' && rw.expiresAt === null, 'read-and-write, never expires')
    const ro = list.find((t) => t.name === 'Home Assistant')
    const days = (Date.parse(ro.expiresAt) - Date.parse(ro.createdAt)) / 86400000
    expect(ro.scope === 'Read' && Math.round(days) === 90, `read-only, 90 days (${days})`)
  })

  await step('curl: GET Debts/Own works with both tokens; LastUsedAt is set', async () => {
    for (const tok of [readToken, writeToken]) {
      const r = await fetch(`${BASE}/Debts/Own`, { headers: { Authorization: `Bearer ${tok}` } })
      expect(r.status === 200, `GET Debts/Own ${r.status}`)
    }
    const list = await call(anna, 'GET', '/profile/tokens')
    expect(list.every((t) => t.lastUsedAt), 'last used is set')
  })

  await step('curl: POST Bills only with read-and-write; PUT Bills/{id} 403 read-only', async () => {
    expect((await status(readToken, 'POST', '/Bills', billBody())) === 403, 'read-only POST Bills is 403')
    const created = await raw(writeToken, 'POST', '/Bills', billBody())
    expect(created.status === 200, `read-and-write POST Bills ${created.status}`)
    expect((await status(readToken, 'PUT', `/Bills/${created.body}`, billBody())) === 403, 'read-only PUT is 403')
    expect((await status(writeToken, 'PUT', `/Bills/${created.body}`, billBody())) === 200, 'read-and-write PUT is 200')
    expect((await status(readToken, 'POST', `/Bills/${created.body}/Cancel`)) === 403, 'read-only cancel is 403')
    expect((await status(writeToken, 'POST', `/Bills/${created.body}/Cancel`)) === 200, 'read-and-write cancel is 200')
  })

  await step('curl: a token gets 403 on the JWT-only endpoints, an admin token still gets admin', async () => {
    for (const [m, p, body] of [
      ['PUT', '/auth/password', { currentPassword: 'anna-pass-123', newPassword: 'whatever-123' }],
      ['GET', '/profile/tokens'],
      ['POST', '/profile/tokens', { name: 'x', scope: 'Read', expiresInDays: 30 }],
      ['POST', '/auth/refresh'],
      ['POST', '/auth/logout'],
      ['POST', `/Users/${ids.lena}/password`, { login: 'lena', password: 'lena-pass-123' }],
      ['POST', `/Users/${ids.boris}/tokens/revoke-all`],
    ]) {
      const r = await raw(writeToken, m, p, body)
      expect(r.status === 403 && /API tokens can't be used here/.test(r.body?.message ?? ''), `${m} ${p}: ${r.status}`)
    }
    expect((await status(readToken, 'GET', '/Users')) === 200, 'Anna’s token lists users (admin)')
    expect((await status(null, 'GET', '/Debts/Own')) === 401, 'no token is 401')
    expect((await status('dbt_' + 'x'.repeat(43), 'GET', '/Debts/Own')) === 401, 'an unknown token is 401')
  })

  await step('screenshots: the list and the New token form', async () => {
    await b.goto(`${URL0}/profile/tokens`)
    await loaded('Budget script')
    await shots('tokens')
    await b.goto(`${URL0}/profile/tokens/new`)
    await loaded('New token')
    await b.fill('#tok-name', 'Monthly report')
    await shots('tokens-new', { reload: false })
  })

  await step('revoke the read-and-write token in the UI (▾, Revoke, confirm); the next call is 401', async () => {
    await b.goto(`${URL0}/profile/tokens`)
    await loaded('Budget script')
    expect(await openRow('Budget script'), 'no ▾ for Budget script')
    await b.waitFor(`location.search.includes("open=") && ${strip}`)
    await b.click('main tbody td[colspan] button', 'Revoke')
    await b.waitText('This can')
    await shots('tokens-revoke', { reload: false })
    await b.click('main button.btn-danger', 'Revoke')
    await b.waitFor(`!location.search.includes("open=") && [...document.querySelectorAll("main tbody tr")].some(r => r.innerText.includes("Budget script") && r.innerText.includes("Revoked"))`)
    expect((await status(writeToken, 'GET', '/Debts/Own')) === 401, 'revoked token is 401')
    expect((await status(readToken, 'GET', '/Debts/Own')) === 200, 'the other token still works')
  })

  await step('the per-token rate limit: 429 after 120 a minute, other tokens and sessions unaffected', async () => {
    const extra = await call(anna, 'POST', '/profile/tokens', { name: 'Rate test', scope: 'Read', expiresInDays: 30 })
    let first429 = 0
    for (let i = 1; i <= 125 && !first429; i++) {
      const r = await fetch(`${BASE}/profile`, { headers: { Authorization: `Bearer ${extra.token}` } })
      if (r.status === 429) first429 = i
    }
    expect(first429 === 121, `first 429 at request ${first429}`)
    expect((await status(readToken, 'GET', '/profile')) === 200, 'another token is fine')
    expect((await status(anna, 'GET', '/profile')) === 200, 'a session is fine')
    await call(anna, 'DELETE', `/profile/tokens/${extra.id}`)
  })

  await step('Admin › Users: the token count, ▾ Revoke all; Boris’s tokens stop working', async () => {
    const b1 = await call(boris, 'POST', '/profile/tokens', { name: 'Boris phone', scope: 'Read', expiresInDays: 365 })
    await call(boris, 'POST', '/profile/tokens', { name: 'Boris laptop', scope: 'ReadWrite', expiresInDays: null })
    expect((await status(b1.token, 'GET', '/Debts/Own')) === 200, 'Boris’s token works')
    const borisRow = `[...document.querySelectorAll("main tbody tr")].find(r => r.innerText.includes("Boris Ivanov") && !r.querySelector("td[colspan]"))`
    await b.goto(`${URL0}/admin/users?search=boris`)
    await b.waitFor(`${borisRow}?.querySelectorAll("td")[7]?.innerText.trim() === "2"`, 20000)
    expect(await openRow('Boris Ivanov'), 'no ▾ for Boris')
    await b.waitFor(`${strip}?.innerText.includes("Revoke all 2 API tokens")`)
    await shots('admin-users', { reload: false })
    // Back at 1280 px the list switches from cards to the table again.
    await b.waitFor(`[...document.querySelectorAll("main tbody td[colspan] button")].some(e => e.innerText.includes("Revoke all 2 API tokens"))`)
    await b.click('main tbody td[colspan] button', 'Revoke all 2 API tokens')
    await b.waitText('Scripts and apps using them stop working at once')
    await shots('admin-users-revoke', { reload: false })
    await b.waitFor(`document.querySelector("main tbody td[colspan] button.btn-danger")`)
    await b.click('main button.btn-danger', 'Revoke all')
    await b.waitFor(`${borisRow}?.querySelectorAll("td")[7]?.innerText.trim() === "0"`)
    expect((await status(b1.token, 'GET', '/Debts/Own')) === 401, 'Boris’s token is revoked')
    expect((await call(boris, 'GET', '/profile/tokens')).every((t) => t.status === 'Revoked'), 'all of Boris’s tokens are revoked')
    // Back closes the strip (it's in the URL).
    await b.reload()
    await b.waitFor(`location.search.includes("open=") && ${strip}`)
    await b.back()
    await b.waitFor(`!location.search.includes("open=") && !${strip}`)
    expect((await status(boris, 'POST', `/Users/${ids.anna}/tokens/revoke-all`)) === 403, 'revoke-all is admin only')
  })

  await step('Swagger: Authorize with a token, then GET /api/v1/profile answers 200', async () => {
    await b.goto(`${URL0}/swagger/index.html`)
    await b.waitFor('document.querySelector("button.authorize")', 30000)
    await b.eval('document.querySelector("button.authorize").click()')
    await b.waitFor('document.querySelector(".modal-ux input")')
    const hint = await b.eval('document.querySelector(".modal-ux").innerText')
    expect(hint.includes('dbt_') && hint.includes('Profile'), `the hint: ${hint.slice(0, 200)}`)
    await b.fill('.modal-ux input', readToken)
    await b.eval('document.querySelector(".modal-ux button.authorize").click()')
    await sleep(300)
    await b.shot(`${OUT}/swagger-authorize-1280-light.png`, false)
    await b.eval('document.querySelector(".modal-ux button.btn-done, .modal-ux .close-modal").click()')
    await b.waitFor('document.querySelector("#operations-Profile-get_api_v1_profile")')
    // Open the operation with its arrow button (a click on the summary isn't picked up from script).
    await sleep(500)
    await b.eval('document.querySelector("#operations-Profile-get_api_v1_profile.is-open") || document.querySelector("#operations-Profile-get_api_v1_profile .opblock-control-arrow").click()')
    await b.waitFor('document.querySelector("#operations-Profile-get_api_v1_profile .try-out__btn")')
    await b.eval('document.querySelector("#operations-Profile-get_api_v1_profile .try-out__btn").click()')
    await b.waitFor('document.querySelector("#operations-Profile-get_api_v1_profile .execute")')
    await b.eval('document.querySelector("#operations-Profile-get_api_v1_profile .execute").click()')
    await b.waitFor('document.querySelector("#operations-Profile-get_api_v1_profile .live-responses-table tbody .response-col_status")', 20000)
    const code = await b.eval('document.querySelector("#operations-Profile-get_api_v1_profile .live-responses-table tbody .response-col_status").innerText.trim()')
    await b.eval('document.querySelector("#operations-Profile-get_api_v1_profile .live-responses-table").scrollIntoView()')
    await b.shot(`${OUT}/swagger-profile-1280-light.png`, false)
    expect(code === '200', `Swagger response ${code}`)
  })
} finally {
  await b.close()
}

const failed = results.filter((r) => r[0] === 'FAIL')
console.log(`\n${results.length - failed.length}/${results.length} passed`)
process.exit(failed.length ? 1 : 0)
