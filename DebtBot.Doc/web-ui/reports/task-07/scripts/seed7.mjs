// Seeds the T7 runtime check through the API. Usage: node seed7.mjs <adminUserId>
import { execFileSync } from 'node:child_process'
import { createHmac } from 'node:crypto'
import { writeFileSync } from 'node:fs'

export const BASE = 'http://localhost:18097/api/v1'
export const KEY = 'T7RuntimeCheckKeyT7RuntimeCheckKeyT7RuntimeCheckKey123'
const adminId = process.argv[2]

const b64 = (o) => Buffer.from(typeof o === 'string' ? o : JSON.stringify(o)).toString('base64url')
/** An access token as the API issues it (the check uses it to stand in for a Telegram sign-in). */
export function mint(id, name, admin = false) {
  const now = Math.floor(Date.now() / 1000)
  const payload = {
    'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier': id,
    'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name': name,
    ...(admin ? { Admin: 'true' } : {}),
    iss: 'DebtBot', aud: 'DebtBot', nbf: now - 5, exp: now + 3600,
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
const sql = (q) => execFileSync('docker', ['exec', 'debtbot-t7-db', 'psql', '-U', 'postgres', '-d', 'debtBot', '-Atc', q]).toString().trim()

if (process.argv[1].endsWith('seed7.mjs')) {
  const admin = mint(adminId, 'admin', true)
  await call(admin, 'PUT', '/auth/password', { login: 'anna', newPassword: 'anna-pass-123' })
  await call(admin, 'PUT', '/profile', { displayName: 'Anna Petrova', phone: '+48 600 123 456', email: 'anna@example.com' })

  // Telegram usernames are stored with a leading "@". Elena has no password: she checks the first-time form.
  const people = [
    ['Boris Ivanov', 200002, 'boris_iv', 'boris'],
    ['Clara Müller', 200003, 'clara_m', 'clara'],
    ['Dmitry K.', 200004, 'dmitry_k', 'dmitry'],
    ['Elena Novak', 200005, 'elena_n', 'elena'],
  ]
  const ids = { anna: adminId }
  for (const [name, tgId, tgUser, lg] of people) {
    await call(admin, 'POST', '/Users', { displayName: name, telegramId: tgId, telegramUserName: `@${tgUser}` })
    const found = await call(admin, 'GET', `/Users?search=${tgUser}`)
    ids[lg] = found.items[0].id
    if (lg !== 'elena') await call(admin, 'POST', `/Users/${ids[lg]}/password`, { login: lg, password: `${lg}-pass-123` })
  }

  const anna = await login('anna', 'anna-pass-123')
  const short = { boris: 'Boris', clara: 'Clara', dmitry: 'Dmitry' }
  for (const lg of ['boris', 'clara', 'dmitry']) {
    const c = await call(anna, 'POST', `/UserContacts/Own/by-telegram?username=${people.find((p) => p[3] === lg)[2]}`)
    await call(anna, 'PUT', `/UserContacts/Own/${c.id}`, { displayName: short[lg] })
  }
  const mum = await call(anna, 'POST', '/UserContacts/Own/by-telegram?username=olga_petrova')
  await call(anna, 'PUT', `/UserContacts/Own/${mum.id}`, { displayName: 'Mum' })
  ids.mum = mum.id

  // Bills for Spendings: 24 finalized in October and November, EUR, PLN, USD, one PLN bill paid in EUR, one cancelled.
  const day = (d, m = 11, h = 19) => new Date(Date.UTC(2026, m - 1, d, h, 30)).toISOString()
  const P = (k) => ({ userId: ids[k] })
  const bill = async (description, date, currency, subtotal, who, paymentCurrency, paid) => {
    const id = await call(anna, 'POST', '/Bills', {
      currencyCode: currency,
      paymentCurrencyCode: paymentCurrency,
      description,
      date,
      lines: [{ itemDescription: description, subtotal, participants: who.map(P) }],
      payments: [{ userId: ids.anna, amount: paid ?? subtotal }],
    })
    await call(anna, 'POST', `/Bills/${id}/Finalize`)
    return id
  }
  const bills = {}
  const names = ['Coffee', 'Lunch', 'Groceries', 'Taxi', 'Cinema', 'Pizza', 'Market', 'Drinks']
  for (let i = 1; i <= 20; i++) {
    const cur = ['EUR', 'PLN', 'USD', 'EUR'][i % 4]
    const who = i % 3 === 0 ? ['anna', 'boris', 'clara'] : ['anna', 'boris']
    await bill(`${names[i % names.length]} #${i}`, day(((i * 3) % 28) + 1, i % 2 ? 10 : 11, 12), cur, 10 + i * 3, who)
  }
  bills.molto = await bill('Dinner at Molto', day(21, 11, 20), 'EUR', 84, ['anna', 'boris', 'dmitry'])
  bills.hotel = await bill('Hotel in Kraków', day(2, 11, 15), 'PLN', 960, ['anna', 'boris', 'clara'], 'EUR', 223.26)
  bills.gift = await bill('Birthday gift for Mum', day(23, 11, 10), 'PLN', 120, ['anna', 'boris', 'clara'])
  bills.cancelled = await bill('Concert tickets', day(14, 11, 18), 'EUR', 90, ['anna', 'clara'])
  await sleep(3000)
  await call(anna, 'POST', `/Bills/${bills.cancelled}/Cancel`)

  // Subscriptions in every state.
  const boris = await login('boris', 'boris-pass-123')
  const clara = await login('clara', 'clara-pass-123')
  const dmitry = await login('dmitry', 'dmitry-pass-123')
  // Dmitry's contacts, so his Subscriptions page lists everyone.
  for (const u of ['anna_p', 'boris_iv', 'clara_m']) await call(dmitry, 'POST', `/UserContacts/Own/by-telegram?username=${u}`)
  for (const [tok, u] of [[boris, 'anna_p'], [boris, 'dmitry_k'], [clara, 'anna_p'], [clara, 'dmitry_k']]) await call(tok, 'POST', `/UserContacts/Own/by-telegram?username=${u}`)
  // Anna ⇄ Boris both confirmed; Anna asked Clara (pending); Dmitry asked Anna (a request waiting for Anna).
  await call(anna, 'POST', `/UserContacts/subscription/request?contactId=${ids.boris}`)
  await call(boris, 'POST', `/UserContacts/subscription/confirm?contactId=${ids.anna}`)
  await call(boris, 'POST', `/UserContacts/subscription/request?contactId=${ids.anna}`)
  await call(anna, 'POST', `/UserContacts/subscription/confirm?contactId=${ids.boris}`)
  await call(anna, 'POST', `/UserContacts/subscription/request?contactId=${ids.clara}`)
  await call(dmitry, 'POST', `/UserContacts/subscription/request?contactId=${ids.anna}`)
  // For Dmitry: Clara asks him (decline), Boris gets his (stop), he gets Boris's (stop), he asked Clara (withdraw).
  await call(clara, 'POST', `/UserContacts/subscription/request?contactId=${ids.dmitry}`)
  await call(boris, 'POST', `/UserContacts/subscription/request?contactId=${ids.dmitry}`)
  await call(dmitry, 'POST', `/UserContacts/subscription/confirm?contactId=${ids.boris}`)
  await call(dmitry, 'POST', `/UserContacts/subscription/request?contactId=${ids.boris}`)
  await call(boris, 'POST', `/UserContacts/subscription/confirm?contactId=${ids.dmitry}`)
  await call(dmitry, 'POST', `/UserContacts/subscription/request?contactId=${ids.clara}`)

  // Real users have started the bot (no API sets this; the bot does on /start). Mum and Elena haven't.
  sql(`update "Users" set "TelegramBotEnabled" = true where "TelegramId" in (100001, 200002, 200003, 200004)`)

  await sleep(4000)
  const sp = await call(anna, 'GET', '/Spendings/Own?countPerPage=1')
  const month = await call(anna, 'GET', '/Spendings/Own/summary?dateFrom=2026-11-01&dateTo=2026-11-30')
  writeFileSync(new URL('./seed7.json', import.meta.url), JSON.stringify({ ids, bills }, null, 2))
  console.log('spendings', sp.totalCount, 'this month', JSON.stringify(month))
  console.log('seeded')
}
