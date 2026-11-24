// API and hosting checks over HTTP, after seed.mjs: routing (SPA, 404 for unknown /api), the password session
// (login, refresh rotation, reuse, logout), access as admin, regular user and subscriber, and the T9 fixes.
// Prints "PASS|FAIL name"; exits 1 on any failure.
import { BASE, URL0, call, loadSeed, mint, raw, sleep } from './lib/api.mjs'

const { ids, bills } = loadSeed()
const t = {
  anna: mint(ids.anna, 'Anna Petrova', true),
  boris: mint(ids.boris, 'Boris Ivanov'),
  clara: mint(ids.clara, 'Clara Müller'),
  lena: mint(ids.lena, 'Lena'),
}
const results = []
const check = (name, ok, detail = '') => {
  results.push(ok)
  console.log(ok ? 'PASS' : 'FAIL', name, ok ? '' : detail)
}
const status = async (who, method, path, body) => (await raw(who && t[who], method, path, body)).status
const expectStatus = async (expected, who, method, path, body) => {
  const s = await status(who, method, path, body)
  check(`${who ?? 'anonymous'} ${method} ${path} → ${expected}`, s === expected, `got ${s}`)
}

// ---------- Hosting ----------
for (const path of ['/', '/bills', '/bills/3fa85f64-5717-4562-b3fc-2c963f66afa6', '/admin/users', '/login', '/exchange?status=waiting']) {
  const r = await fetch(URL0 + path)
  const html = await r.text()
  check(`GET ${path} serves the SPA`, r.status === 200 && html.includes('<div id="root">'), `${r.status}`)
}
for (const path of ['/api/v1/nonexistent', '/api/v1/Bills/nonexistent/thing', '/api/nothing', '/swagger/nothing']) {
  const r = await fetch(URL0 + path)
  check(`GET ${path} → 404`, r.status === 404, `${r.status}`)
}
check('GET swagger/index.html → 200', (await fetch(`${URL0}/swagger/index.html`)).status === 200)
const config = await (await fetch(`${BASE}/auth/config`)).json()
check('auth/config without a bot token: telegramBotUsername null', config.telegramBotUsername === null, JSON.stringify(config))
await expectStatus(401, undefined, 'GET', '/Bills')
await expectStatus(401, undefined, 'GET', '/auth/me')

// ---------- Password session ----------
const cookieOf = (r) => r.headers.getSetCookie().map((c) => c.split(';')[0]).find((c) => c.includes('=') && !c.endsWith('='))
const post = (path, body, cookie) =>
  fetch(BASE + path, { method: 'POST', headers: { 'Content-Type': 'application/json', ...(cookie ? { Cookie: cookie } : {}) }, body: body ? JSON.stringify(body) : undefined })
const bad = await post('/auth/login', { login: 'anna', password: 'wrong-password' })
check('login with a wrong password → 401', bad.status === 401, `${bad.status}`)
const login = await post('/auth/login', { login: 'Anna', password: 'anna-pass-123' })
const session = await login.json()
const c1 = cookieOf(login)
check('login (login is case-insensitive) → session and refresh cookie', login.status === 200 && !!session.accessToken && !!c1 && session.user?.isAdmin === true, `${login.status}`)
const me = await raw(session.accessToken, 'GET', '/auth/me')
check('auth/me with the new access token', me.status === 200 && me.body.id === ids.anna, `${me.status}`)
const r1 = await post('/auth/refresh', null, c1)
const c2 = cookieOf(r1)
check('refresh → 200 and a rotated cookie', r1.status === 200 && !!c2 && c2 !== c1, `${r1.status}`)
const reuse = await post('/auth/refresh', null, c1)
check('reusing the old refresh cookie → 401', reuse.status === 401, `${reuse.status}`)
const afterReuse = await post('/auth/refresh', null, c2)
check('reuse revokes the whole chain (the newer cookie is dead too)', afterReuse.status === 401, `${afterReuse.status}`)
const l2 = await post('/auth/login', { login: 'boris', password: 'boris-pass-123' })
const c3 = cookieOf(l2)
const out = await post('/auth/logout', null, c3)
const afterLogout = await post('/auth/refresh', null, c3)
check('logout → refresh 401', out.status < 300 && afterLogout.status === 401, `${out.status} ${afterLogout.status}`)

