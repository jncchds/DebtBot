// Seeds the T7b runtime check through the API. Usage: node seed7b.mjs <adminUserId>
// Anna (a regular user) is subscribed (confirmed) to Boris and Grisha, pending to Clara, and has nothing with Dmitry.
import { execFileSync } from 'node:child_process'
import { createHmac } from 'node:crypto'
import { writeFileSync } from 'node:fs'

export const BASE = 'http://localhost:18098/api/v1'
export const KEY = 'T7bRuntimeCheckKeyT7bRuntimeCheckKeyT7bRuntimeCheck12'

const b64 = (o) => Buffer.from(typeof o === 'string' ? o : JSON.stringify(o)).toString('base64url')
/** An access token as the API issues it, so seeding doesn't run into the sign-in rate limit. */
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

export async function call(token, method, path, body) {
  const r = await fetch(BASE + path, {
    method,
    headers: { Authorization: `Bearer ${token}`, ...(body !== undefined ? { 'Content-Type': 'application/json' } : {}) },
    body: body === undefined ? undefined : JSON.stringify(body),
  })
  const t = await r.text()
  if (!r.ok) throw new Error(`${method} ${path} -> ${r.status} ${t}`)
  return t ? JSON.parse(t) : undefined
}

const sleep = (ms) => new Promise((r) => setTimeout(r, ms))
const sql = (q) => execFileSync('docker', ['exec', 'debtbot-t7b-db', 'psql', '-U', 'postgres', '-d', 'debtBot', '-Atc', q]).toString().trim()

