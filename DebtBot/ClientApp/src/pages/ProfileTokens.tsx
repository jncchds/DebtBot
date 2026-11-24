import { useState, type FormEvent, type ReactNode } from 'react'
import { Link, Navigate, useLocation, useNavigate } from 'react-router'
import { Trans, useTranslation } from 'react-i18next'
import clsx from 'clsx'
import { ApiError } from '../api/client'
import { useApiTokens, useCreateApiToken, useRevokeApiToken } from '../api/people'
import type { ApiToken, ApiTokenCreated } from '../api/types'
import { ExpandButton } from '../components/list/ExpandButton'
import { Card, Field } from '../components/profile/bits'
import { inputProps } from '../components/profile/inputProps'
import { ErrorBox, Loading, PageTitle } from '../components/ui'
import {
  activeCount,
  canOpen,
  checkTokenForm,
  curlExample,
  emptyTokenForm,
  EXPIRY_CHOICES,
  expiryDate,
  maskedToken,
  toggleOpen,
  tokenRequest,
  type TokenForm,
} from '../lib/apiTokens'
import { useFormat } from '../lib/useFormat'

export type TokensMode = 'list' | 'new' | 'created'

/** The API's limit of active tokens (DebtBot:ApiTokens:MaxActivePerUser). */
const MAX_ACTIVE = 20
const small = 'px-2.5 py-1 text-[0.8rem] whitespace-nowrap'

/**
 * 👤 Profile › API tokens (series-2/mockups/api-tokens): the list (`/profile/tokens?open=<id>`), the New token form
 * (`/profile/tokens/new`) and the shown-once secret (`/profile/tokens/created`). The three routes share this
 * component, so the secret lives only in its state: never in the URL or in storage. Reloading `/created` goes back
 * to the list.
 */
export default function ProfileTokens({ mode }: { mode: TokensMode }) {
  const { t } = useTranslation()
  const tokens = useApiTokens()
  const [created, setCreated] = useState<ApiTokenCreated>()

  // Leaving the shown-once view forgets the secret (adjusted during render when the mode changes).
  const [shownMode, setShownMode] = useState(mode)
  if (mode !== shownMode) {
    setShownMode(mode)
    if (mode !== 'created') setCreated(undefined)
  }

  if (mode === 'created' && !created) return <Navigate to="/profile/tokens" replace />

  return (
    <>
      <PageTitle
        subtitle={t('pages.tokens.subtitle')}
        actions={
          mode === 'list' && (
            <Link to="/profile/tokens/new" className="btn-primary">
              ＋ {t('tokens.newToken')}
            </Link>
          )
        }
      >
        {t('pages.tokens.title')}
      </PageTitle>
      <div className="space-y-4">
        {mode === 'new' && <NewToken onCreated={setCreated} />}
        {mode === 'created' && created && <Created token={created} />}
        {tokens.isLoading ? <Loading /> : tokens.error || !tokens.data ? <ErrorBox error={tokens.error} /> : <TokenList tokens={tokens.data} highlight={created?.id} />}
      </div>
    </>
  )
}

