import { describe, expect, it } from 'vitest'
import { backTarget, originState } from './backToList'

describe('originState', () => {
  it('is the list URL with its query on a list page', () => {
    expect(originState('/bills', '?of=b1&status=draft&page=2', null)).toEqual({ from: '/bills?of=b1&status=draft&page=2' })
    expect(originState('/spendings', '?currency=EUR', null)).toEqual({ from: '/spendings?currency=EUR' })
    expect(originState('/admin/bills', '', null)).toEqual({ from: '/admin/bills' })
    expect(originState('/exchange', '?status=waiting', null)).toEqual({ from: '/exchange?status=waiting' })
  })

  it('carries the origin over on a bill page', () => {
    const state = { from: '/spendings?page=3' }
    expect(originState('/bills/abc', '', state)).toBe(state)
    expect(originState('/bills/abc/add-line', '?people=x', state)).toBe(state)
    expect(originState('/bills/abc', '', null)).toBeUndefined()
  })

  it('is nothing on other pages', () => {
    expect(originState('/', '?with=x', null)).toBeUndefined()
    expect(originState('/bills/new', '', { from: '/bills' })).toBeUndefined()
    expect(originState('/contacts', '', null)).toBeUndefined()
  })
})

describe('backTarget', () => {
  it('goes back to the list it came from, query and all', () => {
    expect(backTarget({ from: '/bills?of=b1&mine=1' })).toEqual({ to: '/bills?of=b1&mine=1', label: 'pages.bills.title' })
    expect(backTarget({ from: '/spendings?page=2' })).toEqual({ to: '/spendings?page=2', label: 'pages.spendings.title' })
    expect(backTarget({ from: '/admin/bills?by=x' })).toEqual({ to: '/admin/bills?by=x', label: 'pages.adminBills.title' })
  })

  it('falls back to your own bills', () => {
    for (const state of [null, undefined, {}, { from: 42 }, { from: 'https://evil.example/' }, { from: '//evil.example/bills' }, { from: '/profile' }])
      expect(backTarget(state)).toEqual({ to: '/bills', label: 'pages.bills.title' })
  })
})
