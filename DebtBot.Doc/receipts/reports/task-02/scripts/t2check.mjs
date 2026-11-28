// Receipts T2 runtime check: the bot's receipt photos, driven by fake Telegram updates through the Development-only
// POST api/v1/dev/telegram/update, against the e2e stack with the fake LLM provider:
//   docker compose -p debtbot-e2e -f docker-compose.yml -f docker-compose.override.yml \
//     -f DebtBot.Doc/web-ui/e2e/compose.e2e.yml -f <a file setting DebtBot_DebtBot__Llm__Provider=Fake,
//     DebtBot_DebtBot__Llm__UserHourlyLimit=6, DebtBot_DebtBot__Llm__UserDailyLimit=20,
//     DebtBot_DebtBot__WebUrl=https://debts.example.com> up -d --build
//   node DebtBot.Doc/receipts/reports/task-02/scripts/t2check.mjs
import { BASE, DEFAULT_ADMIN_TELEGRAM_ID, call, defaultAdminId, mint, raw, sql, waitForApp } from '../../../../web-ui/e2e/lib/api.mjs'

let failed = 0
const check = (name, ok, detail = '') => {
  console.log(`${ok ? 'PASS' : 'FAIL'} ${name}${detail ? ` — ${detail}` : ''}`)
  if (!ok) failed++
}

// A real 1×1 PNG; the fake ignores the pixels and answers by the caption (or file name).
const PNG = Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==', 'base64')
const DEV_BOT_ID = 1
const TG_ID = Number(DEFAULT_ADMIN_TELEGRAM_ID)
const USERNAME = 'anna_t2'
const from = { id: TG_ID, is_bot: false, first_name: 'Anna', username: USERNAME }
let messageId = 100
const date = Math.floor(Date.now() / 1000)

async function update(token, message, file) {
  const form = new FormData()
  form.append('update', JSON.stringify({ update_id: messageId, message: { message_id: messageId++, date, from, ...message } }))
  if (file) form.append('file', new Blob([file], { type: 'image/png' }), 'photo.png')
  const r = await fetch(`${BASE}/dev/telegram/update`, { method: 'POST', headers: { Authorization: `Bearer ${token}` }, body: form })
  const text = await r.text()
  if (r.status !== 200) throw new Error(`dev/telegram/update -> ${r.status} ${text}`)
  return JSON.parse(text)
}

const privateChat = { id: TG_ID, type: 'private', first_name: 'Anna' }
const group = { id: -100500, type: 'supergroup', title: 'Flatmates' }
const photo = [
  { file_id: 'small', file_unique_id: 's', width: 90, height: 120, file_size: 1000 },
  { file_id: 'big', file_unique_id: 'b', width: 960, height: 1280, file_size: 68 },
]
const pre = (text) => {
  const m = /<pre>([\s\S]*)<\/pre>/.exec(text)
  return m ? m[1].replaceAll('&lt;', '<').replaceAll('&gt;', '>').replaceAll('&amp;', '&') : null
}

await waitForApp()

check('the TelegramChatStates table exists', sql(`select count(*) from information_schema.tables where table_name = 'TelegramChatStates'`) === '1')

const adminId = defaultAdminId()
const admin = mint(adminId, 'Admin', true)

// The dev path is admin-only.
const plain = await call(admin, 'POST', '/UserContacts/Own/by-telegram?username=boris_t2')
const borisJwt = mint(plain.id, 'Boris')
const refused = await fetch(`${BASE}/dev/telegram/update`, { method: 'POST', headers: { Authorization: `Bearer ${borisJwt}` }, body: new FormData() })
check('the dev path is admin-only', refused.status === 403, `${refused.status}`)

// /start, as a typed command, turns the bot on for the sender (and sets the username from the update).
await update(admin, { chat: privateChat, text: '/start', entities: [{ type: 'bot_command', offset: 0, length: 6 }] })
check('/start turned the bot on', sql(`select "TelegramBotEnabled" from "Users" where "Id" = '${adminId}'`) === 't')

// 1. A private photo: the bill text, the hint, the warning, the privacy note and the web button.
const first = await update(admin, { chat: privateChat, photo, caption: 'supermarket-discount' }, PNG)
const reply = first.replies[0]
console.log('\n--- the reply (HTML) ---\n' + reply?.text + '\n--- button: ' + JSON.stringify(reply?.inlineKeyboard) + '\n')
const bill = pre(reply?.text ?? '')
check('one reply, to the photo', first.replies.length === 1 && reply.replyToMessageId === 101 && reply.chatId === TG_ID)
check('the largest size was downloaded', JSON.stringify(first.downloads) === '["dev/big"]', JSON.stringify(first.downloads))
check('the bill is in a code block, starting with /AddBill', bill?.startsWith('/AddBill\nFreshMart 2026-11-21\n\n12.06\nEUR\n\n12.06 @anna_t2\n\nMilk 1L\n1.19\n1 @anna_t2'), JSON.stringify(bill?.slice(0, 80)))
check('no negative line in the text', bill !== null && !/\n-/.test(bill))
check('the hint and the folded-discount warning', reply.text.includes('People and payers are you for now; change them before sending.')
  && reply.text.includes('⚠️ The discount “Loyalty discount” (1.00 EUR) was spread over all lines.'))
