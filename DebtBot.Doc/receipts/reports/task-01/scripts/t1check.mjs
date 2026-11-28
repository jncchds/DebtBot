// Receipts T1 runtime check, against the e2e stack with the fake LLM provider and low limits:
//   docker compose -p debtbot-e2e -f docker-compose.yml -f docker-compose.override.yml \
//     -f DebtBot.Doc/web-ui/e2e/compose.e2e.yml -f <a file setting DebtBot_DebtBot__Llm__Provider=Fake,
//     DebtBot_DebtBot__Llm__UserHourlyLimit=3, DebtBot_DebtBot__Llm__UserDailyLimit=5> up -d --build
//   node DebtBot.Doc/receipts/reports/task-01/scripts/t1check.mjs
import { execFileSync } from 'node:child_process'
import { mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { BASE, call, defaultAdminId, mint, raw, sql, waitForApp } from '../../../../web-ui/e2e/lib/api.mjs'

let failed = 0
const check = (name, ok, detail = '') => {
  console.log(`${ok ? 'PASS' : 'FAIL'} ${name}${detail ? ` — ${detail}` : ''}`)
  if (!ok) failed++
}

// A real 1×1 PNG; the fake ignores the pixels and answers by the file name.
const PNG = Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==', 'base64')
const HEIC = Buffer.concat([Buffer.from([0, 0, 0, 0x18]), Buffer.from('ftypheic'), Buffer.alloc(12)])

async function post(token, bytes, name) {
  const form = new FormData()
  form.append('image', new Blob([bytes], { type: 'image/png' }), name)
  const r = await fetch(`${BASE}/Bills/receipt`, { method: 'POST', headers: token ? { Authorization: `Bearer ${token}` } : {}, body: form })
  const text = await r.text()
  let body = text
  try { body = JSON.parse(text) } catch { /* text */ }
  return { status: r.status, body, retryAfter: r.headers.get('retry-after') }
}

await waitForApp()

// 1. The feature flag.
const config = await call(null, 'GET', '/auth/config')
check('GET auth/config has receiptParse', config.receiptParse === true && config.receiptModel === 'fake/receipt-fixtures', JSON.stringify(config))

// 2. The migration.
check('Users.ReceiptOpenIn column exists', sql(`select count(*) from information_schema.columns where table_name = 'Users' and column_name = 'ReceiptOpenIn'`) === '1')

const adminId = defaultAdminId()
const admin = mint(adminId, 'Admin', true)
const boris = await call(admin, 'POST', '/UserContacts/Own/by-telegram?username=boris_t1')
const borisJwt = mint(boris.id, 'Boris')

// 3. A photo with a JWT gives a draft; the owner gets it back by id, Boris doesn't.
check('anonymous POST is 401', (await post(null, PNG, 'cafe-eur.png')).status === 401)
const first = await post(admin, PNG, 'cafe-eur.png')
const d = first.body
check('POST Bills/receipt (JWT) gives a draft', first.status === 200 && d.description === 'Corner Café' && d.currencyCode === 'EUR'
  && d.totalWithTips === 39.5 && d.lines.length === 4 && d.model === 'fake/receipt-fixtures' && typeof d.id === 'string',
  `${first.status} ${d.description} ${d.currencyCode} ${d.totalWithTips} ${d.lines?.length} lines`)
check('the draft text has the sender as stand-in', typeof d.text === 'string' && d.text.startsWith('Corner Café\n\n39.50\nEUR\n\n39.50 ') && d.text.includes('\n1 '), JSON.stringify(d.text?.split('\n').slice(0, 6)))
const parsed = await fetch(`${BASE}/Bills/text/preview`, { method: 'POST', headers: { Authorization: `Bearer ${admin}`, 'Content-Type': 'text/plain' }, body: d.text }).then((r) => r.json())
check('the text parses on Paste text preview', parsed.isValid === true && parsed.result.lines.length === 4 && parsed.result.totalWithTips === 39.5, JSON.stringify(parsed.errors))
const again = await raw(admin, 'GET', `/Bills/receipt/${d.id}`)
check('GET Bills/receipt/{id} for the owner', again.status === 200 && again.body.id === d.id)
const notMine = await raw(borisJwt, 'GET', `/Bills/receipt/${d.id}`)
check('GET Bills/receipt/{id} for someone else is 404', notMine.status === 404 && notMine.body.code === 'notFound')

// 4. API tokens: read-only refused (and not counted), read-and-write works.
const ro = await call(admin, 'POST', '/profile/tokens', { name: 'T1 read', scope: 'Read', expiresInDays: 30 })
const rw = await call(admin, 'POST', '/profile/tokens', { name: 'T1 write', scope: 'ReadWrite', expiresInDays: 30 })
const roPost = await post(ro.token, PNG, 'cafe-eur.png')
check('a read-only token gets 403', roPost.status === 403, JSON.stringify(roPost.body))
const rwPost = await post(rw.token, PNG, 'supermarket-discount.png')
check('a read-and-write token works', rwPost.status === 200 && rwPost.body.description === 'FreshMart 2026-11-21'
  && rwPost.body.warnings.some((w) => w.code === 'discountLine'), JSON.stringify(rwPost.body.warnings))

// 5. The hourly limit (3 here): the third use passes, the fourth is 429 for the JWT and the token alike.
const third = await post(admin, PNG, 'tips-outside.png')
check('third use in the hour works (tips added)', third.status === 200 && third.body.totalWithTips === 35, `${third.status}`)
const fourth = await post(admin, PNG, 'cafe-eur.png')
check('fourth use is 429 rateLimited (hour)', fourth.status === 429 && fourth.body.code === 'rateLimited' && fourth.body.window === 'hour' && Number(fourth.retryAfter) > 3000,
  `${fourth.status} ${JSON.stringify(fourth.body)} Retry-After ${fourth.retryAfter}`)
check('the token shares the user limit', (await post(rw.token, PNG, 'cafe-eur.png')).status === 429)

// 6. Error codes (as Boris, who has his own limit).
const notReceipt = await post(borisJwt, PNG, 'not-a-receipt.png')
check('not a receipt is 422 notAReceipt', notReceipt.status === 422 && notReceipt.body.code === 'notAReceipt')
const heic = await post(borisJwt, HEIC, 'IMG_0001.HEIC')
check('HEIC is 415 heicNotSupported', heic.status === 415 && heic.body.code === 'heicNotSupported')
const busy = await post(borisJwt, PNG, 'busy.png')
check('busy is 503 busy', busy.status === 503 && busy.body.code === 'busy')
const justOver = await post(borisJwt, Buffer.concat([PNG, Buffer.alloc(5 * 1024 * 1024 + 1 - PNG.length)]), 'cafe-eur.png')
check('5 MB + 1 byte is 413 tooLarge', justOver.status === 413 && justOver.body.code === 'tooLarge', `${justOver.status}`)
// A 6 MB request is refused from its Content-Length before the body is read, and the server closes the connection:
// fetch fails with EPIPE while still writing, so this one goes through curl (which reads the early answer).
const dir = mkdtempSync(join(tmpdir(), 't1check-'))
writeFileSync(join(dir, 'big.png'), Buffer.concat([PNG, Buffer.alloc(6 * 1024 * 1024)]))
const out = execFileSync('curl', ['-s', '-w', '\n%{http_code}', '-H', `Authorization: Bearer ${borisJwt}`, '-F', `image=@${join(dir, 'big.png')};type=image/png`, `${BASE}/Bills/receipt`]).toString().split('\n')
rmSync(dir, { recursive: true, force: true })
check('6 MB request is 413 tooLarge', out.at(-1) === '413' && JSON.parse(out[0]).code === 'tooLarge', out.join(' '))

// 7. The Profile setting.
const profile = await call(admin, 'GET', '/profile')
const set = await call(admin, 'PUT', '/profile', { displayName: profile.displayName, phone: profile.phone, email: profile.email, receiptOpenIn: 'Text' })
const kept = await call(admin, 'PUT', '/profile', { displayName: profile.displayName, phone: profile.phone, email: profile.email })
check('Profile receiptOpenIn: Form → Text, kept when left out', profile.receiptOpenIn === 'Form' && set.receiptOpenIn === 'Text' && kept.receiptOpenIn === 'Text')
await call(admin, 'PUT', '/profile', { displayName: profile.displayName, phone: profile.phone, email: profile.email, receiptOpenIn: 'Form' })

console.log(failed ? `${failed} failed` : 'all passed')
process.exit(failed ? 1 : 0)
