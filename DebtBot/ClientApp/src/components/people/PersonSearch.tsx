import { useId, useRef, useState, type KeyboardEvent } from 'react'
import { useTranslation } from 'react-i18next'
import clsx from 'clsx'
import { matchesPerson, type Person } from '../../lib/usePeople'

const MAX = 8

export interface PersonSearchProps {
  /** Who can be picked. Big contact lists are fine: only the first matches are shown. */
  people: Person[]
  onPick: (p: Person) => void
  placeholder?: string
  /** People not to suggest (already picked). */
  exclude?: Set<string>
  id?: string
  className?: string
  autoFocus?: boolean
  'aria-label'?: string
}

/**
 * A text box that suggests people as you type (name, their own name or @handle), as an ARIA combobox:
 * ↑/↓ to move, Enter to pick, Esc to close. It keeps no selection of its own; the caller decides what a pick means.
 */
export function PersonSearch({ people, onPick, placeholder, exclude, id, className, autoFocus, ...rest }: PersonSearchProps) {
  const { t } = useTranslation()
  const listId = useId()
  const [text, setText] = useState('')
  const [open, setOpen] = useState(false)
  const [active, setActive] = useState(0)
  const blurTimer = useRef<ReturnType<typeof setTimeout>>(undefined)

  const matches = people.filter((p) => !exclude?.has(p.id) && matchesPerson(p, text))
  const shown = matches.slice(0, MAX)

  const pick = (p: Person) => {
    onPick(p)
    setText('')
    setActive(0)
  }

  const onKey = (e: KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'ArrowDown') {
      e.preventDefault()
      setOpen(true)
      setActive((a) => Math.min(a + 1, shown.length - 1))
    } else if (e.key === 'ArrowUp') {
      e.preventDefault()
      setActive((a) => Math.max(a - 1, 0))
    } else if (e.key === 'Enter') {
      if (open && shown[active]) {
        e.preventDefault()
        pick(shown[active])
      }
    } else if (e.key === 'Escape') {
      setOpen(false)
    }
  }

  return (
    <div className={clsx('relative', className)}>
      <input
        id={id}
        className="input"
        role="combobox"
        aria-expanded={open && shown.length > 0}
        aria-controls={listId}
        aria-autocomplete="list"
        aria-activedescendant={open && shown[active] ? `${listId}-${shown[active].id}` : undefined}
        autoComplete="off"
        autoFocus={autoFocus}
        placeholder={placeholder}
        value={text}
        onChange={(e) => {
          setText(e.target.value)
          setOpen(true)
          setActive(0)
        }}
        onFocus={() => {
          clearTimeout(blurTimer.current)
          setOpen(true)
        }}
        // Let a click on a suggestion land before the list closes.
        onBlur={() => (blurTimer.current = setTimeout(() => setOpen(false), 150))}
        onKeyDown={onKey}
        aria-label={rest['aria-label']}
      />
      {open && (
        <ul id={listId} role="listbox" className="absolute inset-x-0 top-full z-20 mt-1 max-h-72 overflow-y-auto rounded-md border border-line bg-surface py-1 shadow-lg">
          {shown.map((p, i) => (
            <li
              key={p.id}
              id={`${listId}-${p.id}`}
              role="option"
              aria-selected={i === active}
              onMouseDown={(e) => e.preventDefault()}
              onMouseEnter={() => setActive(i)}
              onClick={() => pick(p)}
              className={clsx('flex cursor-pointer items-baseline gap-2 px-3 py-1.5 text-sm', i === active && 'bg-accent-faint text-accent')}
            >
              <span className="truncate font-medium">{p.name}</span>
              {p.ownName && <span className="truncate text-[0.8rem] muted">{p.ownName}</span>}
              {p.handle && <span className="ml-auto shrink-0 text-[0.8rem] muted">{p.handle}</span>}
            </li>
          ))}
          {shown.length === 0 && <li className="px-3 py-1.5 text-sm muted">{t('people.noMatch')}</li>}
          {matches.length > MAX && <li className="px-3 py-1 text-[0.75rem] muted">{t('people.more', { count: matches.length - MAX })}</li>}
        </ul>
      )}
    </div>
  )
}
