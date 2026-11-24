import type { Bill, ExchangeCreation, ExchangeListItem, UserDisplay } from '../api/types'
import { currencyDigits, fromUnits, parseAmount, toUnits } from './money'

/**
 * Exchange maths (mockups/exchange). Amounts are integer units of 1/10000 (lib/money.ts); a typed rate is
 * an integer of 1/10^8, so "0.23255814" works too. Rates are only shown (4 decimals), never summed.
 */

const RATE_SCALE = 100_000_000n

/** A rate as typed: a positive number with up to 8 decimals, as units of 1/10^8; null for anything else. */
export function parseRate(text: string): bigint | null {
  const t = text.trim().replace(',', '.')
  if (!/^\d{1,9}(\.\d{0,8})?$/.test(t)) return null
  const [int, frac = ''] = t.split('.')
  const v = BigInt(int) * RATE_SCALE + BigInt((frac + '00000000').slice(0, 8))
  return v > 0n ? v : null
}

/** a / b rounded half up, for positive values. */
function divRound(a: bigint, b: bigint): bigint {
  return (2n * a + b) / (2n * b)
}

/** Units rounded to the currency's own digits (EUR: 0.01). */
export function roundToCurrency(units: bigint, currency: string): bigint {
  const step = 10n ** BigInt(4 - Math.min(4, currencyDigits(currency || 'EUR')))
  return divRound(units, step) * step
}

/** Which side is typed as a rate instead of an amount (URL `rate=give|get`); at most one. */
export type RateSide = 'give' | 'get' | null

export interface ExchangeForm {
  giveAmount: string
  giveCurrency: string
  getAmount: string
  getCurrency: string
  /** The typed rate, used by the side in rate mode. */
  rate: string
  /**
   * How the rate reads. false: "1 <give currency> = rate <get currency>"; true: "1 <get currency> = rate <give currency>".
   */
  flip: boolean
  description: string
}

export const emptyExchangeForm = (): ExchangeForm => ({ giveAmount: '', giveCurrency: '', getAmount: '', getCurrency: '', rate: '', flip: false, description: '' })

export interface ExchangeAmounts {
  /** What you give and what you get, in units (rounded to the currency when worked out from a rate); null if unknown. */
  give: bigint | null
  get: bigint | null
}

const amountUnits = (text: string): bigint | null => {
  const n = parseAmount(text)
  return n !== null && n > 0 ? toUnits(n) : null
}

/** The two amounts: as typed, or one of them worked out from the rate. */
export function exchangeAmounts(form: ExchangeForm, side: RateSide): ExchangeAmounts {
  const give = amountUnits(form.giveAmount)
  const get = amountUnits(form.getAmount)
  const rate = parseRate(form.rate)
  if (side === 'get') {
    if (give === null || rate === null) return { give, get: null }
    // 1 give = rate get  →  get = give × rate;  1 get = rate give  →  get = give / rate
    const raw = form.flip ? divRound(give * RATE_SCALE, rate) : divRound(give * rate, RATE_SCALE)
    return { give, get: positive(roundToCurrency(raw, form.getCurrency)) }
  }
  if (side === 'give') {
    if (get === null || rate === null) return { give: null, get }
    const raw = form.flip ? divRound(get * rate, RATE_SCALE) : divRound(get * RATE_SCALE, rate)
    return { give: positive(roundToCurrency(raw, form.giveCurrency)), get }
  }
  return { give, get }
}

const positive = (u: bigint) => (u > 0n ? u : null)

/** "How many `to` for one `from`", for display: 430 PLN for 100 EUR → 4.3. */
export function unitRate(from: number | bigint, to: number | bigint): number {
  const f = typeof from === 'bigint' ? fromUnits(from) : from
  const t = typeof to === 'bigint' ? fromUnits(to) : to
  return f > 0 ? t / f : 0
}

/** A rate with 4 decimals: "4.3000". */
export function formatRate(rate: number, locale = 'en'): string {
  return new Intl.NumberFormat(locale, { minimumFractionDigits: 4, maximumFractionDigits: 4 }).format(rate)
}

const isCurrency = (c: string) => /^[A-Za-z]{3}$/.test(c.trim())

