// Seeds the T6 runtime check through the API. Usage: node seed6.mjs <adminUserId>
import { createHmac } from 'node:crypto'
import { writeFileSync } from 'node:fs'

export const BASE = 'http://localhost:18096/api/v1'
const KEY = 'T6RuntimeCheckKeyT6RuntimeCheckKeyT6RuntimeCheckKey123'
const adminId = process.argv[2]

const b64 = (o) => Buffer.from(typeof o === 'string' ? o : JSON.stringify(o)).toString('base64url')
function mint(id, name) {
  const now = Math.floor(Date.now() / 1000)
  const payload = {
    'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier': id,
    'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name': name,
    Admin: 'true', iss: 'DebtBot', aud: 'DebtBot', nbf: now - 5, exp: now + 3600,
  }
  const h = b64({ alg: 'HS256', typ: 'JWT' })
  const p = b64(payload)
  return `${h}.${p}.${createHmac('sha256', KEY).update(`${h}.${p}`).digest('base64url')}`
}

async function call(token, method, path, body) {
  const r = await fetch(BASE + path, {
    method,
    headers: { Authorization: `Bearer ${token}`, ...(body !== undefined ? { 'Content-Type': 'application/json' } : {}) },
    body: body === undefined ? undefined : JSON.stringify(body),
  })
  const t = await r.text()
  if (!r.ok) throw new Error(`${method} ${path} -> ${r.status} ${t}`)
  return t ? JSON.parse(t) : undefined
}

async function login(login, password) {
  const r = await fetch(`${BASE}/auth/login`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ login, password }) })
  if (!r.ok) throw new Error(`login ${login} ${r.status}`)
  return (await r.json()).accessToken
}

const sleep = (ms) => new Promise((r) => setTimeout(r, ms))

const admin = mint(adminId, 'admin')
await call(admin, 'PUT', '/auth/password', { login: 'anna', newPassword: 'anna-pass-123' })
await call(admin, 'PUT', '/profile', { displayName: 'Anna Petrova' })

// Telegram usernames are stored with a leading "@" (T5's note), so by-telegram finds them.
const people = [
  ['Boris Ivanov', 200002, 'boris_iv', 'boris'],
  ['Clara Meyer', 200003, 'clara_m', 'clara'],
  ['Dmitry Kozlov', 200004, 'dmitry_k', 'dmitry'],
]
const ids = { anna: adminId }
for (const [name, tgId, tgUser, lg] of people) {
  await call(admin, 'POST', '/Users', { displayName: name, telegramId: tgId, telegramUserName: `@${tgUser}` })
  const found = await call(admin, 'GET', `/Users?search=${tgUser}`)
  ids[lg] = found.items[0].id
  await call(admin, 'POST', `/Users/${ids[lg]}/password`, { login: lg, password: `${lg}-pass-123` })
}

const anna = await login('anna', 'anna-pass-123')
const short = { boris: 'Boris', clara: 'Clara', dmitry: 'Dmitry' }
for (const [, , tgUser, lg] of people) {
  const c = await call(anna, 'POST', `/UserContacts/Own/by-telegram?username=${tgUser}`)
  await call(anna, 'PUT', `/UserContacts/Own/${c.id}`, { displayName: short[lg] })
}
const mum = await call(anna, 'POST', '/UserContacts/Own/by-telegram?username=mum_petrova')
await call(anna, 'PUT', `/UserContacts/Own/${mum.id}`, { displayName: 'Mum' })
ids.mum = mum.id

const day = (d, m = 11, h = 19) => new Date(Date.UTC(2026, m - 1, d, h, 30)).toISOString()
const P = (k, part) => (part ? { userId: ids[k], part } : { userId: ids[k] })
const bills = {}
bills.pizza = await call(anna, 'POST', '/Bills', {
  currencyCode: 'EUR', description: 'Pizza night', date: day(18, 11, 20),
  lines: [{ itemDescription: 'Pizza', subtotal: 30, participants: [P('anna'), P('boris')] }],
  payments: [{ userId: ids.anna, amount: 30 }],
})
await call(anna, 'POST', `/Bills/${bills.pizza}/Finalize`)
bills.bowling = await call(anna, 'POST', '/Bills', {
  currencyCode: 'EUR', description: 'Bowling night', date: day(23, 11, 21),
  lines: [{ itemDescription: 'Lanes', subtotal: 36, participants: [P('anna'), P('boris')] }],
})

// Exchanges: 22 in all, so the list has two pages. Some by Anna, one by Dmitry; a few accepted, one cancelled.
const ex = []
const mk = async (token, from, fa, fc, to, ta, tc, d, desc) => {
  const r = await call(token, 'POST', '/Bills/exchange', { fromUserId: ids[from], fromAmount: fa, fromCurrencyCode: fc, toUserId: ids[to], toAmount: ta, toCurrencyCode: tc, date: day(d, 10, 12), description: desc })
  ex.push({ ...r, from, to, fa, fc, ta, tc, d })
  return r
}
const dmitry = await login('dmitry', 'dmitry-pass-123')
await mk(dmitry, 'dmitry', 200, 'PLN', 'anna', 50, 'USD', 19, 'Dollars for the trip')
const accepted = []
for (let i = 1; i <= 20; i++) {
  const who = ['boris', 'clara', 'dmitry'][i % 3]
  const cur = i % 2 ? 'PLN' : 'USD'
  const r = await mk(anna, 'anna', 10 * i, 'EUR', who, cur === 'PLN' ? 43 * i : 11 * i, cur, i, `Cash #${i}`)
  if (i % 4 === 0) accepted.push(r)
}
const cancelled = await mk(anna, 'anna', 70, 'EUR', 'boris', 300, 'PLN', 21, 'Mum’s birthday cash')
for (const r of accepted) await call(anna, 'POST', '/Bills/exchange/finalize', r)
await call(anna, 'POST', `/Bills/${cancelled.forwardBillId}/Cancel`)

await sleep(6000)
const debts = await call(anna, 'GET', '/Debts/Own')
console.log(debts.items.map((d) => `${d.debtorUser.displayName} ${d.amount} ${d.currencyCode}`).join('\n'))
writeFileSync(new URL('./seed6.json', import.meta.url), JSON.stringify({ ids, bills, exchanges: ex }, null, 2))
console.log('seeded', ex.length, 'exchanges')
