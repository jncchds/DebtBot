import { useEffect, useId, useRef, useState, type DragEvent } from 'react'
import { Link } from 'react-router'
import { Trans, useTranslation } from 'react-i18next'
import clsx from 'clsx'
import { uploadReceipt } from '../../api/receipts'
import type { ReceiptDraft, ReceiptWarning } from '../../api/types'
import { canvasEncoder, failureText, readReceipt, ReceiptFailure } from '../../lib/receipt'
import { useFormat } from '../../lib/useFormat'
import { Spinner } from '../ui'

/** The page title's "🧾 From a receipt" (only when receipts are on; the caller checks). */
export function FromReceiptButton({ to }: { to: string }) {
  const { t } = useTranslation()
  return (
    <Link to={to} className="btn-secondary">
      🧾 {t('receipt.fromReceipt')}
    </Link>
  )
}

/** The failure in words: the API's message (the bot's), else the client's, plus when to try again. */
export function FailureMessage({ failure }: { failure: ReceiptFailure }) {
  const { t } = useTranslation()
  const { message, key, retry } = failureText(failure)
  return (
    <>
      {message ?? t(key)}
      {retry && <> {t(retry.key, { count: retry.count })}</>}
    </>
  )
}

const kb = (bytes: number) => (bytes >= 1024 * 1024 ? `${(bytes / 1024 / 1024).toFixed(1)} MB` : `${Math.max(1, Math.round(bytes / 1024))} KB`)

/** "FreshMart 2026-11-21, 4 lines, 12.06 EUR" for the replace question. */
function useSummary(draft: ReceiptDraft) {
  const { t } = useTranslation()
  const f = useFormat()
  const parts = [draft.description, t('receipt.lines', { count: draft.lines.length })]
  if (draft.totalWithTips != null) parts.push(draft.currencyCode ? f.money(draft.totalWithTips, draft.currencyCode) : String(draft.totalWithTips))
  return parts.join(', ')
}

/** "Replace the form?" / "Replace the text?" with Replace and Keep. */
export function ReplaceQuestion({ draft, target, onReplace, onKeep }: { draft: ReceiptDraft; target: 'form' | 'text'; onReplace: () => void; onKeep: () => void }) {
  const { t } = useTranslation()
  const summary = useSummary(draft)
  return (
    <div className="rounded-lg border border-accent/40 bg-accent-faint px-3.5 py-3 text-sm" role="alertdialog" aria-label={t(target === 'form' ? 'receipt.replaceForm' : 'receipt.replaceText', { summary }).replace(/<[^>]+>/g, '')}>
      <p>
        <Trans i18nKey={target === 'form' ? 'receipt.replaceForm' : 'receipt.replaceText'} values={{ summary }} components={{ b: <b />, muted: <span className="muted" /> }} />
      </p>
      <div className="mt-2.5 flex flex-wrap gap-2 max-md:[&>*]:flex-1">
        <button type="button" className="btn-primary" onClick={onReplace} autoFocus>
          {t('receipt.replace')}
        </button>
        <button type="button" className="btn-ghost" onClick={onKeep}>
          {t(target === 'form' ? 'receipt.keepForm' : 'receipt.keepText')}
        </button>
      </div>
    </div>
  )
}

type PanelState = { kind: 'idle' } | { kind: 'busy'; name: string; size: number } | { kind: 'error'; failure: ReceiptFailure }

export interface ReceiptPanelProps {
  /** What the receipt fills: the page the panel was opened on. */
  target: 'form' | 'text'
  /** `receiptModel` from `auth/config`, for the privacy note. */
  model: string | null | undefined
  /** ✕: the page's own URL. */
  closeTo: string
  /** A draft was read; the page fills itself or asks first (`pending`). */
  onRead: (draft: ReceiptDraft) => void
  /** A draft waiting for "Replace the form?". */
  pending?: ReceiptDraft
  onReplace: () => void
  onKeep: () => void
  /** The space under it (default `mb-4`). */
  className?: string
}

/**
 * 🧾 From a receipt (receipts/mockups/receipt): a photo picker and drop zone, what it fills, the privacy note every
 * time, a busy state with Cancel, and one clear message on failure. The page underneath is never touched until a
 * draft is read (and, when it has content, until Replace).
 */
