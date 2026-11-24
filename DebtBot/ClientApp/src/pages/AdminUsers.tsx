import { useMemo, useState, type ReactNode } from 'react'
import { Link, useLocation, useNavigate, useParams } from 'react-router'
import { Trans, useTranslation } from 'react-i18next'
import clsx from 'clsx'
import { useAdminUsers, useDeleteUser, useMergeSuggestions, useRevokeAllTokens, useSetRole } from '../api/admin'
import { ApiError } from '../api/client'
import type { AdminUser } from '../api/types'
import { useAuth } from '../auth/AuthContext'
import { PasswordDialog } from '../components/admin/PasswordDialog'
import { ExpandButton } from '../components/list/ExpandButton'
import { FilterChoice, FilterText } from '../components/list/filters'
import { ListView, type Column } from '../components/list/ListView'
import { PageTitle } from '../components/ui'
import { duplicateIds } from '../lib/admin'
import { toggleOpen } from '../lib/apiTokens'
import { handle } from '../lib/bills'
import { withFilter } from '../lib/listState'
import { adminUsersQuery, adminUsersSpec, PAGE_SIZE } from '../lib/lists'
import { useListState } from '../lib/useListState'

/** An inline confirmation in the open row's strip (not in the URL, like the other inline confirmations). */
type Confirm = { id: string; kind: 'role' | 'delete' | 'revokeAll' } | { id: string; kind: 'error'; message: string } | { id: string; kind: 'inUse' }

const small = 'px-2.5 py-1 text-[0.8rem] whitespace-nowrap'

/**
 * 🛠 Admin › Users (mockups/admin-users, series-2/mockups/api-tokens): everyone DebtBot knows, with search and a role
 * filter. Rows show values only; ▾ at the end opens the row's actions (`?open=<id>`): Login & password
 * (`/admin/users/<id>/password`, a dialog over the list), the role, revoking all API tokens, deleting stray placeholders.
 */
