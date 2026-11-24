// Seeds the T8 runtime check through the API. Usage: node seed8.mjs <defaultAdminUserId>
// Anna (admin, password) signs in; regular users, placeholders, and two duplicate pairs:
// Clara Müller / "Clara M" (same Telegram id), Boris Ivanov / "@Boris_Iv" (same username, other case, no "@").
import { execFileSync } from 'node:child_process'
import { createHmac } from 'node:crypto'
import { writeFileSync } from 'node:fs'

export const BASE = 'http://localhost:18099/api/v1'
export const KEY = 'T8RuntimeCheckKeyT8RuntimeCheckKeyT8RuntimeCheckKey12'

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

export const sql = (q) => execFileSync('docker', ['exec', 'debtbot-t8-db', 'psql', '-U', 'postgres', '-d', 'debtBot', '-Atc', q]).toString().trim()
const sleep = (ms) => new Promise((r) => setTimeout(r, ms))

if (process.argv[1]?.endsWith('seed8.mjs')) {
  const root = mint(process.argv[2], 'admin', true)
  // [key, displayName, telegramId, telegramUserName]
  const people = [
    ['anna', 'Anna Petrova', 200001, '@anna_petrova'],
    ['boris', 'Boris Ivanov', 200002, '@boris_iv'],
    ['clara', 'Clara Müller', 200003, '@clara_m'],
    ['dmitry', 'Dmitry K.', 200004, '@dmitry_k'],
    ['lena', 'Lena', 200006, '@lena_s'],
    // Duplicates and placeholders (no Telegram id, never started the bot):
    ['borisDup', '@Boris_Iv', null, 'Boris_Iv'],
    ['claraDup', 'Clara M', 200003, '@Clara_M'],
    ['grandpa', '@grandpa_vlad', null, '@grandpa_vlad'],
    ['olga', '@olga_petrova', null, '@olga_petrova'],
    ['dima', 'Dima', null, '@dima_k_old'],
  ]
  const ids = {}
  for (const [k, name, tgId, tgUser] of people) {
    const u = await call(root, 'POST', '/Users', { displayName: name, telegramId: tgId, telegramUserName: tgUser })
    ids[k] = u?.id ?? sql(`select "Id" from "Users" where "DisplayName" = '${name.replace(/'/g, "''")}'`)
  }
  const tok = Object.fromEntries(Object.entries(ids).map(([k, id]) => [k, mint(id, k)]))
  sql(`update "Users" set "TelegramBotEnabled" = true where "TelegramId" in (200001, 200002, 200003, 200004, 200006) and "DisplayName" <> 'Clara M'`)
  await call(root, 'POST', `/Users/${ids.anna}/Role?role=Admin`)
  await call(root, 'POST', `/Users/${ids.dmitry}/Role?role=Admin`)
  await call(root, 'POST', `/Users/${ids.anna}/password`, { login: 'anna.p', password: 'anna-pass-123' })
  await call(root, 'POST', `/Users/${ids.clara}/password`, { login: 'clara', password: 'clara-pass-123' })
  // The duplicate has the password; Boris has none, so after the merge he takes it.
  await call(root, 'POST', `/Users/${ids.borisDup}/password`, { login: 'boris.old', password: 'boris-old-123' })

  // Contacts: Anna knows everyone real, and Olga (so Olga can't be deleted).
  for (const u of ['boris_iv', 'clara_m', 'dmitry_k', 'lena_s', 'olga_petrova']) await call(tok.anna, 'POST', `/UserContacts/Own/by-telegram?username=${u}`)
  for (const u of ['anna_petrova', 'dmitry_k']) await call(tok.boris, 'POST', `/UserContacts/Own/by-telegram?username=${u}`)

  const day = (d, m = 11, h = 19) => new Date(Date.UTC(2026, m - 1, d, h, 30)).toISOString()
  const bill = async (creator, description, date, currency, subtotal, who, { finalize = true, payer = creator } = {}) => {
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

  const bills = {}
  const names = ['Football pitch rent', 'Taxi to the airport', 'Board games', 'Lunch', 'Coffee', 'Cinema', 'Market', 'Drinks']
  const groups = [['anna', 'boris'], ['boris', 'dmitry'], ['anna', 'boris', 'clara', 'lena'], ['dmitry', 'lena']]
  const creators = ['boris', 'anna', 'dmitry', 'lena']
  for (let i = 1; i <= 20; i++) {
    const cur = ['EUR', 'PLN', 'USD', 'EUR'][i % 4]
    await bill(creators[i % 4], `${names[i % names.length]} #${i}`, day(((i * 3) % 27) + 1, i % 2 ? 10 : 11, 12), cur, 12 + i * 4, groups[i % 4])
  }
  bills.borisDup1 = await bill('anna', 'Pizza night', day(18), 'EUR', 50, ['anna', 'borisDup'])
  bills.borisDup2 = await bill('anna', 'Bowling', day(14), 'EUR', 36, ['anna', 'borisDup', 'boris'])
  bills.claraDup = await bill('clara', 'Office coffee', day(2), 'PLN', 24, ['clara', 'claraDup'])
  bills.olga = await bill('anna', 'Birthday flowers', day(10), 'EUR', 30, ['anna', 'olga'])
  bills.dima = await bill('dmitry', 'Groceries Lidl', day(12), 'PLN', 63.4, ['dmitry', 'dima'])
  bills.draft = await bill('anna', 'Dinner at Molto', day(21), 'EUR', 84, ['anna', 'boris', 'clara'], { finalize: false })
  bills.cancelled = await bill('clara', 'Cinema tickets', day(8), 'EUR', 24, ['clara', 'anna'])
  await call(tok.clara, 'POST', `/Bills/${bills.cancelled}/Cancel`)
  bills.exchange = await call(tok.anna, 'POST', '/Bills/exchange', { fromUserId: ids.anna, toUserId: ids.boris, fromAmount: 100, fromCurrencyCode: 'EUR', toAmount: 430, toCurrencyCode: 'PLN', description: 'Exchange with Boris' })

  await sleep(6000)
  const all = await call(root, 'GET', '/Bills/All?countPerPage=1')
  const sug = await call(root, 'GET', '/Users/merge/suggestions')
  writeFileSync(new URL('./seed8.json', import.meta.url), JSON.stringify({ ids, bills }, null, 2))
  console.log('bills', all.totalCount, 'suggestions', sug.length, sug.map((s) => `${s.keep.displayName} <- ${s.merge.displayName} [${s.reasons}]`).join('; '))
  console.log('seeded')
}
