import { useEffect, useId, useRef, useState } from 'react'
import { useLocation, useNavigate } from 'react-router'
import { Trans, useTranslation } from 'react-i18next'
import clsx from 'clsx'
import { ApiError } from '../../api/client'
import { FOLLOWING_MAX, ofSearch, searchFollowed, withOf } from '../../lib/following'
import { useFollowing, type FollowPage } from '../../lib/useFollowing'
import { SidebarEntry, SidebarMore, SidebarSection } from '../Sidebar'

/** Switches whose list you see; filters, sort and page start afresh, as a new history entry. */
function useSwitchTo() {
  const navigate = useNavigate()
  const { pathname } = useLocation()
  return (of?: string) => navigate({ pathname, search: ofSearch(of) })
}

/**
 * The "Whose" dropdown under the page title: 👤 Mine, then everyone you're subscribed to, with a search box on top.
 * Hidden when you have no subscriptions.
 */
export function WhoseSelect() {
  const { t } = useTranslation()
  const { of, viewing, followed } = useFollowing()
  const switchTo = useSwitchTo()
  const [open, setOpen] = useState(false)
  const [query, setQuery] = useState('')
  const root = useRef<HTMLDivElement>(null)
  const menuId = useId()
  const shown = searchFollowed(followed, query)

  useEffect(() => {
    if (!open) return
    const close = (e: MouseEvent) => {
      if (!root.current?.contains(e.target as Node)) setOpen(false)
    }
    document.addEventListener('mousedown', close)
    return () => document.removeEventListener('mousedown', close)
  }, [open])

  if (followed.length === 0) return null

  const pick = (id?: string) => {
    setOpen(false)
    setQuery('')
    if (id !== of) switchTo(id)
  }
  const label = viewing.kind === 'of' ? `🔔 ${viewing.name}` : `👤 ${t('following.mine')}`
  const item = 'flex w-full items-center gap-1.5 px-3 py-1.5 text-left text-sm hover:bg-accent-faint focus-visible:bg-accent-faint focus-visible:outline-none'

  return (
    <div className="-mt-3 mb-4 flex flex-wrap items-center gap-2">
      <span className="label mb-0">{t('following.whose')}</span>
      <div
        ref={root}
        className="relative"
        onKeyDown={(e) => {
          if (e.key === 'Escape' && open) {
            e.stopPropagation()
            setOpen(false)
          }
        }}
      >
        <button
          type="button"
          className={clsx('input flex w-52 items-center gap-2 text-left', viewing.kind === 'of' && 'border-accent/60 font-semibold text-accent')}
          aria-haspopup="listbox"
          aria-expanded={open}
          aria-controls={menuId}
          onClick={() => setOpen((o) => !o)}
        >
          <span className="min-w-0 flex-1 truncate">{label}</span>
          <span className="text-[0.7rem] text-muted" aria-hidden>
            ▼
          </span>
        </button>
        {open && (
          <div id={menuId} className="absolute top-full left-0 z-20 mt-1 w-60 max-w-[calc(100vw-2rem)] overflow-hidden rounded-lg border border-line bg-surface pb-1 shadow-lg">
            <input
              autoFocus
              className="input m-1.5 w-[calc(100%-0.75rem)]"
              placeholder={t('following.search')}
              aria-label={t('following.search')}
              value={query}
              onChange={(e) => setQuery(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === 'Enter' && shown.length > 0) pick(shown[0].id)
              }}
            />
            <div role="listbox" aria-label={t('following.whose')} className="max-h-72 overflow-y-auto">
              <button type="button" role="option" aria-selected={!of} className={clsx(item, !of && 'bg-accent-faint font-semibold text-accent')} onClick={() => pick(undefined)}>
                👤 {t('following.mine')}
              </button>
              <div className="mt-1 border-t border-line px-3 pt-2 pb-1 text-[0.68rem] font-bold tracking-wider text-muted uppercase">{t('following.subscribedTo')}</div>
              {shown.map((c) => (
                <button
                  key={c.id}
                  type="button"
                  role="option"
                  aria-selected={c.id === of}
                  className={clsx(item, c.id === of && 'bg-accent-faint font-semibold text-accent')}
                  onClick={() => pick(c.id)}
                >
                  <span aria-hidden>🔔</span>
                  <span className="truncate">{c.displayName}</span>
                </button>
              ))}
              {shown.length === 0 && <div className="px-3 py-1.5 text-sm muted">{t('following.noMatch')}</div>}
            </div>
          </div>
        )}
      </div>
    </div>
  )
}

/** "🔔 You're seeing Boris's debts because you're subscribed to Boris. Read-only." with Back to mine. */
export function FollowingBanner({ page, name }: { page: FollowPage; name: string }) {
  const { t } = useTranslation()
  const switchTo = useSwitchTo()
  return (
    <div className="alert-info mb-4 flex flex-wrap items-center gap-x-3 gap-y-2" role="status">
      <span aria-hidden>🔔</span>
      <span className="min-w-0 flex-1">
        <Trans i18nKey={`following.banner.${page}`} values={{ name }} components={{ b: <strong /> }} />
      </span>
      <button type="button" className="btn-secondary px-2.5 py-1 text-[0.8rem]" onClick={() => switchTo(undefined)}>
        {t('following.backToMine')}
      </button>
    </div>
  )
}

/** Someone you aren't (or no longer) subscribed to: the normal error box with Back to mine. */
export function NoAccess({ error }: { error: unknown }) {
  const { t } = useTranslation()
  const switchTo = useSwitchTo()
  const message = error instanceof ApiError && error.status === 403 && error.serverMessage ? error.serverMessage : t('following.noAccess')
  return (
    <div className="alert-danger flex flex-wrap items-center gap-x-3 gap-y-2" role="alert">
      <span className="min-w-0 flex-1">
        <strong>{t('common.error')}:</strong> {message}
      </span>
      <button type="button" className="btn-secondary px-2.5 py-1 text-[0.8rem]" onClick={() => switchTo(undefined)}>
        {t('following.backToMine')}
      </button>
    </div>
  )
}

/** Sidebar "Following" on Debts, Bills and Spendings: up to five people, then … All subscriptions. Hidden with nobody. */
export function FollowingSubNav({ page, path }: { page: FollowPage; path: string }) {
  const { t } = useTranslation()
  const { of, followed } = useFollowing()
  if (followed.length === 0) return null
  return (
    <>
      <SidebarSection>{t('following.subnav.title')}</SidebarSection>
      {followed.slice(0, FOLLOWING_MAX).map((c) => (
        <SidebarEntry
          key={c.id}
          to={withOf(path, c.id)}
          icon="🔔"
          title={c.displayName}
          subtitle={c.id === of ? t('following.subnav.showingNow') : t(`following.subnav.see.${page}`, { name: c.displayName })}
          active={c.id === of}
        />
      ))}
      <SidebarMore to="/subscriptions" label={t('following.subnav.all')} />
    </>
  )
}
