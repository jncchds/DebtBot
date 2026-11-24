import { useId, useState, type FormEvent, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import type { BillPaymentCreation } from '../../api/types'
import { defaultPayer } from '../../lib/editors'
import { parseAmount } from '../../lib/money'
import type { Person } from '../../lib/usePeople'

export interface PaymentEditorProps {
  /** Who can have paid: the people on the bill (PeoplePicker). */
  people: Person[]
  /** The payment currency. */
  currency: string
  onSubmit: (payment: BillPaymentCreation) => void
  onCancel?: () => void
  busy?: boolean
  error?: ReactNode
  hint?: ReactNode
  title?: string
  submitLabel?: string
}

/** Who paid how much. Reusable like LineEditor (bill page and New bill form); it only builds a BillPaymentCreation. */
export function PaymentEditor({ people, currency, onSubmit, onCancel, busy, error, hint, title, submitLabel }: PaymentEditorProps) {
  const { t } = useTranslation()
  const id = useId()
  const [who, setWho] = useState(() => defaultPayer(people))
  const [text, setText] = useState('')
  const [tried, setTried] = useState(false)
  const amount = parseAmount(text)
  const amountProblem = amount === null || amount <= 0 ? t('editor.needAmount') : undefined
  const whoProblem = !people.some((p) => p.id === who) ? t('editor.needPayer') : undefined

  const submit = (e: FormEvent) => {
    e.preventDefault()
    setTried(true)
    if (amountProblem || whoProblem || amount === null) return
    onSubmit({ userId: who, amount })
  }

  return (
    <form onSubmit={submit} className="rounded-lg border border-accent/40 bg-surface-2 p-3 md:p-4" noValidate>
      {title && <h2 className="mb-3 font-bold">{title}</h2>}
      <div className="grid gap-3 md:grid-cols-[2fr_1fr]">
        <div>
          <label className="label" htmlFor={`${id}-who`}>
            {t('editor.whoPaid')}
          </label>
          <select id={`${id}-who`} className="input" value={who} onChange={(e) => setWho(e.target.value)}>
            {people.map((p) => (
              <option key={p.id} value={p.id}>
                {p.name}
              </option>
            ))}
          </select>
          {people.length === 0 ? (
            <p className="mt-1 text-xs muted">{t('editor.addPeopleFirst')}</p>
          ) : (
            tried && whoProblem && <p className="mt-1 text-xs text-danger">{whoProblem}</p>
          )}
        </div>
        <div>
          <label className="label" htmlFor={`${id}-amount`}>
            {t('editor.amount', { currency })}
          </label>
          <input id={`${id}-amount`} className="input text-right tabular-nums" inputMode="decimal" placeholder="0.00" autoFocus value={text} onChange={(e) => setText(e.target.value)} />
          {tried && amountProblem && <p className="mt-1 text-xs text-danger">{amountProblem}</p>}
        </div>
      </div>
      {hint && <p className="mt-2 text-xs muted">{hint}</p>}
      {error && <div className="mt-3">{error}</div>}
      <div className="mt-3.5 flex flex-wrap items-center gap-2">
        <button type="submit" className="btn-primary" disabled={busy}>
          {submitLabel ?? t('editor.addPayment')}
        </button>
        {onCancel && (
          <button type="button" className="btn-ghost" onClick={onCancel}>
            {t('common.cancel')}
          </button>
        )}
      </div>
    </form>
  )
}
