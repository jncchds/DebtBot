import { useState, type ReactNode } from 'react'
import { useNavigate, useSearchParams } from 'react-router'
import { Trans, useTranslation } from 'react-i18next'
import clsx from 'clsx'
import { useAdminUserRow, useMergeSuggestions, useMergeUsers } from '../api/admin'
import { ApiError } from '../api/client'
import type { AdminUser, MergeSuggestion } from '../api/types'
import { ChosenUser, UserHint, UserSearch } from '../components/admin/UserSearch'
import { ErrorBox, Loading, PageTitle } from '../components/ui'
import { afterMerge, canMergeManual, keptSignIn, mergeSearch, parseMergeUrl, shownPair, withManual, withManualSwapped, withSwap, type MergeUrl } from '../lib/admin'
import { handle } from '../lib/bills'
import { useFormat } from '../lib/useFormat'

const small = 'px-3 py-1 text-[0.85rem]'

/**
 * 🛠 Admin › Merge users (mockups/admin-merge): the suggested duplicate pairs with one-click merge and Swap, plus
 * a manual picker. `/admin/merge?swap=<mergeId>,…&keep=<id>&merge=<id>`; open confirmations aren't in the URL.
 */
export default function AdminMerge() {
  const { t } = useTranslation()
  const [params] = useSearchParams()
  const navigate = useNavigate()
  const url = parseMergeUrl(params)
  const setUrl = (next: MergeUrl, replace = false) => navigate({ search: mergeSearch(next) }, { replace })
  const suggestions = useMergeSuggestions()
  const merge = useMergeUsers()
  // Which confirmation is open: a suggestion (by its users) or the manual picker.
  const [open, setOpen] = useState<string>()
  const [error, setError] = useState<{ at: string; message: string }>()
  const [done, setDone] = useState<{ old: string; kept: string }>()

  const run = (at: string, keep: AdminUser, old: AdminUser) => {
    setError(undefined)
    merge.mutate(
      { oldUserId: old.id, newUserId: keep.id },
      {
        onSuccess: (kept) => {
          setOpen(undefined)
          setDone({ old: old.displayName, kept: kept?.displayName ?? keep.displayName })
          setUrl(afterMerge(url, old.id, keep.id), true)
          document.getElementById('main')?.scrollTo({ top: 0, behavior: 'smooth' })
        },
        onError: (e) => setError({ at, message: e instanceof ApiError && e.serverMessage ? e.serverMessage : t('admin.merge.failed') }),
      },
    )
  }

  const confirmation = (at: string, keep: AdminUser, old: AdminUser) =>
    open === at && (
      <Confirmation
        keep={keep}
        old={old}
        pending={merge.isPending}
        error={error?.at === at ? error.message : undefined}
        onMerge={() => run(at, keep, old)}
        onCancel={() => {
          setOpen(undefined)
          setError(undefined)
        }}
      />
    )

  const list = suggestions.data ?? []

  return (
    <>
      <PageTitle subtitle={t('pages.adminMerge.subtitle')}>{t('pages.adminMerge.title')}</PageTitle>
      {done && (
        <div className="alert-info mb-4" role="status">
          ✅ <Trans i18nKey="admin.merge.done" values={done} components={{ b: <strong /> }} />
        </div>
      )}

      {suggestions.isLoading ? (
        <Loading />
      ) : suggestions.error ? (
        <ErrorBox error={suggestions.error} />
      ) : list.length === 0 ? (
        <div className="card mb-4 px-5 py-10 text-center">
          <div className="mb-2 text-[2rem]" aria-hidden>
            ✅
          </div>
          <p className="font-semibold">{t('admin.merge.empty')}</p>
          <p className="mt-1 text-sm muted">{t('admin.merge.emptyHint')}</p>
        </div>
      ) : (
        <>
          <h2 className="mb-3 font-bold">
            {t('admin.merge.suggestions')} <span className="font-normal muted">({list.length})</span>
          </h2>
          {list.map((s) => {
            const shown = shownPair(url, s)
            const at = `${s.keep.id}:${s.merge.id}`
            return (
              <section key={at} className="card mb-4 overflow-hidden">
                <header className="flex flex-wrap items-center gap-2 border-b border-line px-4 py-2.5">
                  {s.reasons.map((r) => (
                    <span key={r} className="pill-accent">
                      {t(`admin.merge.reason.${r}`)}
                    </span>
                  ))}
                  <span className="flex-1" />
                  {shown.swapped ? (
                    <span className="pill-danger">{t('admin.merge.swapped')}</span>
                  ) : (
                    <span className="text-[0.8rem] muted">
                      <span className="max-md:hidden">{t('admin.merge.hintLeft')}</span>
                      <span className="md:hidden">{t('admin.merge.hintTop')}</span>
                    </span>
                  )}
                </header>
                <Pair keep={shown.keep} old={shown.merge} />
                <Actions
                  onMerge={() => {
                    setError(undefined)
                    setOpen(at)
                  }}
                  onSwap={() => setUrl(withSwap(url, s.merge.id, !shown.swapped))}
                  extra={
                    shown.swapped && (
                      <button type="button" className={clsx('btn-ghost', small)} onClick={() => setUrl(withSwap(url, s.merge.id, false))}>
                        {t('admin.merge.backToSuggestion')}
                      </button>
                    )
                  }
                  hidden={open === at}
                />
                {confirmation(at, shown.keep, shown.merge)}
              </section>
            )
          })}
        </>
      )}

      <Manual url={url} setUrl={setUrl} known={list.flatMap((s: MergeSuggestion) => [s.keep, s.merge])} openConfirm={() => {
          setError(undefined)
          setOpen('manual')
        }} confirmation={confirmation} />
    </>
  )
}

