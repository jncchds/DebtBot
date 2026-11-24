// T8 runtime check in headless Chrome: Admin › Users, Merge users and All bills. Usage: node check8.mjs <screenshot dir>
import { readFileSync } from 'node:fs'
import { launch } from './cdp.mjs'
import { call, mint } from './seed8.mjs'

const URL0 = 'http://localhost:18099'
const OUT = process.argv[2]
const { ids } = JSON.parse(readFileSync(new URL('./seed8.json', import.meta.url)))
const results = []
const sleep = (ms) => new Promise((r) => setTimeout(r, ms))
const root = mint(ids.anna, 'Anna Petrova', true)
const api = (path) => call(root, 'GET', path)
const userRow = async (search, id) => (await api(`/Users?search=${encodeURIComponent(search)}`)).items.find((u) => u.id === id)

async function step(name, fn) {
  try {
    await fn()
    results.push(['PASS', name])
    console.log('PASS', name)
  } catch (e) {
    results.push(['FAIL', name, e.message])
    console.log('FAIL', name, e.message)
    try {
      await b.shot(`${process.env.TMPDIR ?? '/tmp'}/t8-fail-${results.length}.png`)
      console.log('  at', await b.path(), '|', (await b.text()).replace(/\s+/g, ' ').slice(0, 500))
    } catch {
      /* page gone */
    }
  }
}
const expect = (cond, msg) => {
  if (!cond) throw new Error(msg)
}

const b = await launch(9358)
for (const ev of ['uncaughtException', 'unhandledRejection'])
  process.on(ev, async (e) => {
    console.log('ABORT', e?.message ?? e)
    await b.close()
    process.exit(1)
  })
await b.viewport(1280, 900)
await b.theme(false)

const shots = async (name) => {
  for (const dark of [false, true]) {
    await b.theme(dark)
    await sleep(300)
    await b.shot(`${OUT}/${name}-${dark ? 'dark' : 'light'}.png`)
  }
  await b.theme(false)
}
const phone = async () => {
  await b.eval('localStorage.removeItem("debtbot.sidebar")')
  await b.viewport(390, 844, true)
  await b.reload()
}
const desktop = async () => {
  await b.eval('localStorage.removeItem("debtbot.sidebar")')
  await b.viewport(1280, 900)
  await b.reload()
}
const noOverflow = async () => {
  const w = await b.eval('document.documentElement.scrollWidth')
  const vw = await b.eval('window.innerWidth')
  const wide = await b.eval(`[...document.querySelectorAll('main *')].filter(e => e.getBoundingClientRect().right > window.innerWidth + 1).map(e => e.tagName + '.' + (e.className?.toString?.() ?? '').slice(0, 40)).slice(0, 3)`)
  expect(w <= vw && wide.length === 0, `wider than the screen: ${w} > ${vw} ${JSON.stringify(wide)}`)
}
const signIn = async (login, password) => {
  await b.goto(`${URL0}/login`)
  await b.waitFor('document.querySelector("input[autocomplete=username]")')
  await b.fill('input[autocomplete=username]', login)
  await b.fill('input[autocomplete=current-password]', password)
  for (let i = 0; ; i++) {
    await b.click('button.btn-primary')
    try {
      await b.waitFor('location.pathname !== "/login" || document.body.innerText.includes("Sign-in failed")')
    } catch {
      /* still waiting */
    }
    if ((await b.path()) !== '/login') return
    if (i === 3) throw new Error('sign-in failed 4 times')
    console.log('  sign-in refused, waiting 30 s')
    await sleep(30000)
  }
}
const signOut = async () => {
  await b.click('aside button', 'Sign out')
  await b.waitFor('location.pathname === "/login"')
}
const main = () => b.eval('document.querySelector("main").innerText')
const aside = () => b.eval('document.querySelector("aside").innerText')
const rows = () => b.eval('[...document.querySelectorAll("main tbody tr")].map(r => r.innerText.replace(/\\s+/g, " ").trim())')
const params = () => b.eval('Object.fromEntries(new URLSearchParams(location.search))')
/** Clicks a button or link whose text includes `label`, inside the table row (or card) whose text includes `row`. */
const clickIn = (row, label) =>
  b.eval(`(() => { const r = [...document.querySelectorAll('main tbody tr, main li')].find(e => e.innerText.includes(${JSON.stringify(row)})); if (!r) return 'no row';
    const el = [...r.querySelectorAll('a,button')].find(e => (e.innerText + ' ' + (e.title || '') + ' ' + (e.getAttribute('aria-label') || '')).includes(${JSON.stringify(label)})); if (!el) return 'no button'; if (el.disabled) return 'disabled'; el.click(); return 'ok' })()`).then(async (r) => {
    expect(r === 'ok', `${row} / ${label}: ${r}`)
    await sleep(300)
  })
