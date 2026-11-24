// T8 API checks: admin-only endpoints refuse non-admins (403), your own role can't change (400), Regular removes admin,
// and GET Bills/All takes its filters. Usage: node api8.mjs (after seed8.mjs)
import { readFileSync } from 'node:fs'
import { BASE, mint } from './seed8.mjs'

const { ids } = JSON.parse(readFileSync(new URL('./seed8.json', import.meta.url)))
const anna = mint(ids.anna, 'Anna Petrova', true)
const boris = mint(ids.boris, 'Boris Ivanov')
let failed = 0

async function status(token, method, path) {
  const r = await fetch(BASE + path, { method, headers: { Authorization: `Bearer ${token}` } })
  return { status: r.status, body: await r.text() }
}
async function expect(name, token, method, path, want) {
  const r = await status(token, method, path)
  const ok = r.status === want
  if (!ok) failed++
  console.log(ok ? 'PASS' : 'FAIL', `${method} ${path} as ${name} -> ${r.status} (want ${want})`, ok ? '' : r.body.slice(0, 200))
  return r
}

for (const [m, p] of [['GET', '/Users'], ['GET', '/Users/merge/suggestions'], ['GET', '/Bills/All'], ['POST', `/Users/${ids.lena}/Role?role=Admin`], ['POST', `/Users/${ids.lena}/password`], ['DELETE', `/Users/${ids.grandpa}`], ['POST', '/Users/merge']])
  await expect('Boris (regular)', boris, m, p, 403)

await expect('Anna', anna, 'POST', `/Users/${ids.anna}/Role?role=Regular`, 400)
await expect('Anna', anna, 'POST', `/Users/${ids.lena}/Role?role=Admin`, 200)
await expect('Anna', anna, 'POST', `/Users/${ids.lena}/Role?role=Regular`, 200)
const lena = JSON.parse((await expect('Anna', anna, 'GET', '/Users?search=lena_s', 200)).body).items[0]
if (lena.role !== 'Regular') (failed++, console.log('FAIL Lena should be Regular again', lena.role))
await expect('Anna', anna, 'POST', `/Users/00000000-0000-0000-0000-000000000001/Role?role=Admin`, 404)

const all = JSON.parse((await expect('Anna', anna, 'GET', `/Bills/All?creatorId=${ids.boris}&countPerPage=50`, 200)).body)
if (!all.items.length || all.items.some((b) => b.creator.id !== ids.boris)) (failed++, console.log('FAIL creatorId filter'))
const withOlga = JSON.parse((await expect('Anna', anna, 'GET', `/Bills/All?withUserId=${ids.olga}&currencyCode=eur`, 200)).body)
if (withOlga.totalCount !== 1 || withOlga.items[0].participantCount !== 2) (failed++, console.log('FAIL withUserId/participantCount', JSON.stringify(withOlga.items.map((b) => [b.description, b.participantCount]))))
await expect('Anna', anna, 'GET', '/Bills/All?sortBy=Nope', 400)

console.log(failed ? `${failed} FAILED` : 'all API checks passed')
process.exitCode = failed ? 1 : 0
