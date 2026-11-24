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

// ---------- Series 2, T1: the creator can be off the bill; PUT Bills/{id} replaces a draft ----------
t.dmitry = mint(ids.dmitry, 'Dmitry K.', true) // an admin who isn't the creator
t.grisha = mint(ids.grisha, 'Grisha Volkov')
const pasta = (people, payer = 'boris') => ({
  currencyCode: 'EUR',
  description: 'Creator off the bill',
  lines: [{ itemDescription: 'Pasta', subtotal: 60, participants: people.map((k) => ({ userId: ids[k] })) }],
  payments: [{ userId: ids[payer], amount: 60 }],
})
const lineIds = (bill) => bill.lines[0].participants.map((p) => p.user.id).sort().join()
const offId = await call(t.anna, 'POST', '/Bills', pasta(['boris', 'dmitry']))
const annaDrafts = await call(t.anna, 'GET', '/Bills?status=Draft&countPerPage=100')
check('a draft Anna made without herself is in her Bills', annaDrafts.items.some((x) => x.id === offId))
const put1 = await raw(t.anna, 'PUT', `/Bills/${offId}`, pasta(['anna', 'boris', 'dmitry']))
check('PUT Bills/{id} by the creator replaces the line (Anna back on it)', put1.status === 200 && lineIds(put1.body) === [ids.anna, ids.boris, ids.dmitry].sort().join(), `${put1.status}`)
await expectStatus(403, 'boris', 'PUT', `/Bills/${offId}`, pasta(['boris', 'dmitry'])) // on the bill, not the creator
await expectStatus(404, 'lena', 'PUT', `/Bills/${offId}`, pasta(['boris', 'dmitry'])) // no access at all
await expectStatus(409, 'anna', 'PUT', `/Bills/${bills.pizza}`, pasta(['anna', 'boris'])) // finalized
await expectStatus(400, 'boris', 'PUT', `/Bills/${bills.exBoris.forwardBillId}`, pasta(['boris', 'grisha'])) // an exchange draft
const put2 = await raw(t.dmitry, 'PUT', `/Bills/${offId}`, pasta(['boris', 'dmitry']))
check('PUT by an admin: Anna is off the line again and stays the creator', put2.status === 200 && lineIds(put2.body) === [ids.boris, ids.dmitry].sort().join() && put2.body.creator.id === ids.anna, `${put2.status}`)
await call(t.anna, 'POST', `/Bills/${offId}/Finalize`)
let offBill
for (let i = 0; i < 60; i++) {
  offBill = await call(t.anna, 'GET', `/Bills/${offId}`)
  if (offBill.status !== 'Ready' && offBill.status !== 'Processing') break
  await sleep(1000)
}
const rec = offBill.ledgerRecords
check(
  'finalized without the creator: Dmitry owes Boris 30, nothing for Anna, still in her Bills',
  offBill.status === 'Processed' &&
    rec.length === 1 &&
    rec[0].debtorUser.id === ids.dmitry &&
    rec[0].creditorUser.id === ids.boris &&
    rec[0].amount === 30 &&
    (offBill.spendings ?? []).every((s) => (s.user?.id ?? s.userId) !== ids.anna) &&
    (await call(t.anna, 'GET', '/Bills?countPerPage=100')).items.some((x) => x.id === offId),
  `${offBill.status} ${JSON.stringify(rec)}`,
)

