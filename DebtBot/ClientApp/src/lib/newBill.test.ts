import { describe, expect, it, vi } from 'vitest'
import { checkNewBill, draftChanged, draftKey, emptyNewBill, fromBill, impliedRate, loadDraft, lockedPeople, previewBill, removePerson, toBillCreation, withLine, withPayment, type NewBillForm } from './newBill'
import type { Bill } from '../api/types'
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

  it('locks payers, people on by-parts lines and anyone alone on a line, but not you as such', () => {
    const f = base({
      people: ['anna', 'boris', 'clara', 'dmitry', 'erik'],
      lines: [
        { itemDescription: 'Pasta', subtotal: 48, participants: [{ userId: 'anna' }, { userId: 'boris' }, { userId: 'clara' }] },
        { itemDescription: 'Tiramisu', subtotal: 10, participants: [{ userId: 'boris', part: 1 }, { userId: 'dmitry', part: 2 }] },
        { itemDescription: 'Coffee', subtotal: 3, participants: [{ userId: 'erik' }] },
      ],
      payments: [{ userId: 'boris', amount: 61 }],
    })
    expect([...lockedPeople(f)].sort()).toEqual(['boris', 'dmitry', 'erik'])
    // Nobody is locked on an empty bill: you can leave it too.
    expect(lockedPeople(base({})).size).toBe(0)
    // Equal parts other than 1 are still an equal split.
    expect(lockedPeople(base({ lines: [{ itemDescription: 'x', subtotal: 1, participants: [{ userId: 'anna', part: 2 }, { userId: 'boris', part: 2 }] }] })).size).toBe(0)
  })

  it('takes a removed person off every equal-split line', () => {
    const f = base({
      people: ['anna', 'boris', 'dmitry'],
      lines: [
        { itemDescription: 'Pasta', subtotal: 48, participants: [{ userId: 'anna' }, { userId: 'boris' }, { userId: 'dmitry' }] },
        { itemDescription: 'Wine', subtotal: 18, participants: [{ userId: 'anna' }, { userId: 'boris' }] },
      ],
      payments: [{ userId: 'boris', amount: 66 }],
    })
    const g = removePerson(f, 'anna')
    expect(g.people).toEqual(['boris', 'dmitry'])
    expect(g.lines.map((l) => l.participants.map((p) => p.userId))).toEqual([['boris', 'dmitry'], ['boris']])
    expect(g.payments).toEqual(f.payments)
    // Who owes whom no longer has you in it.
    expect(previewBill(g)!.people.map((p) => p.userId)).toEqual(['boris', 'dmitry'])
    expect(previewBill(g)!.debts).toEqual([{ debtorId: 'dmitry', creditorId: 'boris', amount: toUnits(24) }])
  })

  it('replaces a line when it is edited', () => {
    const line = { itemDescription: 'Tiramisu', subtotal: 12, participants: [{ userId: 'boris', part: 1 }, { userId: 'clara', part: 3 }] }
    const g = withLine(dinner, 1, line)
    expect(g.lines[1]).toEqual(line)
    expect(g.lines[0]).toEqual(dinner.lines[0])
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

describe('a draft as the form', () => {
  const u = (id: string, displayName: string) => ({ id, displayName })
  const anna = u('anna', 'Anna')
  const boris = u('boris', 'Boris')
  const dmitry = u('dmitry', 'Dmitry')
  const part = (billLineId: string, user: { id: string; displayName: string }, p: number) => ({ billLineId, user, part: p, userDisplayName: user.displayName })
  const draft = (patch: Partial<Bill> = {}): Bill =>
    ({
      id: 'b1',
      creator: anna,
      currencyCode: 'EUR',
      paymentCurrencyCode: 'EUR',
      description: 'Dinner at Molto',
      date: new Date(2026, 10, 21, 21, 15).toISOString(),
      status: 'Draft',
      totalWithTips: 76,
      exchangeBillId: null,
      lines: [
        { id: 'l1', billId: 'b1', itemDescription: 'Pasta', subtotal: 66, participants: [part('l1', boris, 1), part('l1', dmitry, 1)] },
        { id: 'l2', billId: 'b1', itemDescription: 'Tiramisu', subtotal: 10, participants: [part('l2', boris, 1), part('l2', dmitry, 2)] },
      ],
      payments: [{ user: boris, amount: 76 }],
      spendings: [],
      ledgerRecords: [],
      ...patch,
    }) as Bill

  it('fills the form from the saved draft; a creator on no line or payment is off the people', () => {
    const f = fromBill(draft())
    expect(f.people).toEqual(['boris', 'dmitry'])
    expect(f.lines[0].participants).toEqual([{ userId: 'boris' }, { userId: 'dmitry' }])
    expect(f.lines[1].participants).toEqual([
      { userId: 'boris', part: 1 },
      { userId: 'dmitry', part: 2 },
    ])
    expect(f.payments).toEqual([{ userId: 'boris', amount: 76 }])
    expect(f.total).toBe('')
    expect(f.paymentCurrency).toBe('')
    expect(f.date).toBe('2026-11-21T21:15')
    // The creator comes first while they take part, and on an empty draft.
    expect(fromBill(draft({ payments: [{ user: anna, amount: 76 }] } as Partial<Bill>)).people).toEqual(['anna', 'boris', 'dmitry'])
    expect(fromBill(draft({ lines: [], payments: [] })).people).toEqual(['anna'])
    // Tips and another payment currency show as typed values.
    const g = fromBill(draft({ totalWithTips: 80, paymentCurrencyCode: 'PLN' }))
    expect(g.total).toBe('80')
    expect(g.paymentCurrency).toBe('PLN')
  })

  it('knows when the form would change the saved draft', () => {
    const bill = draft()
    const f = fromBill(bill)
    expect(draftChanged(f, bill)).toBe(false)
    // People who are on nothing aren't saved, so adding or removing one isn't a change.
    expect(draftChanged({ ...f, people: [...f.people, 'clara'] }, bill)).toBe(false)
    expect(draftChanged(removePerson({ ...f, people: [...f.people, 'anna'] }, 'anna'), bill)).toBe(false)
    expect(draftChanged(withLine(f, 0, { ...f.lines[0], participants: [{ userId: 'boris' }] }), bill)).toBe(true)
    expect(draftChanged({ ...f, total: '80' }, bill)).toBe(true)
    // The server's order, and an equal split with parts other than 1, are the same bill.
    expect(draftChanged({ ...f, lines: [...f.lines].reverse(), payments: [...f.payments].reverse() }, bill)).toBe(false)
    expect(
      draftChanged(
        withLine(f, 0, {
          ...f.lines[0],
          participants: [
            { userId: 'dmitry', part: 2 },
            { userId: 'boris', part: 2 },
          ],
        }),
        bill,
      ),
    ).toBe(false)
  })

  it('takes the form from the tab only when it was based on the same saved draft', () => {
    const items = new Map<string, string>()
    vi.stubGlobal('sessionStorage', { getItem: (k: string) => items.get(k) ?? null, setItem: (k: string, v: string) => items.set(k, v), removeItem: (k: string) => items.delete(k) })
    try {
      const bill = draft()
      const edited = { ...fromBill(bill), description: 'Edited' }
      items.set(draftKey('b1'), JSON.stringify({ base: JSON.stringify(fromBill(bill)), form: edited }))
      expect(loadDraft(bill).description).toBe('Edited')
      // Saved again elsewhere meanwhile: start from the server's version.
      expect(loadDraft(draft({ description: 'Changed on the server' })).description).toBe('Changed on the server')
    } finally {
      vi.unstubAllGlobals()
    }
  })
})
