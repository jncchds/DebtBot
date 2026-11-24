import { useEffect, useId, useState, type ReactNode } from 'react'
import { useNavigate, useSearchParams } from 'react-router'
import { useTranslation } from 'react-i18next'
import { useQuery } from '@tanstack/react-query'
import { previewBillText, useCreateBillFromText } from '../api/bills'
import { ApiError } from '../api/client'
import type { ParsedBill, UserSearch } from '../api/types'
import { PageTitle, Spinner } from '../components/ui'
import { isEqualSplit } from '../lib/bills'
import { sumUnits, toUnits } from '../lib/money'
import { loadStored, store } from '../lib/newBill'
import { useFormat } from '../lib/useFormat'
import { matchTextPerson, usePeople } from '../lib/usePeople'

const PASTE_KEY = 'debtbot.pasteBill'

interface PasteState {
  text: string
  /** The text the preview below belongs to. */
  previewed: string
}

/** The bot's /AddBill format, shown as help (FormatCommand). */
const EXAMPLE = `Dinner at Molto

84
EUR

84 @anna

Pasta
48
1 @anna
1 @boris

Tiramisu
28
1 @boris`

/**
 * ➕ New bill, paste-text mode: /bills/new/paste, `?preview=1` once previewed (mockups/new-bill).
 * Preview parses the text on the server without saving it; create sends the same text.
 */
