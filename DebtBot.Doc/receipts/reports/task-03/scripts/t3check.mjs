// Receipts T3 runtime check: the receipt UI in headless Chrome, against the e2e stack with the fake LLM provider:
//   docker compose -p debtbot-e2e -f docker-compose.yml -f docker-compose.override.yml \
//     -f DebtBot.Doc/web-ui/e2e/compose.e2e.yml -f <a file setting DebtBot_DebtBot__Llm__Provider=Fake> up -d --build
//   node DebtBot.Doc/receipts/reports/task-03/scripts/t3check.mjs [screenshot dir]
// Uploads are PNGs drawn in the page and named after the fake's fixtures (it answers by the file name).
import { mkdirSync } from 'node:fs'
import { join } from 'node:path'
import { BASE, URL0, call, defaultAdminId, mint, raw, sleep, waitForApp } from '../../../../web-ui/e2e/lib/api.mjs'
import { launch } from '../../../../web-ui/e2e/lib/cdp.mjs'

const SHOTS = process.argv[2]
if (SHOTS) mkdirSync(SHOTS, { recursive: true })

let failed = 0
const check = (name, ok, detail = '') => {
  console.log(`${ok ? 'PASS' : 'FAIL'} ${name}${detail ? ` — ${detail}` : ''}`)
  if (!ok) failed++
}

await waitForApp()
const adminId = defaultAdminId()
const admin = mint(adminId, 'Admin', true)

// A web login for the default admin, and a contact to put on the bill.
const pw = await raw(admin, 'PUT', '/auth/password', { login: 'anna_t3', newPassword: 'anna-pass-123' })
check('a password for the browser sign-in', pw.status === 204 || pw.status === 200, `${pw.status}`)
await call(admin, 'POST', '/UserContacts/Own/by-telegram?username=boris_t3')

const config = await call(null, 'GET', '/auth/config')
check('auth/config: receiptParse and the fake model', config.receiptParse === true && config.receiptModel === 'fake/receipt-fixtures', JSON.stringify(config))
check('Profile receiptOpenIn starts as Form', (await call(admin, 'GET', '/profile')).receiptOpenIn === 'Form')

const b = await launch(9351)
const text = () => b.text()
const value = (sel) => b.eval(`document.querySelector(${JSON.stringify(sel)})?.value ?? null`)
const DESC = 'main textarea'
const inputs = () => b.eval('[...document.querySelectorAll("main input")].map((i) => i.value)')

/** Records what the page uploads (name, type, size) in window.__uploads. */
const spy = () =>
  b.eval(`(() => { if (window.__spy) return; window.__spy = true; window.__uploads = []; const orig = window.fetch;
    window.fetch = (u, init) => { const f = init && init.body instanceof FormData ? init.body.get('image') : null;
      if (f) window.__uploads.push({ name: f.name, type: f.type, size: f.size }); return orig(u, init) } })()`)

/** Picks a 1200×1600 PNG named `name` in the panel's file input. */
const upload = (name) =>
  b.eval(`(async () => { const c = document.createElement('canvas'); c.width = 1200; c.height = 1600; const x = c.getContext('2d');
    x.fillStyle = '#fff'; x.fillRect(0, 0, 1200, 1600); x.fillStyle = '#000'; x.font = '60px sans-serif';
    for (let i = 0; i < 20; i++) x.fillText('ITEM ' + i + '   ' + (i * 1.37).toFixed(2), 80, 120 + i * 70);
    const blob = await new Promise((r) => c.toBlob(r, 'image/png')); const f = new File([blob], ${JSON.stringify(name)}, { type: 'image/png' });
    const dt = new DataTransfer(); dt.items.add(f); const input = document.querySelector('main input[type=file]');
    input.files = dt.files; input.dispatchEvent(new Event('change', { bubbles: true })); return f.size })()`)

async function signIn() {
  await b.goto(`${URL0}/login`)
  await b.waitFor('document.querySelector("input[autocomplete=username]")')
  await b.fill('input[autocomplete=username]', 'anna_t3')
  await b.fill('input[autocomplete=current-password]', 'anna-pass-123')
  await b.click('button.btn-primary')
  await b.waitFor('location.pathname !== "/login"')
}

async function step(name, fn) {
  try {
    await fn()
  } catch (e) {
    check(name, false, e.message.slice(0, 300))
  }
}