if (process.argv[1]?.endsWith('seed7b.mjs')) {
  const admin = mint(process.argv[2], 'admin', true)
  const people = [
    ['Anna Petrova', 200001, 'anna_petrova', 'anna'],
    ['Boris Ivanov', 200002, 'boris_iv', 'boris'],
    ['Clara Müller', 200003, 'clara_m', 'clara'],
    ['Dmitry K.', 200004, 'dmitry_k', 'dmitry'],
    ['Grisha Volkov', 200005, 'grisha_v', 'grisha'],
  ]
  const ids = {}
  const tok = {}
  for (const [name, tgId, tgUser, lg] of people) {
    await call(admin, 'POST', '/Users', { displayName: name, telegramId: tgId, telegramUserName: `@${tgUser}` })
    const found = await call(admin, 'GET', `/Users?search=${tgUser}`)
    ids[lg] = found.items[0].id
    tok[lg] = mint(ids[lg], name)
  }
  // Anna signs in to the web app with a password; the others only need tokens.
  await call(admin, 'POST', `/Users/${ids.anna}/password`, { login: 'anna', password: 'anna-pass-123' })

  // Contacts. Anna's names for people are short; Boris calls Grisha "Grisha from work" (Anna must see "Grisha Volkov").
  const contact = async (who, username, name) => {
    const c = await call(tok[who], 'POST', `/UserContacts/Own/by-telegram?username=${username}`)
    if (name) await call(tok[who], 'PUT', `/UserContacts/Own/${c.id}`, { displayName: name })
  }
  for (const [u, n] of [['boris_iv', 'Boris'], ['clara_m', 'Clara'], ['dmitry_k', 'Dmitry'], ['grisha_v', 'Grisha']]) await contact('anna', u, n)
  for (const u of ['anna_petrova', 'dmitry_k']) await contact('boris', u)
  await contact('boris', 'grisha_v', 'Grisha from work')
  for (const u of ['boris_iv', 'anna_petrova']) await contact('grisha', u)
  for (const u of ['dmitry_k', 'anna_petrova']) await contact('clara', u)

  // Subscriptions: Anna → Boris and Grisha confirmed, Anna → Clara pending, nothing with Dmitry.
  await call(tok.anna, 'POST', `/UserContacts/subscription/request?contactId=${ids.boris}`)
  await call(tok.boris, 'POST', `/UserContacts/subscription/confirm?contactId=${ids.anna}`)
  await call(tok.anna, 'POST', `/UserContacts/subscription/request?contactId=${ids.grisha}`)
  await call(tok.grisha, 'POST', `/UserContacts/subscription/confirm?contactId=${ids.anna}`)
  await call(tok.anna, 'POST', `/UserContacts/subscription/request?contactId=${ids.clara}`)

  const day = (d, m = 11, h = 19) => new Date(Date.UTC(2026, m - 1, d, h, 30)).toISOString()
  const bill = async (creator, description, date, currency, subtotal, who, payer = creator, finalize = true) => {
    const id = await call(tok[creator], 'POST', '/Bills', {
      currencyCode: currency,
      description,
      date,
      lines: [{ itemDescription: description, subtotal, participants: who.map((k) => ({ userId: ids[k] })) }],
      payments: [{ userId: ids[payer], amount: subtotal }],
    })
    if (finalize) await call(tok[creator], 'POST', `/Bills/${id}/Finalize`)
    return id
  }

  // Boris's bills: 22 of his own (with Anna, Dmitry, Grisha), 2 by Anna, 1 by Grisha, 1 draft, so his list has 2 pages.
  const bills = {}
  const names = ['Football pitch rent', 'Taxi to the airport', 'Board games', 'Lunch', 'Coffee', 'Cinema', 'Market', 'Drinks']
  const groups = [['boris', 'anna'], ['boris', 'dmitry'], ['boris', 'grisha'], ['boris', 'anna', 'grisha']]
  for (let i = 1; i <= 22; i++) {
    const cur = ['EUR', 'PLN', 'USD', 'EUR'][i % 4]
    const id = await bill('boris', `${names[i % names.length]} #${i}`, day(((i * 3) % 28) + 1, i % 2 ? 10 : 11, 12), cur, 12 + i * 4, groups[i % 4])
    if (i === 2) bills.borisDmitry = id // Anna isn't on it
  }
  bills.pizza = await bill('anna', 'Pizza night', day(18), 'EUR', 50, ['anna', 'boris'])
  bills.cake = await bill('anna', 'Cake for the office', day(5), 'PLN', 80, ['anna', 'boris', 'clara'])
  bills.grisha = await bill('grisha', 'Concert tickets', day(13), 'USD', 73.6, ['boris', 'grisha'])
  bills.draft = await bill('boris', 'Groceries', day(9), 'PLN', 42, ['boris', 'dmitry'], 'boris', false)
  // Not Boris's: Anna and Clara; Clara and Dmitry (Anna's pending and none).
  bills.annaClara = await bill('anna', 'Flowers', day(20), 'EUR', 30, ['anna', 'clara'])
  bills.clara = await bill('clara', 'Clara and Dmitry dinner', day(19), 'EUR', 64, ['clara', 'dmitry'])
  bills.dmitry = await bill('dmitry', 'Dmitry alone', day(17), 'EUR', 10, ['dmitry'])
  // A draft exchange Boris → Grisha (no Accept for Anna).
  const ex = await call(tok.boris, 'POST', '/Bills/exchange', { fromUserId: ids.boris, toUserId: ids.grisha, fromAmount: 100, fromCurrencyCode: 'EUR', toAmount: 430, toCurrencyCode: 'PLN' })
  bills.exchange = ex

  sql(`update "Users" set "TelegramBotEnabled" = true where "TelegramId" between 200001 and 200005`)
  await sleep(6000)
  const borisBills = await call(tok.anna, 'GET', `/Bills?ofUserId=${ids.boris}&countPerPage=1`)
  const borisDebts = await call(tok.anna, 'GET', `/Debts/Own?ofUserId=${ids.boris}`)
  const borisSp = await call(tok.anna, 'GET', `/Spendings/Own?ofUserId=${ids.boris}&countPerPage=1`)
  writeFileSync(new URL('./seed7b.json', import.meta.url), JSON.stringify({ ids, bills }, null, 2))
  console.log('boris bills', borisBills.totalCount, 'debts', borisDebts.totalCount, 'spendings', borisSp.totalCount)
  console.log('seeded')
}
