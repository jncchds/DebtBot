import type { Bill, BillCreation, BillLineCreation, BillPaymentCreation } from '../api/types'
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

const isEqualLine = (l: BillLineCreation) => l.participants.every((p) => (p.part ?? 1) === (l.participants[0].part ?? 1))

/**
 * Who can't be taken off the bill (series 2, user decision): anyone who paid, is on a by-parts line or is the only
 * person on a line. The same for everyone, the creator included. Equal-split lines don't lock: removing someone
 * takes them off those lines (removePerson).
 */
export function lockedPeople(f: Pick<NewBillForm, 'lines' | 'payments'>): Set<string> {
  const ids = new Set<string>()
  f.payments.forEach((p) => ids.add(p.userId))
  f.lines.forEach((l) => {
    if (l.participants.length === 1 || !isEqualLine(l)) l.participants.forEach((p) => ids.add(p.userId))
  })
  return ids
}

/** Takes someone off the bill and off every equal-split line; those lines are then split among the people left. */
export function removePerson(f: NewBillForm, userId: string): NewBillForm {
  return {
    ...f,
    people: f.people.filter((x) => x !== userId),
    lines: f.lines.map((l) => (l.participants.length > 1 && isEqualLine(l) ? { ...l, participants: l.participants.filter((p) => p.userId !== userId) } : l)),
  }
}

/** Which editor is open, from the URL: <base>/add-line, <base>/add-payment, <base>/edit-line/<n> (1-based). */
export type FormEditor = { kind: 'add-line' } | { kind: 'add-payment' } | { kind: 'edit-line'; index: number }

/** The editor named by a route's `mode` and its `:n` (edit-line), or none. */
export function formEditor(mode: 'add-line' | 'add-payment' | 'edit-line' | undefined, n: string | undefined): FormEditor | undefined {
  if (mode === 'edit-line') {
    const index = Number(n) - 1
    return Number.isInteger(index) && index >= 0 ? { kind: 'edit-line', index } : undefined
  }
  return mode ? { kind: mode } : undefined
}

/** Replaces line `index` (✎ Edit line). */
export function withLine(f: NewBillForm, index: number, line: BillLineCreation): NewBillForm {
  return { ...f, lines: f.lines.map((l, i) => (i === index ? line : l)) }
}

/**
 * A saved draft as the form (the draft page is the New bill form, prefilled). People are the creator, then everyone
 * on a line or a payment; the creator only while they're on one, or while the draft has no lines or payments yet
 * (they took themselves off otherwise). Equal splits leave `part` out; an empty total and payment currency stand
 * for "the lines' total" and "the bill currency", as on New bill.
 */
export function fromBill(bill: Bill): NewBillForm {
  const lines: BillLineCreation[] = bill.lines.map((l) => {
    const equal = l.participants.every((p) => p.part === l.participants[0].part)
    return {
      itemDescription: l.itemDescription,
      subtotal: l.subtotal,
      participants: l.participants.map((p) => (equal ? { userId: p.user.id } : { userId: p.user.id, part: p.part })),
    }
  })
  const payments: BillPaymentCreation[] = bill.payments.map((p) => ({ userId: p.user.id, amount: p.amount }))
  const taking = new Set([...lines.flatMap((l) => l.participants.map((p) => p.userId)), ...payments.map((p) => p.userId)])
  const people = [...new Set([...(taking.size === 0 || taking.has(bill.creator.id) ? [bill.creator.id] : []), ...taking])]
  const sameTotal = toUnits(bill.totalWithTips) === linesUnits({ lines })
  return {
    description: bill.description,
    date: localDateTime(new Date(bill.date)),
    currency: bill.currencyCode,
    paymentCurrency: bill.paymentCurrencyCode === bill.currencyCode ? '' : bill.paymentCurrencyCode,
    total: sameTotal ? '' : String(bill.totalWithTips),
    people,
    lines,
    payments,
  }
}

/** The request for comparing: order doesn't matter, and an equal split is the same whatever its parts. */
function comparable(f: NewBillForm): string {
  const body = toBillCreation(f)
  const lines = body.lines!.map((l) =>
    JSON.stringify({
      ...l,
      participants: l.participants.map((p) => (isEqualLine(l) ? { userId: p.userId } : { userId: p.userId, part: p.part ?? 1 })).sort((a, b) => a.userId.localeCompare(b.userId)),
    }),
  )
  const payments = [...body.payments!].sort((a, b) => a.userId.localeCompare(b.userId))
  return JSON.stringify({ ...body, lines: lines.sort(), payments })
}

/** Whether the form would change the saved draft (people who are on no line or payment aren't saved). */
export function draftChanged(f: NewBillForm, bill: Bill): boolean {
  return comparable(f) !== comparable(fromBill(bill))
}

/** What's kept in the tab for a draft: the form and the saved draft it was based on (fromBill, as JSON). */
export interface StoredDraft {
  base: string
  form: NewBillForm
}

export const draftKey = (billId: string) => `debtbot.draft.${billId}`

/** The draft's form from the tab when it was based on this very version of the bill, else the bill as saved. */
export function loadDraft(bill: Bill): NewBillForm {
  const base = JSON.stringify(fromBill(bill))
  try {
    const raw = sessionStorage.getItem(draftKey(bill.id))
    const stored = raw ? (JSON.parse(raw) as StoredDraft) : undefined
    if (stored?.base === base && stored.form) return stored.form
  } catch {
    // Storage blocked or broken: start from the bill.
  }
  return fromBill(bill)
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
