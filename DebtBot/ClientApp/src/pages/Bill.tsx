import { useEffect, useState, type ReactNode } from 'react'
import { Link, useLocation, useParams } from 'react-router'
import { useTranslation } from 'react-i18next'
import { useAcceptExchange, useBill, useBillAction, useReplaceBill } from '../api/bills'
import { ApiError } from '../api/client'
import type { Bill as BillModel, UserDisplay } from '../api/types'
import { useAuth } from '../auth/AuthContext'
import { StatusPill } from '../components/bill/bits'
import { BillForm } from '../components/bill/BillForm'
import { ErrorBox, Loading, Spinner } from '../components/ui'
import { canCancel, canChange, firstLine, isEqualSplit, peopleCount, tipsUnits } from '../lib/bills'
import { checkNewBill, draftChanged, draftKey, formEditor, fromBill, loadDraft, store, toBillCreation, type FormEditor, type NewBillForm, type StoredDraft } from '../lib/newBill'
import { useFormat } from '../lib/useFormat'
import { usePeople, useNameOf, type Person } from '../lib/usePeople'
import { ExchangeBanner, ExchangeStatusPill } from '../components/exchange/ExchangeBits'
import { dealFromBill, orientDeal } from '../lib/exchange'
import { POLL_MS, usePollWindow } from '../lib/usePollWindow'
import { backTarget } from '../lib/backToList'
import { useOrigin } from '../lib/useOrigin'

/** What the page is waiting for after an action that finishes in the background. */
type Waiting = { action: 'finalize' | 'cancel' | 'accept'; until: number }

const done = (w: Waiting, bill: BillModel) => (w.action === 'cancel' ? bill.status === 'Cancelled' : bill.status === 'Processed' || bill.status === 'Cancelled')

/** A friendly message for an action's error: the server's own, or one for 403/404. */
function useActionError() {
  const { t } = useTranslation()
  return (e: unknown) => {
    if (e instanceof ApiError) {
      if (e.status === 403) return e.serverMessage ?? t('bill.forbidden')
      if (e.status === 404) return t('bill.notFoundHint')
      if (e.serverMessage) return e.serverMessage
    }
    return e instanceof Error ? e.message : String(e)
  }
}

/**
 * A bill: /bills/<id> (mockups/bill). A draft you can change is the New bill form (DraftForm), with its editors at
 * /bills/<id>/add-line, /add-payment and /edit-line/<n>.
 * Finalize, cancel and accept run in the background (T2), so the page polls until the status changes.
 */
