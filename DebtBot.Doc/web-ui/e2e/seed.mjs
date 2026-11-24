// Seeds one data set for every e2e check, through the API (plus SQL only for TelegramBotEnabled, which only the
// bot's /start sets). Run on a fresh database. Writes seed.json (ids and bill ids) for the checks.
//   Anna Petrova  admin    login anna  / anna-pass-123
//   Boris Ivanov  regular  login boris / boris-pass-123   (most of the bills)
//   Clara Müller  regular  login clara / clara-pass-123   (subscriber: follows Boris, pending with Dmitry)
//   Dmitry K.     admin (a second admin, so "admins can't be deleted" can be seen)
//   Grisha, Lena  regular; placeholders Mum, @grandpa_vlad (stray), @olga_petrova; duplicates "Clara M", "@Boris_Iv"
import { call, defaultAdminId, mint, saveSeed, sleep, sql, waitForApp } from './lib/api.mjs'

await waitForApp()
const rootId = defaultAdminId()
if (!rootId) throw new Error('no default admin: is DEFAULT_USER_TELEGRAM_ID set?')
const root = mint(rootId, 'admin', true)

// [key, displayName, telegramId, telegramUserName]
const people = [
  ['anna', 'Anna Petrova', 200001, '@anna_petrova'],
  ['boris', 'Boris Ivanov', 200002, '@boris_iv'],
  ['clara', 'Clara Müller', 200003, '@clara_m'],
  ['dmitry', 'Dmitry K.', 200004, '@dmitry_k'],
  ['grisha', 'Grisha Volkov', 200005, '@grisha_v'],
  ['lena', 'Lena', 200006, '@lena_s'],
  // Placeholders (never started the bot) and duplicates for Merge users:
  ['grandpa', '@grandpa_vlad', null, '@grandpa_vlad'],
  ['olga', '@olga_petrova', null, '@olga_petrova'],
  ['mum', '@mum_p', null, '@mum_p'],
  ['claraDup', 'Clara M', 200003, '@Clara_M'],
  ['borisDup', '@Boris_Iv', null, 'Boris_Iv'],
]
const ids = {}
for (const [k, name, tgId, tgUser] of people) {
  await call(root, 'POST', '/Users', { displayName: name, telegramId: tgId, telegramUserName: tgUser })
  ids[k] = sql(`select "Id" from "Users" where "DisplayName" = '${name.replace(/'/g, "''")}' order by "Id" desc limit 1`)
}
const tok = Object.fromEntries(Object.entries(ids).map(([k, id]) => [k, mint(id, k)]))
sql(`update "Users" set "TelegramBotEnabled" = true where "TelegramId" between 200001 and 200006 and "DisplayName" <> 'Clara M'`)
await call(root, 'POST', `/Users/${ids.anna}/Role?role=Admin`)
await call(root, 'POST', `/Users/${ids.dmitry}/Role?role=Admin`)
for (const [k, login] of [['anna', 'anna'], ['boris', 'boris'], ['clara', 'clara'], ['lena', 'lena']])
  await call(root, 'POST', `/Users/${ids[k]}/password`, { login, password: `${login}-pass-123` })
// The duplicate has a password; Boris keeps his own after the merge.
await call(root, 'POST', `/Users/${ids.borisDup}/password`, { login: 'boris.old', password: 'boris-old-123' })

// Contacts, with a few own names ("Mum"; Boris calls Grisha "Grisha from work", Clara must see "Grisha Volkov").
const contact = async (who, username, name) => {
  const c = await call(tok[who], 'POST', `/UserContacts/Own/by-telegram?username=${username}`)
  if (name) await call(tok[who], 'PUT', `/UserContacts/Own/${c.id}`, { displayName: name })
}
for (const u of ['boris_iv', 'clara_m', 'dmitry_k', 'grisha_v', 'lena_s', 'olga_petrova']) await contact('anna', u)
await contact('anna', 'mum_p', 'Mum')
for (const u of ['anna_petrova', 'dmitry_k', 'clara_m']) await contact('boris', u)
await contact('boris', 'grisha_v', 'Grisha from work')
for (const u of ['anna_petrova', 'boris_iv', 'dmitry_k']) await contact('clara', u)
for (const u of ['anna_petrova', 'boris_iv', 'clara_m']) await contact('dmitry', u)
for (const u of ['anna_petrova', 'boris_iv']) await contact('grisha', u)

// Subscriptions ("A asks for B's bills": A requests, B confirms).
const ask = (a, b2, confirm) =>
  call(tok[a], 'POST', `/UserContacts/subscription/request?contactId=${ids[b2]}`).then(() =>
    confirm ? call(tok[b2], 'POST', `/UserContacts/subscription/confirm?contactId=${ids[a]}`) : undefined,
  )