function Actions({ onMerge, onSwap, extra, hidden, disabled, hint }: { onMerge: () => void; onSwap?: () => void; extra?: ReactNode; hidden?: boolean; disabled?: boolean; hint?: string }) {
  const { t } = useTranslation()
  if (hidden) return null
  return (
    <div className="flex flex-wrap items-center gap-2 border-t border-line px-4 py-3">
      <button type="button" className={clsx('btn-primary', small)} disabled={disabled} onClick={onMerge}>
        🔀 {t('admin.merge.mergeEllipsis')}
      </button>
      {onSwap && (
        <button type="button" className={clsx('btn-secondary', small)} onClick={onSwap}>
          ⇄ {t('admin.merge.swap')}
        </button>
      )}
      {extra}
      {hint && <span className="text-[0.8rem] muted">{hint}</span>}
    </div>
  )
}

/** ✓ Keep ← ✕ Merge away (stacked with ↑ on phones). */
function Pair({ keep, old }: { keep: AdminUser; old: AdminUser }) {
  return (
    <div className="grid items-center gap-3 p-4 md:grid-cols-[1fr_auto_1fr]">
      <UserBox user={keep} kind="keep" />
      <span className="text-center muted" aria-hidden>
        <span className="max-md:hidden">←</span>
        <span className="md:hidden">↑</span>
      </span>
      <UserBox user={old} kind="merge" />
    </div>
  )
}

function UserBox({ user, kind }: { user: AdminUser; kind: 'keep' | 'merge' }) {
  const { t } = useTranslation()
  const f = useFormat()
  const rows: [string, ReactNode][] = [
    [
      t('admin.merge.telegram'),
      <>
        {user.telegramUserName ? handle(user.telegramUserName) : '—'}
        <span className="muted"> · </span>
        {user.telegramId ? <span className="tabular-nums">{user.telegramId}</span> : <span className="muted">{t('admin.merge.noId')}</span>}
      </>,
    ],
    [t('admin.merge.bot'), user.telegramBotEnabled ? <span className="text-success">{t('admin.users.started')}</span> : <span className="muted">{t('admin.users.placeholder')}</span>],
    [
      t('admin.merge.login'),
      user.login || user.hasPassword ? (
        <>
          {user.login ?? '—'}
          {user.hasPassword && (
            <>
              <span className="muted"> · </span>🔑 {t('admin.merge.password')}
            </>
          )}
        </>
      ) : (
        '—'
      ),
    ],
    [
      t('admin.merge.bills'),
      <>
        <span className="tabular-nums">{user.billCount}</span>
        {user.firstBillDate && <span className="muted"> · {t('admin.merge.since', { date: f.dateYear(user.firstBillDate) })}</span>}
      </>,
    ],
    [t('admin.merge.contacts'), String(user.contactCount)],
  ]
  return (
    <div className={clsx('min-w-0 rounded-[10px] border px-4 py-3', kind === 'keep' ? 'border-accent bg-accent-faint' : 'border-dashed border-line')}>
      <div className={clsx('text-[0.7rem] font-bold tracking-wide uppercase', kind === 'keep' ? 'text-accent' : 'muted')}>{kind === 'keep' ? `✓ ${t('admin.merge.keep')}` : `✕ ${t('admin.merge.mergeAway')}`}</div>
      <div className="mb-1.5 truncate text-[1.05rem] font-bold">{user.displayName}</div>
      <dl className="grid grid-cols-[4.75rem_1fr] gap-x-3 md:grid-cols-[6.5rem_1fr] gap-y-0.5 text-[0.85rem]">
        {rows.map(([k, v]) => (
          <div key={k} className="contents">
            <dt className="muted">{k}</dt>
            <dd className="min-w-0 break-words">{v}</dd>
          </div>
        ))}
      </dl>
    </div>
  )
}

