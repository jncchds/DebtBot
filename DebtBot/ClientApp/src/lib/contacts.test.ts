import { describe, expect, it } from 'vitest'
import type { Contact } from '../api/types'
import { groupSubscriptions, matchesContact, monthRange, pendingRequestCount, recentContacts, searchContacts } from './contacts'

const c = (id: string, displayName: string, more: Partial<Contact> = {}): Contact => ({
  id,
  displayName,
  userDisplayName: displayName,
  telegramId: null,
  telegramUserName: null,
  telegramBotEnabled: true,
  mySubscription: 'None',
  theirSubscription: 'None',
  addedAt: null,
  ...more,
})

const boris = c('b', 'Boris', { userDisplayName: 'Boris Ivanov', telegramUserName: '@boris_iv', mySubscription: 'Confirmed', theirSubscription: 'Confirmed', addedAt: '2026-11-20T10:00:00Z' })
const clara = c('c', 'clara', { userDisplayName: 'Clara Müller', telegramUserName: '@clara_m', mySubscription: 'Pending', addedAt: '2026-11-22T10:00:00Z' })
const dmitry = c('d', 'Dmitry', { telegramUserName: '@dmitry_k', theirSubscription: 'Pending', addedAt: '2026-11-01T10:00:00Z' })
const mum = c('m', 'Mum', { userDisplayName: 'Olga', telegramUserName: '@olga_petrova', telegramBotEnabled: false })
const all = [mum, dmitry, clara, boris]

describe('contact search', () => {
  it('matches your name, their name and the username, ignoring case and @', () => {
    expect(matchesContact(boris, 'IVAN')).toBe(true)
    expect(matchesContact(boris, '@boris_')).toBe(true)
    expect(matchesContact(boris, 'ris_i')).toBe(true)
    expect(matchesContact(mum, 'olga')).toBe(true)
    expect(matchesContact(mum, 'boris')).toBe(false)
    expect(matchesContact(mum, '  ')).toBe(true)
  })

  it('lists the matches A–Z by your name, ignoring case', () => {
    expect(searchContacts(all, '').map((x) => x.id)).toEqual(['b', 'c', 'd', 'm'])
    expect(searchContacts(all, '@').map((x) => x.id)).toEqual(['b', 'c', 'd', 'm'])
    expect(searchContacts(all, '_m').map((x) => x.id)).toEqual(['c'])
    expect(searchContacts(all, 'zzz')).toEqual([])
  })
})

describe('recentContacts', () => {
  it('newest first, undated last', () => {
    expect(recentContacts(all).map((x) => x.id)).toEqual(['c', 'b', 'd'])
    expect(recentContacts(all, 10).map((x) => x.id)).toEqual(['c', 'b', 'd', 'm'])
    expect(recentContacts([])).toEqual([])
  })
})

describe('groupSubscriptions', () => {
  it('splits contacts into requests, people who get yours, and yours', () => {
    const g = groupSubscriptions(all)
    expect(g.requests.map((x) => x.id)).toEqual(['d'])
    expect(g.theyGetYours.map((x) => x.id)).toEqual(['b'])
    expect(g.yours.map((x) => x.id)).toEqual(['b', 'c'])
    expect(pendingRequestCount(all)).toBe(1)
    expect(pendingRequestCount(undefined)).toBe(0)
  })

  it('is empty without subscriptions', () => {
    expect(groupSubscriptions([mum])).toEqual({ requests: [], theyGetYours: [], yours: [] })
  })
})

describe('monthRange', () => {
  it('covers the local calendar month', () => {
    expect(monthRange(new Date(2026, 10, 24, 23, 30))).toEqual({ from: '2026-11-01', to: '2026-11-30' })
    expect(monthRange(new Date(2028, 1, 3))).toEqual({ from: '2028-02-01', to: '2028-02-29' })
    expect(monthRange(new Date(2026, 11, 31))).toEqual({ from: '2026-12-01', to: '2026-12-31' })
  })
})