await ask('clara', 'boris', true) // Clara follows Boris
await ask('anna', 'boris', true)
await ask('anna', 'grisha', true)
await ask('clara', 'dmitry', false) // pending
await ask('dmitry', 'anna', false) // a request for Anna to review

const day = (d, m = 11, h = 19) => new Date(Date.UTC(2026, m - 1, d, h, 30)).toISOString()
const bill = async (creator, description, date, currency, subtotal, who, { finalize = true, payer = creator, paymentCurrency, paid } = {}) => {
  const id = await call(tok[creator], 'POST', '/Bills', {
    currencyCode: currency,
    paymentCurrencyCode: paymentCurrency,
    description,
    date,
    lines: [{ itemDescription: description.split('\n')[0], subtotal, participants: who.map((k) => ({ userId: ids[k] })) }],
    payments: [{ userId: ids[payer], amount: paid ?? subtotal }],
  })
  if (finalize) await call(tok[creator], 'POST', `/Bills/${id}/Finalize`)
  return id
}

const bills = {}
const names = ['Football pitch rent', 'Taxi to the airport', 'Board games', 'Lunch', 'Coffee', 'Cinema', 'Market', 'Drinks']
const groups = [['boris', 'anna'], ['boris', 'dmitry'], ['boris', 'grisha'], ['boris', 'anna', 'clara'], ['anna', 'clara', 'dmitry']]
const creators = ['boris', 'anna', 'boris', 'dmitry', 'boris']
for (let i = 1; i <= 24; i++) {
  const cur = ['EUR', 'PLN', 'USD', 'EUR'][i % 4]
  const id = await bill(creators[i % 5], `${names[i % names.length]} #${i}`, day(((i * 3) % 23) + 1, i % 2 ? 10 : 11, 12), cur, 12 + i * 4, groups[i % 5])
  if (i === 2) bills.borisDmitry = id // Boris and Dmitry only: Clara reads it as Boris's subscriber
}
bills.pizza = await bill('anna', 'Pizza night', day(18), 'EUR', 50, ['anna', 'boris'])
bills.fx = await bill('anna', 'Groceries Biedronka', day(16), 'PLN', 86, ['anna', 'clara'], { paymentCurrency: 'EUR', paid: 20 })
bills.olga = await bill('anna', 'Birthday flowers', day(10), 'EUR', 30, ['anna', 'olga'])
bills.mum = await bill('anna', 'Theatre with Mum', day(6), 'EUR', 40, ['anna', 'mum'])
bills.borisDup = await bill('anna', 'Bowling', day(14), 'EUR', 36, ['anna', 'borisDup'])
bills.claraDup = await bill('clara', 'Office coffee', day(2), 'PLN', 24, ['clara', 'claraDup'])
bills.grisha = await bill('grisha', 'Concert tickets', day(13), 'USD', 73.6, ['boris', 'grisha'])
bills.dmitry = await bill('dmitry', 'Dmitry alone', day(17), 'EUR', 10, ['dmitry'])
// Drafts: Anna's (the draft actions check works on it) and Boris's (read-only for his subscribers).
bills.draft = await bill('anna', 'Dinner at Molto', day(21), 'EUR', 60, ['anna', 'boris', 'clara'], { finalize: false })
bills.borisDraft = await bill('boris', 'Groceries', day(9), 'PLN', 42, ['boris', 'dmitry'], { finalize: false })
bills.cancelled = await bill('clara', 'Cinema tickets', day(8), 'EUR', 24, ['clara', 'anna'])
await sleep(3000)
await call(tok.clara, 'POST', `/Bills/${bills.cancelled}/Cancel`)

// Exchanges: Anna ⇄ Boris accepted, Boris → Grisha waiting, Anna → Clara waiting.
const exchange = (creator, from, to, fromAmount, fromCurrencyCode, toAmount, toCurrencyCode) =>
  call(tok[creator], 'POST', '/Bills/exchange', { fromUserId: ids[from], toUserId: ids[to], fromAmount, fromCurrencyCode, toAmount, toCurrencyCode })
bills.exAccepted = await exchange('anna', 'anna', 'boris', 100, 'EUR', 430, 'PLN')
await call(tok.anna, 'POST', '/Bills/exchange/finalize', bills.exAccepted)
bills.exBoris = await exchange('boris', 'boris', 'grisha', 50, 'USD', 46, 'EUR')
bills.exClara = await exchange('anna', 'anna', 'clara', 200, 'PLN', 46.5, 'EUR')

// Finalizing runs in the background: wait until nothing is Ready any more.
for (let i = 0; i < 60; i++) {
  if (sql(`select count(*) from "Bills" where "Status" = 1`) === '0') break
  await sleep(1000)
}
const statuses = sql(`select "Status", count(*) from "Bills" group by 1 order by 1`).replace(/\n/g, ', ')
saveSeed({ ids, bills })
console.log(`seeded: ${Object.keys(ids).length} users, bills by status ${statuses}`)