/** What a merge does (api.md, "What a merge does"), built from the two users. */
function Confirmation({ keep, old, pending, error, onMerge, onCancel }: { keep: AdminUser; old: AdminUser; pending: boolean; error?: string; onMerge: () => void; onCancel: () => void }) {
  const { t } = useTranslation()
  const kept = keep.displayName
  const gone = old.displayName
  const item = (label: string, text: string) => (
    <li>
      <strong>{label}</strong> {text}
    </li>
  )
  return (
    <div className="border-t border-line bg-danger/5 px-4 py-3" role="alertdialog" aria-label={t('admin.merge.confirmTitle', { old: gone, kept })}>
      <p className="mb-2 font-bold text-danger">{t('admin.merge.confirmTitle', { old: gone, kept })}</p>
      <ul className="mb-3 list-disc space-y-1 pl-5 text-sm">
        {item(t('admin.merge.movesTo', { name: kept }), t('admin.merge.moves', { old: gone, count: old.billCount }))}
        {item(t('admin.merge.deletedLabel'), t('admin.merge.deleted', { old: gone }))}
        {item(t('admin.merge.profileLabel'), t('admin.merge.profile', { kept, old: gone }))}
        {item(t('admin.merge.signInLabel'), `${keptSignIn(keep) === 'keeps' ? t('admin.merge.signInKeeps', { kept, old: gone }) : t('admin.merge.signInTakes', { kept, old: gone })} ${t('admin.merge.signedOut', { old: gone })}`)}
      </ul>
      {error && (
        <div className="alert-danger mb-3" role="alert">
          {error}
        </div>
      )}
      <div className="flex flex-wrap items-center gap-2">
        <button type="button" className={clsx('btn-danger', small)} disabled={pending} onClick={onMerge}>
          🔀 {t('admin.merge.merge')}
        </button>
        <button type="button" className={clsx('btn-ghost', small)} onClick={onCancel}>
          {t('common.cancel')}
        </button>
      </div>
    </div>
  )
}

/** "Merge any two users": two searches over all users, then the same boxes, Swap and confirmation (hidden on phones). */
function Manual({
  url,
  setUrl,
  known,
  openConfirm,
  confirmation,
}: {
  url: MergeUrl
  setUrl: (next: MergeUrl, replace?: boolean) => void
  known: AdminUser[]
  openConfirm: () => void
  confirmation: (at: string, keep: AdminUser, old: AdminUser) => ReactNode
}) {
  const { t } = useTranslation()
  const keep = useAdminUserRow(url.keep, known)
  const old = useAdminUserRow(url.merge, known)
  const ready = canMergeManual(url) && keep.row && old.row
  const confirm = ready ? confirmation('manual', keep.row!, old.row!) : null

  const side = (which: 'keep' | 'merge', label: string) => {
    const id = url[which]
    const row = which === 'keep' ? keep.row : old.row
    const other = url[which === 'keep' ? 'merge' : 'keep']
    return (
      <div className="min-w-0">
        <label className="label" htmlFor={`manual-${which}`}>
          {label}
        </label>
        {id ? (
          <ChosenUser id={id} label={label} onClear={() => setUrl(withManual(url, which, undefined))} hint={row && <span className="ml-2 text-[0.8rem] muted">{row.telegramUserName ? handle(row.telegramUserName) : <UserHint user={row} />}</span>} />
        ) : (
          <UserSearch id={`manual-${which}`} placeholder={t('admin.merge.searchHint')} exclude={other ? [other] : undefined} onPick={(u) => setUrl(withManual(url, which, u.id))} />
        )}
      </div>
    )
  }

  return (
    <section className="card overflow-hidden max-md:hidden">
      <header className="flex flex-wrap items-center gap-2 border-b border-line px-4 py-3">
        <h2 className="flex-1 font-bold">{t('admin.merge.manual')}</h2>
        <span className="text-[0.8rem] muted">{t('admin.merge.manualHint')}</span>
      </header>
      <div className="grid items-end gap-3 p-4 md:grid-cols-[1fr_auto_1fr]">
        {side('keep', `✓ ${t('admin.merge.keepLabel')}`)}
        <span className="pb-2 text-center muted" aria-hidden>
          ←
        </span>
        {side('merge', `✕ ${t('admin.merge.mergeAwayLabel')}`)}
      </div>
      {(keep.error || old.error) && (
        <div className="px-4 pb-3">
          <ErrorBox error={keep.error ?? old.error} />
        </div>
      )}
      {ready && (
        <div className="-mt-1">
          <Pair keep={keep.row!} old={old.row!} />
        </div>
      )}
      <Actions
        onMerge={openConfirm}
        onSwap={ready ? () => setUrl(withManualSwapped(url)) : undefined}
        disabled={!ready}
        hint={ready ? undefined : t('admin.merge.pickTwo')}
        hidden={!!confirm}
      />
      {confirm}
    </section>
  )
}