export default function PasteBill() {
  const { t } = useTranslation()
  const id = useId()
  const navigate = useNavigate()
  const [search, setSearch] = useSearchParams()
  const [state, setState] = useState<PasteState>(() => loadStored(PASTE_KEY, { text: '', previewed: '' }))
  const showPreview = search.get('preview') === '1' && !!state.previewed.trim()
  const preview = useQuery({ queryKey: ['bills', 'textPreview', state.previewed], queryFn: () => previewBillText(state.previewed), enabled: showPreview, staleTime: Infinity })
  const create = useCreateBillFromText()
  const [error, setError] = useState<string>()

  useEffect(() => store(PASTE_KEY, state), [state])

  const runPreview = () => {
    setError(undefined)
    setState((s) => ({ ...s, previewed: s.text }))
    if (!showPreview) setSearch({ preview: '1' })
  }
  const changed = showPreview && state.text !== state.previewed

  const submit = (finalize: boolean) => {
    setError(undefined)
    create.mutate(
      { text: state.previewed, finalize },
      {
        onSuccess: (billId) => {
          store(PASTE_KEY, undefined)
          navigate(`/bills/${billId}`)
        },
        // POST Bills/text answers 400 with a plain list of errors.
        onError: (e) => setError(e instanceof ApiError && Array.isArray(e.body) ? (e.body as string[]).join('; ') : e.message),
      },
    )
  }

  const textCard = (
    <section className="card">
      <div className="flex items-center gap-2 border-b border-line px-4 py-3">
        <h2 className="flex-1 font-bold">
          <label htmlFor={`${id}-text`}>{t('paste.text')}</label>
        </h2>
        <button type="button" className="btn-ghost px-2 py-1 text-[0.8rem]" onClick={() => setState((s) => ({ ...s, text: EXAMPLE }))}>
          {t('paste.example')}
        </button>
      </div>
      <div className="p-4">
        <textarea
          id={`${id}-text`}
          className="input min-h-[14rem] resize-y font-mono text-[0.82rem] leading-relaxed"
          rows={state.text ? 20 : 14}
          spellCheck={false}
          placeholder={t('paste.placeholder')}
          value={state.text}
          onChange={(e) => setState((s) => ({ ...s, text: e.target.value }))}
        />
        <div className="mt-3 flex flex-wrap items-center gap-2">
          <button type="button" className={showPreview && !changed ? 'btn-secondary' : 'btn-primary'} disabled={!state.text.trim() || preview.isFetching} onClick={runPreview}>
            👁 {t('paste.preview')}
          </button>
          <span className="text-[0.8rem] muted">{changed ? t('paste.changed') : t('paste.nothingSaved')}</span>
        </div>
      </div>
    </section>
  )

  const help = (
    <section className="card">
      <div className="flex flex-wrap items-center gap-2 border-b border-line px-4 py-3">
        <h2 className="flex-1 font-bold">{t('paste.format')}</h2>
        <span className="text-[0.8rem] muted">{t('paste.formatHint')}</span>
      </div>
      <pre className="overflow-x-auto px-4 py-3.5 font-mono text-[0.82rem] leading-relaxed">{t('paste.formatText')}</pre>
      <ul className="list-disc px-4 pb-3.5 pl-8 text-[0.8rem] muted">
        <li>{t('paste.noteSections')}</li>
        <li>{t('paste.notePeople')}</li>
        <li>{t('paste.noteParts')}</li>
      </ul>
    </section>
  )

  let result: ReactNode = null
  if (showPreview) {
    if (preview.isLoading) result = <div className="card flex items-center gap-2 px-4 py-3.5 text-sm muted"><Spinner className="h-4 w-4" />{t('common.loading')}</div>
    else if (preview.error) result = <div className="alert-danger">{preview.error.message}</div>
    else if (preview.data)
      result = (
        <PreviewResult
          errors={preview.data.errors}
          bill={preview.data.isValid ? preview.data.result : null}
          actions={
            preview.data.isValid && (
              <>
                {error && (
                  <div className="alert-danger mb-2 w-full" role="alert">
                    {error}
                  </div>
                )}
                <span className="text-[0.8rem] muted">{t('paste.namesMatched')}</span>
                <span className="flex-1" />
                {create.isPending && <Spinner className="h-4 w-4" />}
                <div className="flex flex-wrap gap-2 max-md:w-full max-md:[&>*]:flex-1">
                  <button type="button" className="btn-secondary" disabled={create.isPending || changed} onClick={() => submit(false)}>
                    {t('newBill.saveDraft')}
                  </button>
                  <button type="button" className="btn-primary" disabled={create.isPending || changed} onClick={() => submit(true)}>
                    ✓ {t('newBill.createFinalize')}
                  </button>
                </div>
              </>
            )
          }
        />
      )
  }

  return (
    <>
      <PageTitle subtitle={t('paste.subtitle')}>{t('pages.newBill.title')}</PageTitle>
      <div className="grid items-start gap-4 lg:grid-cols-[3fr_2fr] [&>*]:min-w-0">
        <div className="space-y-4">
          {textCard}
          {result}
        </div>
        {help}
      </div>
    </>
  )
}

/** "<pre>84,5O @anna</pre>" in the parser's messages becomes code. */
function ParserMessage({ text }: { text: string }) {
  const parts = text.split(/<pre>([\s\S]*?)<\/pre>/)
  return (
    <>
      {parts.map((p, i) =>
        i % 2 ? (
          <code key={i} className="rounded bg-danger/10 px-1 font-mono text-[0.8rem]">
            {p}
          </code>
        ) : (
          p
        ),
      )}
    </>
  )
}