const badge = () => b.eval(`(() => { const e = [...document.querySelectorAll('aside a')].find(a => a.innerText.includes('Merge users')); return e ? e.innerText.replace(/\\s+/g, ' ') : null })()`)
/** Types into a user search box (the n-th on the page) and picks the option containing `name`. */
const pickUser = async (inputSel, text, name) => {
  await b.eval(`document.querySelector(${JSON.stringify(inputSel)}).focus()`)
  await b.fill(inputSel, text)
  await b.waitFor(`[...document.querySelectorAll('[role=option]')].some(o => o.innerText.includes(${JSON.stringify(name)}))`)
  await b.click('[role=option]', name)
}

await step('sign in as Anna (admin)', () => signIn('anna.p', 'anna-pass-123'))

// ---------------- Users ----------------
await step('Users: list, duplicates marked, own role disabled', async () => {
  await b.goto(`${URL0}/admin/users`)
  await b.waitText('Clara Müller')
  const r = await rows()
  const total = (await api('/Users?countPerPage=1')).totalCount
  expect((await main()).includes(`1–${total} of ${total}`), `range for ${total}`)
  expect(r.find((x) => x.startsWith('@Boris_Iv'))?.includes('possible duplicate'), 'duplicate marker on @Boris_Iv')
  expect(r.find((x) => x.startsWith('Clara M '))?.includes('possible duplicate'), 'duplicate marker on Clara M')
  expect(!r.find((x) => x.startsWith('Lena'))?.includes('possible duplicate'), 'no marker on Lena')
  expect(r.find((x) => x.startsWith('Anna Petrova'))?.includes('(you)'), '(you)')
  const disabled = await b.eval(`(() => { const btn = [...[...document.querySelectorAll('main tbody tr')].find(r => r.innerText.includes('(you)')).querySelectorAll('button')].find(x => x.innerText.includes('Remove admin')); return btn.disabled && btn.title })()`)
  expect(disabled === 'You can’t change your own role', `own role button: ${disabled}`)
  expect((await badge()).includes('2'), `badge: ${await badge()}`)
  // Only placeholders that aren't you get 🗑.
  const bins = await b.eval(`[...document.querySelectorAll('main tbody tr')].filter(r => r.querySelector('button[aria-label^="Delete"]')).map(r => r.innerText.split('\\n')[0])`)
  expect(bins.every((n) => ['@Boris_Iv', 'Clara M', '@grandpa_vlad', '@olga_petrova', 'Dima', 'Admin'].some((p) => n.startsWith(p))) && bins.length === 6, `bins: ${bins}`)
  await shots('users-1280')
})

await step('Users: search and role filter in the URL, Back and reload', async () => {
  await b.fill('main input[type=search]', 'boris')
  await b.eval(`document.querySelector('main input[type=search]').dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true }))`)
  await b.waitFor('location.search.includes("search=boris")')
  await b.waitFor('document.querySelectorAll("main tbody tr").length === 2')
  expect((await rows()).every((r) => /boris/i.test(r)), 'search rows')
  await shots('users-search-1280')
  await b.click('main [role=group] button', 'Admin')
  await b.waitFor('location.search.includes("role=admin")')
  await b.waitFor('document.body.innerText.includes("No users match")')
  await b.click('main button', 'Clear filters')
  await b.waitFor('location.search === ""')
  await b.click('main [role=group] button', 'Admin')
  await b.waitFor('location.search === "?role=admin"')
  await b.waitFor('document.querySelectorAll("main tbody tr").length === 3')
  const names = (await rows()).map((r) => r.split(' ')[0])
  expect(names.join() === 'admin,Anna,Dmitry' || names.length === 3, `admins: ${names}`)
  await b.reload()
  await b.waitFor('document.querySelectorAll("main tbody tr").length === 3')
  await b.back()
  await b.waitFor('location.search === ""')
  await b.back()
  await b.waitFor('location.search.includes("search=boris")')
  await b.goto(`${URL0}/admin/users?search=nobody-here`)
  await b.waitText('No users match')
  await shots('users-empty-1280')
})