export default function AdminUsers() {
  const { t } = useTranslation()
  const { user: me } = useAuth()
  const { id: passwordFor } = useParams()
  const { search } = useLocation()
  const navigate = useNavigate()
  const [state, setState] = useListState(adminUsersSpec)
  // The API orders by name only.
  const shownState = useMemo(() => ({ ...state, order: 'asc' as const }), [state])
  const query = useMemo(() => adminUsersQuery(state), [state])
  const users = useAdminUsers(query)
  const duplicates = duplicateIds(useMergeSuggestions().data)
  const setRole = useSetRole()
  const remove = useDeleteUser()
  const revokeAll = useRevokeAllTokens()
  const [confirm, setConfirm] = useState<Confirm>()
  const openId = new URLSearchParams(search).get('open')
  const toggle = (u: AdminUser) => {
    setConfirm(undefined)
    navigate({ search: toggleOpen(search, u.id) })
  }

  const isMe = (u: AdminUser) => u.id === me?.id
  const isPlaceholder = (u: AdminUser) => !u.telegramBotEnabled
  const passwordHref = (u: AdminUser) => `/admin/users/${u.id}/password${search}`
  const failed = (u: AdminUser, e: unknown) => setConfirm({ id: u.id, kind: 'error', message: e instanceof ApiError && e.serverMessage ? e.serverMessage : t('admin.users.failed') })

  const name = (u: AdminUser) => (
    <>
      <div>
        <span className="font-semibold">{u.displayName}</span>
        {isMe(u) && <span className="text-[0.8rem] muted"> {t('admin.users.you')}</span>}
      </div>
      {duplicates.has(u.id) && (
        <Link to="/admin/merge" className="text-[0.78rem] whitespace-nowrap text-accent hover:underline">
          🔀 {t('admin.users.duplicate')}
        </Link>
      )}
    </>
  )

  const telegramId = (u: AdminUser) => (u.telegramId ? String(u.telegramId) : t('admin.users.noTelegramId'))

  const passwordButton = (u: AdminUser) => (
    <Link to={passwordHref(u)} className={clsx('btn-secondary', small)}>
      {u.login || u.hasPassword ? `✏️ ${t('admin.users.changeLogin')}` : `＋ ${t('admin.users.setLogin')}`}
    </Link>
  )

  const passwordSet = <span className="pill-muted px-1.5 py-0 whitespace-nowrap">🔑 {t('admin.users.passwordSet')}</span>

  const rolePill = (u: AdminUser) => (u.role === 'Admin' ? <span className="pill-accent">{t('admin.users.admin')}</span> : <span className="px-2 text-xs muted">{t('admin.users.regular')}</span>)

  const roleButton = (u: AdminUser) => (
    <button
      type="button"
      className={clsx('btn-secondary', small)}
      disabled={isMe(u)}
      title={isMe(u) ? t('admin.users.ownRole') : undefined}
      onClick={() => setConfirm({ id: u.id, kind: 'role' })}
    >
      {u.role === 'Admin' ? t('admin.users.removeAdmin') : t('admin.users.makeAdmin')}
    </button>
  )

  const botPill = (u: AdminUser) => (u.telegramBotEnabled ? <span className="pill-success">{t('admin.users.started')}</span> : <span className="pill-muted">{t('admin.users.placeholder')}</span>)

  const deleteButton = (u: AdminUser) =>
    // Admins can't be deleted (the API refuses): make them Regular first.
    isPlaceholder(u) &&
    u.role !== 'Admin' &&
    !isMe(u) && (
      <button type="button" className={clsx('btn-secondary text-danger', small)} aria-label={t('admin.users.deleteNamed', { name: u.displayName })} onClick={() => setConfirm({ id: u.id, kind: 'delete' })}>
        🗑 {t('admin.users.delete')}
      </button>
    )

  const revokeAllButton = (u: AdminUser) =>
    u.apiTokenCount > 0 && (
      <button type="button" className={clsx('btn-secondary text-danger', small)} onClick={() => setConfirm({ id: u.id, kind: 'revokeAll' })}>
        🚫 {t('admin.users.revokeAll', { count: u.apiTokenCount })}
      </button>
    )

  const box = (tone: 'ask' | 'danger', text: ReactNode, actions: ReactNode) => (
    <div className="flex flex-wrap items-center gap-x-3 gap-y-2 text-sm" role={tone === 'danger' ? 'alert' : undefined}>
      <span className={clsx('min-w-48 flex-1', tone === 'danger' && 'text-danger')}>{text}</span>
      {actions}
    </div>
  )

  const cancel = (label: string) => (
    <button type="button" className={clsx('btn-ghost', small)} onClick={() => setConfirm(undefined)}>
      {label}
    </button>
  )

  /** The open row's strip: its actions, or the confirmation one of them asked for. */
  const strip = (u: AdminUser): { content: ReactNode; danger?: boolean } | undefined => {
    if (openId !== u.id) return undefined
    const c = confirm?.id === u.id ? confirm : undefined
    if (!c)
      return {
        content: (
          <div className="flex flex-wrap items-center gap-2">
            {passwordButton(u)}
            {roleButton(u)}
            {revokeAllButton(u)}
            {deleteButton(u)}
          </div>
        ),
      }
    return { content: confirmation(u, c), danger: c.kind !== 'role' }
  }

  const confirmation = (u: AdminUser, confirm: Confirm) => {
    const b = { b: <strong /> }
    switch (confirm.kind) {
      case 'role': {
        const make = u.role !== 'Admin'
        return box(
          'ask',
          <Trans i18nKey={make ? 'admin.users.makeAdminAsk' : 'admin.users.removeAdminAsk'} values={{ name: u.displayName }} components={b} />,
          <>
            <button
              type="button"
              className={clsx('btn-primary', small)}
              disabled={setRole.isPending}
              onClick={() => setRole.mutate({ id: u.id, role: make ? 'Admin' : 'Regular' }, { onSuccess: () => setConfirm(undefined), onError: (e) => failed(u, e) })}
            >
              {make ? t('admin.users.makeAdmin') : t('admin.users.removeAdmin')}
            </button>
            {cancel(t('common.cancel'))}
          </>,
        )
      }
      case 'delete':
        return box(
          'danger',
          <Trans i18nKey="admin.users.deleteAsk" values={{ name: u.displayName }} components={b} />,
          <>
            <button
              type="button"
              className={clsx('btn-danger', small)}
              disabled={remove.isPending}
              onClick={() =>
                remove.mutate(u.id, {
                  onSuccess: () => setConfirm(undefined),
                  onError: (e) => (e instanceof ApiError && e.status === 409 ? setConfirm({ id: u.id, kind: 'inUse' }) : failed(u, e)),
                })
              }
            >
              {t('admin.users.delete')}
            </button>
            {cancel(t('admin.users.keep'))}
          </>,
        )
      case 'inUse':
        return box(
          'danger',
          <Trans i18nKey="admin.users.inUse" values={{ name: u.displayName }} components={b} />,
          <>
            <Link to={`/admin/merge?merge=${u.id}`} className={clsx('btn-secondary', small)}>
              🔀 {t('admin.users.mergeEllipsis')}
            </Link>
            {cancel(t('common.close'))}
          </>,
        )
      case 'revokeAll':
        return box(
          'danger',
          <Trans i18nKey="admin.users.revokeAllAsk" count={u.apiTokenCount} values={{ name: u.displayName, count: u.apiTokenCount }} components={b} />,
          <>
            <button
              type="button"
              className={clsx('btn-danger', small)}
              disabled={revokeAll.isPending}
              onClick={() => revokeAll.mutate(u.id, { onSuccess: () => setConfirm(undefined), onError: (e) => failed(u, e) })}
            >
              🚫 {t('admin.users.revokeAllButton')}
            </button>
            {cancel(t('common.cancel'))}
          </>,
        )
      case 'error':
        return box('danger', confirm.message, cancel(t('common.close')))
    }
  }

  const columns: Column<AdminUser>[] = [
    { key: 'name', header: t('admin.users.name'), sort: 'name', cell: name },
    {
      key: 'telegram',
      header: t('admin.users.telegram'),
      cell: (u) => (
        <>
          {/* Long usernames can't wrap: cut them (full name on hover) so the table still fits at 1280 px. */}
          <div className="max-w-28 truncate" title={u.telegramUserName ?? undefined}>
            {u.telegramUserName ? handle(u.telegramUserName) : '—'}
          </div>
          <div className="text-[0.78rem] whitespace-nowrap tabular-nums muted">{telegramId(u)}</div>
        </>
      ),
    },
    {
      key: 'login',
      header: t('admin.users.loginPassword'),
      cell: (u) => (
        <div className="min-w-0">
          <div className="truncate">{u.login ?? '—'}</div>
          {u.hasPassword && <div>{passwordSet}</div>}
        </div>
      ),
    },
    { key: 'role', header: t('admin.users.role'), cell: rolePill },
    { key: 'bot', header: t('admin.users.bot'), cell: botPill },
    { key: 'bills', header: t('admin.users.bills'), align: 'right', cell: (u) => <span className="tabular-nums">{u.billCount}</span> },
    { key: 'contacts', header: t('admin.users.contacts'), align: 'right', cell: (u) => <span className="tabular-nums">{u.contactCount}</span> },
    { key: 'tokens', header: t('admin.users.apiTokens'), align: 'right', cell: (u) => <span className={clsx('tabular-nums', !u.apiTokenCount && 'muted')}>{u.apiTokenCount}</span> },
    { key: 'open', header: <span className="sr-only">{t('list.actions')}</span>, align: 'right', width: '2.5rem', cell: (u) => <ExpandButton open={openId === u.id} label={u.displayName} onClick={() => toggle(u)} /> },
  ]

  const card = (u: AdminUser) => (
    <div className="space-y-1.5">
      <div className="flex items-baseline gap-2">
        <span className="min-w-0 flex-1">{name(u)}</span>
        {rolePill(u)}
        <span className="self-center">
          <ExpandButton open={openId === u.id} label={u.displayName} onClick={() => toggle(u)} />
        </span>
      </div>
      <div className="flex items-center gap-2 text-[0.8rem]">
        <span className="min-w-0 flex-1 truncate muted">{[u.telegramUserName ? handle(u.telegramUserName) : '—', u.telegramId && String(u.telegramId)].filter(Boolean).join(' · ')}</span>
        {botPill(u)}
      </div>
      <div className="flex items-center gap-2 text-[0.8rem]">
        <span className="min-w-0 flex-1 muted">
          {u.login || u.hasPassword ? (
            <>
              {u.login && (
                <>
                  {t('admin.users.loginLabel')} <strong className="text-fg">{u.login}</strong>
                </>
              )}
              {u.login && u.hasPassword && ' · '}
              {u.hasPassword && `🔑 ${t('admin.users.passwordSet')}`}
            </>
          ) : (
            t('admin.users.noLogin')
          )}
        </span>
      </div>
      <div className="text-[0.8rem] muted">
        {t('admin.bills.count', { count: u.billCount })} · {t('admin.users.contactCount', { count: u.contactCount })} · {t('admin.users.tokenCount', { count: u.apiTokenCount })}
      </div>
    </div>
  )

  return (
    <>
      <PageTitle subtitle={t('pages.adminUsers.subtitle')}>{t('pages.adminUsers.title')}</PageTitle>
      <ListView<AdminUser>
        spec={adminUsersSpec}
        state={shownState}
        onChange={(next) => {
          setConfirm(undefined)
          setState(next)
        }}
        rowExpand={strip}
        filters={
          <>
            <FilterText label={t('admin.users.search')} placeholder={t('admin.users.searchHint')} value={state.filters.search} onChange={(v) => setState(withFilter(state, 'search', v))} />
            <FilterChoice
              label={t('admin.users.role')}
              value={state.filters.role}
              options={[
                { value: '', label: t('list.all') },
                { value: 'admin', label: t('admin.users.admin') },
                { value: 'regular', label: t('admin.users.regular') },
              ]}
              onChange={(v) => setState(withFilter(state, 'role', v))}
            />
          </>
        }
        sorts={[{ value: 'name', label: t('admin.users.name'), asc: t('list.az'), desc: t('list.za') }]}
        orderFixed
        dense
        columns={columns}
        card={card}
        rowKey={(u) => u.id}
        items={users.data?.items}
        total={users.data?.totalCount ?? 0}
        pageSize={PAGE_SIZE}
        loading={users.isLoading}
        fetching={users.isFetching && users.isPlaceholderData}
        error={users.error}
        empty={<p className="font-semibold">{t('admin.users.empty')}</p>}
        filteredEmpty={
          <>
            <div className="mb-2 text-[2rem]" aria-hidden>
              🔍
            </div>
            <p className="font-semibold">{t('admin.users.noMatch')}</p>
            <p className="mt-1 text-sm muted">{t('admin.users.noMatchHint')}</p>
          </>
        }
        tableMinWidth={960}
      />
      {passwordFor && <PasswordDialog id={passwordFor} pageUsers={users.data?.items} onClose={() => navigate(`/admin/users${search}`)} />}
    </>
  )
}