function PreviewResult({ errors, bill, actions }: { errors: string[]; bill: ParsedBill | null; actions: ReactNode }) {
  const { t } = useTranslation()
  const f = useFormat()
  const { people } = usePeople()
  const who = (u: UserSearch) => {
    const q = u.queryString ?? u.telegramUserName ?? u.displayName ?? ''
    const p = matchTextPerson(q, people)
    return (
      <span key={q} className="inline-flex items-baseline gap-1 text-[0.8rem]">
        <span className="font-mono">{q}</span>
        <span className="muted">→</span>
        {p ? <b>{p.name}</b> : <i className="muted">{t('paste.newPerson')}</i>}
      </span>
    )
  }

  if (!bill)
    return (
      <section className="card">
        <div className="flex items-center gap-2 border-b border-line px-4 py-3">
          <h2 className="flex-1 font-bold">{t('paste.previewTitle')}</h2>
          <span className="pill-danger">{t('paste.problems', { count: errors.length })}</span>
        </div>
        <div className="p-4">
          <div className="alert-danger" role="alert">
            <ul className="list-disc pl-5">
              {errors.map((e, i) => (
                <li key={i}>
                  <ParserMessage text={e} />
                </li>
              ))}
            </ul>
          </div>
          <p className="mt-1.5 text-xs muted">{t('paste.fixAndPreview')}</p>
        </div>
      </section>
    )

  const cur = bill.currencyCode ?? ''
  const payCur = bill.paymentCurrencyCode ?? cur
  const [title, ...rest] = (bill.description ?? '').split('\n')
  const lines = bill.lines ?? []
  const payments = bill.payments ?? []
  const linesTotal = sumUnits(lines.map((l) => l.subtotal ?? 0))
  const tips = bill.totalWithTips != null ? toUnits(bill.totalWithTips) - linesTotal : 0n

  return (
    <section className="card">
      <div className="flex items-center gap-2 border-b border-line px-4 py-3">
        <h2 className="flex-1 font-bold">{t('paste.previewTitle')}</h2>
        <span className="pill-success">{t('paste.looksGood')}</span>
      </div>
      <div className="px-4 pt-3.5 pb-2">
        <div className="text-[1.05rem] font-bold break-words">{title || t('bill.untitled')}</div>
        {rest.length > 0 && <div className="text-sm whitespace-pre-line muted">{rest.join('\n')}</div>}
        <div className="mt-2 flex flex-wrap gap-x-5 gap-y-1 text-sm">
          <span>
            <span className="muted">{t('bill.total')}</span> <b className="tabular-nums">{bill.totalWithTips != null ? f.money(bill.totalWithTips, cur) : '—'}</b>
          </span>
          <span>
            <span className="muted">{t('bill.paidIn')}</span> <b>{payCur}</b>
          </span>
          <span>
            <span className="muted">{t('bill.tips')}</span> <b className="tabular-nums">{tips > 0n ? f.money(tips, cur) : '—'}</b>
          </span>
        </div>
      </div>
      <ul className="divide-y divide-line border-t border-line">
        {lines.length === 0 && <li className="px-4 py-3 text-sm muted">{t('bill.noLines')}</li>}
        {lines.map((l, i) => (
          <li key={i} className="flex items-start gap-3 px-4 py-3">
            <div className="min-w-0 flex-1">
              <b className="break-words">{l.itemDescription}</b>{' '}
              <span className="text-[0.8rem] muted">{t(isEqualSplit(l.participants.map((p) => p.part ?? 1)) ? 'bill.splitEqually' : 'bill.byParts')}</span>
              <div className="mt-1.5 flex flex-wrap gap-x-3.5 gap-y-1">
                {l.participants.map((p, j) => (
                  <span key={j}>
                    {who(p.user)}
                    {!isEqualSplit(l.participants.map((x) => x.part ?? 1)) && <span className="text-[0.8rem] muted"> ×{p.part}</span>}
                  </span>
                ))}
              </div>
            </div>
            <span className="font-semibold tabular-nums whitespace-nowrap">{f.money(l.subtotal ?? 0, cur)}</span>
          </li>
        ))}
        {payments.map((p, i) => (
          <li key={`p${i}`} className="flex flex-wrap items-baseline gap-x-3 gap-y-1 px-4 py-2.5 text-[0.9rem]">
            <span className="w-16 muted">{t('paste.paid')}</span>
            <span className="min-w-0 flex-1">{who(p.user)}</span>
            <span className="font-semibold tabular-nums whitespace-nowrap">{f.money(p.amount ?? 0, payCur)}</span>
          </li>
        ))}
      </ul>
      <div className="flex flex-wrap items-center gap-2 border-t border-line px-4 py-3.5">{actions}</div>
    </section>
  )
}