export default function Bill({ mode }: { mode?: 'add-line' | 'add-payment' | 'edit-line' }) {
  const { t } = useTranslation()
  const { id = '', n } = useParams()
  const f = useFormat()
  const names = useNameOf()
  const { user, isAdmin } = useAuth()
  const [waiting, setWaiting] = useState<Waiting | null>(null)
  const [readyUntil] = useState(() => Date.now() + POLL_MS)
  const [confirmCancel, setConfirmCancel] = useState(false)
  const [error, setError] = useState<string>()
  const message = useActionError()

  const pollWindow = usePollWindow(waiting?.until ?? readyUntil)
  const first = useBill(id)
  const bill = first.data
  // Poll while an action of ours is pending, or while the bill is "Ready" (someone just finalized it).
  const poll = pollWindow && !!bill && (waiting ? !done(waiting, bill) : bill.status === 'Ready')
  useBill(id, poll)
  const other = useBill(bill?.exchangeBillId ?? undefined)

  const action = useBillAction(id)
  const accept = useAcceptExchange()
  // The list this bill was opened from; kept on the add-line/add-payment steps so "← Bills" still goes back to it.
  const origin = useOrigin()

  if (first.isLoading) return <Loading />
  if (first.error) {
    if (first.error instanceof ApiError && first.error.status === 404) return <NotFoundBill />
    return <ErrorBox error={first.error} />
  }
  if (!bill) return <NotFoundBill />

  if (waiting && done(waiting, bill)) setWaiting(null)
  const stuck = !!waiting && !pollWindow && !done(waiting, bill)
  const processing = (!!waiting && !stuck) || bill.status === 'Ready'

  const mine = canChange(bill, user?.id, isAdmin)
  const isExchange = !!bill.exchangeBillId
  const rawDeal = isExchange ? dealFromBill(bill) : null
  const deal = rawDeal && orientDeal(rawDeal, user?.id)
  const editable = mine && bill.status === 'Draft' && !isExchange && !processing

  const run = (kind: Waiting['action'], p: Promise<unknown>) => {
    setError(undefined)
    p.then(
      () => setWaiting({ action: kind, until: Date.now() + POLL_MS }),
      (e) => setError(message(e)),
    )
  }

  const title = firstLine(bill.description) || t('bill.untitled')
  const rest = bill.description.split('\n').slice(1).join('\n').trim()
  const tips = tipsUnits(bill)

  const actions: ReactNode[] = []
  // A draft you can change is the New bill form (series-2 mockups/creator-leave): Finalize is in its actions.
  if (mine && !processing) {
    if (canCancel(bill.status))
      actions.push(
        <button key="cancel" type="button" className="btn-danger" disabled={action.isPending} onClick={() => setConfirmCancel(true)}>
          {t(isExchange ? 'bill.cancelExchange' : 'bill.cancel')}
        </button>,
      )
  }

  return (
    <>
      <BackToList />
      <div className="flex flex-wrap items-center gap-3">
        <h1 className="min-w-0 text-[1.4rem] font-bold break-words">{title}</h1>
        {isExchange ? <ExchangeStatusPill status={bill.status} /> : <StatusPill status={bill.status} />}
        <span className="flex-1" />
        {actions}
      </div>
      <p className="mt-1 mb-6 text-sm muted">{t('bill.subtitle', { date: f.dateTime(bill.date), name: names.short(bill.creator) })}</p>
      {rest && <p className="-mt-4 mb-5 text-sm whitespace-pre-line muted">{rest}</p>}

      {confirmCancel && (
        <div className="alert-danger mb-4 flex flex-wrap items-center gap-3" role="alertdialog" aria-label={t('bill.cancelConfirm')}>
          <span className="flex-1">{t(isExchange ? 'bill.cancelExchangeConfirm' : 'bill.cancelConfirm')}</span>
          <button
            type="button"
            className="btn-danger"
            onClick={() => {
              setConfirmCancel(false)
              run('cancel', action.mutateAsync('Cancel'))
            }}
          >
            {t('bill.cancelYes')}
          </button>
          <button type="button" className="btn-ghost" onClick={() => setConfirmCancel(false)}>
            {t('bill.cancelNo')}
          </button>
        </div>
      )}
      {error && (
        <div className="alert-danger mb-4" role="alert">
          {error}
        </div>
      )}
      {processing && (
        <div className="mb-4 flex items-center gap-2.5 rounded-md border border-accent/30 bg-accent-faint px-3 py-2.5 text-sm" role="status">
          <Spinner className="h-4 w-4" />
          <span>
            <b>{t(waiting?.action === 'cancel' ? 'bill.cancelling' : 'bill.finalizing')}</b> {t('bill.processingHint')}
          </span>
        </div>
      )}
      {stuck && (
        <div className="alert-danger mb-4" role="status">
          {t('bill.stuck')}{' '}
          <button type="button" className="underline" onClick={() => first.refetch()}>
            {t('bill.refresh')}
          </button>
        </div>
      )}

      {deal && (
        <ExchangeBanner
          deal={deal}
          status={bill.status}
          actions={
            mine &&
            bill.status === 'Draft' &&
            !processing &&
            other.data?.status === 'Draft' && (
              <button
                type="button"
                className="btn-primary"
                disabled={accept.isPending}
                onClick={() => run('accept', accept.mutateAsync({ forwardBillId: bill.id, backwardBillId: bill.exchangeBillId! }))}
              >
                ✓ {t('exchange.accept')}
              </button>
            )
          }
        />
      )}

      {editable ? (
        <DraftForm
          bill={bill}
          editor={formEditor(mode, n)}
          linkState={origin}
          onError={setError}
          onFinalizing={() => setWaiting({ action: 'finalize', until: Date.now() + POLL_MS })}
        />
      ) : (
      <>
      <dl className="card mb-4 grid grid-cols-2 md:grid-cols-4">
        {[
          [t('bill.total'), f.money(bill.totalWithTips, bill.currencyCode)],
          [t('bill.paidIn'), bill.paymentCurrencyCode],
          [t('bill.tips'), tips > 0n ? f.money(tips, bill.currencyCode) : '—'],
          [t('bill.people'), String(peopleCount(bill))],
        ].map(([k, v], i) => (
          <div key={k} className={`border-line px-4 py-3 ${['', 'border-l', 'max-md:border-t md:border-l', 'border-l max-md:border-t'][i]}`}>
            <dt className="text-[0.72rem] font-semibold tracking-wide text-muted uppercase">{k}</dt>
            <dd className="mt-0.5 font-bold tabular-nums">{v}</dd>
          </div>
        ))}
      </dl>

      <div className="space-y-4">
        <section className="card">
          <div className="flex items-center gap-2 border-b border-line px-4 py-3">
            <h2 className="flex-1 font-bold">{t('bill.lines')}</h2>
          </div>
          {bill.lines.length === 0 && <p className="px-4 py-3.5 text-sm muted">{t('bill.noLines')}</p>}
          <ul className="divide-y divide-line">
            {bill.lines.map((l) => {
              const parts = l.participants.map((p) => p.part)
              const equal = isEqualSplit(parts)
              return (
                <li key={l.id} className="flex items-start gap-4 px-4 py-3">
                  <div className="min-w-0 flex-1">
                    <b className="break-words">{l.itemDescription}</b> <span className="text-[0.8rem] muted">{t(equal ? 'bill.splitEqually' : 'bill.byParts')}</span>
                    <div className="mt-1.5 flex flex-wrap gap-1.5">
                      {l.participants.map((p) => (
                        <span key={p.user.id} className="chip">
                          {names.long(p.user)}
                          {!equal && ` ×${p.part}`}
                        </span>
                      ))}
                    </div>
                  </div>
                  <span className="font-semibold tabular-nums whitespace-nowrap">{f.money(l.subtotal, bill.currencyCode)}</span>
                </li>
              )
            })}
          </ul>
        </section>

        <div className="grid gap-4 md:grid-cols-2">
          <section className="card">
            <div className="flex items-center gap-2 border-b border-line px-4 py-3">
              <h2 className="flex-1 font-bold">{t('bill.payments')}</h2>
            </div>
            {bill.payments.length === 0 && <p className="px-4 py-3.5 text-sm muted">{t('bill.noPayments')}</p>}
            <ul className="divide-y divide-line">
              {bill.payments.map((p) => (
                <li key={p.user.id} className="flex items-baseline gap-2 px-4 py-2.5 text-[0.9rem]">
                  <span className="flex-1">{names.long(p.user)}</span>
                  <span className="font-semibold tabular-nums whitespace-nowrap">{f.money(p.amount, bill.paymentCurrencyCode)}</span>
                </li>
              ))}
            </ul>
          </section>

          <section className="card">
            <div className="border-b border-line px-4 py-3">
              <h2 className="font-bold">{t('bill.ledger')}</h2>
            </div>
            {bill.ledgerRecords.length === 0 ? (
              <p className="px-4 py-3.5 text-sm muted">{t(processing ? 'bill.ledgerWriting' : 'bill.ledgerLater')}</p>
            ) : (
              <>
                {bill.status === 'Cancelled' && <p className="px-4 pt-3 text-[0.8rem] text-danger">{t('bill.ledgerCancelled')}</p>}
                <ul className={`divide-y divide-line ${bill.status === 'Cancelled' ? 'line-through opacity-60' : ''}`}>
                  {bill.ledgerRecords.map((r) => {
                    const cls = names.isMe(r.creditorUser.id) ? 'text-success' : names.isMe(r.debtorUser.id) ? 'text-danger' : ''
                    return (
                      <li key={`${r.creditorUser.id}:${r.debtorUser.id}:${r.currencyCode}`} className="flex items-baseline gap-2 px-4 py-2.5 text-[0.9rem]">
                        <span className="flex-1">
                          {names.isMe(r.debtorUser.id)
                            ? t('bill.youOwe', { name: names.short(r.creditorUser) })
                            : t('bill.owes', { debtor: names.short(r.debtorUser), creditor: names.short(r.creditorUser) })}
                        </span>
                        <span className={`font-bold tabular-nums whitespace-nowrap ${cls}`}>{f.money(r.amount, r.currencyCode)}</span>
                      </li>
                    )
                  })}
                </ul>
              </>
            )}
          </section>
        </div>
      </div>
      </>
      )}
    </>
  )
}

