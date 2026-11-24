import { describe, expect, it } from 'vitest'
import type { Bill, ExchangeListItem, UserDisplay } from '../api/types'
import {
  dealFromBill,
  dealFromListItem,
  emptyExchangeForm,
  exchangeAmounts,
  exchangeProblems,
  formatRate,
  orientDeal,
  parseRate,
  roundToCurrency,
  swapSides,
  toExchangeCreation,
  unitRate,
  type ExchangeForm,
} from './exchange'
import { toUnits } from './money'

const form = (patch: Partial<ExchangeForm>): ExchangeForm => ({ ...emptyExchangeForm(), giveCurrency: 'EUR', getCurrency: 'PLN', ...patch })
const u = (n: number) => toUnits(n)

describe('rates', () => {
  it('parses rates with up to 8 decimals', () => {
    expect(parseRate('4.3')).toBe(430_000_000n)
    expect(parseRate('0,23255814')).toBe(23_255_814n)
    expect(parseRate('0')).toBeNull()
    expect(parseRate('1.123456789')).toBeNull()
    expect(parseRate('-1')).toBeNull()
    expect(parseRate('abc')).toBeNull()
  })

  it('shows the rate both ways with 4 decimals', () => {
    expect(formatRate(unitRate(100, 430))).toBe('4.3000')
    expect(formatRate(unitRate(430, 100))).toBe('0.2326')
    expect(formatRate(unitRate(u(232.5), u(1000)))).toBe('4.3011')
    expect(unitRate(0, 5)).toBe(0)
  })

  it('rounds to the currency', () => {
    expect(roundToCurrency(4_300_049n, 'EUR')).toBe(4_300_000n)
    expect(roundToCurrency(4_300_050n, 'EUR')).toBe(4_300_100n)
    expect(roundToCurrency(1_234_567n, 'JPY')).toBe(1_230_000n)
  })
})

describe('exchangeAmounts', () => {
  it('takes both amounts as typed', () => {
    expect(exchangeAmounts(form({ giveAmount: '100', getAmount: '430' }), null)).toEqual({ give: u(100), get: u(430) })
  })

  it('works out what you get from a rate (1 EUR = 4.30 PLN), and the flipped rate', () => {
    expect(exchangeAmounts(form({ giveAmount: '100', rate: '4.30' }), 'get').get).toBe(u(430))
    // 1 PLN = 0.2326 EUR → 100 / 0.2326 = 429.922… → 429.92 PLN
    expect(exchangeAmounts(form({ giveAmount: '100', rate: '0.2326', flip: true }), 'get').get).toBe(u(429.92))
  })

  it('works out what you give from a rate on the first side', () => {
    // 1 EUR = 4.30 PLN, you get 430 PLN → you give 100 EUR
    expect(exchangeAmounts(form({ getAmount: '430', rate: '4.3' }), 'give').give).toBe(u(100))
    // 1 PLN = 0.2326 EUR → 430 × 0.2326 = 100.018 → 100.02 EUR
    expect(exchangeAmounts(form({ getAmount: '430', rate: '0.2326', flip: true }), 'give').give).toBe(u(100.02))
  })

  it('ignores the other amount box while a side is a rate', () => {
    expect(exchangeAmounts(form({ giveAmount: '100', getAmount: '1', rate: '4.3' }), 'get')).toEqual({ give: u(100), get: u(430) })
  })

  it('is unknown without the rate or the amount', () => {
    expect(exchangeAmounts(form({ giveAmount: '100' }), 'get').get).toBeNull()
    expect(exchangeAmounts(form({ rate: '4.3' }), 'get').give).toBeNull()
  })
})

