import { describe, expect, it } from 'vitest'
import { checkNewBill, emptyNewBill, impliedRate, lockedPeople, previewBill, toBillCreation, withPayment, type NewBillForm } from './newBill'
import { toUnits } from './money'

const base = (patch: Partial<NewBillForm>): NewBillForm => ({ ...emptyNewBill('anna', new Date(2026, 10, 24, 20, 30)), currency: 'EUR', ...patch })

// The approved mockup: Pasta 48 EUR for three, Tiramisu 28 EUR by parts, Anna paid 84 EUR (8 EUR tips).
const dinner = base({
  description: ' Dinner at Molto\nBirthday of Boris ',
  total: '84',
  people: ['anna', 'boris', 'dmitry'],
  lines: [
    { itemDescription: 'Pasta', subtotal: 48, participants: [{ userId: 'anna' }, { userId: 'boris' }, { userId: 'dmitry' }] },
    { itemDescription: 'Tiramisu', subtotal: 28, participants: [{ userId: 'boris', part: 1 }, { userId: 'dmitry', part: 1 }] },
  ],
  payments: [{ userId: 'anna', amount: 84 }],
})

describe('the form → POST Bills', () => {
  it('maps every field; codes upper case, text trimmed, the date as ISO', () => {
    const body = toBillCreation({ ...dinner, currency: 'eur', paymentCurrency: ' pln ' })
    expect(body).toEqual({
      currencyCode: 'EUR',
      paymentCurrencyCode: 'PLN',
      description: 'Dinner at Molto\nBirthday of Boris',
      date: new Date(2026, 10, 24, 20, 30).toISOString(),
      totalWithTips: 84,
      lines: dinner.lines,
      payments: dinner.payments,
    })
  })

  it('leaves empty optional fields to the API defaults', () => {
    const body = toBillCreation(base({}))
    expect(body.paymentCurrencyCode).toBeUndefined()
    expect(body.totalWithTips).toBeUndefined()
    expect(body.description).toBeUndefined()
    expect(body.lines).toEqual([])
  })
})

describe('checks', () => {
  it('needs a currency to save, and lines and payments to finalize', () => {
    const empty = checkNewBill(base({ currency: 'EU' }))
    expect(empty.canSave).toBe(false)
    expect(empty.fields.currency).toBe('newBill.needCurrency')
    expect(checkNewBill(base({ paymentCurrency: 'P' })).fields.paymentCurrency).toBe('newBill.needCurrency')
    expect(checkNewBill(base({ total: '0' })).fields.total).toBe('editor.needAmount')
    expect(checkNewBill(base({ date: '' })).fields.date).toBe('newBill.needDate')

    const draft = checkNewBill(base({}))
    expect(draft.canSave).toBe(true)
    expect(draft.finalizeBlockers).toEqual(['bill.blockers.noLines', 'bill.blockers.noPayments'])
    expect(checkNewBill({ ...dinner, payments: [] }).finalizeBlockers).toEqual(['bill.blockers.noPayments'])
    expect(checkNewBill(dinner)).toEqual({ fields: {}, canSave: true, finalizeBlockers: [] })
  })

  it('keeps people who are on a line or a payment, and you', () => {
    expect([...lockedPeople(base({ people: ['anna', 'boris', 'clara'], payments: [{ userId: 'boris', amount: 1 }] }), 'anna')].sort()).toEqual(['anna', 'boris'])
  })

  it('replaces a payment by the same person, as the API does', () => {
    const f = withPayment(withPayment(base({}), { userId: 'anna', amount: 10 }), { userId: 'anna', amount: 12 })
    expect(f.payments).toEqual([{ userId: 'anna', amount: 12 }])
  })
})

describe('the preview, like the server finalize', () => {
  it('splits lines by parts and scales them to what was paid (tips)', () => {
    const p = previewBill(dinner)!
    const by = Object.fromEntries(p.people.map((x) => [x.userId, x]))
    expect(by.anna.sharePay).toBe(toUnits(17.6842))
    expect(by.boris.sharePay).toBe(toUnits(33.1579))
    expect(by.anna.balance).toBe(toUnits(66.3158))
    expect(p.debts).toEqual([
      { debtorId: 'boris', creditorId: 'anna', amount: toUnits(33.1579) },
      { debtorId: 'dmitry', creditorId: 'anna', amount: toUnits(33.1579) },
    ])
  })

  it('converts a bill paid in another currency at the payments’ rate', () => {
    // Hotel: 960 PLN of lines, paid as 225 EUR.
    const hotel = base({
      currency: 'PLN',
      paymentCurrency: 'EUR',
      people: ['anna', 'boris', 'dmitry'],
      lines: [
        { itemDescription: 'Room', subtotal: 840, participants: [{ userId: 'anna' }, { userId: 'boris' }, { userId: 'dmitry' }] },
        { itemDescription: 'Breakfast', subtotal: 120, participants: [{ userId: 'anna', part: 1 }, { userId: 'boris', part: 2 }, { userId: 'dmitry', part: 1 }] },
      ],
      payments: [{ userId: 'anna', amount: 225 }],
    })
    const p = previewBill(hotel)!
    const boris = p.people.find((x) => x.userId === 'boris')!
    expect(boris.shareBill).toBe(toUnits(340))
    expect(boris.sharePay).toBe(toUnits(79.6875))
    expect(p.debts.map((d) => [d.debtorId, d.amount])).toEqual([
      ['boris', toUnits(79.6875)],
      ['dmitry', toUnits(72.6563)],
    ])
    expect(impliedRate(hotel)).toBeCloseTo(4.2667, 4)
  })

  it('has nothing to show without lines or payments', () => {
    expect(previewBill(base({}))).toBeNull()
    expect(previewBill({ ...dinner, payments: [] })).toBeNull()
  })

  it('makes the biggest payer the creditor, and lets them owe someone who paid more than their share', () => {
    const p = previewBill(
      base({
        lines: [{ itemDescription: 'Taxi', subtotal: 30, participants: [{ userId: 'anna' }, { userId: 'boris' }, { userId: 'clara' }] }],
        payments: [
          { userId: 'anna', amount: 20 },
          { userId: 'clara', amount: 15 },
        ],
      }),
    )!
    // Shares scale to 35 paid: 11.6667 each. Anna is +8.3333, Clara +3.3333, Boris −11.6667.
    expect(p.debts).toEqual([
      { debtorId: 'boris', creditorId: 'anna', amount: toUnits(11.6667) },
      { debtorId: 'anna', creditorId: 'clara', amount: toUnits(3.3333) },
    ])
  })
})