await step('Users: make Lena admin, then remove it', async () => {
  await b.goto(`${URL0}/admin/users`)
  await b.waitText('Lena')
  await clickIn('Lena', 'Make admin')
  await b.waitText('Make Lena an admin?')
  await shots('users-role-1280')
  await b.eval(`[...document.querySelectorAll('main tbody tr')].find(r => r.innerText.includes('Make Lena an admin?')).querySelector('.btn-primary').click()`)
  await b.waitFor(`[...document.querySelectorAll('main tbody tr')].find(r => r.innerText.startsWith('Lena'))?.innerText.includes('Remove admin')`)
  expect((await userRow('lena_s', ids.lena)).role === 'Admin', 'API: Lena admin')
  await clickIn('Lena', 'Remove admin')
  await b.waitText('Remove admin from Lena?')
  await b.eval(`[...document.querySelectorAll('main tbody tr')].find(r => r.innerText.includes('Remove admin from Lena?')).querySelector('.btn-primary').click()`)
  await b.waitFor(`[...document.querySelectorAll('main tbody tr')].find(r => r.innerText.startsWith('Lena'))?.innerText.includes('Make admin')`)
  expect((await userRow('lena_s', ids.lena)).role === 'Regular', 'API: Lena regular')
})

await step('Users: Login & password dialog (409, then saved), with its own URL', async () => {
  await clickIn('Lena', 'Set')
  await b.waitFor(`location.pathname === '/admin/users/${ids.lena}/password'`)
  await b.waitText('Login & password · Lena')
  expect((await b.text()).includes('Lena has no login yet.'), 'login help')
  await shots('users-password-1280')
  await b.fill('#apw-login', 'clara')
  await b.fill('#apw-next', 'lena-pass-123')
  await b.fill('#apw-repeat', 'lena-pass-123')
  await b.click('[role=dialog] button[type=submit]')
  await b.waitText('The login “clara” is taken. Pick another one.')
  expect(await b.eval('document.querySelector("#apw-login").getAttribute("aria-invalid") === "true"'), 'login marked')
  await shots('users-password-taken-1280')
  // Reload keeps the dialog (its own URL).
  await b.reload()
  await b.waitText('Login & password · Lena')
  await b.fill('#apw-login', 'lena')
  await b.fill('#apw-next', 'short')
  await b.fill('#apw-repeat', 'short')
  await b.click('[role=dialog] button[type=submit]')
  await b.waitText('At least 8 characters.')
  await b.fill('#apw-next', 'lena-pass-123')
  await b.fill('#apw-repeat', 'lena-pass-123')
  await b.click('[role=dialog] button[type=submit]')
  await b.waitFor(`location.pathname === '/admin/users' && !document.querySelector('[role=dialog]')`)
  await b.waitFor(`[...document.querySelectorAll('main tbody tr')].find(r => r.innerText.startsWith('Lena'))?.innerText.includes('password set')`)
  const lena = await userRow('lena_s', ids.lena)
  expect(lena.login === 'lena' && lena.hasPassword, 'API: Lena login')
  // Back reopens the dialog's URL, Forward closes it again.
  await b.back()
  await b.waitText('Login & password · Lena')
  await b.forward()
  await b.waitFor('!document.querySelector("[role=dialog]")')
})

