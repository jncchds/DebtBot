// Shared settings and API helpers for the end-to-end scripts. Everything can be overridden from the environment:
//   E2E_URL (http://localhost:8080), E2E_KEY (the JWT key in compose.e2e.yml), E2E_DB (debtbot_postgres)
import { execFileSync } from 'node:child_process'
import { createHmac } from 'node:crypto'
import { readFileSync, writeFileSync } from 'node:fs'

export const URL0 = process.env.E2E_URL ?? 'http://localhost:8080'
export const BASE = `${URL0}/api/v1`
export const KEY = process.env.E2E_KEY ?? 'E2eRunKeyE2eRunKeyE2eRunKeyE2eRunKeyE2eRunKey00'
export const DB = process.env.E2E_DB ?? 'debtbot_postgres'
/** The default admin from docker-compose.yml (DEFAULT_USER_TELEGRAM_ID). */
export const DEFAULT_ADMIN_TELEGRAM_ID = process.env.E2E_DEFAULT_TELEGRAM_ID ?? '123456789'

const SEED_FILE = new URL('../seed.json', import.meta.url)
export const sleep = (ms) => new Promise((r) => setTimeout(r, ms))

const b64 = (o) => Buffer.from(typeof o === 'string' ? o : JSON.stringify(o)).toString('base64url')

/** An access token as the API issues it (same claims as IdentityService), so seeding doesn't hit the sign-in limit. */
export function mint(id, name, admin = false) {
  const now = Math.floor(Date.now() / 1000)
  const payload = {
    'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier': id,
    'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name': name,
    ...(admin ? { Admin: 'true' } : {}),
    iss: 'DebtBot',
    aud: 'DebtBot',
    nbf: now - 5,
    exp: now + 3600,
  }
  const h = b64({ alg: 'HS256', typ: 'JWT' })
  const p = b64(payload)
  return `${h}.${p}.${createHmac('sha256', KEY).update(`${h}.${p}`).digest('base64url')}`
}

/** Calls the API; returns { status, body } (body parsed when it's JSON). */
export async function raw(token, method, path, body) {
  const r = await fetch(BASE + path, {
    method,
    headers: { ...(token ? { Authorization: `Bearer ${token}` } : {}), ...(body !== undefined ? { 'Content-Type': 'application/json' } : {}) },
    body: body === undefined ? undefined : JSON.stringify(body),
  })
  const t = await r.text()
  let parsed = t
  try {
    parsed = t ? JSON.parse(t) : undefined
  } catch {
    /* text */
  }
  return { status: r.status, body: parsed }
}

/** Calls the API and throws unless it's 2xx. */
export async function call(token, method, path, body) {
  const r = await raw(token, method, path, body)
  if (r.status < 200 || r.status > 299) throw new Error(`${method} ${path} -> ${r.status} ${JSON.stringify(r.body)}`)
  return r.body
}

export const sql = (q) => execFileSync('docker', ['exec', DB, 'psql', '-U', 'postgres', '-d', 'debtBot', '-Atc', q]).toString().trim()

export const defaultAdminId = () => sql(`select "Id" from "Users" where "TelegramId" = ${Number(DEFAULT_ADMIN_TELEGRAM_ID)} order by "Id" limit 1`)

export const saveSeed = (data) => writeFileSync(SEED_FILE, JSON.stringify(data, null, 2))
export const loadSeed = () => JSON.parse(readFileSync(SEED_FILE))

/** Waits until the app answers at all (it applies the migrations before it listens). Works for main's app too. */
export async function waitForApp(seconds = 180) {
  for (let i = 0; i < seconds; i++) {
    const ok = await fetch(`${URL0}/swagger/index.html`).then((r) => r.status < 500).catch(() => false)
    if (ok) return
    await sleep(1000)
  }
  throw new Error(`${URL0} didn't come up in ${seconds} s`)
}
