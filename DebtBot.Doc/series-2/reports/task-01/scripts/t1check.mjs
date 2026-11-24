// Series 2, T1 runtime check against the e2e stack, after web-ui/e2e/seed.mjs:
// New bill with yourself removed (others pay and share), save as draft, the draft as the form: add yourself back
// (search + ✎ Edit line), save, remove again, save, finalize; the debts and lists through the API; screenshots.
// Usage: node t1check.mjs <screenshot dir>
import { mkdirSync } from 'node:fs'
import { launch } from '../../../../web-ui/e2e/lib/cdp.mjs'
import { URL0, call, loadSeed, mint, sleep } from '../../../../web-ui/e2e/lib/api.mjs'

const OUT = process.argv[2] ?? `${process.env.TMPDIR ?? '/tmp'}/t1check`
mkdirSync(OUT, { recursive: true })
const { ids } = loadSeed()
const anna = mint(ids.anna, 'Anna Petrova', true)
const results = []
const b = await launch(Number(process.env.E2E_CDP_PORT ?? 9381))

const expect = (cond, msg) => {
  if (!cond) throw new Error(msg)
}
const allowed = (p) => /status of 401/.test(p) || /auth\/refresh/.test(p)
async function step(name, fn) {
  b.takeProblems()
  try {
    await fn()
    const problems = b.takeProblems().filter((p) => !allowed(p))
    expect(problems.length === 0, `console: ${problems.join(' | ')}`)
    results.push(['PASS', name])
    console.log('PASS', name)
  } catch (e) {
    results.push(['FAIL', name, e.message])
    console.log('FAIL', name, '—', e.message)
    await b.shot(`${OUT}/FAIL-${results.length}.png`).catch(() => {})
    console.log('  at', await b.path().catch(() => '?'), '|', (await main().catch(() => '')).replace(/\s+/g, ' ').slice(0, 500))
  }
}
const W = { desktop: [1280, 900], phone: [390, 844] }
async function shots(name) {
  // No open suggestion list, and the sidebar as it starts on each width.
  await b.eval('document.activeElement?.blur()')
  for (const w of ['desktop', 'phone']) {
    await b.eval('try { localStorage.removeItem("debtbot.sidebar") } catch {}')
    await b.viewport(...W[w], w === 'phone')
    await b.reload()
    await b.waitFor('document.querySelector("main h1")')
    await sleep(500)
    for (const dark of [false, true]) {
      await b.theme(dark)
      await sleep(300)
      await b.shot(`${OUT}/${name}-${W[w][0]}-${dark ? 'dark' : 'light'}.png`)
    }
  }
  await b.theme(false)
  await b.viewport(...W.desktop)
}
const main = () => b.eval('document.querySelector("main")?.innerText ?? ""')
const loaded = (text) => b.waitFor(`document.querySelector("main") && document.querySelector("main").innerText.includes(${JSON.stringify(text)})`, 20000)
async function pick(inputSelector, typed, name) {
  await b.eval(`document.querySelector(${JSON.stringify(inputSelector)}).focus()`)
  await b.fill(inputSelector, typed)
  await b.waitFor(`[...document.querySelectorAll('main [role=option]')].some(o => o.innerText.includes(${JSON.stringify(name)}))`)
  await b.click('main [role=option]', name)
}
const fillLabel = (label, value, scope = 'main') =>
  b
    .eval(
      `(() => { const l = [...document.querySelectorAll(${JSON.stringify(scope + ' label')})].find(x => x.innerText.trim().startsWith(${JSON.stringify(label)})); if (!l) return null;
    const el = l.htmlFor ? document.getElementById(l.htmlFor) : l.querySelector('input,textarea,select'); if (!el) return null; el.setAttribute('data-e2e', '1'); return true })()`,
    )
    .then(async (ok) => {
      expect(ok, `no field ${label}`)
      await b.fill('[data-e2e="1"]', value)
      await b.eval('document.querySelector("[data-e2e=\\"1\\"]").removeAttribute("data-e2e")')
    })