await step('Users: delete a stray placeholder; a user with data gives 409 → Merge', async () => {
  await clickIn('@grandpa_vlad', 'Delete')
  await b.waitText('Only stray placeholders can be deleted')
  await shots('users-delete-1280')
  await b.eval(`[...document.querySelectorAll('main tbody tr')].find(r => r.innerText.includes('Only stray placeholders')).querySelector('.btn-danger').click()`)
  await b.waitFor(`![...document.querySelectorAll('main tbody tr')].some(r => r.innerText.includes('grandpa_vlad'))`)
  expect(!(await userRow('grandpa', ids.grandpa)), 'API: grandpa gone')
  await clickIn('@olga_petrova', 'Delete')
  await b.eval(`[...document.querySelectorAll('main tbody tr')].find(r => r.innerText.includes('Only stray placeholders')).querySelector('.btn-danger').click()`)
  await b.waitText("can’t be deleted: they have bills or are in someone’s contacts")
  await shots('users-delete-409-1280')
  await clickIn("can’t be deleted", 'Merge…')
  await b.waitFor(`location.pathname === '/admin/merge' && location.search === '?merge=${ids.olga}'`)
  await b.waitFor(`document.querySelector('main').innerText.includes('@olga_petrova')`)
  expect(await b.eval(`!!document.querySelector('#manual-keep') && !document.querySelector('#manual-merge')`), 'Olga is the one to merge away; keep is a search box')
})

// ---------------- Merge ----------------
await step('Merge: suggestions, swap in the URL, Back undoes it', async () => {
  await b.goto(`${URL0}/admin/merge`)
  await b.waitText('Suggestions (2)')
  const text = await main()
  expect(text.includes('Same username') && text.includes('Same Telegram id'), 'reasons')
  expect(text.includes('suggested keeper on the left'), 'hint')
  await shots('merge-1280')
  const first = await b.eval(`document.querySelectorAll('main section')[0].innerText`)
  expect(/KEEP\s+Boris Ivanov/.test(first) && /MERGE AWAY\s+@Boris_Iv/.test(first), `Boris pair: ${first.slice(0, 200)}`)
  await b.eval(`[...document.querySelectorAll('main section')[0].querySelectorAll('button')].find(x => x.innerText.includes('Swap')).click()`)
  await b.waitFor(`location.search === '?swap=${ids.borisDup}'`)
  const swapped = await b.eval(`document.querySelectorAll('main section')[0].innerText`)
  expect(swapped.includes('Swapped: against the suggestion') && /KEEP\s+@Boris_Iv/.test(swapped), 'swapped')
  await shots('merge-swapped-1280')
  await b.reload()
  await b.waitText('Swapped: against the suggestion')
  await b.back()
  await b.waitFor('location.search === ""')
  await b.waitFor(`!document.body.innerText.includes('Swapped: against')`)
})

await step('Merge: one-click merge of a suggested pair, badge drops, kept user signs in with the password', async () => {
  const before = await userRow('boris_iv', ids.boris)
  await b.eval(`[...document.querySelectorAll('main section')[0].querySelectorAll('button')].find(x => x.innerText.includes('Merge…')).click()`)
  await b.waitText('Merge @Boris_Iv into Boris Ivanov? This can’t be undone.')
  const t = await main()
  expect(t.includes('@Boris_Iv’s 2 bills') && t.includes('Boris Ivanov has none, so they take @Boris_Iv’s login and password'), 'confirmation text')
  await shots('merge-confirm-1280')
  await b.click('main .btn-danger', 'Merge')
  await b.waitText('Merged @Boris_Iv into Boris Ivanov.')
  await b.waitText('Suggestions (1)')
  await b.waitFor(`document.querySelector('aside').innerText.includes('Merge users')`)
  await b.waitFor(`(() => { const e = [...document.querySelectorAll('aside a')].find(a => a.innerText.includes('Merge users')); return e && !/\\b2\\b/.test(e.innerText) && /\\b1\\b/.test(e.innerText) })()`)
  await shots('merge-done-1280')
  const after = await userRow('boris_iv', ids.boris)
  expect(after.login === 'boris.old' && after.hasPassword, `API: Boris took the login (${after.login})`)
  expect(after.billCount === before.billCount + 1, `API: bills ${before.billCount} -> ${after.billCount} (Bowling had both)`)
  expect(!(await userRow('Boris_Iv', ids.borisDup)), 'API: @Boris_Iv gone')
})

