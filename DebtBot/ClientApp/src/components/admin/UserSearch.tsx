import { useEffect, useId, useRef, useState, type KeyboardEvent, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import clsx from 'clsx'
import { useAdminUser, useAdminUsers } from '../../api/admin'
import type { AdminUser } from '../../api/types'
import { handle } from '../../lib/bills'

const MAX = 8

/** "@name · placeholder · 3 bills": what tells two users apart in a pick list. */
export function UserHint({ user }: { user: AdminUser }) {
  const { t } = useTranslation()
  return (
    <>
      {[user.telegramUserName ? handle(user.telegramUserName) : t('admin.users.noTelegramId'), !user.telegramBotEnabled && t('admin.users.placeholder'), t('admin.bills.count', { count: user.billCount })]
        .filter(Boolean)
        .join(' · ')}
    </>
  )
}

/**
 * A text box that searches **all users** (`GET Users?search=`, admin only) as you type: display name, Telegram
 * username or login. An ARIA combobox like PersonSearch: ↑/↓, Enter to pick, Esc to close.
 */
export function UserSearch({ onPick, placeholder, exclude, id, autoFocus, ...rest }: { onPick: (u: AdminUser) => void; placeholder?: string; exclude?: string[]; id?: string; autoFocus?: boolean; 'aria-label'?: string }) {
  const { t } = useTranslation()
  const listId = useId()
  const [text, setText] = useState('')
  const [search, setSearch] = useState('')
  const [open, setOpen] = useState(false)
  const [active, setActive] = useState(0)
  const blurTimer = useRef<ReturnType<typeof setTimeout>>(undefined)

  // Ask the server after a short pause in typing.
  useEffect(() => {
    const timer = setTimeout(() => setSearch(text.trim().replace(/^@/, '')), 250)
    return () => clearTimeout(timer)
  }, [text])
  useEffect(() => () => clearTimeout(blurTimer.current), [])

  const users = useAdminUsers({ search, countPerPage: MAX + (exclude?.length ?? 0) }, { enabled: open && !!search })
  const shown = search ? (users.data?.items ?? []).filter((u) => !exclude?.includes(u.id)).slice(0, MAX) : []

  const pick = (u: AdminUser) => {
    onPick(u)
    setText('')
    setSearch('')
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

  const waiting = !!text.trim() && (text.trim().replace(/^@/, '') !== search || users.isFetching)

  return (
    <div className="relative">
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
      {open && !!text.trim() && (
        <ul id={listId} role="listbox" className="absolute inset-x-0 top-full z-20 mt-1 max-h-72 overflow-y-auto rounded-md border border-line bg-surface py-1 shadow-lg">
          {shown.map((u, i) => (
            <li
              key={u.id}
              id={`${listId}-${u.id}`}
              role="option"
              aria-selected={i === active}
              onMouseDown={(e) => e.preventDefault()}
              onMouseEnter={() => setActive(i)}
              onClick={() => pick(u)}
              className={clsx('flex cursor-pointer items-baseline gap-2 px-3 py-1.5 text-sm', i === active && 'bg-accent-faint')}
            >
              <span className={clsx('truncate font-semibold', i === active && 'text-accent')}>{u.displayName}</span>
              <span className="min-w-0 truncate text-[0.8rem] muted">
                <UserHint user={u} />
              </span>
            </li>
          ))}
          {shown.length === 0 && <li className="px-3 py-1.5 text-sm muted">{waiting ? t('common.loading') : t('people.noMatch')}</li>}
        </ul>
      )}
    </div>
  )
}

/** A chosen user, named from `GET Users/{id}`, with ✕ to clear. */
export function ChosenUser({ id, onClear, label, hint, className }: { id: string; onClear: () => void; label: string; hint?: ReactNode; className?: string }) {
  const user = useAdminUser(id)
  return (
    <div className={clsx('input flex items-center gap-2', className)}>
      <span className="min-w-0 flex-1 truncate">
        <span className="font-semibold">{user.data?.displayName ?? (user.isError ? id.slice(0, 8) : '…')}</span>
        {hint ?? (user.data?.telegramUserName && <span className="ml-2 text-[0.8rem] muted">{handle(user.data.telegramUserName)}</span>)}
      </span>
      <button type="button" className="rounded px-1 hover:bg-accent-faint" aria-label={`${label}: ✕`} onClick={onClear}>
        ✕
      </button>
    </div>
  )
}

/** A list filter over all users (Admin › All bills: Created by, Person). */
export function FilterUser({ label, value, placeholder, onChange }: { label: string; value?: string; placeholder: string; onChange: (v: string | undefined) => void }) {
  const id = useId()
  return (
    <div className="min-w-0 flex-[1_1_11rem] md:max-w-64">
      <label className="label" htmlFor={id}>
        {label}
      </label>
      {value ? (
        <ChosenUser id={value} label={label} onClear={() => onChange(undefined)} className="border-accent/60 text-accent" />
      ) : (
        <UserSearch id={id} placeholder={placeholder} onPick={(u) => onChange(u.id)} />
      )}
    </div>
  )
}
