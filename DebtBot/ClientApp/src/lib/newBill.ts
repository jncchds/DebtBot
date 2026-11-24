import type { BillCreation, BillLineCreation, BillPaymentCreation } from '../api/types'
import { fromUnits, parseAmount, sumUnits, toUnits } from './money'

/**
 * The New bill form (mockups/new-bill): its state, checks, the request it sends and the live preview of who
 * owes whom. The state is kept in sessionStorage so a reload keeps it; lines and payments are kept as the
 * request objects the shared editors build.
 */

export interface NewBillForm {
  description: string
  /** `yyyy-MM-ddTHH:mm`, local time (datetime-local). */
  date: string
  currency: string
  /** Empty: the bill currency. */
  paymentCurrency: string
  /** Total with tips in the bill currency; empty: the lines' total. */
  total: string
  /** People on the bill (PeoplePicker), in order. */
  people: string[]
  lines: BillLineCreation[]
  payments: BillPaymentCreation[]
}

const pad = (n: number) => String(n).padStart(2, '0')

/** A Date as the value of a datetime-local input. */
export function localDateTime(d: Date): string {
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`
}

export function emptyNewBill(meId: string | undefined, now = new Date()): NewBillForm {
  return { description: '', date: localDateTime(now), currency: '', paymentCurrency: '', total: '', people: meId ? [meId] : [], lines: [], payments: [] }
}

const isCurrency = (c: string) => /^[A-Za-z]{3}$/.test(c.trim())

export const billCurrency = (f: NewBillForm) => f.currency.trim().toUpperCase()
export const paymentCurrency = (f: NewBillForm) => (f.paymentCurrency.trim() ? f.paymentCurrency.trim().toUpperCase() : billCurrency(f))

/** Everyone on a line or a payment (they can't be taken off the bill), plus you. */
export function lockedPeople(f: NewBillForm, meId: string | undefined): Set<string> {
  const ids = new Set<string>(meId ? [meId] : [])
  f.lines.forEach((l) => l.participants.forEach((p) => ids.add(p.userId)))
  f.payments.forEach((p) => ids.add(p.userId))
  return ids
}

/** A payment for someone who already paid replaces their amount (as the API does). */
export function withPayment(f: NewBillForm, p: BillPaymentCreation): NewBillForm {
  return { ...f, payments: [...f.payments.filter((x) => x.userId !== p.userId), p] }
}

export const linesUnits = (f: Pick<NewBillForm, 'lines'>) => sumUnits(f.lines.map((l) => l.subtotal))
export const paymentsUnits = (f: Pick<NewBillForm, 'payments'>) => sumUnits(f.payments.map((p) => p.amount))

/** The total with tips in units: as typed, or the lines' total. Null when the typed total isn't a number. */
export function totalUnits(f: NewBillForm): bigint | null {
  if (!f.total.trim()) return linesUnits(f)
  const n = parseAmount(f.total)
  return n === null ? null : toUnits(n)
}

export type FieldProblems = Partial<Record<'currency' | 'paymentCurrency' | 'total' | 'date', string>>

export interface NewBillCheck {
  /** Per field, as locale keys. */
  fields: FieldProblems
  /** Save as draft is possible (the fields are fine). */
  canSave: boolean
  /** What's missing to finalize, as locale keys (bill.blockers.*). */
  finalizeBlockers: string[]
}

export function checkNewBill(f: NewBillForm): NewBillCheck {
  const fields: FieldProblems = {}
  if (!isCurrency(f.currency)) fields.currency = 'newBill.needCurrency'
  if (f.paymentCurrency.trim() && !isCurrency(f.paymentCurrency)) fields.paymentCurrency = 'newBill.needCurrency'
  if (f.total.trim()) {
    const n = parseAmount(f.total)
    if (n === null || n <= 0) fields.total = 'editor.needAmount'
  }
  if (!f.date || Number.isNaN(new Date(f.date).getTime())) fields.date = 'newBill.needDate'
  const canSave = Object.keys(fields).length === 0
  const finalizeBlockers: string[] = []
  if (!f.lines.some((l) => l.subtotal > 0)) finalizeBlockers.push('bill.blockers.noLines')
  if (!f.payments.some((p) => p.amount > 0)) finalizeBlockers.push('bill.blockers.noPayments')
  const total = totalUnits(f)
  if (canSave && f.lines.length > 0 && (total === null || total <= 0n)) finalizeBlockers.push('bill.blockers.noTotal')
  return { fields, canSave, finalizeBlockers }
}

/** The form as `POST Bills`. Amounts go as typed; an empty total or payment currency is left to the API's default. */
export function toBillCreation(f: NewBillForm): BillCreation {
  const total = f.total.trim() ? parseAmount(f.total) : null
  const description = f.description.trim()
  const date = new Date(f.date)
  return {
    currencyCode: billCurrency(f),
    paymentCurrencyCode: f.paymentCurrency.trim() ? f.paymentCurrency.trim().toUpperCase() : undefined,
    description: description || undefined,
    date: Number.isNaN(date.getTime()) ? undefined : date.toISOString(),
    totalWithTips: total ?? undefined,
    lines: f.lines,
    payments: f.payments,
  }
}

export interface PreviewPerson {
  userId: string
  /** Share in the bill currency, with tips (as the bill's spendings). */
  shareBill: bigint
  /** Share in the payment currency (what the ledger uses). */
  sharePay: bigint
  paid: bigint
  /** paid − share: > 0 others owe them. */
  balance: bigint
}

export interface PreviewDebt {
  debtorId: string
  creditorId: string
  amount: bigint
}

export interface BillPreview {
  people: PreviewPerson[]
  debts: PreviewDebt[]
}

/** a × b / c rounded half away from zero (units stay integers). */
function mulDiv(a: bigint, b: bigint, c: bigint): bigint {
  const n = a * b
  const neg = n < 0n !== c < 0n
  const q = ((n < 0n ? -n : n) * 2n + (c < 0n ? -c : c)) / (2n * (c < 0n ? -c : c))
  return neg ? -q : q
}

/**
 * Who owes whom, worked out like the server's finalize (BillFinalizedConsumer): each person's share of each line
 * (subtotal × part / parts), scaled to what was paid (× payments total / lines total) in the payment currency.
 * The person with the highest balance is owed by everyone else. A preview only: the server does the real sums.
 */
export function previewBill(f: NewBillForm): BillPreview | null {
  const lines = linesUnits(f)
  const paid = paymentsUnits(f)
  if (lines <= 0n || paid <= 0n) return null
  const total = totalUnits(f) ?? lines
  const raw = new Map<string, bigint>() // share in bill units, without tips
  for (const l of f.lines) {
    const parts = l.participants.map((p) => toUnits(p.part ?? 1))
    const sum = parts.reduce((a, b) => a + b, 0n)
    if (sum <= 0n) continue
    l.participants.forEach((p, i) => raw.set(p.userId, (raw.get(p.userId) ?? 0n) + mulDiv(toUnits(l.subtotal), parts[i], sum)))
  }
  const paidBy = new Map<string, bigint>()
  for (const p of f.payments) paidBy.set(p.userId, (paidBy.get(p.userId) ?? 0n) + toUnits(p.amount))
  const ids = [...new Set([...f.people, ...raw.keys(), ...paidBy.keys()])]
  const people = ids
    .filter((id) => raw.has(id) || paidBy.has(id))
    .map((userId) => {
      const r = raw.get(userId) ?? 0n
      const sharePay = mulDiv(r, paid, lines)
      const p = paidBy.get(userId) ?? 0n
      return { userId, shareBill: mulDiv(r, total, lines), sharePay, paid: p, balance: p - sharePay }
    })
  const sponsor = people.reduce<PreviewPerson | undefined>((best, p) => (!best || p.balance > best.balance ? p : best), undefined)
  const debts: PreviewDebt[] = []
  for (const p of people) {
    if (!sponsor || p.userId === sponsor.userId) continue
    const owe = p.sharePay - p.paid
    if (owe > 0n) debts.push({ debtorId: p.userId, creditorId: sponsor.userId, amount: owe })
    else if (owe < 0n) debts.push({ debtorId: sponsor.userId, creditorId: p.userId, amount: -owe })
  }
  return { people, debts }
}

/** "1 <payment> = x <bill>" when the payment currency differs: the rate the payments imply. */
export function impliedRate(f: NewBillForm): number | null {
  const paid = paymentsUnits(f)
  const total = totalUnits(f)
  if (paid <= 0n || !total || total <= 0n) return null
  return fromUnits(total) / fromUnits(paid)
}

// sessionStorage: per tab, so a reload keeps the form; it's cleared once the bill is created.

export function loadStored<T>(key: string, fallback: T): T {
  try {
    const raw = sessionStorage.getItem(key)
    return raw ? { ...fallback, ...(JSON.parse(raw) as T) } : fallback
  } catch {
    return fallback
  }
}

export function store(key: string, value: unknown) {
  try {
    if (value === undefined) sessionStorage.removeItem(key)
    else sessionStorage.setItem(key, JSON.stringify(value))
  } catch {
    // Storage blocked: the form still works, it just doesn't survive a reload.
  }
}