/** "← Bills": back to the list the bill was opened from, with its filters and `?of=`, else your own bills. */
function BackToList() {
  const { t } = useTranslation()
  const back = backTarget(useLocation().state)
  return (
    <Link to={back.to} className="mb-3 inline-flex gap-1 text-sm muted hover:text-fg">
      ← {t(back.label)}
    </Link>
  )
}

function NotFoundBill() {
  const { t } = useTranslation()
  return (
    <>
      <BackToList />
      <div className="card mt-2 px-5 py-10 text-center">
        <div className="mb-2 text-[2rem]" aria-hidden>
          🔍
        </div>
        <p className="font-semibold">{t('bill.notFound')}</p>
        <p className="mt-1 text-sm muted">{t('bill.notFoundHint')}</p>
      </div>
    </>
  )
}

/** Everyone the saved bill names: its creator, line participants and payers, once each. */
function billUsers(bill: BillModel): UserDisplay[] {
  const all = [bill.creator, ...bill.lines.flatMap((l) => l.participants.map((p) => p.user)), ...bill.payments.map((p) => p.user)]
  return [...new Map(all.map((u) => [u.id, u])).values()]
}

/**
 * A draft you can change, as the New bill form prefilled from it (series-2 mockups/creator-leave). Every change
 * stays in the form, kept in this tab, until Save (`PUT Bills/{id}` replaces the draft) or Save and finalize.
 * The editors are at /bills/<id>/add-line, /add-payment and /edit-line/<n>.
 */
