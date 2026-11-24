import { describe, expect, it } from 'vitest'
import type { Contact, SubscriptionState } from '../api/types'
import { balanceOwner, followedPeople, listActionsAllowed, ofSearch, parseOf, searchFollowed, viewingFor, withOf, youreOnIt } from './following'
import { listSearch, parseListState, withFilter, withOrderToggled, withoutFilters, withPage, withSort } from './listState'
import { billsQuery, billsSpec, debtsSpec } from './lists'

const ME = 'aaaa-1111'
const BORIS = 'bbbb-2222'

const contact = (id: string, displayName: string, mySubscription: SubscriptionState, extra: Partial<Contact> = {}): Contact => ({
  id,
  displayName,
  userDisplayName: displayName,
  telegramId: null,
  telegramUserName: null,
  telegramBotEnabled: true,
  mySubscription,
  theirSubscription: 'None',
  addedAt: null,
  ...extra,
})

describe('of in the URL', () => {
  it('reads a well-formed id; yours, empty or junk means your own list', () => {
    expect(parseOf(new URLSearchParams(`of=${BORIS}`), ME)).toBe(BORIS)
    expect(parseOf(new URLSearchParams(`of=${ME}`), ME)).toBeUndefined()
    expect(parseOf(new URLSearchParams(`of=${ME.toUpperCase()}`), ME)).toBeUndefined()
    expect(parseOf(new URLSearchParams('of='), ME)).toBeUndefined()
    expect(parseOf(new URLSearchParams('of=<script>'), ME)).toBeUndefined()
    expect(parseOf(new URLSearchParams(''), ME)).toBeUndefined()
  })

  it('puts of first on links, or leaves it out for yours', () => {
    expect(withOf('/bills?with=x&currency=EUR', BORIS)).toBe(`/bills?of=${BORIS}&with=x&currency=EUR`)
    expect(withOf('/bills', BORIS)).toBe(`/bills?of=${BORIS}`)
    expect(withOf(`/bills?of=old&with=x`, BORIS)).toBe(`/bills?of=${BORIS}&with=x`)
    expect(withOf(`/bills?of=old&with=x`, undefined)).toBe('/bills?with=x')
    expect(withOf('/', undefined)).toBe('/')
  })

  it('switching whose list you see starts filters, sort and page afresh', () => {
    expect(ofSearch(BORIS)).toBe(`?of=${BORIS}`)
    expect(ofSearch(undefined)).toBe('')
  })

  it('survives filters, sort, order, paging and Clear filters', () => {
    const params = new URLSearchParams(`of=${BORIS}&currency=EUR&page=3`)
    const state = parseListState(params, billsSpec)
    expect(state.filters).toEqual({ currency: 'EUR' })
    for (const next of [
      withFilter(state, 'status', 'draft'),
      withSort(state, billsSpec, 'total'),
      withOrderToggled(state),
      withPage(state, 2),
      withoutFilters(state),
    ]) {
      expect(new URLSearchParams(listSearch(next, billsSpec, params)).get('of')).toBe(BORIS)
    }
    expect(listSearch(withFilter(state, 'mine', true), billsSpec, params)).toBe(`?of=${BORIS}&currency=EUR&mine=1`)
    // of isn't a filter: it doesn't count as one, and on Debts it isn't mistaken for the person filter.
    expect(parseListState(new URLSearchParams(`of=${BORIS}`), debtsSpec).filters).toEqual({})
    expect(billsQuery(parseListState(params, billsSpec))).not.toHaveProperty('ofUserId')
  })
})

describe('who you can view', () => {
  const contacts = [
    contact('c1', 'clara', 'Pending'),
    contact('d1', 'Dmitry', 'None'),
    contact(BORIS, 'Boris', 'Confirmed', { telegramUserName: '@boris_iv', userDisplayName: 'Boris Ivanov' }),
    contact('g1', 'grisha', 'Confirmed'),
    contact(ME, 'Me', 'Confirmed'),
  ]

  it('lists confirmed subscriptions only, A–Z by your name for them, never yourself', () => {
    expect(followedPeople(contacts, ME).map((c) => c.displayName)).toEqual(['Boris', 'grisha'])
    expect(followedPeople([], ME)).toEqual([])
  })

  it('searches your name for them, their own name and their @username', () => {
    const followed = followedPeople(contacts, ME)
    expect(searchFollowed(followed, 'gri').map((c) => c.id)).toEqual(['g1'])
    expect(searchFollowed(followed, 'ivanov').map((c) => c.id)).toEqual([BORIS])
    expect(searchFollowed(followed, '@boris_').map((c) => c.id)).toEqual([BORIS])
    expect(searchFollowed(followed, '  ')).toHaveLength(2)
    expect(searchFollowed(followed, 'zed')).toEqual([])
  })

  it('names the viewed person by your name for them', () => {
    expect(viewingFor(undefined, contacts)).toEqual({ kind: 'mine' })
    expect(viewingFor(BORIS, contacts)).toEqual({ kind: 'of', id: BORIS, name: 'Boris' })
    expect(viewingFor('ffff', contacts)).toEqual({ kind: 'of', id: 'ffff', name: '…' })
  })
})

describe('read-only switches', () => {
  const mine = viewingFor(undefined, [])
  const boris = viewingFor(BORIS, [contact(BORIS, 'Boris', 'Confirmed')])
  const bill = (creator: string, participantIds: string[]) => ({ creator: { id: creator, displayName: creator, telegramId: null, telegramUserName: null }, participantIds })

  it('offers no list actions on someone else’s list', () => {
    expect(listActionsAllowed(mine)).toBe(true)
    expect(listActionsAllowed(boris)).toBe(false)
  })

  it('works balances out from the viewed person’s side', () => {
    expect(balanceOwner(mine, ME)).toBe(ME)
    expect(balanceOwner(boris, ME)).toBe(BORIS)
  })

  it('marks bills you’re on too, only on someone else’s list', () => {
    expect(youreOnIt(boris, bill(BORIS, [BORIS, ME]), ME)).toBe(true)
    expect(youreOnIt(boris, bill(ME, [BORIS]), ME)).toBe(true)
    expect(youreOnIt(boris, bill(BORIS, [BORIS, 'g1']), ME)).toBe(false)
    expect(youreOnIt(mine, bill(BORIS, [BORIS, ME]), ME)).toBe(false)
  })
})
