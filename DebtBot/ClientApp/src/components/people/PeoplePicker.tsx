import { useId } from 'react'
import { useTranslation } from 'react-i18next'
import type { Person } from '../../lib/usePeople'
import { PersonSearch } from './PersonSearch'

export interface PeoplePickerProps {
  /** Everyone who can be added (yourself and your contacts, from usePeople). */
  people: Person[]
  /** The chosen people, in order. */
  selected: Person[]
  onAdd: (p: Person) => void
  onRemove: (p: Person) => void
  /** People who can't be removed (they're already on a line or a payment), with the reason as a tooltip. */
  locked?: Set<string>
  label?: string
}

/**
 * The people on a bill: chips, plus a search box to add more. The line and payment editors then offer only
 * these people, so a long contact list never turns into a long checkbox list. Reusable for T6's New bill form.
 */
export function PeoplePicker({ people, selected, onAdd, onRemove, locked, label }: PeoplePickerProps) {
  const { t } = useTranslation()
  const id = useId()
  const chosen = new Set(selected.map((p) => p.id))
  return (
    <div>
      <label className="label" htmlFor={id}>
        {label ?? t('people.onBill')}
      </label>
      <div className="flex flex-wrap items-center gap-1.5">
        {selected.map((p) => {
          const fixed = locked?.has(p.id)
          return (
            <span key={p.id} className="inline-flex items-center gap-1 rounded bg-accent-faint py-0.5 pr-1 pl-2 text-[0.8rem] text-accent">
              {p.name}
              {fixed ? (
                <span className="px-1 opacity-50" title={t('people.locked')} aria-hidden>
                  •
                </span>
              ) : (
                <button type="button" className="rounded px-1 hover:bg-accent/20" aria-label={t('people.remove', { name: p.name })} onClick={() => onRemove(p)}>
                  ×
                </button>
              )}
            </span>
          )
        })}
        <PersonSearch id={id} className="min-w-48 flex-1" people={people} exclude={chosen} onPick={onAdd} placeholder={t('people.add')} />
      </div>
    </div>
  )
}
