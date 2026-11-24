import { useEffect, useId, useRef, useState, type ReactNode } from 'react'
import clsx from 'clsx'
import type { Person } from '../../lib/usePeople'
import { PersonSearch } from '../people/PersonSearch'

/**
 * Filter controls for the list header (ListView's `filters`). Each one shows its label and reports a new value;
 * the page turns that into URL state with withFilter(). An active filter is drawn in the accent colour.
 */

const active = 'border-accent/60 font-semibold text-accent'

function Field({ label, htmlFor, className, children }: { label: string; htmlFor?: string; className?: string; children: ReactNode }) {
  return (
    <div className={clsx('min-w-0', className)}>
      <label className="label" htmlFor={htmlFor}>
        {label}
      </label>
      {children}
    </div>
  )
}

export interface Option {
  value: string
  label: string
}

/** A select with an "all" entry for no filter. */
export function FilterSelect({ label, value, allLabel, options, onChange }: { label: string; value?: string; allLabel: string; options: Option[]; onChange: (v: string | undefined) => void }) {
  const id = useId()
  // Keep a value from the URL selectable even if the options don't have it (e.g. a removed contact).
  const opts = value && !options.some((o) => o.value === value) ? [...options, { value, label: value }] : options
  return (
    <Field label={label} htmlFor={id} className="flex-[1_1_9rem] md:max-w-56">
      <select id={id} className={clsx('input', value && active)} value={value ?? ''} onChange={(e) => onChange(e.target.value || undefined)}>
        <option value="">{allLabel}</option>
        {opts.map((o) => (
          <option key={o.value} value={o.value}>
            {o.label}
          </option>
        ))}
      </select>
    </Field>
  )
}

/** Pills for a choice; the first option is "all" (no filter) and has value ''. */
export function FilterChoice({ label, value, options, onChange }: { label: string; value?: string; options: Option[]; onChange: (v: string | undefined) => void }) {
  return (
    <div className="min-w-0 flex-[1_1_100%] md:flex-none" role="group" aria-label={label}>
      <span className="label">{label}</span>
      {/* As tall as an input, so this caption lines up with the text boxes' captions in the same row. */}
      <div className="input-h flex flex-wrap items-center gap-1">
        {options.map((o) => {
          const on = (value ?? '') === o.value
          return (
            <button
              key={o.value || 'all'}
              type="button"
              aria-pressed={on}
              onClick={() => onChange(o.value || undefined)}
              className={clsx(
                'rounded-full border px-2.5 py-0.5 text-[0.8rem] transition-colors',
                on ? 'border-accent/40 bg-accent-faint font-semibold text-accent' : 'border-line text-muted hover:border-accent hover:text-fg',
              )}
            >
              {o.label}
            </button>
          )
        })}
      </div>
    </div>
  )
}

export function FilterFlag({ label, value, onChange }: { label: string; value: boolean; onChange: (v: boolean) => void }) {
  return (
    <label className="input-h inline-flex cursor-pointer items-center gap-2 text-sm md:mt-6">
      <input type="checkbox" className="h-4 w-4 accent-[var(--accent)]" checked={value} onChange={(e) => onChange(e.target.checked)} />
      {label}
    </label>
  )
}

/** Free text; commits after a pause in typing, on Enter and on blur. */
export function FilterText({ label, value, placeholder, onChange }: { label: string; value?: string; placeholder?: string; onChange: (v: string | undefined) => void }) {
  const id = useId()
  const [text, setText] = useState(value ?? '')
  const [shown, setShown] = useState(value)
  // Follow the URL (Back/Forward, Clear filters).
  if (shown !== value) {
    setShown(value)
    setText(value ?? '')
  }
  const timer = useRef<ReturnType<typeof setTimeout>>(undefined)
  const commit = (v: string) => {
    clearTimeout(timer.current)
    if ((v.trim() || undefined) !== value) onChange(v.trim() || undefined)
  }
  useEffect(() => () => clearTimeout(timer.current), [])
  return (
    <Field label={label} htmlFor={id} className="flex-[1_1_100%] md:flex-[1_1_14rem]">
      <input
        id={id}
        type="search"
        className={clsx('input', value && active)}
        value={text}
        placeholder={placeholder}
        onChange={(e) => {
          setText(e.target.value)
          clearTimeout(timer.current)
          const v = e.target.value
          timer.current = setTimeout(() => commit(v), 500)
        }}
        onKeyDown={(e) => e.key === 'Enter' && commit(text)}
        onBlur={() => commit(text)}
      />
    </Field>
  )
}

export function FilterDate({ label, value, onChange }: { label: string; value?: string; onChange: (v: string | undefined) => void }) {
  const id = useId()
  return (
    <Field label={label} htmlFor={id} className="flex-[1_1_8.5rem] md:max-w-40">
      <input id={id} type="date" className={clsx('input', value && active)} value={value ?? ''} onChange={(e) => onChange(e.target.value || undefined)} />
    </Field>
  )
}

/** A 3-letter currency code with suggestions; commits once it has 3 letters, or when cleared. */
export function FilterCurrency({ label, value, placeholder, suggestions, onChange }: { label: string; value?: string; placeholder: string; suggestions: string[]; onChange: (v: string | undefined) => void }) {
  const id = useId()
  const listId = useId()
  const [text, setText] = useState(value ?? '')
  const [shown, setShown] = useState(value)
  if (shown !== value) {
    setShown(value)
    setText(value ?? '')
  }
  return (
    <Field label={label} htmlFor={id} className="flex-[1_1_8rem] md:max-w-40">
      <input
        id={id}
        className={clsx('input uppercase placeholder:normal-case', value && active)}
        value={text}
        maxLength={3}
        placeholder={placeholder}
        list={listId}
        autoComplete="off"
        onChange={(e) => {
          const v = e.target.value.replace(/[^a-zA-Z]/g, '').toUpperCase()
          setText(v)
          if (v.length === 3 && v !== value) onChange(v)
          if (v.length === 0 && value) onChange(undefined)
        }}
      />
      <datalist id={listId}>
        {suggestions.map((c) => (
          <option key={c} value={c} />
        ))}
      </datalist>
    </Field>
  )
}

/** One person, picked by typing (contact lists can be long): the chosen person shows with a ✕ to clear. */
export function FilterPerson({ label, value, people, placeholder, onChange }: { label: string; value?: string; people: Person[]; placeholder: string; onChange: (v: string | undefined) => void }) {
  const id = useId()
  const chosen = value ? people.find((p) => p.id === value) : undefined
  return (
    <Field label={label} htmlFor={id} className="flex-[1_1_11rem] md:max-w-64">
      {value ? (
        <div className={clsx('input flex items-center gap-2', active)}>
          <span className="min-w-0 flex-1 truncate">{chosen?.name ?? '…'}</span>
          <button id={id} type="button" className="rounded px-1 hover:bg-accent-faint" aria-label={`${label}: ✕`} onClick={() => onChange(undefined)}>
            ✕
          </button>
        </div>
      ) : (
        <PersonSearch id={id} people={people} placeholder={placeholder} onPick={(p) => onChange(p.id)} />
      )}
    </Field>
  )
}
