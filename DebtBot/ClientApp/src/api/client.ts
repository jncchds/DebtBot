import type { ApiErrorBody, Session } from './types'

/**
 * Fetch wrapper for /api/v1.
 * - The access token lives only in memory; the refresh token is the httpOnly cookie `debtbot_refresh` (Path=/api/v1/auth).
 * - A 401 triggers one refresh and one retry.
 * - The backend has no grace window for refresh reuse: a second refresh with the same cookie revokes the session.
 *   So every caller in this tab shares one in-flight refresh promise, and tabs take turns through a Web Lock,
 *   so a tab only refreshes after the previous tab's rotated cookie is stored.
 */

const BASE = '/api/v1'
const REFRESH_LOCK = 'debtbot.refresh'

let accessToken: string | null = null
let refreshing: Promise<Session | null> | null = null
const listeners = new Set<(session: Session | null) => void>()

export function onSessionChanged(fn: (session: Session | null) => void) {
  listeners.add(fn)
  return () => {
    listeners.delete(fn)
  }
}

export function setSession(session: Session | null) {
  accessToken = session?.accessToken ?? null
  listeners.forEach((l) => l(session))
}

export function getAccessToken() {
  return accessToken
}

export class ApiError extends Error {
  status: number
  body: unknown
  constructor(status: number, message: string, body?: unknown) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.body = body
  }

  /** The server's `{ message }`, if it sent one. */
  get serverMessage(): string | undefined {
    const b = this.body as ApiErrorBody | undefined
    return b && typeof b === 'object' && typeof b.message === 'string' && b.message ? b.message : undefined
  }
}

async function postRefresh(): Promise<Session | null> {
  try {
    const r = await fetch(`${BASE}/auth/refresh`, { method: 'POST', credentials: 'same-origin' })
    return r.ok ? ((await r.json()) as Session) : null
  } catch {
    return null
  }
}

function withRefreshLock(fn: () => Promise<Session | null>): Promise<Session | null> {
  const locks = typeof navigator !== 'undefined' ? navigator.locks : undefined
  return locks ? locks.request(REFRESH_LOCK, fn) : fn()
}

/** Restores or renews the session from the cookie. Concurrent callers share one request. */
export function refreshSession(): Promise<Session | null> {
  refreshing ??= withRefreshLock(postRefresh)
    .then((session) => {
      setSession(session)
      return session
    })
    .finally(() => {
      refreshing = null
    })
  return refreshing
}

type QueryValue = string | number | boolean | undefined | null
export type Query = Record<string, QueryValue>

export function qs(query?: Query) {
  if (!query) return ''
  const p = new URLSearchParams()
  for (const [k, v] of Object.entries(query)) {
    if (v !== undefined && v !== null && v !== '') p.set(k, String(v))
  }
  const s = p.toString()
  return s ? `?${s}` : ''
}

/** Auth calls that must not trigger a refresh-and-retry on 401. */
const NO_RETRY = /^\/auth\/(login|telegram|refresh|logout|config)$/

export interface ApiInit extends Omit<RequestInit, 'body'> {
  query?: Query
  json?: unknown
  body?: BodyInit
}

export async function api<T>(path: string, init: ApiInit = {}): Promise<T> {
  const { query, json, ...rest } = init
  const url = `${BASE}${path}${qs(query)}`
  const send = () => {
    const headers = new Headers(rest.headers)
    if (accessToken) headers.set('Authorization', `Bearer ${accessToken}`)
    if (json !== undefined) headers.set('Content-Type', 'application/json')
    return fetch(url, { ...rest, headers, credentials: 'same-origin', body: json !== undefined ? JSON.stringify(json) : rest.body })
  }

  let res = await send()
  if (res.status === 401 && !NO_RETRY.test(path)) {
    const session = await refreshSession()
    if (session) res = await send()
  }

  const text = res.status === 204 ? '' : await res.text()
  const body = text ? parseJson(text) : undefined
  if (!res.ok) {
    const message = (body as ApiErrorBody | undefined)?.message || res.statusText || `HTTP ${res.status}`
    throw new ApiError(res.status, message, body)
  }
  return body as T
}

function parseJson(text: string): unknown {
  try {
    return JSON.parse(text)
  } catch {
    return text
  }
}