check('the privacy note, the first time', reply.text.includes('The receipt is sent to an AI service (OpenRouter, model fake/receipt-fixtures) to read it.'))
const url = reply.inlineKeyboard?.[0]?.[0]?.url ?? ''
check('the "Open in the web form" button', reply.inlineKeyboard?.[0]?.[0]?.text === 'Open in the web form' && url.startsWith('https://debts.example.com/bills/new?receipt='), url)
const draftId = url.split('receipt=')[1]
const mine = await raw(admin, 'GET', `/Bills/receipt/${draftId}`)
check('the button\'s draft opens for the sender', mine.status === 200 && mine.body.lines.every((l) => l.subtotal > 0) && mine.body.warnings[0].code === 'discountSpread', `${mine.status}`)
check('…and is 404 for someone else', (await raw(borisJwt, 'GET', `/Bills/receipt/${draftId}`)).status === 404)

// 2. The second photo in the chat: the footer, not the note; the state is in the database.
const second = await update(admin, { chat: privateChat, photo, caption: 'cafe-eur' }, PNG)
check('the footer the second time', second.replies[0].text.includes('<i>Read by an AI service (OpenRouter, fake/receipt-fixtures).</i>') && !second.replies[0].text.includes('Don\'t send receipts'))
check('the note state is kept per chat in the database', sql(`select count(*) from "TelegramChatStates" where "ChatId" = ${TG_ID} and "ReceiptNoteShownAt" is not null`) === '1')

// 3. Groups: ignored without a mention, read with one.
const ignored = await update(admin, { chat: group, photo, caption: 'lunch' }, PNG)
check('a group photo without a mention is ignored', ignored.replies.length === 0 && ignored.downloads.length === 0)
const mentioned = await update(admin, { chat: group, photo, caption: 'bot, lunch', caption_entities: [{ type: 'text_mention', offset: 0, length: 3, user: { id: DEV_BOT_ID, is_bot: true, first_name: 'Debt bot' } }] }, PNG)
check('a group photo that mentions the bot is read, with its own note', mentioned.replies.length === 1 && mentioned.replies[0].chatId === group.id
  && mentioned.replies[0].text.startsWith('<pre>/AddBill\nCorner Café') && mentioned.replies[0].text.includes('Don\'t send receipts'))

// 4. The reply text, sent back as a message, makes the bill.
const entities = [{ type: 'bot_command', offset: 0, length: '/AddBill'.length }]
for (let at = bill.indexOf('@' + USERNAME); at >= 0; at = bill.indexOf('@' + USERNAME, at + 1)) entities.push({ type: 'mention', offset: at, length: USERNAME.length + 1 })
const before = Number(sql(`select count(*) from "Bills" where "Description" = 'FreshMart 2026-11-21'`))
await update(admin, { chat: privateChat, text: bill, entities })
const made = sql(`select "TotalWithTips"::float8, "CurrencyCode", "CreatorId" from "Bills" where "Description" = 'FreshMart 2026-11-21' order by "Date" desc limit 1`)
const lines = sql(`select string_agg(to_char(l."Subtotal", 'FM990.00'), ',' order by l."Subtotal") from "BillLines" l join "Bills" b on b."Id" = l."BillId" where b."Description" = 'FreshMart 2026-11-21'`)
check('the text sent back made the bill', before === 0 && made === `12.06|EUR|${adminId}`, made)
check('…with the folded lines', lines === '1.19,2.30,3.41,5.16', lines)

// 5. Errors and the limit shared with the web (6 an hour: 4 photos so far reached the model; the ignored one didn't).
const notReceipt = await update(admin, { chat: privateChat, photo, caption: 'not-a-receipt' }, PNG)
check('notAReceipt gets the web\'s message', notReceipt.replies[0].text.startsWith('This doesn\'t look like a receipt.'))
const form = new FormData()
form.append('image', new Blob([PNG], { type: 'image/png' }), 'cafe-eur.png')
const web = await fetch(`${BASE}/Bills/receipt`, { method: 'POST', headers: { Authorization: `Bearer ${admin}` }, body: form })
check('a web read counts too (the 5th)', web.status === 200, `${web.status}`)
const sixth = await update(admin, { chat: privateChat, photo }, PNG)
check('the 6th, in the bot, is read', sixth.replies[0].text.startsWith('<pre>/AddBill'))
const limited = await update(admin, { chat: privateChat, photo }, PNG)
check('the 7th, in the bot, is rate limited with when to try again', limited.replies[0].text === 'You\'ve read too many receipts for now. Try later or fill the bill in by hand. You can read the next one in 1 hour.', limited.replies[0].text)
const webLimited = await fetch(`${BASE}/Bills/receipt`, { method: 'POST', headers: { Authorization: `Bearer ${admin}` }, body: form })
check('…and on the web', webLimited.status === 429, `${webLimited.status}`)

// 6. Someone who never started the bot.
const stranger = await fetch(`${BASE}/dev/telegram/update`, {
  method: 'POST', headers: { Authorization: `Bearer ${admin}` },
  body: (() => { const f = new FormData(); f.append('update', JSON.stringify({ update_id: 9, message: { message_id: 9, date, from: { id: 777000123, is_bot: false, first_name: 'Carl' }, chat: { id: 777000123, type: 'private' }, photo } })); f.append('file', new Blob([PNG]), 'p.png'); return f })(),
}).then((r) => r.json())
check('a sender who never started the bot is asked to start it', stranger.replies[0]?.text.startsWith('Start the bot first') && stranger.downloads.length === 0, stranger.replies[0]?.text)

console.log(failed ? `\n${failed} FAILED` : '\nALL PASSED')
process.exit(failed ? 1 : 0)