await step('Merge: manual merge with two searches over all users', async () => {
  await b.goto(`${URL0}/admin/merge`)
  await b.waitText('Merge any two users')
  expect((await main()).includes('Pick two different users.'), 'pick two hint')
  expect(await b.eval(`[...document.querySelectorAll('main section')].pop().querySelector('.btn-primary').disabled`), 'Merge… disabled')
  await b.eval(`document.querySelector('#manual-merge').focus()`)
  await b.fill('#manual-merge', 'dim')
  await b.waitFor(`[...document.querySelectorAll('[role=option]')].some(o => o.innerText.includes('@dima_k_old'))`)
  await shots('merge-manual-typing-1280')
  await b.click('[role=option]', 'Dima')
  await b.waitFor(`location.search === '?merge=${ids.dima}'`)
  await pickUser('#manual-keep', 'dmitry', 'Dmitry K.')
  await b.waitFor(`location.search === '?keep=${ids.dmitry}&merge=${ids.dima}'`)
  await b.waitFor(`[...document.querySelectorAll('main section')].pop().innerText.includes('MERGE AWAY')`)
  // Swap and back.
  await b.eval(`[...[...document.querySelectorAll('main section')].pop().querySelectorAll('button')].find(x => x.innerText.includes('Swap')).click()`)
  await b.waitFor(`location.search === '?keep=${ids.dima}&merge=${ids.dmitry}'`)
  await b.back()
  await b.waitFor(`location.search === '?keep=${ids.dmitry}&merge=${ids.dima}'`)
  await b.reload()
  await b.waitFor(`[...document.querySelectorAll('main section')].pop().innerText.includes('MERGE AWAY')`)
  await b.eval(`[...[...document.querySelectorAll('main section')].pop().querySelectorAll('button')].find(x => x.innerText.includes('Merge…')).click()`)
  await b.waitText('Merge Dima into Dmitry K.? This can’t be undone.')
  await shots('merge-manual-confirm-1280')
  await b.click('main .btn-danger', 'Merge')
  await b.waitText('Merged Dima into Dmitry K.')
  await b.waitFor(`location.search === ''`)
  expect(!(await userRow('dima', ids.dima)), 'API: Dima gone')
  expect((await userRow('dmitry_k', ids.dmitry)).billCount >= 1, 'API: Dmitry has the bills')
})

await step('Merge: merging away your own account is refused with the API message', async () => {
  await b.goto(`${URL0}/admin/merge?keep=${ids.lena}&merge=${ids.anna}`)
  await b.waitFor(`[...document.querySelectorAll('main section')].pop().innerText.includes('MERGE AWAY')`)
  await b.eval(`[...[...document.querySelectorAll('main section')].pop().querySelectorAll('button')].find(x => x.innerText.includes('Merge…')).click()`)
  await b.click('main .btn-danger', 'Merge')
  await b.waitText('You can')
  expect((await main()).includes('merge away the account you'), 'API message shown')
  expect(!!(await userRow('anna_petrova', ids.anna)), 'Anna still there')
})