function NewToken({ onCreated }: { onCreated: (t: ApiTokenCreated) => void }) {
  const { t } = useTranslation()
  const f = useFormat()
  const navigate = useNavigate()
  const create = useCreateApiToken()
  const [form, setForm] = useState<TokenForm>(emptyTokenForm)
  const [error, setError] = useState<string>()
  const [failure, setFailure] = useState<string>()

  const submit = (e: FormEvent) => {
    e.preventDefault()
    setFailure(undefined)
    const found = checkTokenForm(form)
    setError(found.name)
    if (found.name) return
    create.mutate(tokenRequest(form), {
      onSuccess: (token) => {
        onCreated(token)
        // The form's entry is replaced: Back from the secret goes to the list, not to a filled-in form.
        navigate('/profile/tokens/created', { replace: true })
      },
      onError: (err) => setFailure(err instanceof ApiError && err.serverMessage ? err.serverMessage : t('tokens.createFailed')),
    })
  }

  const until = expiryDate(form.expiry)
  const scope = (value: TokenForm['scope'], title: string, help: string) => (
    <label
      className={clsx(
        'flex cursor-pointer items-start gap-2.5 rounded-lg border px-3 py-2.5',
        form.scope === value ? 'border-accent/45 bg-accent-faint' : 'border-line hover:border-accent/30',
      )}
    >
      <input type="radio" name="scope" className="mt-1 accent-[var(--accent)]" checked={form.scope === value} onChange={() => setForm({ ...form, scope: value })} />
      <span>
        <span className="block text-sm font-semibold">{title}</span>
        <span className="block text-[0.8rem] muted">{help}</span>
      </span>
    </label>
  )

  return (
    <div className="max-w-[34rem]">
      <Card title={`＋ ${t('tokens.newToken')}`}>
        <form onSubmit={submit} noValidate>
          {failure && (
            <div className="alert-danger mb-4" role="alert">
              {failure}
            </div>
          )}
          <Field id="tok-name" label={t('tokens.name')} help={t('tokens.nameHelp')} error={error && t(error)}>
            <input {...inputProps('tok-name', error)} autoFocus maxLength={100} value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} />
          </Field>
          <fieldset className="mb-4">
            <legend className="label">{t('tokens.scope')}</legend>
            <div className="grid gap-2">
              {scope('Read', t('tokens.readOnly'), t('tokens.readOnlyHelp'))}
              {scope('ReadWrite', t('tokens.readWrite'), t('tokens.readWriteHelp'))}
            </div>
          </fieldset>
          <fieldset className="mb-5">
            <legend className="label">{t('tokens.expires')}</legend>
            <div className="flex flex-wrap gap-1.5" role="radiogroup">
              {EXPIRY_CHOICES.map((c) => (
                <button
                  key={c}
                  type="button"
                  role="radio"
                  aria-checked={form.expiry === c}
                  className={clsx(
                    'rounded-full border px-3 py-0.5 text-[0.8rem]',
                    form.expiry === c ? 'border-accent/40 bg-accent-faint font-semibold text-accent' : 'border-line text-muted hover:text-fg',
                  )}
                  onClick={() => setForm({ ...form, expiry: c })}
                >
                  {c === 'never' ? t('tokens.never') : t('tokens.days', { count: Number(c) })}
                </button>
              ))}
            </div>
            <p className="mt-1 text-[0.8rem] muted">{until ? t('tokens.expiresOn', { date: f.dateYear(until.toISOString()) }) : t('tokens.expiresNever')}</p>
          </fieldset>
          <div className="flex flex-wrap items-center gap-2">
            <button type="submit" className="btn-primary" disabled={create.isPending}>
              {t('tokens.create')}
            </button>
            <Link to="/profile/tokens" className="btn-ghost">
              {t('common.cancel')}
            </Link>
          </div>
        </form>
      </Card>
    </div>
  )
}

function Created({ token }: { token: ApiTokenCreated }) {
  const { t } = useTranslation()
  const [copied, setCopied] = useState<'ok' | 'failed'>()

  const copy = async () => {
    try {
      await navigator.clipboard.writeText(token.token)
      setCopied('ok')
    } catch {
      setCopied('failed')
    }
  }

  return (
    <div className="max-w-[34rem]">
      <Card title={`✅ ${t('tokens.createdTitle', { name: token.name })}`}>
        <div className="alert-info flex items-start gap-2">
          <span aria-hidden>⚠️</span>
          <span>
            <Trans i18nKey="tokens.secretWarning" components={{ b: <strong /> }} />
          </span>
        </div>
        <p className="mt-4 mb-1.5 text-[0.8rem] muted">{t('tokens.yourToken')}</p>
        <div className="flex gap-2 max-sm:flex-col">
          <code data-testid="token-secret" className="min-w-0 flex-1 rounded-md border border-line bg-surface-2 px-2.5 py-2 font-mono text-[0.82rem] break-all select-all">
            {token.token}
          </code>
          <button type="button" className="btn-primary shrink-0" onClick={copy}>
            📋 {copied === 'ok' ? t('tokens.copied') : t('tokens.copy')}
          </button>
        </div>
        {copied === 'failed' && <p className="mt-1 text-[0.8rem] text-danger">{t('tokens.copyFailed')}</p>}
        <p className="mt-4 mb-1.5 text-[0.8rem] muted">{t('tokens.tryIt')}</p>
        <pre className="rounded-md border border-line bg-surface-2 px-3 py-2 font-mono text-[0.78rem] break-all whitespace-pre-wrap">{curlExample(window.location.origin, token.token)}</pre>
        <p className="mt-1.5 text-[0.8rem] muted">
          <Trans i18nKey="tokens.swagger" components={{ a: <a href="/swagger" target="_blank" rel="noopener noreferrer" className="link" /> }} />
        </p>
        <Link to="/profile/tokens" className="btn-secondary mt-5">
          {t('tokens.done')}
        </Link>
      </Card>
    </div>
  )
}

