import { describe, expect, it } from 'vitest'
import type { AdminUser, MergeSuggestion } from '../api/types'
import {
  adminPasswordRequest,
  afterMerge,
  canMergeManual,
  checkAdminPassword,
  duplicateIds,
  keptSignIn,
  mergeSearch,
  parseMergeUrl,
  shownPair,
  withManual,
  withManualSwapped,
  withSwap,
} from './admin'
import { listSearch, parseListState, withFilter, withSort } from './listState'
import { adminBillsQuery, adminBillsSpec, adminUsersQuery, adminUsersSpec } from './lists'

const user = (id: string, extra: Partial<AdminUser> = {}): AdminUser => ({
  id,
  displayName: id,
  telegramId: null,
  telegramUserName: null,
  phone: null,
  email: null,
  role: 'Regular',
  telegramBotEnabled: false,
  login: null,
  hasPassword: false,
  billCount: 0,
  contactCount: 0,
  firstBillDate: null,
  ...extra,
})

const pair = (keep: string, merge: string): MergeSuggestion => ({ keep: user(keep), merge: user(merge), reasons: ['SameTelegramUserName'] })

describe('adminUsersSpec', () => {
  const parse = (s: string) => parseListState(new URLSearchParams(s), adminUsersSpec)

  it('defaults to everyone by name, page 1', () => {
    expect(adminUsersQuery(parse(''))).toEqual({ search: undefined, role: undefined, pageNumber: 0, countPerPage: 20 })
    expect(listSearch(parse(''), adminUsersSpec)).toBe('')
  })

  it('maps search, role and page to GET Users', () => {
    expect(adminUsersQuery(parse('search=%20boris%20&role=admin&page=2'))).toEqual({ search: 'boris', role: 'Admin', pageNumber: 1, countPerPage: 20 })
    expect(adminUsersQuery(parse('role=regular')).role).toBe('Regular')
  })

  it('ignores invalid values and sorts it has no field for', () => {
    const s = parse('role=owner&sort=bills&page=x')
    expect(adminUsersQuery(s)).toEqual({ search: undefined, role: undefined, pageNumber: 0, countPerPage: 20 })
    expect(s.sort).toBe('name')
    expect(withSort(s, adminUsersSpec, 'bills')).toBe(s)
  })

  it('goes back to page 1 when a filter changes', () => {
    const next = withFilter(parse('search=bo&page=3'), 'role', 'admin')
    expect(listSearch(next, adminUsersSpec)).toBe('?search=bo&role=admin')
  })
})

describe('adminBillsSpec', () => {
  const parse = (s: string) => parseListState(new URLSearchParams(s), adminBillsSpec)

  it('defaults to every bill, newest first', () => {
    expect(adminBillsQuery(parse(''))).toEqual({
      search: undefined,
      creatorId: undefined,
      withUserId: undefined,
      currencyCode: undefined,
      dateFrom: undefined,
      dateTo: undefined,
      status: undefined,
      sortBy: 'Date',
      sortOrder: 'Desc',
      pageNumber: 0,
      countPerPage: 20,
    })
  })

  it('maps the URL to GET Bills/All, with `by` as the creator', () => {
    expect(adminBillsQuery(parse('q=pizza&by=b0b&with=c1a&currency=eur&from=2026-11-01&to=2026-11-30&status=finalized&sort=total&order=asc&page=4'))).toEqual({
      search: 'pizza',
      creatorId: 'b0b',
      withUserId: 'c1a',
      currencyCode: 'EUR',
      dateFrom: '2026-11-01',
      dateTo: '2026-11-30',
      status: 'Processed',
      sortBy: 'Total',
      sortOrder: 'Asc',
      pageNumber: 3,
      countPerPage: 20,
    })
  })

  it('has no "created by me"', () => {
    const s = parse('mine=1&by=not-an-id!')
    expect(s.filters).toEqual({})
    expect('createdByMe' in adminBillsQuery(s)).toBe(false)
  })

  it('keeps the sort when the creator filter changes, and drops the page', () => {
    const next = withFilter(parse('sort=description&page=5'), 'by', 'b0b')
    expect(listSearch(next, adminBillsSpec)).toBe('?by=b0b&sort=description')
  })
})

