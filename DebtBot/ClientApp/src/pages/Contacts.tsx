import { useEffect, useRef, useState, type FormEvent, type ReactNode } from 'react'
import { Link, useSearchParams } from 'react-router'
import { Trans, useTranslation } from 'react-i18next'
import clsx from 'clsx'
import { useContacts } from '../api/bills'
import { ApiError } from '../api/client'
import { useAddContact, useRemoveContact, useRenameContact, useSubscriptionAction } from '../api/people'
import type { Contact } from '../api/types'
import { ErrorBox, Loading, PageTitle } from '../components/ui'
import { handle } from '../lib/bills'
import { isPlaceholder, searchContacts, sortContacts } from '../lib/contacts'

const USERNAME = /^@?[A-Za-z0-9_]{5,32}$/
const MAX_NAME = 100

/** What the add card says after an add: a placeholder, a new contact, or one you already had. */
type Added = { id: string; kind: 'placeholder' | 'added' | 'existing'; name: string }

const errorText = (e: unknown, fallback: string) => (e instanceof ApiError && e.serverMessage ? e.serverMessage : fallback)

/** 👥 Contacts: every contact at once (not paged), search in the URL (`?q=`), inline rename and remove (mockups/contacts). */
export default function Contacts() {
  const { t } = useTranslation()
  const contacts = useContacts()
  const [params, setParams] = useSearchParams()
  const q = params.get('q') ?? ''
  const [added, setAdded] = useState<Added>()
  const [renaming, setRenaming] = useState<string>()
  const [removing, setRemoving] = useState<string>()
  const [actionError, setActionError] = useState<string>()
  const remove = useRemoveContact()
  const subscription = useSubscriptionAction()

  // Typing replaces the history entry: no entry per keystroke.
  const setQuery = (v: string) => {
    const next = new URLSearchParams(params)
    if (v.trim()) next.set('q', v)
    else next.delete('q')
    setParams(next, { replace: true })
  }

  if (contacts.isLoading) return <Loading />
  if (contacts.error) return <ErrorBox error={contacts.error} />

  const all = contacts.data ?? []
  const shown = searchContacts(all, q)
  const requests = sortContacts(all.filter((c) => c.theirSubscription === 'Pending'))

  const ask = (c: Contact) => {
    setActionError(undefined)
    subscription.mutate({ action: 'request', contactId: c.id }, { onError: (e) => setActionError(errorText(e, t('contacts.actionFailed'))) })
  }

  const doRemove = (c: Contact) => {
    setActionError(undefined)
    remove.mutate(c.id, {
      onSuccess: () => {
        setRemoving(undefined)
        if (added?.id === c.id) setAdded(undefined)
      },
      onError: (e) => setActionError(errorText(e, t('contacts.actionFailed'))),
    })
  }

  const notifications = (c: Contact) => {
    if (isPlaceholder(c)) return <span className="muted">—</span>
    if (c.mySubscription === 'Confirmed') return <span className="pill-success whitespace-nowrap">🔔 {t('contacts.youGetTheirs')}</span>
    if (c.mySubscription === 'Pending') return <span className="pill-muted whitespace-nowrap">⏳ {t('contacts.asked')}</span>
    return (
      <button type="button" className="btn-secondary px-2.5 py-1 text-[0.8rem] whitespace-nowrap" disabled={subscription.isPending} onClick={() => ask(c)}>
        🔔 {t('contacts.ask')}
      </button>
    )
  }

  const ownName = (c: Contact) => {
    const own = c.userDisplayName?.trim()
    if (!own || own === c.displayName) return undefined
    if (c.telegramUserName && handle(c.telegramUserName).toLowerCase() === handle(own).toLowerCase()) return undefined
    return own
  }

  const neverStarted = <span className="pill-muted whitespace-nowrap">{t('contacts.neverStarted')}</span>

  const removeConfirm = (c: Contact) => (
    <div className="flex flex-wrap items-center gap-2 text-sm text-danger">
      <span className="min-w-0 flex-1">
        <Trans i18nKey="contacts.removeConfirm" values={{ name: c.displayName }} components={{ b: <strong /> }} />
      </span>
      <span className="flex shrink-0 gap-2">
        <button type="button" className="btn-danger px-2.5 py-1 text-[0.8rem]" disabled={remove.isPending} onClick={() => doRemove(c)}>
          {t('contacts.remove')}
        </button>
        <button type="button" className="btn-ghost px-2.5 py-1 text-[0.8rem]" onClick={() => setRemoving(undefined)}>
          {t('contacts.keep')}
        </button>
      </span>
    </div>
  )

  const actions = (c: Contact, iconsOnly: boolean) => (
    <span className="flex shrink-0 justify-end gap-1">
      <button
        type="button"
        className="btn-ghost px-2 py-1 text-[0.8rem]"
        title={t('contacts.rename')}
        aria-label={iconsOnly ? t('contacts.renameName', { name: c.displayName }) : undefined}
        onClick={() => {
          setRemoving(undefined)
          setRenaming(c.id)
        }}
      >
        ✏️{!iconsOnly && ` ${t('contacts.rename')}`}
      </button>
      <button
        type="button"
        className="btn-ghost px-2 py-1 text-[0.8rem]"
        title={t('contacts.remove')}
        aria-label={iconsOnly ? t('contacts.removeName', { name: c.displayName }) : undefined}
        onClick={() => {
          setRenaming(undefined)
          setRemoving(c.id)
        }}
      >
        🗑{!iconsOnly && ` ${t('contacts.remove')}`}
      </button>
    </span>
  )

  return (
    <>
      <PageTitle subtitle={t('pages.contacts.subtitle')}>{t('pages.contacts.title')}</PageTitle>

      {requests.length > 0 && !q.trim() && (
        <div className="alert-info mb-4 flex items-center gap-2.5">
          <span aria-hidden>🔔</span>
          <span className="min-w-0 flex-1">
            {requests.length === 1 ? (
              <Trans i18nKey="contacts.requestOne" values={{ name: requests[0].displayName }} components={{ b: <strong /> }} />
            ) : (
              <Trans i18nKey="contacts.requestMany" values={{ name: requests[0].displayName, count: requests.length - 1 }} components={{ b: <strong /> }} />
            )}
          </span>
          <Link to="/subscriptions" className="btn-primary shrink-0 px-2.5 py-1 text-[0.8rem]">
            {t('contacts.review')}
          </Link>
        </div>
      )}

      <AddCard
        added={added}
        onAdded={(a) => {
          setAdded(a)
          setRenaming(undefined)
          setRemoving(undefined)
          if (q) setQuery('')
        }}
      />

      {actionError && (
        <div className="alert-danger mt-4" role="alert">
          {actionError}
        </div>
      )}

      {all.length === 0 ? (
        <div className="card mt-4 px-5 py-10 text-center">
          <div className="mb-2 text-[2rem]" aria-hidden>
            👥
          </div>
          <p className="font-semibold">{t('contacts.empty')}</p>
          <p className="mx-auto mt-1 max-w-md text-sm muted">{t('contacts.emptyHint')}</p>
        </div>
      ) : (
        <div className="card mt-4 overflow-hidden">
          <div className="flex items-center gap-3 border-b border-line px-4 py-3">
            <SearchBox value={q} onChange={setQuery} />
            <span className="ml-auto shrink-0 text-sm muted max-md:hidden">
              {q.trim() ? t('contacts.countOf', { shown: shown.length, count: all.length }) : t('contacts.count', { count: all.length })}
            </span>
          </div>

          {shown.length === 0 ? (
            <div className="px-5 py-10 text-center">
              <div className="mb-2 text-[2rem]" aria-hidden>
                🔍
              </div>
              <p className="font-semibold">{t('contacts.noMatch')}</p>
              <p className="mt-1 text-sm muted">{t('contacts.noMatchHint')}</p>
            </div>
          ) : (
            <>
              {/* Wide: a table. */}
              <table className="w-full text-sm max-lg:hidden">
                <thead>
                  <tr className="border-b border-line text-left text-[0.7rem] font-bold tracking-wider text-muted uppercase">
                    <th className="px-4 py-2.5 font-bold">{t('contacts.name')} ↑</th>
                    <th className="px-4 py-2.5 font-bold">{t('contacts.telegram')}</th>
                    <th className="px-4 py-2.5 font-bold">{t('contacts.notifications')}</th>
                    <th className="px-4 py-2.5">
                      <span className="sr-only">{t('contacts.actions')}</span>
                    </th>
                  </tr>
                </thead>
                <tbody>
                  {shown.map((c) =>
                    removing === c.id ? (
                      <tr key={c.id} className="border-b border-line bg-danger/10 last:border-b-0">
                        <td colSpan={4} className="px-4 py-3">
                          {removeConfirm(c)}
                        </td>
                      </tr>
                    ) : (
                      <tr key={c.id} className={clsx('border-b border-line last:border-b-0', added?.id === c.id && 'bg-accent-faint')}>
                        <td className="px-4 py-3">
                          {renaming === c.id ? (
                            <RenameForm contact={c} onDone={() => setRenaming(undefined)} />
                          ) : (
                            <>
                              <div className="font-semibold">{c.displayName}</div>
                              {ownName(c) && <div className="text-[0.8rem] muted">{ownName(c)}</div>}
                            </>
                          )}
                        </td>
                        <td className="px-4 py-3">
                          <span className="flex flex-wrap items-center gap-x-1.5 gap-y-0.5">
                            {c.telegramUserName ? handle(c.telegramUserName) : <span className="muted">—</span>}
                            {isPlaceholder(c) && neverStarted}
                          </span>
                        </td>
                        <td className="px-4 py-3">{notifications(c)}</td>
                        <td className="px-4 py-3 text-right">{renaming !== c.id && actions(c, false)}</td>
                      </tr>
                    ),
                  )}
                </tbody>
              </table>

              {/* Narrow: cards. */}
              <ul className="lg:hidden">
                {shown.map((c) => (
                  <li key={c.id} className={clsx('border-b border-line px-4 py-3 last:border-b-0', added?.id === c.id && 'bg-accent-faint', removing === c.id && 'bg-danger/10')}>
                    {removing === c.id ? (
                      removeConfirm(c)
                    ) : renaming === c.id ? (
                      <RenameForm contact={c} onDone={() => setRenaming(undefined)} />
                    ) : (
                      <>
                        <div className="flex items-center gap-2">
                          <span className="min-w-0 flex-1 font-semibold break-words">{c.displayName}</span>
                          {actions(c, true)}
                        </div>
                        <div className="mt-0.5 text-[0.8rem] break-words muted">{[ownName(c), c.telegramUserName && handle(c.telegramUserName)].filter(Boolean).join(' · ')}</div>
                        <div className="mt-1">{isPlaceholder(c) ? neverStarted : notifications(c)}</div>
                      </>
                    )}
                  </li>
                ))}
              </ul>
            </>
          )}
        </div>
      )}
    </>
  )
}