// ---------------- All bills ----------------
await step('All bills: list, filters, sort, paging, reload, Back; rows open the bill', async () => {
  const all = await api('/Bills/All?countPerPage=1')
  await b.goto(`${URL0}/admin/bills`)
  await b.waitText(`of ${all.totalCount}`)
  expect((await main()).includes('Exchange with Boris') && (await main()).includes('Exchange pair'), 'exchange pair row')
  expect(/(100\.00 EUR ⇄ 430\.00 PLN|430\.00 PLN ⇄ 100\.00 EUR)/.test(await main()), 'pair total')
  await shots('bills-1280')
  await pickUser('main input[placeholder="Type a name…"]', 'boris', 'Boris Ivanov')
  await b.waitFor(`location.search === '?by=${ids.boris}'`)
  await b.waitText('Created by Boris Ivanov.')
  const byBoris = await api(`/Bills/All?creatorId=${ids.boris}&countPerPage=1`)
  await b.waitText(`of ${byBoris.totalCount}`)
  expect((await rows()).every((r) => r.includes('by Boris Ivanov')), 'creator rows')
  await shots('bills-filtered-1280')
  await b.click('main [role=group] button', 'Finalized')
  await b.waitFor('location.search.includes("status=finalized")')
  await b.fill('#list-sort', 'total')
  await b.waitFor('location.search.includes("sort=total")')
  await b.reload()
  await b.waitFor('document.querySelectorAll("main tbody tr").length > 0')
  const totals = await b.eval(`[...document.querySelectorAll('main tbody tr')].map(r => parseFloat(r.children[3].innerText))`)
  expect(totals.every((v, i) => i === 0 || totals[i - 1] >= v), `largest first: ${totals}`)
  await b.back()
  await b.waitFor('!location.search.includes("sort=total")')
  await b.goto(`${URL0}/admin/bills?with=${ids.olga}`)
  await b.waitText('Birthday flowers')
  await b.goto(`${URL0}/admin/bills?q=no-such-bill`)
  await b.waitText('No bills match these filters')
  await shots('bills-filtered-empty-1280')
  await b.goto(`${URL0}/admin/bills`)
  await b.waitText(`of ${all.totalCount}`)
  await b.click('main nav button', '2')
  await b.waitFor('location.search === "?page=2"')
  await b.waitText(`21–`)
  await b.reload()
  await b.waitText(`21–`)
  const first = await b.eval(`document.querySelector('main tbody tr td:nth-child(2) a').getAttribute('href')`)
  await b.eval(`document.querySelector('main tbody tr td:nth-child(3)').click()`)
  await b.waitFor(`location.pathname === ${JSON.stringify(first)}`)
  await b.back()
  await b.waitFor('location.search === "?page=2"')
})

// ---------------- Phone ----------------
await step('Phone: Users, Merge and All bills as cards; drawer with the Admin section', async () => {
  await b.goto(`${URL0}/admin/users`)
  await phone()
  await b.waitText('Clara Müller')
  expect(await b.eval('!document.querySelector("main table")'), 'cards')
  await noOverflow()
  await shots('users-390')
  await b.goto(`${URL0}/admin/merge`)
  await b.waitText('Suggestions (1)')
  await noOverflow()
  expect((await main()).includes('suggested keeper on top'), 'phone hint')
  expect(await b.eval(`getComputedStyle([...document.querySelectorAll('main section')].pop()).display === 'none'`), 'manual picker hidden')
  await shots('merge-390')
  await b.goto(`${URL0}/admin/bills`)
  await b.waitText('Exchange with Boris')
  await noOverflow()
  await shots('bills-390')
  await b.click('aside button', 'DebtBot')
  await b.waitFor(`document.querySelector('aside').innerText.includes('Duplicate suggestions')`)
  await shots('bills-390-drawer')
  await desktop()
})

// ---------------- Other users ----------------
await step('The merged user signs in with the moved password; a non-admin gets 404 on /admin/*', async () => {
  await signOut()
  await signIn('boris.old', 'boris-old-123')
  expect((await aside()).includes('Sign out (Boris Ivanov)'), 'signed in as Boris Ivanov')
  expect(!(await aside()).includes('Admin'), 'no Admin for Boris')
  await signOut()
  await signIn('lena', 'lena-pass-123')
  expect((await aside()).includes('Sign out (Lena)'), 'signed in as Lena')
  for (const p of ['/admin/users', '/admin/merge', '/admin/bills', `/admin/users/${ids.lena}/password`]) {
    await b.goto(`${URL0}${p}`)
    await b.waitFor(`document.querySelector('main') && document.querySelector('main').innerText.length > 0`)
    await sleep(400)
    const t = await main()
    expect(/not found|doesn’t exist|404/i.test(t) && !t.includes('Everyone DebtBot knows'), `${p}: ${t.slice(0, 120)}`)
  }
  await shots('nonadmin-404-1280')
})

await b.close()
const failed = results.filter((r) => r[0] === 'FAIL')
console.log(`${results.length - failed.length}/${results.length} passed`)
process.exitCode = failed.length ? 1 : 0
