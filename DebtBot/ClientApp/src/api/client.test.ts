import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { api, ApiError, getAccessToken, refreshSession, setSession } from './client'
import type { Session } from './types'

const session = (token: string): Session => ({
  user: { id: 'u1', displayName: 'Anna', telegramUserName: '@anna', isAdmin: false },
  accessToken: token,
  accessTokenExpiresAt: '2030-01-01T00:00:00Z',
})

const json = (status: number, body?: unknown) =>
  new Response(body === undefined ? null : JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } })

type Handler = (url: string, init: RequestInit) => Response | Promise<Response>

function mockFetch(handler: Handler) {
  const fn = vi.fn((input: RequestInfo | URL, init?: RequestInit) => Promise.resolve(handler(String(input), init ?? {})))
  vi.stubGlobal('fetch', fn)
  return fn
}

const auth = (init: RequestInit) => new Headers(init.headers).get('Authorization')
const refreshCalls = (fn: ReturnType<typeof mockFetch>) => fn.mock.calls.filter(([u]) => String(u).endsWith('/auth/refresh')).length

beforeEach(() => setSession(null))
afterEach(() => vi.unstubAllGlobals())

describe('api', () => {
  it('sends the bearer token and parses JSON', async () => {
    setSession(session('t1'))
    const fn = mockFetch((_, init) => json(200, { ok: auth(init) }))
    await expect(api('/profile')).resolves.toEqual({ ok: 'Bearer t1' })
    expect(fn.mock.calls[0][0]).toBe('/api/v1/profile')
  })

  it('builds the query string and skips empty values', async () => {
    const fn = mockFetch(() => json(200, []))
    await api('/Bills', { query: { pageNumber: 0, countPerPage: 20, status: undefined, currencyCode: '' } })
    expect(fn.mock.calls[0][0]).toBe('/api/v1/Bills?pageNumber=0&countPerPage=20')
  })

  it('refreshes once on 401 and retries with the new token', async () => {
    setSession(session('old'))
    const fn = mockFetch((url, init) => {
      if (url.endsWith('/auth/refresh')) return json(200, session('new'))
      return auth(init) === 'Bearer new' ? json(200, { ok: true }) : json(401)
    })
    await expect(api('/profile')).resolves.toEqual({ ok: true })
    expect(refreshCalls(fn)).toBe(1)
    expect(getAccessToken()).toBe('new')
  })

  it('shares one refresh between concurrent 401s', async () => {
    setSession(session('old'))
    let release!: () => void
    const gate = new Promise<void>((r) => (release = r))
    const fn = mockFetch(async (url, init) => {
      if (url.endsWith('/auth/refresh')) {
        await gate
        return json(200, session('new'))
      }
      return auth(init) === 'Bearer new' ? json(200, { url }) : json(401)
    })
    const calls = Promise.all([api('/a'), api('/b'), api('/c'), refreshSession()])
    await new Promise((r) => setTimeout(r, 10))
    release()
    await calls
    expect(refreshCalls(fn)).toBe(1)
  })

  it('gives up after one retry and signs out when the refresh fails', async () => {
    setSession(session('old'))
    const fn = mockFetch((url) => (url.endsWith('/auth/refresh') ? json(401, { message: 'Not signed in.' }) : json(401)))
    await expect(api('/profile')).rejects.toMatchObject({ status: 401 })
    expect(refreshCalls(fn)).toBe(1)
    expect(fn).toHaveBeenCalledTimes(2)
    expect(getAccessToken()).toBeNull()
  })

  it('does not refresh on a failed login', async () => {
    const fn = mockFetch(() => json(401, { message: 'Invalid login or password.' }))
    const err = await api('/auth/login', { method: 'POST', json: { login: 'a', password: 'b' } }).catch((e: unknown) => e)
    expect(err).toBeInstanceOf(ApiError)
    expect((err as ApiError).serverMessage).toBe('Invalid login or password.')
    expect(refreshCalls(fn)).toBe(0)
  })

  it('returns undefined for 204', async () => {
    mockFetch(() => new Response(null, { status: 204 }))
    await expect(api('/auth/logout', { method: 'POST' })).resolves.toBeUndefined()
  })

  it('has no server message for an empty error body', async () => {
    mockFetch(() => new Response(null, { status: 429, statusText: 'Too Many Requests' }))
    const err = (await api('/auth/login', { method: 'POST' }).catch((e: unknown) => e)) as ApiError
    expect(err.status).toBe(429)
    expect(err.serverMessage).toBeUndefined()
  })
})
