import { describe, expect, it } from 'vitest'
import type { BillListItem, LedgerRecord, UserDisplay } from '../api/types'
import { handle, myBalance, pairExchanges, peopleCount, tipsUnits } from './bills'

const u = (id: string): UserDisplay => ({ id, displayName: id, telegramId: null, telegramUserName: null })
const rec = (creditor: string, debtor: string, amount: number, currencyCode: string): LedgerRecord => ({ creditorUser: u(creditor), debtorUser: u(debtor), amount, currencyCode })

describe('myBalance', () => {
  it('nets your records per currency', () => {
    const b = myBalance([rec('me', 'bob', 15, 'EUR'), rec('me', 'bob', 15, 'EUR'), rec('ann', 'me', 0.1, 'PLN'), rec('ann', 'bob', 99, 'EUR')], 'me')
    expect([...b]).toEqual([
      ['EUR', 300000n],
      ['PLN', -1000n],
    ])
  })

  it('drops currencies that net to zero', () => {
    expect(myBalance([rec('me', 'bob', 5, 'EUR'), rec('bob', 'me', 5, 'EUR')], 'me').size).toBe(0)
  })
})

describe('bill facts', () => {
  const line = (subtotal: number, ...ids: string[]) => ({ id: 'l', billId: 'b', itemDescription: 'x', subtotal, participants: ids.map((id) => ({ billLineId: 'l', user: u(id), part: 1, userDisplayName: id })) })

  it('tips are the total minus the lines, exactly', () => {
    expect(tipsUnits({ totalWithTips: 84, lines: [line(48.1, 'a'), line(27.9, 'b')] })).toBe(80000n)
    expect(tipsUnits({ totalWithTips: 0.3, lines: [line(0.1, 'a'), line(0.2, 'b')] })).toBe(0n)
  })

  it('counts people once', () => {
    expect(peopleCount({ lines: [line(1, 'a', 'b'), line(1, 'b', 'c')], payments: [{ user: u('a'), amount: 2 }, { user: u('d'), amount: 1 }] })).toBe(4)
  })
})

describe('pairExchanges', () => {
  const item = (id: string, exchangeBillId: string | null = null): BillListItem => ({
    id, exchangeBillId, currencyCode: 'EUR', paymentCurrencyCode: 'EUR', description: id, date: '', status: 'Draft', totalWithTips: 1, creator: u('me'), ledgerRecords: [], participantIds: [], participantCount: 0,
  })

  it('folds a pair into one row at the first one’s place', () => {
    const rows = pairExchanges([item('a'), item('f', 'b'), item('c'), item('b', 'f')])
    expect(rows.map((r) => [r.bill.id, r.pair?.id])).toEqual([
      ['a', undefined],
      ['f', 'b'],
      ['c', undefined],
    ])
  })

  it('keeps a half whose other bill is on another page', () => {
    expect(pairExchanges([item('f', 'b')]).map((r) => [r.bill.id, r.pair?.id])).toEqual([['f', undefined]])
  })
})

describe('handle', () => {
  it('shows one @ whether or not the name has it', () => {
    expect(handle('@boris_iv')).toBe('@boris_iv')
    expect(handle('boris_iv')).toBe('@boris_iv')
  })
})