/** Problems with the form, as locale keys per field; empty when it can be created. */
export function exchangeProblems(form: ExchangeForm, side: RateSide, otherId: string | undefined): Partial<Record<'person' | 'give' | 'get' | 'giveCurrency' | 'getCurrency' | 'rate', string>> {
  const out: Partial<Record<'person' | 'give' | 'get' | 'giveCurrency' | 'getCurrency' | 'rate', string>> = {}
  const { give, get } = exchangeAmounts(form, side)
  if (!otherId) out.person = 'exchange.needPerson'
  if (!isCurrency(form.giveCurrency)) out.giveCurrency = 'exchange.needCurrency'
  if (!isCurrency(form.getCurrency)) out.getCurrency = 'exchange.needCurrency'
  else if (isCurrency(form.giveCurrency) && form.giveCurrency.trim().toUpperCase() === form.getCurrency.trim().toUpperCase()) out.getCurrency = 'exchange.sameCurrency'
  if (side !== null && parseRate(form.rate) === null) out.rate = 'exchange.needRate'
  if (side !== 'give' && give === null) out.give = 'exchange.needAmount'
  if (side !== 'get' && get === null) out.get = 'exchange.needAmount'
  if (side !== null && !out.rate && (give === null || get === null) && !out.give && !out.get) out.rate = 'exchange.rateTooSmall'
  return out
}

/** The request: you hand over what you give, they hand back what you get. */
export function toExchangeCreation(meId: string, otherId: string, form: ExchangeForm, side: RateSide): ExchangeCreation | null {
  const { give, get } = exchangeAmounts(form, side)
  if (give === null || get === null) return null
  return {
    fromUserId: meId,
    fromAmount: fromUnits(give),
    fromCurrencyCode: form.giveCurrency.trim().toUpperCase(),
    toUserId: otherId,
    toAmount: fromUnits(get),
    toCurrencyCode: form.getCurrency.trim().toUpperCase(),
    description: form.description.trim() || undefined,
  }
}

/** Swaps the two sides (⇄): amounts, currencies and which side is a rate; the rate keeps its meaning. */
export function swapSides(form: ExchangeForm, side: RateSide): { form: ExchangeForm; side: RateSide } {
  return {
    form: { ...form, giveAmount: form.getAmount, giveCurrency: form.getCurrency, getAmount: form.giveAmount, getCurrency: form.giveCurrency, flip: !form.flip },
    side: side === 'give' ? 'get' : side === 'get' ? 'give' : null,
  }
}

/** An exchange seen as one deal: "from" hands over `fromAmount`, "to" hands back `toAmount`. */
export interface ExchangeDeal {
  from: UserDisplay
  fromAmount: number
  fromCurrency: string
  /** The bill in which "from" pays. */
  fromBillId: string
  to: UserDisplay
  toAmount: number
  toCurrency: string
  toBillId: string
}

/** One bill of an exchange describes the whole deal: its payer hands over the payment, its line's person hands back the subtotal. */
export function dealFromBill(bill: Pick<Bill, 'id' | 'exchangeBillId' | 'payments' | 'lines' | 'currencyCode' | 'paymentCurrencyCode'>): ExchangeDeal | null {
  const payment = bill.payments[0]
  const line = bill.lines[0]
  const taker = line?.participants[0]?.user
  if (!payment || !line || !taker || !bill.exchangeBillId) return null
  return {
    from: payment.user,
    fromAmount: payment.amount,
    fromCurrency: bill.paymentCurrencyCode,
    fromBillId: bill.id,
    to: taker,
    toAmount: line.subtotal,
    toCurrency: bill.currencyCode,
    toBillId: bill.exchangeBillId,
  }
}

export function dealFromListItem(x: ExchangeListItem): ExchangeDeal | null {
  if (!x.fromUser || !x.toUser) return null
  return {
    from: x.fromUser,
    fromAmount: x.fromAmount,
    fromCurrency: x.fromCurrencyCode,
    fromBillId: x.forwardBillId,
    to: x.toUser,
    toAmount: x.toAmount,
    toCurrency: x.toCurrencyCode,
    toBillId: x.backwardBillId,
  }
}

/** The deal from your side: when you're "to", the sides are swapped so "from" is you. */
export function orientDeal(deal: ExchangeDeal, meId: string | undefined): ExchangeDeal {
  if (deal.to.id !== meId) return deal
  return {
    from: deal.to,
    fromAmount: deal.toAmount,
    fromCurrency: deal.toCurrency,
    fromBillId: deal.toBillId,
    to: deal.from,
    toAmount: deal.fromAmount,
    toCurrency: deal.fromCurrency,
    toBillId: deal.fromBillId,
  }
}
