import { useId, useState, type FormEvent, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import clsx from 'clsx'
import type { BillLineCreation } from '../../api/types'
import { lineStart } from '../../lib/editors'
import { parseAmount, parsePart } from '../../lib/money'
import type { Person } from '../../lib/usePeople'

export interface LineEditorProps {
  /** Who can take part: the people on the bill (PeoplePicker). All of them are ticked to start with. */
  people: Person[]
  /** The bill currency, shown next to the subtotal. */
  currency: string
  onSubmit: (line: BillLineCreation) => void
  onCancel?: () => void
  busy?: boolean
  /** An error from the API, shown above the buttons. */
  error?: ReactNode
  /** A note next to the buttons (e.g. what happens to the total). */
  hint?: ReactNode
  title?: string
  submitLabel?: string
  /** ✎ Edit line: the line to start from. People on the bill but not on it start unticked. */
  initial?: BillLineCreation
}

type Mode = 'equal' | 'parts'

/**
 * One bill line: item, subtotal and who shares it, equally or by parts. Reusable: the bill page adds lines
 * to a draft with it, and the New bill form (T6) builds its lines with it. It only builds a BillLineCreation;
 * the caller sends it. Amounts are sent as typed (parsed decimals, never computed).
 */
export function LineEditor({ people, currency, onSubmit, onCancel, busy, error, hint, title, submitLabel, initial }: LineEditorProps) {
  const { t } = useTranslation()
  const id = useId()
  const [start] = useState(() => lineStart(people, initial))
  const [item, setItem] = useState(start.item)
  const [subtotal, setSubtotal] = useState(start.subtotal)
  const [mode, setMode] = useState<Mode>(start.mode)
  // Unticked people and parts, so people added to the bill later start ticked with part 1.
  const [off, setOff] = useState<Set<string>>(() => new Set(start.off))
  const [parts, setParts] = useState<Record<string, string>>(start.parts)
  const partOf = (pid: string) => parts[pid] ?? '1'
  const [tried, setTried] = useState(false)

  const selected = people.filter((p) => !off.has(p.id))
  const amount = parseAmount(subtotal)
  const problems = {
    item: !item.trim() ? t('editor.needItem') : undefined,
    subtotal: amount === null || amount <= 0 ? t('editor.needAmount') : undefined,
    people: selected.length === 0 ? t('editor.needPeople') : undefined,
    parts: mode === 'parts' && selected.some((p) => parsePart(partOf(p.id)) === null) ? t('editor.needParts') : undefined,
  }
  const valid = !Object.values(problems).some(Boolean)

  const toggle = (pid: string) =>
    setOff((o) => {
      const next = new Set(o)
      if (next.has(pid)) next.delete(pid)
      else next.add(pid)
      return next
    })

  const submit = (e: FormEvent) => {
    e.preventDefault()
    setTried(true)
    if (!valid || amount === null) return
    onSubmit({
      itemDescription: item.trim(),
      subtotal: amount,
      participants: selected.map((p) => (mode === 'parts' ? { userId: p.id, part: parsePart(partOf(p.id)) } : { userId: p.id })),
    })
  }

  const show = (msg?: string) => tried && msg && <p className="mt-1 text-xs text-danger">{msg}</p>

  return (
    <form onSubmit={submit} className="rounded-lg border border-accent/40 bg-surface-2 p-3 md:p-4" noValidate>
      {title && <h2 className="mb-3 font-bold">{title}</h2>}
      <div className="grid gap-3 md:grid-cols-[2fr_1fr]">
        <div>
          <label className="label" htmlFor={`${id}-item`}>
            {t('editor.item')}
          </label>
          <input id={`${id}-item`} className="input" value={item} maxLength={200} autoFocus onChange={(e) => setItem(e.target.value)} />
          {show(problems.item)}
        </div>
        <div>
          <label className="label" htmlFor={`${id}-sub`}>
            {t('editor.subtotal', { currency })}
          </label>
          <input id={`${id}-sub`} className="input text-right tabular-nums" inputMode="decimal" value={subtotal} placeholder="0.00" onChange={(e) => setSubtotal(e.target.value)} />
          {show(problems.subtotal)}
        </div>
      </div>

      <div className="mt-3 flex flex-wrap items-center gap-2" role="group" aria-label={t('editor.split')}>
        <span className="label mb-0">{t('editor.split')}</span>
        {(['equal', 'parts'] as const).map((m) => (
          <button
            key={m}
            type="button"
            aria-pressed={mode === m}
            onClick={() => setMode(m)}
            className={clsx('rounded-full border px-2.5 py-0.5 text-[0.8rem]', mode === m ? 'border-accent/40 bg-accent-faint font-semibold text-accent' : 'border-line text-muted hover:border-accent')}
          >
            {t(m === 'equal' ? 'editor.equally' : 'editor.byParts')}
          </button>
        ))}
      </div>

      <ul className="mt-3 divide-y divide-line rounded-md border border-line bg-surface">
        {people.map((p) => {
          const on = !off.has(p.id)
          return (
            <li key={p.id} className="flex items-center gap-3 px-3 py-1.5 text-sm">
              <label className="flex min-w-0 flex-1 cursor-pointer items-center gap-2.5">
                <input type="checkbox" className="h-4 w-4 accent-[var(--accent)]" checked={on} onChange={() => toggle(p.id)} />
                <span className={clsx('truncate', !on && 'text-muted')}>{p.name}</span>
              </label>
              {mode === 'parts' && (
                <input
                  className="input w-18 text-right tabular-nums"
                  inputMode="decimal"
                  aria-label={t('editor.partOf', { name: p.name })}
                  disabled={!on}
                  value={on ? partOf(p.id) : ''}
                  onChange={(e) => setParts((c) => ({ ...c, [p.id]: e.target.value }))}
                />
              )}
            </li>
          )
        })}
        {people.length === 0 && <li className="px-3 py-2 text-sm muted">{t('editor.addPeopleFirst')}</li>}
      </ul>
      {show(problems.people ?? problems.parts)}

      {error && <div className="mt-3">{error}</div>}
      <div className="mt-3.5 flex flex-wrap items-center gap-2">
        <button type="submit" className="btn-primary" disabled={busy}>
          {submitLabel ?? t('editor.addLine')}
        </button>
        {onCancel && (
          <button type="button" className="btn-ghost" onClick={onCancel}>
            {t('common.cancel')}
          </button>
        )}
        <span className="flex-1" />
        {hint && <span className="text-xs muted">{hint}</span>}
      </div>
    </form>
  )
}