export function ReceiptPanel({ target, model, closeTo, onRead, pending, onReplace, onKeep, className = 'mb-4' }: ReceiptPanelProps) {
  const { t } = useTranslation()
  const id = useId()
  const input = useRef<HTMLInputElement>(null)
  const abort = useRef<AbortController | null>(null)
  const [state, setState] = useState<PanelState>({ kind: 'idle' })
  const [over, setOver] = useState(false)

  // Leaving the panel cancels a read in progress.
  useEffect(() => () => abort.current?.abort(), [])

  const read = async (file: File | undefined) => {
    if (!file) return
    abort.current?.abort()
    const ctrl = new AbortController()
    abort.current = ctrl
    setState({ kind: 'busy', name: file.name, size: file.size })
    try {
      const draft = await readReceipt(file, { upload: uploadReceipt, encode: canvasEncoder, signal: ctrl.signal })
      if (ctrl.signal.aborted) return
      setState({ kind: 'idle' })
      onRead(draft)
    } catch (e) {
      if (ctrl.signal.aborted) return
      const failure = e instanceof ReceiptFailure ? e : new ReceiptFailure('other')
      setState(failure.code === 'cancelled' ? { kind: 'idle' } : { kind: 'error', failure })
    } finally {
      if (input.current) input.current.value = ''
    }
  }

  const cancel = () => {
    abort.current?.abort()
    setState({ kind: 'idle' })
  }

  const drop = (e: DragEvent) => {
    e.preventDefault()
    setOver(false)
    if (state.kind !== 'busy') void read(e.dataTransfer.files[0])
  }

  let body
  if (pending) body = <ReplaceQuestion draft={pending} target={target} onReplace={onReplace} onKeep={onKeep} />
  else if (state.kind === 'busy')
    body = (
      <div className="flex flex-wrap items-center gap-3 rounded-lg bg-surface-2 p-4" role="status">
        <Spinner className="h-5 w-5" />
        <div className="min-w-0 flex-1">
          <b>{t('receipt.busy')}</b>
          <div className="text-[0.8rem] break-words muted">{t('receipt.busyFile', { name: state.name, size: kb(state.size) })}</div>
        </div>
        <button type="button" className="btn-secondary max-md:w-full" onClick={cancel}>
          {t('receipt.cancel')}
        </button>
      </div>
    )
  else
    body = (
      <>
        {state.kind === 'error' && (
          <div className="alert-danger mb-3" role="alert">
            <FailureMessage failure={state.failure} />
          </div>
        )}
        <div
          className={clsx('rounded-lg border-[1.5px] border-dashed bg-surface-2 px-4 py-5 text-center', over ? 'border-accent' : 'border-accent/45')}
          onDragOver={(e) => {
            e.preventDefault()
            setOver(true)
          }}
          onDragLeave={() => setOver(false)}
          onDrop={drop}
        >
          <div className="mb-2 text-[1.6rem] leading-none" aria-hidden>
            📷
          </div>
          {/* accept="image/*" offers the camera and the gallery on phones; `capture` would leave only the camera. */}
          <input ref={input} id={`${id}-file`} type="file" accept="image/*" className="sr-only" onChange={(e) => void read(e.target.files?.[0])} />
          <label htmlFor={`${id}-file`} className="btn-primary cursor-pointer">
            <span className="md:hidden">{t('receipt.choosePhone')}</span>
            <span className="max-md:hidden">{t('receipt.choose')}</span>
          </label>
          <p className="mt-2 text-[0.8rem] muted">
            <span className="md:hidden">{t('receipt.chooseHintPhone')}</span>
            <span className="max-md:hidden">{t('receipt.chooseHint')}</span>
          </p>
        </div>
        <p className="mt-3 text-xs muted">{t(target === 'form' ? 'receipt.fillsForm' : 'receipt.fillsText')}</p>
      </>
    )

  return (
    <section className={clsx('card border-accent/40', className)} aria-labelledby={`${id}-title`}>
      <div className="flex items-center gap-2 border-b border-line px-4 py-3">
        <h2 id={`${id}-title`} className="flex-1 font-bold">
          🧾 {t('receipt.title')}
        </h2>
        <Link to={closeTo} replace className="px-1 text-sm muted hover:text-fg" title={t('receipt.close')} aria-label={t('receipt.close')} onClick={() => abort.current?.abort()}>
          ✕
        </Link>
      </div>
      <div className="p-4">
        {body}
        <p className="mt-3 flex gap-1.5 text-[0.78rem] muted">
          <span aria-hidden>🔒</span>
          <span>{model ? <Trans i18nKey="receipt.privacy" values={{ model }} components={{ code: <code className="font-mono text-[0.95em]" /> }} /> : t('receipt.privacyNoModel')}</span>
        </p>
      </div>
    </section>
  )
}

/** The receipt's warnings (`warning.message`, the bot's wording), each dismissable. */
export function ReceiptWarnings({ warnings, onDismiss, className }: { warnings: ReceiptWarning[]; onDismiss: (index: number) => void; className?: string }) {
  const { t } = useTranslation()
  if (!warnings.length) return null
  return (
    <ul className={clsx('space-y-1.5', className)}>
      {warnings.map((w, i) => (
        <li key={`${w.code}-${i}`} className="flex items-start gap-2 rounded-md border border-accent/30 bg-accent-faint py-2 pr-2 pl-3 text-[0.84rem]" role="status">
          <span aria-hidden>⚠️</span>
          <span className="min-w-0 flex-1">{w.message}</span>
          <button type="button" className="px-1 muted hover:text-fg" title={t('receipt.dismiss')} aria-label={t('receipt.dismiss')} onClick={() => onDismiss(i)}>
            ✕
          </button>
        </li>
      ))}
    </ul>
  )
}