/** The search box; it follows the URL (Back/Forward) and writes every keystroke back, replacing the entry. */
function SearchBox({ value, onChange }: { value: string; onChange: (v: string) => void }) {
  const { t } = useTranslation()
  return (
    <label className="relative block w-full max-w-sm min-w-0">
      <span className="pointer-events-none absolute top-1/2 left-2.5 -translate-y-1/2 text-[0.85rem]" aria-hidden>
        🔍
      </span>
      <span className="sr-only">{t('contacts.search')}</span>
      <input type="search" className="input pl-8" placeholder={t('contacts.search')} value={value} onChange={(e) => onChange(e.target.value)} />
    </label>
  )
}

/** "Add by Telegram username": the `@` is optional; a placeholder is explained, an existing contact is pointed out. */
function AddCard({ added, onAdded }: { added?: Added; onAdded: (a: Added) => void }) {
  const { t } = useTranslation()
  const contacts = useContacts()
  const add = useAddContact()
  const [text, setText] = useState('')
  const [error, setError] = useState<string>()

  const submit = (e: FormEvent) => {
    e.preventDefault()
    const name = text.trim()
    setError(undefined)
    if (!USERNAME.test(name)) {
      setError(t('contacts.badUsername'))
      return
    }
    const known = new Set((contacts.data ?? []).map((c) => c.id))
    add.mutate(name, {
      onSuccess: (c) => {
        setText('')
        onAdded({
          id: c.id,
          kind: known.has(c.id) ? 'existing' : isPlaceholder(c) ? 'placeholder' : 'added',
          name: isPlaceholder(c) && c.telegramUserName ? handle(c.telegramUserName) : c.displayName,
        })
      },
      onError: (err) => setError(errorText(err, t('contacts.addFailed'))),
    })
  }

  let note: ReactNode = <p className="mt-2 text-[0.8rem] muted">{t('contacts.addHint')}</p>
  if (added) {
    const key = added.kind === 'placeholder' ? 'contacts.addedPlaceholder' : added.kind === 'existing' ? 'contacts.alreadyThere' : 'contacts.added'
    note = (
      <div className="alert-info mt-3 flex items-start gap-2.5" role="status">
        <span aria-hidden>✅</span>
        <span className="min-w-0 flex-1">
          <Trans i18nKey={key} values={{ name: added.name }} components={{ b: <strong /> }} />
        </span>
      </div>
    )
  }

  return (
    <form className="card p-4" onSubmit={submit} noValidate>
      <label htmlFor="add-username" className="label">
        {t('contacts.addTitle')}
      </label>
      <div className="flex max-w-md gap-2">
        <input
          id="add-username"
          className={clsx('input min-w-0 flex-1', error && 'border-danger')}
          placeholder="@username"
          autoComplete="off"
          autoCapitalize="none"
          spellCheck={false}
          value={text}
          aria-invalid={!!error}
          onChange={(e) => setText(e.target.value)}
        />
        <button type="submit" className="btn-primary shrink-0" disabled={add.isPending || !text.trim()}>
          ＋ {t('contacts.add')}
        </button>
      </div>
      {error ? (
        <div className="alert-danger mt-3" role="alert">
          {error}
        </div>
      ) : (
        note
      )}
    </form>
  )
}