// ---------- Series 2, T2: API tokens (as Grisha, so Anna's Profile › API tokens starts empty for check.mjs) ----------
const newTok = (who, name, scope, expiresInDays = 30) => call(t[who], 'POST', '/profile/tokens', { name, scope, expiresInDays })
const ro = await newTok('grisha', 'e2e read', 'Read')
const rw = await newTok('grisha', 'e2e write', 'ReadWrite', null)
check('POST profile/tokens returns the dbt_ secret once', [ro, rw].every((x) => /^dbt_[A-Za-z0-9_-]{43}$/.test(x.token)) && ro.prefix === ro.token.slice(4, 12))
const listed = await call(t.grisha, 'GET', '/profile/tokens')
check('GET profile/tokens: both, never the secret', listed.length === 2 && listed.every((x) => !('token' in x) && x.status === 'Active') && listed.find((x) => x.id === rw.id).expiresAt === null)
const tokStatus = async (token, method, path, body) => (await raw(token, method, path, body)).status
check('both tokens read (GET Debts/Own)', (await tokStatus(ro.token, 'GET', '/Debts/Own')) === 200 && (await tokStatus(rw.token, 'GET', '/Debts/Own')) === 200)
const tokBill = { currencyCode: 'EUR', description: 'Token bill', lines: [{ itemDescription: 'Coffee', subtotal: 8, participants: [{ userId: ids.grisha }, { userId: ids.boris }] }], payments: [{ userId: ids.grisha, amount: 8 }] }
const roPost = await raw(ro.token, 'POST', '/Bills', tokBill)
check('read-only token: POST Bills → 403 "read-only"', roPost.status === 403 && /read-only/.test(roPost.body?.message ?? ''), `${roPost.status}`)
const rwPost = await raw(rw.token, 'POST', '/Bills', tokBill)
check('read-and-write token: POST Bills → 200', rwPost.status === 200, `${rwPost.status}`)
check(
  'PUT Bills/{id} and Cancel: 403 read-only, 200 read-and-write',
  (await tokStatus(ro.token, 'PUT', `/Bills/${rwPost.body}`, tokBill)) === 403 &&
    (await tokStatus(rw.token, 'PUT', `/Bills/${rwPost.body}`, tokBill)) === 200 &&
    (await tokStatus(ro.token, 'POST', `/Bills/${rwPost.body}/Cancel`)) === 403 &&
    (await tokStatus(rw.token, 'POST', `/Bills/${rwPost.body}/Cancel`)) === 200,
)
const jwtOnly = [
  ['PUT', '/auth/password', { currentPassword: 'x', newPassword: 'whatever-123' }],
  ['GET', '/profile/tokens'],
  ['POST', '/profile/tokens', { name: 'x', scope: 'Read', expiresInDays: 30 }],
  ['DELETE', `/profile/tokens/${ro.id}`],
  ['POST', '/auth/refresh'],
  ['POST', '/auth/logout'],
  ['POST', `/Users/${ids.lena}/password`, { login: 'lena', password: 'lena-pass-123' }],
  ['POST', `/Users/${ids.boris}/tokens/revoke-all`],
]
const notJwt = []
for (const [m, p, body] of jwtOnly) {
  const r = await raw(rw.token, m, p, body)
  if (r.status !== 403 || !/API tokens can't be used here/.test(r.body?.message ?? '')) notJwt.push(`${m} ${p} ${r.status}`)
}
check(`a token gets 403 on all ${jwtOnly.length} JWT-only endpoints`, notJwt.length === 0, notJwt.join(', '))
const dmitryTok = await newTok('dmitry', 'e2e admin', 'Read')
check('an admin’s token is admin (GET Users 200), a regular one isn’t (403)', (await tokStatus(dmitryTok.token, 'GET', '/Users')) === 200 && (await tokStatus(ro.token, 'GET', '/Users')) === 403)
await call(t.dmitry, 'DELETE', `/profile/tokens/${dmitryTok.id}`)
const del = await raw(t.grisha, 'DELETE', `/profile/tokens/${rw.id}`)
check('DELETE profile/tokens/{id} → 204; the revoked token → 401, the other still 200', del.status === 204 && (await tokStatus(rw.token, 'GET', '/Debts/Own')) === 401 && (await tokStatus(ro.token, 'GET', '/Debts/Own')) === 200, `${del.status}`)
await expectStatus(404, 'boris', 'DELETE', `/profile/tokens/${ro.id}`) // someone else's
check('no token and an unknown dbt_ token → 401', (await tokStatus(null, 'GET', '/Debts/Own')) === 401 && (await tokStatus('dbt_' + 'x'.repeat(43), 'GET', '/Debts/Own')) === 401)
// The e2e stack sets the limit to 30 a minute (compose.e2e.yml).
const rate = await newTok('grisha', 'e2e rate', 'Read')
let first429 = 0
let retryAfter = null
for (let i = 1; i <= 40 && !first429; i++) {
  const r = await fetch(`${BASE}/profile`, { headers: { Authorization: `Bearer ${rate.token}` } })
  if (r.status === 429) [first429, retryAfter] = [i, r.headers.get('retry-after')]
}
check('the per-token rate limit: the 31st request in a minute → 429 with Retry-After', first429 === 31 && !!retryAfter, `first 429 at ${first429}, Retry-After ${retryAfter}`)
check('…another token and a session are unaffected', (await tokStatus(ro.token, 'GET', '/profile')) === 200 && (await status('grisha', 'GET', '/profile')) === 200)
const counted = (await call(t.anna, 'GET', '/Users?search=grisha')).items.find((u) => u.id === ids.grisha)
check('Admin › Users counts Grisha’s 2 active tokens, no prefixes', counted?.apiTokenCount === 2 && !JSON.stringify(counted).includes(ro.prefix), JSON.stringify(counted))
await expectStatus(403, 'grisha', 'POST', `/Users/${ids.grisha}/tokens/revoke-all`) // admins only
const all = await raw(t.anna, 'POST', `/Users/${ids.grisha}/tokens/revoke-all`)
check('revoke-all (admin) → { revoked: 2 }; his tokens → 401', all.status === 200 && all.body.revoked === 2 && (await tokStatus(ro.token, 'GET', '/Debts/Own')) === 401, `${all.status} ${JSON.stringify(all.body)}`)
await expectStatus(404, 'anna', 'POST', '/Users/3fa85f64-5717-4562-b3fc-2c963f66afa6/tokens/revoke-all')

const failed = results.filter((x) => !x).length
console.log(`\napi: ${results.length - failed}/${results.length} passed`)
process.exit(failed ? 1 : 0)