describe('merge URL state', () => {
  const p = (s: string) => parseMergeUrl(new URLSearchParams(s))

  it('reads swap, keep and merge, dropping junk', () => {
    expect(p('')).toEqual({ swap: [], keep: undefined, merge: undefined })
    expect(p('swap=a1,,b2,a1,bad!&keep=c3&merge=%20d4%20')).toEqual({ swap: ['a1', 'b2'], keep: 'c3', merge: 'd4' })
    expect(p('keep=<script>').keep).toBeUndefined()
  })

  it('writes it back without empty parts and with readable commas', () => {
    expect(mergeSearch({ swap: [] })).toBe('')
    expect(mergeSearch({ swap: ['a1', 'b2'], merge: 'd4' })).toBe('?swap=a1,b2&merge=d4')
    expect(mergeSearch(p('swap=a1&keep=c3&merge=d4'))).toBe('?swap=a1&keep=c3&merge=d4')
  })

  it('swaps a suggestion on and back off', () => {
    const on = withSwap(p(''), 'b2', true)
    expect(mergeSearch(on)).toBe('?swap=b2')
    expect(mergeSearch(withSwap(on, 'a1', true))).toBe('?swap=b2,a1')
    expect(mergeSearch(withSwap(withSwap(on, 'a1', true), 'b2', false))).toBe('?swap=a1')
    expect(withSwap(on, 'b2', true).swap).toEqual(['b2'])
  })

  it('shows a swapped suggestion with its users traded', () => {
    const s = pair('aa1', 'bb2')
    expect(shownPair(p(''), s)).toMatchObject({ keep: { id: 'aa1' }, merge: { id: 'bb2' }, swapped: false })
    expect(shownPair(p('swap=bb2'), s)).toMatchObject({ keep: { id: 'bb2' }, merge: { id: 'aa1' }, swapped: true })
    // Swapping is by the suggested merge user, not the keeper.
    expect(shownPair(p('swap=aa1'), s).swapped).toBe(false)
  })

  it('fills and swaps the manual picker', () => {
    const s = withManual(withManual(p(''), 'keep', 'c1'), 'merge', 'd1')
    expect(mergeSearch(s)).toBe('?keep=c1&merge=d1')
    expect(mergeSearch(withManualSwapped(s))).toBe('?keep=d1&merge=c1')
    expect(mergeSearch(withManual(s, 'keep', undefined))).toBe('?merge=d1')
  })

  it('needs two different users for a manual merge', () => {
    expect(canMergeManual(p('merge=d1'))).toBe(false)
    expect(canMergeManual(p('keep=d1&merge=D1'))).toBe(false)
    expect(canMergeManual(p('keep=c1&merge=d1'))).toBe(true)
  })

  it('forgets the merged users afterwards', () => {
    expect(afterMerge(p('swap=a1,d1&keep=c1&merge=d1'), 'd1', 'c1')).toEqual({ swap: ['a1'], keep: undefined, merge: undefined })
    expect(afterMerge(p('swap=d1&keep=e1&merge=f1'), 'd1', 'c1')).toEqual({ swap: [], keep: 'e1', merge: 'f1' })
  })
})

describe('duplicates and sign-in', () => {
  it('marks everyone in a suggestion', () => {
    expect([...duplicateIds([pair('a', 'b'), pair('a', 'c')])].sort()).toEqual(['a', 'b', 'c'])
    expect(duplicateIds(undefined).size).toBe(0)
  })

  it('says whose login and password survive', () => {
    expect(keptSignIn(user('k'))).toBe('takes')
    expect(keptSignIn(user('k', { login: 'boris' }))).toBe('keeps')
    expect(keptSignIn(user('k', { hasPassword: true }))).toBe('keeps')
  })
})

describe('admin Login & password', () => {
  const f = (login: string, next: string, repeat = next) => ({ login, next, repeat })

  it('needs a login only when the user has none', () => {
    expect(checkAdminPassword(f('', 'secret-pw'), false)).toEqual({ login: 'admin.password.loginRequired' })
    expect(checkAdminPassword(f('', 'secret-pw'), true)).toEqual({})
  })

  it('checks the login like the API (any case, 3–64 of a–z 0–9 . _ - @)', () => {
    expect(checkAdminPassword(f('Boris.I@x', 'secret-pw'), false)).toEqual({})
    expect(checkAdminPassword(f('bo', 'secret-pw'), false).login).toBe('admin.password.loginRule')
    expect(checkAdminPassword(f('bo ris', 'secret-pw'), false).login).toBe('admin.password.loginRule')
    expect(checkAdminPassword(f('x'.repeat(65), 'secret-pw'), false).login).toBe('admin.password.loginRule')
  })

  it('needs a matching password of 8 or more', () => {
    expect(checkAdminPassword(f('boris', ''), false)).toEqual({ next: 'admin.password.passwordRequired' })
    expect(checkAdminPassword(f('boris', 'short'), false)).toEqual({ next: 'admin.password.passwordShort' })
    expect(checkAdminPassword(f('boris', 'secret-pw', 'secret-px'), false)).toEqual({ repeat: 'admin.password.mismatch' })
  })

  it('sends the login only when one was typed, lower case', () => {
    expect(adminPasswordRequest(f(' Boris ', 'secret-pw'))).toEqual({ login: 'boris', password: 'secret-pw' })
    expect(adminPasswordRequest(f('', 'secret-pw'))).toEqual({ password: 'secret-pw' })
  })
})