describe('the form', () => {
  it('lists what is missing', () => {
    expect(exchangeProblems(emptyExchangeForm(), null, undefined)).toEqual({
      person: 'exchange.needPerson',
      giveCurrency: 'exchange.needCurrency',
      getCurrency: 'exchange.needCurrency',
      give: 'exchange.needAmount',
      get: 'exchange.needAmount',
    })
    expect(exchangeProblems(form({ giveAmount: '1', getAmount: '1', getCurrency: 'eur' }), null, 'b')).toEqual({ getCurrency: 'exchange.sameCurrency' })
    expect(exchangeProblems(form({ giveAmount: '1', rate: 'x' }), 'get', 'b')).toEqual({ rate: 'exchange.needRate' })
    expect(exchangeProblems(form({ giveAmount: '0.01', rate: '0.0001' }), 'get', 'b')).toEqual({ rate: 'exchange.rateTooSmall' })
    expect(exchangeProblems(form({ giveAmount: '100', rate: '4.3' }), 'get', 'b')).toEqual({})
  })

  it('maps to POST Bills/exchange: you are "from", amounts as numbers, codes upper case', () => {
    expect(toExchangeCreation('me', 'boris', form({ giveAmount: '100', rate: '4.30', giveCurrency: 'eur', description: '  Kraków cash ' }), 'get')).toEqual({
      fromUserId: 'me',
      fromAmount: 100,
      fromCurrencyCode: 'EUR',
      toUserId: 'boris',
      toAmount: 430,
      toCurrencyCode: 'PLN',
      description: 'Kraków cash',
    })
    expect(toExchangeCreation('me', 'boris', form({ giveAmount: '100' }), null)).toBeNull()
    expect(toExchangeCreation('me', 'boris', form({ giveAmount: '100', getAmount: '430' }), null)?.description).toBeUndefined()
  })

  it('swaps the sides and keeps the rate meaning the same', () => {
    const f = form({ giveAmount: '100', rate: '4.3' })
    const s = swapSides(f, 'get')
    expect(s.side).toBe('give')
    expect(s.form).toMatchObject({ getAmount: '100', getCurrency: 'EUR', giveCurrency: 'PLN', flip: true })
    // You now get 100 EUR at 1 EUR = 4.3 PLN: you give 430 PLN.
    expect(exchangeAmounts(s.form, s.side).give).toBe(u(430))
  })
})

describe('deals', () => {
  const user = (id: string): UserDisplay => ({ id, displayName: id, telegramId: null, telegramUserName: null })
  const bill = {
    id: 'b1',
    exchangeBillId: 'b2',
    currencyCode: 'PLN',
    paymentCurrencyCode: 'EUR',
    payments: [{ user: user('anna'), amount: 100 }],
    lines: [{ id: 'l', billId: 'b1', itemDescription: 'Exchange', subtotal: 430, participants: [{ billLineId: 'l', user: user('boris'), part: 1, userDisplayName: 'Boris' }] }],
  } as Pick<Bill, 'id' | 'exchangeBillId' | 'payments' | 'lines' | 'currencyCode' | 'paymentCurrencyCode'>

  it('reads the whole deal from one bill and turns it to your side', () => {
    const d = dealFromBill(bill)!
    expect(d).toMatchObject({ fromAmount: 100, fromCurrency: 'EUR', fromBillId: 'b1', toAmount: 430, toCurrency: 'PLN', toBillId: 'b2' })
    expect(d.from.id).toBe('anna')
    expect(orientDeal(d, 'anna')).toBe(d)
    expect(orientDeal(d, 'boris')).toMatchObject({ fromAmount: 430, fromCurrency: 'PLN', fromBillId: 'b2', toAmount: 100, toBillId: 'b1' })
    expect(dealFromBill({ ...bill, exchangeBillId: null })).toBeNull()
  })

  it('reads a list item', () => {
    const item = { forwardBillId: 'f', backwardBillId: 'b', fromUser: user('a'), fromAmount: 1, fromCurrencyCode: 'EUR', toUser: user('b'), toAmount: 2, toCurrencyCode: 'USD' } as ExchangeListItem
    expect(dealFromListItem(item)).toMatchObject({ fromBillId: 'f', toBillId: 'b', toAmount: 2 })
    expect(dealFromListItem({ ...item, toUser: null })).toBeNull()
  })
})
