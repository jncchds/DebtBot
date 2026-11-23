import type { Bill, BillListItem, LedgerRecord, ProcessingState } from '../api/types'
import { sumUnits, toUnits } from './money'

/** Your net balance on a bill per currency, in units: > 0 others owe you, < 0 you owe. Zero currencies are left out. */
export function myBalance(records: LedgerRecord[], meId: string): Map<string, bigint> {
  const out = new Map<string, bigint>()
  for (const r of records) {
    const sign = r.creditorUser.id === meId ? 1n : r.debtorUser.id === meId ? -1n : 0n
    if (sign === 0n) continue
    out.set(r.currencyCode, (out.get(r.currencyCode) ?? 0n) + sign * toUnits(r.amount))
  }
  for (const [c, v] of out) if (v === 0n) out.delete(c)
  return out
}

/** Tips in units: the total minus the line subtotals (0 when the total is just the lines). */
export function tipsUnits(bill: Pick<Bill, 'totalWithTips' | 'lines'>): bigint {
  return toUnits(bill.totalWithTips) - sumUnits(bill.lines.map((l) => l.subtotal))
}

/** Everyone on the bill: line participants and payers, once each. */
export function peopleCount(bill: Pick<Bill, 'lines' | 'payments'>): number {
  const ids = new Set<string>()
  bill.lines.forEach((l) => l.participants.forEach((p) => ids.add(p.user.id)))
  bill.payments.forEach((p) => ids.add(p.user.id))
  return ids.size
}

/** Whether every participant of a line has the same part (an equal split). */
export function isEqualSplit(parts: number[]): boolean {
  return parts.every((p) => p === parts[0])
}

/** Who may change a bill: the creator or an admin (the API checks again). */
export function canChange(bill: Pick<Bill, 'creator'>, meId: string | undefined, isAdmin: boolean): boolean {
  return isAdmin || bill.creator.id === meId
}

/** Reasons the API would refuse Finalize (api.md), as locale keys; empty when it can be finalized. */
export function finalizeBlockers(bill: Pick<Bill, 'status' | 'lines' | 'payments' | 'totalWithTips'>): string[] {
  const out: string[] = []
  if (bill.status !== 'Draft') return ['bill.blockers.notDraft']
  if (!bill.lines.some((l) => l.subtotal > 0)) out.push('bill.blockers.noLines')
  else if (bill.lines.some((l) => l.participants.length === 0)) out.push('bill.blockers.noParticipants')
  if (!bill.payments.some((p) => p.amount > 0)) out.push('bill.blockers.noPayments')
  if (!(bill.totalWithTips > 0)) out.push('bill.blockers.noTotal')
  return out
}

export const canCancel = (status: ProcessingState) => status === 'Draft' || status === 'Processed'

/** A row of the Bills list: a bill, or both bills of an exchange (when both are on the page). */
export interface BillRow {
  bill: BillListItem
  /** The other bill of an exchange, when it's on this page too. */
  pair?: BillListItem
}

/** Folds the two bills of an exchange into one row, at the position of the first one. */
export function pairExchanges(items: BillListItem[]): BillRow[] {
  const byId = new Map(items.map((b) => [b.id, b]))
  const used = new Set<string>()
  const rows: BillRow[] = []
  for (const b of items) {
    if (used.has(b.id)) continue
    const pair = b.exchangeBillId ? byId.get(b.exchangeBillId) : undefined
    if (pair && pair.exchangeBillId === b.id) used.add(pair.id)
    rows.push({ bill: b, pair: pair && pair.exchangeBillId === b.id ? pair : undefined })
  }
  return rows
}

/** Bill descriptions can have several lines (an exchange adds its rate); titles and lists show the first. */
export function firstLine(s: string): string {
  return s.split('\n')[0].trim()
}
