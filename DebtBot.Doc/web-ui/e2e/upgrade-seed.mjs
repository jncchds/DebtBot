// Upgrade path, step 1: seeds a database through the API of the app at the state of `main` (before the web-ui
// branch), the way the bot made data: bills from the bot's text format, and exchanges the way /ExchangeDebt made them
// before Bills.ExchangeBillId existed (two mirrored bills, "<text>\nExchange <rate> <A>/<B>").
// Saves what main shows (Anna's debts and bills) to upgrade.json, for upgrade-check.mjs to compare after the upgrade.
import { writeFileSync } from 'node:fs'
import { BASE, defaultAdminId, mint, sleep, sql, waitForApp } from './lib/api.mjs'

await waitForApp()
const root = mint(defaultAdminId(), 'admin', true)
const req = async (token, method, path, body, text = false) => {
  const r = await fetch(BASE + path, {
    method,
    headers: { Authorization: `Bearer ${token}`, ...(body !== undefined ? { 'Content-Type': text ? 'text/plain' : 'application/json' } : {}) },
    body: body === undefined ? undefined : text ? body : JSON.stringify(body),
  })
  const t = await r.text()
  if (!r.ok) throw new Error(`${method} ${path} -> ${r.status} ${t}`)
  return t ? JSON.parse(t) : undefined
}

const people = [
  ['anna', 'Anna Petrova', 300001, '@anna_petrova'],
  ['boris', 'Boris Ivanov', 300002, '@boris_iv'],
  ['clara', 'Clara Müller', 300003, '@clara_m'],
]
const ids = {}
for (const [k, name, tgId, tgUser] of people) {
  await req(root, 'POST', '/Users', { displayName: name, telegramId: tgId, telegramUserName: tgUser })
  ids[k] = sql(`select "Id" from "Users" where "TelegramId" = ${tgId}`)
}
const anna = mint(ids.anna, 'Anna Petrova')
const text = (body, draft = false) => req(anna, 'POST', `/Bills/text?createDrafted=${draft}`, body, true)

// Ordinary bills in the bot's text format.
await text('Pizza night\n\n60\nEUR\n\n60 @anna_petrova\n\nPizza\n60\n1 @anna_petrova\n1 @boris_iv\n1 @clara_m')
await text('Groceries\n\n86\nPLN\nEUR\n\n20 @anna_petrova\n\nFood\n86\n1 @anna_petrova\n1 @clara_m')
await text('Taxi\n\n30\nEUR\n\n30 @boris_iv\n\nTaxi\n30\n1 @anna_petrova\n1 @boris_iv')
await text('Dinner draft\n\n40\nEUR\n\n40 @anna_petrova\n\nDinner\n40\n1 @anna_petrova\n1 @clara_m', true)

// /ExchangeDebt as main's TelegramService.ParseExchange built it: "from" pays in their currency, the line is the
// other person's amount in the other currency, and the description gets "\nExchange <from/to> <A>/<B>".
const fmt = (x) => String(Math.round(x * 100) / 100)
async function oldExchange(base, from, fromAmount, fromCcy, to, toAmount, toCcy, draft) {
  const half = (payer, paid, payCcy, receiver, got, gotCcy) =>
    `${base}\nExchange ${fmt(paid / got)} ${payCcy}/${gotCcy}\n\n${got}\n${gotCcy}\n${payCcy}\n\n${paid} @${payer}\n\nExchange\n${got}\n1 @${receiver}`
  return [
    await text(half(from, fromAmount, fromCcy, to, toAmount, toCcy), draft),
    await text(half(to, toAmount, toCcy, from, fromAmount, fromCcy), draft),
  ]
}
const exAccepted = await oldExchange('Cash for the trip', 'anna_petrova', 100, 'EUR', 'boris_iv', 430, 'PLN', false)
const exDraft = await oldExchange('Dollars', 'anna_petrova', 50, 'USD', 'clara_m', 46, 'EUR', true)
// Ambiguous: two identical halves mirror the same bill, so the migration must not link any of the three.
const ambiguous = await oldExchange('Twice', 'boris_iv', 10, 'EUR', 'clara_m', 43, 'PLN', true)
ambiguous.push(await text(`Twice\nExchange ${fmt(43 / 10)} PLN/EUR\n\n10\nEUR\nPLN\n\n43 @clara_m\n\nExchange\n10\n1 @boris_iv`, true))

// Finalizing and the participants run in the background.
for (let i = 0; i < 60; i++) {
  if (sql(`select count(*) from "Bills" where "Status" = 1`) === '0') break
  await sleep(1000)
}
await sleep(3000)
const debts = await req(anna, 'GET', '/Debts/Own')
// On main, GET Bills was admin-only; Anna created every bill here, so Bills/Own lists them all.
const bills = await req(anna, 'GET', '/Bills/Own')
writeFileSync(
  new URL('./upgrade.json', import.meta.url),
  JSON.stringify({ ids, exAccepted, exDraft, ambiguous, debts: debts.items ?? debts, billIds: (bills.items ?? bills).map((b) => b.id) }, null, 2),
)
console.log(`main seeded: ${(bills.items ?? bills).length} bills for Anna, ${(debts.items ?? debts).length} debts`)