function TokenList({ tokens, highlight }: { tokens: ApiToken[]; highlight?: string }) {
  const { t } = useTranslation()
  const f = useFormat()
  const { search } = useLocation()
  const navigate = useNavigate()
  const revoke = useRevokeApiToken()
  const [asking, setAsking] = useState<string>()
  const [failure, setFailure] = useState<string>()
  const openId = new URLSearchParams(search).get('open')

  const toggle = (tok: ApiToken) => {
    setAsking(undefined)
    setFailure(undefined)
    navigate({ search: toggleOpen(search, tok.id) })
  }

  const scope = (tok: ApiToken) => (tok.scope === 'Read' ? <span className="pill-muted whitespace-nowrap">{t('tokens.readOnly')}</span> : <span className="pill-accent whitespace-nowrap">{t('tokens.readWrite')}</span>)
  const status = (tok: ApiToken) =>
    tok.status === 'Active' ? (
      <span className="pill-success">{t('tokens.active')}</span>
    ) : tok.status === 'Expired' ? (
      <span className="pill-muted">{t('tokens.expired')}</span>
    ) : (
      <span className="pill-danger">{t('tokens.revoked')}</span>
    )
  const masked = (tok: ApiToken) => <code className="font-mono text-[0.8rem] whitespace-nowrap">{maskedToken(tok.prefix)}</code>
  const used = (tok: ApiToken) => (tok.lastUsedAt ? f.dateTime(tok.lastUsedAt) : <span className="muted">{t('tokens.neverUsed')}</span>)
  const expires = (tok: ApiToken) => (tok.expiresAt ? f.dateYear(tok.expiresAt) : <span className="muted">{t('tokens.never')}</span>)
  const expander = (tok: ApiToken) => canOpen(tok) && <ExpandButton open={openId === tok.id} label={tok.name} onClick={() => toggle(tok)} />
  const open = (tok: ApiToken) => canOpen(tok) && openId === tok.id
  const danger = (tok: ApiToken) => open(tok) && (asking === tok.id || !!failure)
  const tint = (tok: ApiToken) => (danger(tok) ? 'bg-danger/5' : open(tok) || highlight === tok.id ? 'bg-accent-faint' : undefined)

  const strip = (tok: ApiToken): ReactNode =>
    asking === tok.id ? (
      <div className="flex flex-wrap items-center gap-x-3 gap-y-2 text-sm" role="alert">
        <span className="min-w-48 flex-1 text-danger">
          <Trans i18nKey="tokens.revokeAsk" values={{ name: tok.name, token: maskedToken(tok.prefix) }} components={{ b: <strong />, code: <code className="font-mono text-[0.8rem]" /> }} />
        </span>
        <button
          type="button"
          className={clsx('btn-danger', small)}
          disabled={revoke.isPending}
          onClick={() =>
            revoke.mutate(tok.id, {
              onSuccess: () => {
                setAsking(undefined)
                navigate({ search: toggleOpen(search, tok.id) }, { replace: true })
              },
              onError: (err) => {
                setAsking(undefined)
                setFailure(err instanceof ApiError && err.serverMessage ? err.serverMessage : t('tokens.revokeFailed'))
              },
            })
          }
        >
          🚫 {t('tokens.revoke')}
        </button>
        <button type="button" className={clsx('btn-ghost', small)} onClick={() => setAsking(undefined)}>
          {t('tokens.keep')}
        </button>
      </div>
    ) : (
      <div className="flex flex-wrap items-center gap-2 text-sm">
        {failure && (
          <span className="min-w-48 flex-1 text-danger" role="alert">
            {failure}
          </span>
        )}
        <button
          type="button"
          className={clsx('btn-secondary text-danger', small)}
          onClick={() => {
            setFailure(undefined)
            setAsking(tok.id)
          }}
        >
          🚫 {t('tokens.revoke')}
        </button>
      </div>
    )

  const head = (
    <header className="flex items-center gap-2 border-b border-line px-4 py-3">
      <h2 className="min-w-0 flex-1 font-bold">🗝️ {t('tokens.yourTokens')}</h2>
      <span className="text-[0.8rem] muted">{t('tokens.activeCount', { count: activeCount(tokens), max: MAX_ACTIVE })}</span>
    </header>
  )

  if (!tokens.length)
    return (
      <section className="card overflow-hidden">
        {head}
        <p className="px-4 py-8 text-center text-sm muted">{t('tokens.empty')}</p>
      </section>
    )

  const th = 'border-b border-line px-3 py-2 text-left text-[0.72rem] font-semibold tracking-wide text-muted uppercase first:pl-4 last:pr-4'
  const td = 'px-3 py-2.5 align-middle first:pl-4 last:pr-4'

  return (
    <section className="card overflow-hidden">
      {head}
      {/* A table on wide screens, cards below. */}
      <table className="hidden w-full border-collapse text-sm lg:table">
        <thead>
          <tr>
            <th className={th}>{t('tokens.name')}</th>
            <th className={th}>{t('tokens.token')}</th>
            <th className={th}>{t('tokens.scope')}</th>
            <th className={th}>{t('tokens.created')}</th>
            <th className={th}>{t('tokens.lastUsed')}</th>
            <th className={th}>{t('tokens.expires')}</th>
            <th className={th}>{t('tokens.status')}</th>
            <th className={clsx(th, 'w-10')}>
              <span className="sr-only">{t('list.actions')}</span>
            </th>
          </tr>
        </thead>
        <tbody>
          {tokens.map((tok) => (
            <TokenRows key={tok.id} open={open(tok)} tint={tint(tok)} faded={tok.status !== 'Active'} strip={open(tok) && strip(tok)}>
              <td className={clsx(td, 'font-semibold')}>{tok.name}</td>
              <td className={td}>{masked(tok)}</td>
              <td className={td}>{scope(tok)}</td>
              <td className={clsx(td, 'whitespace-nowrap')}>{f.dateYear(tok.createdAt)}</td>
              <td className={clsx(td, 'whitespace-nowrap')}>{used(tok)}</td>
              <td className={clsx(td, 'whitespace-nowrap')}>{expires(tok)}</td>
              <td className={td}>
                {status(tok)}
                {tok.revokedAt && <div className="text-[0.78rem] whitespace-nowrap muted">{f.dateYear(tok.revokedAt)}</div>}
              </td>
              <td className={clsx(td, 'text-right')}>{expander(tok)}</td>
            </TokenRows>
          ))}
        </tbody>
      </table>
      <ul className="divide-y divide-line lg:hidden">
        {tokens.map((tok) => (
          <li key={tok.id} className={clsx('space-y-1 px-4 py-3', tint(tok))}>
            <div className={clsx('space-y-1', tok.status !== 'Active' && 'opacity-60')}>
              <div className="flex items-center gap-2">
                <span className="min-w-0 flex-1 font-semibold break-words">{tok.name}</span>
                {status(tok)}
                {expander(tok)}
              </div>
              <div className="flex items-center gap-2 text-[0.8rem] muted">
                <span className="min-w-0 flex-1">{masked(tok)}</span>
                {scope(tok)}
              </div>
              <div className="text-[0.8rem] muted">
                {t('tokens.createdCard', { created: f.dateYear(tok.createdAt), used: tok.lastUsedAt ? f.dateTime(tok.lastUsedAt) : t('tokens.neverUsed') })}
              </div>
              <div className="text-[0.8rem] muted">
                {tok.revokedAt
                  ? t('tokens.revokedOn', { date: f.dateYear(tok.revokedAt) })
                  : tok.status === 'Expired' && tok.expiresAt
                    ? t('tokens.expiredOn', { date: f.dateYear(tok.expiresAt) })
                    : tok.expiresAt
                      ? t('tokens.expiresOnCard', { date: f.dateYear(tok.expiresAt) })
                      : t('tokens.expiresNever')}
              </div>
            </div>
            {open(tok) && <div className="mt-2.5 border-t border-dashed border-line pt-2.5">{strip(tok)}</div>}
          </li>
        ))}
      </ul>
    </section>
  )
}

/** A token's table row, plus its action strip when it's open. */
function TokenRows({ open, tint, faded, strip, children }: { open: boolean; tint?: string; faded: boolean; strip: ReactNode; children: ReactNode }) {
  return (
    <>
      <tr className={clsx('border-b border-line last:border-b-0', tint, open && 'border-b-0', faded && 'opacity-60')}>{children}</tr>
      {open && (
        <tr className={clsx('border-b border-line last:border-b-0', tint)}>
          <td colSpan={8} className="px-4 pt-0.5 pb-3">
            {strip}
          </td>
        </tr>
      )}
    </>
  )
}