function DraftForm({
  bill,
  editor,
  linkState,
  onError,
  onFinalizing,
}: {
  bill: BillModel
  editor?: FormEditor
  linkState?: unknown
  onError: (message?: string) => void
  onFinalizing: () => void
}) {
  const { t } = useTranslation()
  const names = useNameOf()
  const { user } = useAuth()
  const { people } = usePeople()
  const message = useActionError()
  const replace = useReplaceBill(bill.id)
  const [form, setForm] = useState<NewBillForm>(() => loadDraft(bill))
  const [tried, setTried] = useState(false)
  const base = JSON.stringify(fromBill(bill))

  // The form is kept in the tab together with the saved version it's based on (lib/newBill.ts loadDraft).
  useEffect(() => store(draftKey(bill.id), { base, form } satisfies StoredDraft), [bill.id, base, form])

  // Anyone on the saved bill can be put back, also people who aren't your contacts (an admin editing someone's
  // draft can add its creator back).
  const known = new Set(people.map((p) => p.id))
  const fromBillUsers: Person[] = billUsers(bill)
    .filter((u) => !known.has(u.id))
    .map((u) => ({ id: u.id, name: u.id === user?.id ? t('common.youNamed', { name: u.displayName }) : names.long(u), isMe: u.id === user?.id }))
  const pickable = [...people, ...fromBillUsers]
  const byId = new Map(pickable.map((p) => [p.id, p]))
  const personOf = (pid: string): Person => byId.get(pid) ?? { id: pid, name: '…', isMe: pid === user?.id }

  const check = checkNewBill(form)
  const changed = draftChanged(form, bill)
  const busy = replace.isPending

  const save = (finalize: boolean) => {
    setTried(true)
    onError(undefined)
    if (!check.canSave || (finalize && check.finalizeBlockers.length > 0)) return
    replace.mutate(
      { bill: changed ? toBillCreation(form) : undefined, finalize },
      {
        onSuccess: () => {
          setTried(false)
          if (finalize) {
            store(draftKey(bill.id), undefined)
            onFinalizing()
          }
        },
        onError: (e) => onError(message(e)),
      },
    )
  }

  return (
    <BillForm form={form} setForm={setForm} base={`/bills/${bill.id}`} editor={editor} tried={tried} people={pickable} personOf={personOf} linkState={linkState}>
      <section className="card">
        {tried && !check.canSave && (
          <div className="px-4 pt-3.5">
            <div className="alert-danger" role="alert">
              <b>{t('newBill.cantSave')}</b> {t('newBill.fixFields')}
            </div>
          </div>
        )}
        <div className="flex flex-wrap items-center gap-2 px-4 py-3.5">
          <span className="text-[0.8rem] muted">{t(changed ? 'newBill.unsaved' : 'newBill.saved')}</span>
          {changed && (
            <button
              type="button"
              className="btn-ghost px-2 py-1 text-[0.8rem]"
              disabled={busy}
              onClick={() => {
                setForm(fromBill(bill))
                setTried(false)
                onError(undefined)
              }}
            >
              {t('newBill.discard')}
            </button>
          )}
          <span className="flex-1" />
          {busy && (
            <span className="inline-flex items-center gap-2 text-sm" role="status">
              <Spinner className="h-4 w-4" />
              {t('newBill.saving')}
            </span>
          )}
          {!busy && check.canSave && check.finalizeBlockers.length > 0 && <span className="text-[0.8rem] muted">{t(check.finalizeBlockers[0])}</span>}
          <div className="flex flex-wrap gap-2 max-md:w-full max-md:[&>*]:flex-1">
            <button type="button" className="btn-secondary" disabled={busy || !changed} onClick={() => save(false)}>
              {t('newBill.save')}
            </button>
            <button type="button" className="btn-primary" disabled={busy || (check.canSave && check.finalizeBlockers.length > 0)} onClick={() => save(true)}>
              ✓ {t(changed ? 'newBill.saveFinalize' : 'bill.finalize')}
            </button>
          </div>
        </div>
      </section>
    </BillForm>
  )
}