/** Inline rename: Enter saves, Esc cancels; 1–100 characters. */
function RenameForm({ contact, onDone }: { contact: Contact; onDone: () => void }) {
  const { t } = useTranslation()
  const rename = useRenameContact()
  const [name, setName] = useState(contact.displayName)
  const [error, setError] = useState<string>()
  const input = useRef<HTMLInputElement>(null)
  useEffect(() => input.current?.select(), [])

  const save = (e?: FormEvent) => {
    e?.preventDefault()
    const v = name.trim()
    if (!v || v.length > MAX_NAME) {
      setError(t('contacts.nameRule'))
      return
    }
    if (v === contact.displayName) return onDone()
    rename.mutate({ id: contact.id, displayName: v }, { onSuccess: onDone, onError: (err) => setError(errorText(err, t('contacts.actionFailed'))) })
  }

  return (
    <form onSubmit={save} className="min-w-0">
      <div className="flex flex-wrap items-center gap-2">
        <input
          ref={input}
          className={clsx('input max-w-48 min-w-0 flex-1', error && 'border-danger')}
          aria-label={t('contacts.renameName', { name: contact.displayName })}
          value={name}
          maxLength={MAX_NAME}
          onChange={(e) => setName(e.target.value)}
          onKeyDown={(e) => e.key === 'Escape' && onDone()}
        />
        <button type="submit" className="btn-primary px-2.5 py-1 text-[0.8rem]" disabled={rename.isPending}>
          {t('contacts.save')}
        </button>
        <button type="button" className="btn-ghost px-2.5 py-1 text-[0.8rem]" onClick={onDone}>
          {t('contacts.cancel')}
        </button>
      </div>
      {error && <div className="mt-1 text-[0.8rem] text-danger">{error}</div>}
    </form>
  )
}
