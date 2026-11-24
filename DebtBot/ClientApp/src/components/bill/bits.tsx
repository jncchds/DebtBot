import { useTranslation } from 'react-i18next'
import type { LedgerRecord, ProcessingState } from '../../api/types'
import { myBalance } from '../../lib/bills'
import { useFormat } from '../../lib/useFormat'

const PILL: Record<ProcessingState, string> = {
  Draft: 'pill-accent',
  Ready: 'pill-muted',
  Processed: 'pill-muted',
  Cancelled: 'pill-danger',
}

const STATUS_KEY: Record<ProcessingState, string> = {
  Draft: 'bill.status.draft',
  Ready: 'bill.status.processing',
  Processed: 'bill.status.finalized',
  Cancelled: 'bill.status.cancelled',
}

export function StatusPill({ status }: { status: ProcessingState }) {
  const { t } = useTranslation()
  return <span className={`${PILL[status]} whitespace-nowrap`}>{t(STATUS_KEY[status])}</span>
}

/**
 * Your balance on a bill: "owed to you 30.00 EUR" / "you owe 10.00 EUR", one per currency, or a dash.
 * With `name`, someone else's (T7b following): "owed to Boris" / "Boris owes", and `meId` is their id.
 */
export function MyBalance({ records, meId, name }: { records: LedgerRecord[]; meId: string | undefined; name?: string }) {
  const { t } = useTranslation()
  const f = useFormat()
  const b = meId ? [...myBalance(records, meId)] : []
  if (!b.length) return <span className="muted">—</span>
  return (
    <span className="inline-flex flex-col items-end">
      {b.map(([c, u]) => (
        <span key={c} className={`tabular-nums whitespace-nowrap ${u > 0n ? 'text-success' : 'text-danger'}`}>
          {name
            ? t(u > 0n ? 'following.bills.owedTo' : 'following.bills.owes', { amount: f.money(u, c), name })
            : t(u > 0n ? 'bills.owedToYou' : 'bills.youOwe', { amount: f.money(u, c) })}
        </span>
      ))}
    </span>
  )
}