try {
  await b.viewport(1280, 900)
  await signIn()

  await step('New bill: the button opens the panel', async () => {
    await b.goto(`${URL0}/bills/new`)
    await b.waitText('From a receipt')
    await spy()
    await b.click('main a', 'From a receipt')
    await b.waitFor('location.pathname === "/bills/new/receipt"')
    const t = await text()
    check('the panel: picker, what it fills, the privacy note with the model', t.includes('Choose a photo') && t.includes('It fills this form') && t.includes('model fake/receipt-fixtures'))
    check('no Form/Text switch in the panel', !t.includes('Open in'))
  })

  await step('form result', async () => {
    await upload('supermarket-discount.png')
    await b.waitFor('location.pathname === "/bills/new"', 20000)
    await b.waitText('Milk 1L')
    const up = await b.eval('window.__uploads')
    check('the shrunk upload keeps the stem, as JPEG', up.length === 1 && up[0].name === 'supermarket-discount.jpg' && up[0].type === 'image/jpeg', JSON.stringify(up))
    const vals = await inputs()
    check('the form: description, currency, total', (await value(DESC)) === 'FreshMart 2026-11-21' && vals.includes('EUR') && vals.includes('12.06'), JSON.stringify(vals))
    const t = await text()
    check('the folded lines', ['Milk 1L', 'Rye bread', 'Gouda cheese', 'Apples ×1.235', '5.16'].every((s) => t.includes(s)))
    check('the warning above the lines', t.includes('was spread over all lines'))
    check('the panel closed', !t.includes('Choose a photo'))
  })

  if (SHOTS) await step('screenshot: form', () => shots('form-filled', '/bills/new'))

  await step('dismiss a warning', async () => {
    await b.click('main button[aria-label="Dismiss"]')
    check('the warning is gone', !(await text()).includes('was spread over all lines'))
  })

  await step('replace question', async () => {
    await b.click('main a', 'From a receipt')
    await b.waitFor('location.pathname === "/bills/new/receipt"')
    await spy()
    await upload('cafe-eur.png')
    await b.waitText('Replace the form?', 20000)
    if (SHOTS) await b.shot(join(SHOTS, 'replace-1280-light.png'))
    await b.click('main button', 'Keep my form')
    check('Keep: the form is unchanged', (await value(DESC)) === 'FreshMart 2026-11-21')
    await upload('cafe-eur.png')
    await b.waitText('Replace the form?', 20000)
    await b.click('main button', 'Replace')
    await b.waitFor('location.pathname === "/bills/new"')
    check('Replace: the cafe receipt is in', (await value(DESC)).startsWith('Corner Café'), await value(DESC))
  })

  await step('errors keep the form', async () => {
    await b.click('main a', 'From a receipt')
    await b.waitFor('location.pathname === "/bills/new/receipt"')
    await upload('busy.png')
    await b.waitText('busy or out of quota', 20000)
    if (SHOTS) await b.shot(join(SHOTS, 'error-busy-1280-light.png'))
    await upload('not-a-receipt.png')
    await b.waitText("This doesn't look like a receipt.", 20000)
    check('busy, then not a receipt: one message each, the form kept', (await value(DESC)).startsWith('Corner Café') && !(await text()).includes('busy or out of quota'))
  })

  await step('cancel', async () => {
    await b.eval(`(() => { const orig = window.fetch; window.fetch = (u, init) => init && init.body instanceof FormData ? new Promise((res, rej) => init.signal.addEventListener('abort', () => rej(new DOMException('aborted', 'AbortError')))) : orig(u, init) })()`)
    await upload('cafe-eur.png')
    await b.waitText('Reading the receipt…')
    if (SHOTS) await b.shot(join(SHOTS, 'busy-1280-light.png'))
    await b.click('main button', 'Cancel')
    await sleep(300)
    const t = await text()
    check('Cancel: back to the picker, no error', t.includes('Choose a photo') && !t.includes('Reading the receipt'))
    await b.reload() // drops the stub
  })

  let billId
  await step('save a draft from a prefilled form', async () => {
    await b.goto(`${URL0}/bills/new`)
    await b.waitFor(`document.querySelector('main textarea')?.value.startsWith('Corner Café')`)
    check('the receipt’s printed time, not shifted by the time zone', (await inputs()).includes('2026-11-20T19:42'), JSON.stringify(await inputs()))
    await b.click('main button', 'Save as draft')
    await b.waitFor('/^\\/bills\\/[0-9a-f-]{36}$/.test(location.pathname)', 20000)
    billId = (await b.path()).split('/')[2]
    const bill = await call(admin, 'GET', `/Bills/${billId}`)
    check('the draft: cafe lines and total', bill.description.startsWith('Corner Café') && bill.currencyCode === 'EUR' && bill.lines.length === 4 && bill.status === 'Draft', `${bill.description} ${bill.totalWithTips} ${bill.lines.length} ${bill.status}`)
  })

  await step('text result', async () => {
    await b.goto(`${URL0}/bills/new/paste`)
    await b.waitText('From a receipt')
    await b.click('main a', 'From a receipt')
    await b.waitFor('location.pathname === "/bills/new/paste/receipt"')
    check('the panel on Paste text fills the text', (await text()).includes('It fills the bill text below'))
    await upload('supermarket-discount.png')
    await b.waitFor('location.pathname + location.search === "/bills/new/paste?preview=1"', 20000)
    await b.waitText('Looks good', 20000)
    const t = await text()
    check('the text, the hint, the warning and the preview', (await value(DESC)).startsWith('FreshMart 2026-11-21\n\n12.06\nEUR') && t.includes('People and payers are you for now; change them before saving.') && t.includes('was spread over all lines'))
  })

  if (SHOTS) await step('screenshot: text', () => shots('paste-filled', '/bills/new/paste?preview=1'))

  await step('the bot’s link', async () => {
    await b.goto(`${URL0}/bills/new`)
    await b.waitText('Clear form')
    await b.click('main button', 'Clear form')
    const form = new FormData()
    form.append('image', new Blob([Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==', 'base64')], { type: 'image/png' }), 'tips-outside.png')
    const r = await fetch(`${BASE}/Bills/receipt`, { method: 'POST', headers: { Authorization: `Bearer ${admin}` }, body: form })
    const draft = await r.json()
    check('POST Bills/receipt gives a draft', r.status === 200 && !!draft.id, `${r.status}`)
    await b.goto(`${URL0}/bills/new?receipt=${draft.id}`)
    await b.waitFor(`document.querySelector('main textarea')?.value === ${JSON.stringify(draft.description)}`, 20000)
    check('the link fills the empty form and leaves the URL', (await b.path()) === '/bills/new', await b.path())
    await b.reload()
    await b.waitText('Clear form')
    check('a reload keeps the form and asks nothing', !(await text()).includes('Replace the form?') && (await value(DESC)) === draft.description)

    await b.goto(`${URL0}/bills/new?receipt=${draft.id}`)
    await b.waitText('Replace the form?', 20000)
    check('the link on a filled form asks first, and leaves the URL', (await b.path()) === '/bills/new')
    await b.click('main button', 'Keep my form')

    await b.goto(`${URL0}/bills/new?receipt=00000000-0000-0000-0000-000000000000`)
    await b.waitText("This receipt has expired or isn't yours.", 20000)
    check('404: the message, the form left alone, the URL clean', (await value(DESC)) === draft.description && (await b.path()) === '/bills/new')
  })

  await step('Profile: New bill opens', async () => {
    await b.goto(`${URL0}/profile`)
    await b.waitText('New bill opens')
    await b.click('main label', 'Paste text')
    const saved = await b.eval(`(() => { const s = [...document.querySelectorAll('main section')].find((e) => e.innerText.includes('New bill opens')); const btn = s && s.querySelector('button[type=submit]'); if (!btn) return false; btn.click(); return true })()`)
    check('the New bill card has its own Save', saved)
    await b.waitText('✓ Saved')
    const p = await call(admin, 'GET', '/profile')
    check('saved as Text, the details unchanged', p.receiptOpenIn === 'Text' && p.displayName.length > 0, `${p.receiptOpenIn}`)
    if (SHOTS) await shots('profile', '/profile')
    const href = await b.eval(`[...document.querySelectorAll('aside nav a')].find(a => a.innerText.includes('New bill'))?.getAttribute('href')`)
    check('➕ New bill goes to Paste text', href === '/bills/new/paste', href)

    const form = new FormData()
    form.append('image', new Blob([Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==', 'base64')], { type: 'image/png' }), 'decimal-comma.png')
    const draft = await (await fetch(`${BASE}/Bills/receipt`, { method: 'POST', headers: { Authorization: `Bearer ${admin}` }, body: form })).json()
    await b.goto(`${URL0}/bills/new?receipt=${draft.id}`)
    await b.waitText('Replace the text?', 20000).catch(() => b.waitText('Looks good', 20000))
    if ((await text()).includes('Replace the text?')) await b.click('main button', 'Replace')
    await b.waitFor(`location.pathname + location.search === "/bills/new/paste?preview=1"`, 20000)
    check('with Paste text, the bot’s link opens the text', (await value(DESC)) === draft.text)

    await call(admin, 'PUT', '/profile', { displayName: p.displayName, phone: p.phone, email: p.email, receiptOpenIn: 'Form' })
  })

  if (SHOTS) await step('screenshot: panel', () => shots('panel', '/bills/new/receipt'))

  const problems = b.takeProblems().filter((p) => !/status of 4\d\d|401|404|422|503/.test(p))
  check('no console errors', problems.length === 0, problems.join(' | '))
} finally {
  await b.close()
}

/** The page at 1280 and 390 px, light and dark. */
async function shots(name, path) {
  for (const [w, phone] of [
    [1280, false],
    [390, true],
  ])
    for (const theme of ['light', 'dark']) {
      await b.viewport(w, 900, phone)
      await b.eval(`localStorage.setItem('debtbot.theme', '${theme}'); localStorage.setItem('debtbot.sidebar', '${phone ? '1' : '0'}')`)
      await b.goto(`${URL0}${path}`)
      await sleep(800)
      await b.shot(join(SHOTS, `${name}-${w}-${theme}.png`))
    }
  await b.viewport(1280, 900)
  await b.eval(`localStorage.setItem('debtbot.theme', 'light'); localStorage.setItem('debtbot.sidebar', '0')`)
  await b.goto(`${URL0}${path}`)
}

console.log(failed ? `${failed} FAILED` : 'ALL PASSED')
process.exit(failed ? 1 : 0)