/** The chips of "People on this bill": [name, removable]. */
const chips = () =>
  b.eval(`[...document.querySelectorAll('main section')].find(s => s.innerText.includes('People on this bill'))
    .querySelectorAll('span.inline-flex').length ? [...[...document.querySelectorAll('main section')].find(s => s.innerText.includes('People on this bill')).querySelectorAll('span.inline-flex')].map(c => [c.firstChild.textContent, !!c.querySelector('button')]) : []`)
const lineText = (item) => b.eval(`[...document.querySelectorAll('main li')].find(l => l.querySelector('b')?.innerText === ${JSON.stringify(item)})?.innerText ?? ''`)
const ME = 'Anna Petrova (you)'
let billId

try {
  await step('sign in as Anna', async () => {
    await b.viewport(...W.desktop)
    await b.goto(`${URL0}/login`)
    await b.waitFor('document.querySelector("input[autocomplete=username]")')
    await b.fill('input[autocomplete=username]', 'anna')
    await b.fill('input[autocomplete=current-password]', 'anna-pass-123')
    await b.click('button.btn-primary')
    await b.waitFor('location.pathname !== "/login"')
    await b.eval('sessionStorage.clear()')
  })

  await step('New bill: you leave; Boris pays, Boris and Dmitry share', async () => {
    await b.goto(`${URL0}/bills/new`)
    await loaded('People on this bill')
    await fillLabel('Description', 'T1 dinner')
    await fillLabel('Bill currency', 'eur')
    await pick('main input[placeholder^="Add a person"]', 'boris i', 'Boris Ivanov')
    await pick('main input[placeholder^="Add a person"]', 'dmi', 'Dmitry K.')
    await b.click('main a', 'Add line')
    await fillLabel('Item', 'Pasta', 'main form')
    await fillLabel('Subtotal', '60', 'main form')
    await b.click('main form button[type=submit]')
    await b.waitFor('location.pathname === "/bills/new"')
    expect((await lineText('Pasta')).includes(ME.replace(' (you)', '')) || (await lineText('Pasta')).includes('Anna'), 'you are not on the new line')
    // Equal-split line only: your chip is removable.
    expect((await chips()).some(([n, x]) => n.startsWith('Anna') && x), `your chip isn't removable: ${JSON.stringify(await chips())}`)
    await b.click('main a', 'Add payment')
    await b.waitFor('location.pathname === "/bills/new/add-payment"')
    // You're on the bill, so you're preselected; pick Boris.
    expect((await b.eval('document.querySelector("main form select").value')) === ids.anna, 'you are not the preselected payer')
    await fillLabel('Who paid', ids.boris, 'main form')
    await fillLabel('Amount', '60', 'main form')
    await b.click('main form button[type=submit]')
    await b.waitFor('location.pathname === "/bills/new"')
    expect(!(await chips()).find(([n]) => n.startsWith('Boris'))[1], 'the payer is removable')
    await b.click('main button[aria-label^="Remove Anna"]')
    await sleep(200)
    expect(!(await chips()).some(([n]) => n.startsWith('Anna')), 'you are still on the bill')
    expect(!(await lineText('Pasta')).includes('Anna'), 'you are still on Pasta')
    const m = await main()
    expect(m.includes('Dmitry K. owes Boris Ivanov'), 'the preview has no "Dmitry owes Boris"')
    expect(!/owes you|You owe/.test(m), 'the preview still has you')
    await b.reload()
    await loaded('People on this bill')
    expect(!(await chips()).some(([n]) => n.startsWith('Anna')), 'reload brought you back')
    await b.click('main a', 'Add payment')
    await b.waitFor('location.pathname === "/bills/new/add-payment"')
    expect((await b.eval('document.querySelector("main form select").value')) === ids.boris, 'without you, the first person should be preselected')
    await b.click('main form button', 'Cancel')
    await b.waitFor('location.pathname === "/bills/new"')
    await shots('newbill-you-left')
  })

  await step('Save as draft; the draft opens as the form, without you', async () => {
    await b.click('main button', 'Save as draft')
    await b.waitFor('/^\\/bills\\/[0-9a-f-]{36}$/.test(location.pathname)', 20000)
    billId = (await b.path()).split('/')[2]
    await loaded('Saved.')
    expect(!(await chips()).some(([n]) => n.startsWith('Anna')), 'the draft shows you on the bill')
    const bill = await call(anna, 'GET', `/Bills/${billId}`)
    expect(bill.creator.id === ids.anna, 'not your bill')
    expect(bill.lines[0].participants.every((p) => p.user.id !== ids.anna), 'you are on the saved line')
    const mine = await call(anna, 'GET', '/Bills?status=Draft&countPerPage=50')
    expect(mine.items.some((x) => x.id === billId), 'the draft is not in your Bills')
    await shots('draft-form')
  })

  await step('Draft: add yourself back from the search, tick yourself in ✎ Edit line, save', async () => {
    await pick('main input[placeholder^="Add a person"]', 'ann', ME)
    expect((await chips()).some(([n]) => n.startsWith('Anna')), 'not added back')
    expect((await main()).includes('Saved.'), 'adding a person alone shows unsaved changes')
    await b.click('main a[aria-label="Edit Pasta"]')
    await b.waitFor(`location.pathname === "/bills/${billId}/edit-line/1"`)
    await loaded('Edit line')
    await shots('draft-edit-line')
    await b.eval(`[...document.querySelectorAll('main form li')].find(l => l.innerText.includes('Anna')).querySelector('input[type=checkbox]').click()`)
    await b.click('main form button[type=submit]')
    await b.waitFor(`location.pathname === "/bills/${billId}"`)
    await loaded('Unsaved changes')
    // Reload keeps the unsaved form.
    await b.reload()
    await loaded('Unsaved changes')
    expect((await lineText('Pasta')).includes('Anna'), 'the edit was lost on reload')
    await b.click('main button', 'Save')
    await loaded('Saved.')
    const bill = await call(anna, 'GET', `/Bills/${billId}`)
    expect(bill.lines[0].participants.some((p) => p.user.id === ids.anna), 'you are not on the saved line')
  })

  await step('Draft: remove yourself again (equal line), save', async () => {
    expect((await chips()).find(([n]) => n.startsWith('Anna'))[1], 'your chip is locked on an equal line')
    await b.click('main button[aria-label^="Remove Anna"]')
    await loaded('Unsaved changes')
    await b.click('main button', 'Save')
    await loaded('Saved.')
    const bill = await call(anna, 'GET', `/Bills/${billId}`)
    expect(bill.lines[0].participants.map((p) => p.user.id).sort().join() === [ids.boris, ids.dmitry].sort().join(), 'Pasta is not Boris and Dmitry')
    await b.reload()
    await loaded('Saved.')
    expect(!(await chips()).some(([n]) => n.startsWith('Anna')), 'you came back after reload')
  })

  await step('Finalize: Dmitry owes Boris 30 EUR, you get nothing and still own the bill', async () => {
    await b.click('main button', 'Finalize')
    await loaded('Finalized')
    const bill = await call(anna, 'GET', `/Bills/${billId}`)
    expect(bill.status === 'Processed', `status ${bill.status}`)
    expect(bill.ledgerRecords.length === 1, `ledger ${JSON.stringify(bill.ledgerRecords)}`)
    const r = bill.ledgerRecords[0]
    expect(r.debtorUser.id === ids.dmitry && r.creditorUser.id === ids.boris && r.amount === 30, `record ${JSON.stringify(r)}`)
    expect(bill.spendings.every((s) => s.user?.id !== ids.anna && s.userId !== ids.anna), 'you have a spending')
    const mine = await call(anna, 'GET', '/Bills?countPerPage=100')
    expect(mine.items.some((x) => x.id === billId), 'the bill left your Bills')
    await shots('finalized')
  })
} finally {
  await b.close()
}
const failed = results.filter((r) => r[0] === 'FAIL')
console.log(`${results.length - failed.length}/${results.length} passed`)
process.exit(failed.length ? 1 : 0)