// ---------- Regular user: no admin endpoints ----------
for (const [m, p] of [
  ['GET', '/Users'],
  ['GET', '/Users/merge/suggestions'],
  ['GET', '/Bills/All'],
  ['POST', `/Users/${ids.lena}/Role?role=Admin`],
  ['POST', `/Users/${ids.lena}/password`],
  ['DELETE', `/Users/${ids.grandpa}`],
])
  await expectStatus(403, 'boris', m, p, m === 'POST' && p.endsWith('password') ? { login: 'x', password: 'x-pass-123' } : undefined)

// ---------- Admin: deleting ----------
await expectStatus(400, 'anna', 'DELETE', `/Users/${ids.dmitry}`) // an admin, even with no own data
await expectStatus(400, 'anna', 'DELETE', `/Users/${ids.anna}`) // yourself
await expectStatus(409, 'anna', 'DELETE', `/Users/${ids.olga}`) // a placeholder with a bill
await call(t.anna, 'POST', '/Users', { displayName: '@stray_e2e', telegramUserName: '@stray_e2e' })
const stray = (await call(t.anna, 'GET', '/Users?search=stray_e2e')).items[0]
await expectStatus(200, 'anna', 'DELETE', `/Users/${stray.id}`)
await expectStatus(400, 'anna', 'POST', `/Users/${ids.anna}/Role?role=Regular`) // your own role

// ---------- Subscriber (Clara follows Boris; pending with Dmitry) ----------
for (const p of ['/Debts/Own', '/Bills', '/Spendings/Own', '/Spendings/Own/summary']) {
  await expectStatus(200, 'clara', 'GET', `${p}?ofUserId=${ids.boris}`)
  await expectStatus(403, 'clara', 'GET', `${p}?ofUserId=${ids.dmitry}`)
}
await expectStatus(200, 'clara', 'GET', `/Bills/${bills.borisDmitry}`) // Boris's bill she isn't on
await expectStatus(404, 'clara', 'GET', `/Bills/${bills.dmitry}`)
await expectStatus(404, 'clara', 'POST', `/Bills/${bills.borisDraft}/Finalize`)
await expectStatus(404, 'clara', 'POST', `/Bills/${bills.borisDraft}/Cancel`)
await expectStatus(404, 'clara', 'POST', `/Bills/${bills.borisDraft}/payments`, [{ userId: ids.boris, amount: 2 }])
await expectStatus(404, 'clara', 'POST', '/Bills/exchange/finalize', bills.exBoris)
await expectStatus(404, 'lena', 'GET', `/Bills/${bills.draft}`) // not on it, follows nobody

// ---------- T9: a new bill is in GET Bills at once (participants are written with the bill) ----------
let missing = 0
for (let i = 0; i < 5; i++) {
  const ex = await call(t.boris, 'POST', '/Bills/exchange', { fromUserId: ids.boris, toUserId: ids.grisha, fromAmount: 10 + i, fromCurrencyCode: 'EUR', toAmount: 40 + i, toCurrencyCode: 'PLN' })
  for (const who of ['boris', 'grisha']) {
    const page = await call(mint(ids[who], who), 'GET', '/Bills?status=Draft&countPerPage=100')
    const got = page.items.filter((x) => x.id === ex.forwardBillId || x.id === ex.backwardBillId).length
    if (got !== 2 || page.items.length > page.totalCount) missing++
  }
  await call(t.boris, 'POST', `/Bills/${ex.forwardBillId}/Cancel`)
}
check('a just-created exchange lists both drafts for both people, 5 times in a row', missing === 0, `${missing} misses`)
await sleep(2000)

// ---------- Lists ----------
const ex = await call(t.anna, 'GET', '/Bills/exchanges?countPerPage=50')
check('Bills/exchanges lists the accepted and the waiting exchanges', ex.items.some((x) => x.status === 'Processed') && ex.items.some((x) => x.status === 'Draft'), JSON.stringify(ex.items.map((x) => x.status)))
const page2 = await call(t.boris, 'GET', '/Bills?countPerPage=20&pageNumber=1')
check('Boris has a second page of bills, and the total matches', page2.totalCount > 20 && page2.items.length === Math.min(20, page2.totalCount - 20), `${page2.totalCount} / ${page2.items.length}`)

const failed = results.filter((x) => !x).length
console.log(`\napi: ${results.length - failed}/${results.length} passed`)
process.exit(failed ? 1 : 0)
