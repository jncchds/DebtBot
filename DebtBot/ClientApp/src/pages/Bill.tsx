import { useState, type ReactNode } from 'react'
import { Link, useNavigate, useParams, useSearchParams } from 'react-router'
import { useTranslation } from 'react-i18next'
import { useAcceptExchange, useAddLines, useAddPayments, useBill, useBillAction } from '../api/bills'
import { ApiError } from '../api/client'
import type { Bill as BillModel } from '../api/types'
import { useAuth } from '../auth/AuthContext'
import { StatusPill } from '../components/bill/bits'
import { LineEditor } from '../components/bill/LineEditor'
import { PaymentEditor } from '../components/bill/PaymentEditor'
import { ErrorBox, Loading, Spinner } from '../components/ui'
import { canCancel, canChange, finalizeBlockers, firstLine, isEqualSplit, peopleCount, tipsUnits } from '../lib/bills'
import { useFormat } from '../lib/useFormat'
import { usePeople, useNameOf, type Person } from '../lib/usePeople'
import { PeoplePicker } from '../components/people/PeoplePicker'
import { POLL_MS, usePollWindow } from '../lib/usePollWindow'

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
 * A bill: /bills/<id>, with the editors at /bills/<id>/add-line and /bills/<id>/add-payment (mockups/bill).
 * Finalize, cancel and accept run in the background (T2), so the page polls until the status changes.
 */
export default function Bill({ mode }: { mode?: 'add-line' | 'add-payment' }) {
  const { t } = useTranslation()
  const { id = '' } = useParams()
  const navigate = useNavigate()
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
  const addLines = useAddLines(id)
  const addPayments = useAddPayments(id)
  const { people } = usePeople()
  const [search, setSearch] = useSearchParams()

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
  const editable = mine && bill.status === 'Draft' && !isExchange && !processing
  const blockers = finalizeBlockers(bill)
  const base = `/bills/${bill.id}`
  // People added to the bill but not on a line or payment yet live in the URL (?people=a,b), so reload keeps them.
  const extraIds = (search.get('people') ?? '').split(',').filter(Boolean)
  const qs = search.toString() ? `?${search}` : ''
  const backToBill = () => navigate(`${base}${qs}`, { replace: true })
  const onBill = billPeople(bill, user?.id)
  const byId = new Map(people.map((p) => [p.id, p]))
  const billPeopleList: Person[] = [...new Set([...onBill, ...extraIds])].map(
    (pid) => byId.get(pid) ?? { id: pid, name: names.long(findUser(bill, pid) ?? { id: pid, displayName: pid }), isMe: pid === user?.id },
  )
  const setExtras = (ids: string[]) =>
    setSearch(
      (prev) => {
        const next = new URLSearchParams(prev)
        if (ids.length) next.set('people', ids.join(','))
        else next.delete('people')
        return next
      },
      { replace: true },
    )

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
  if (mine && !processing) {
    if (bill.status === 'Draft' && !isExchange)
      actions.push(
        blockers.length > 0 && (
          <span key="hint" className="text-[0.8rem] muted">
            {t(blockers[0])}
          </span>
        ),
        <button key="fin" type="button" className="btn-primary" disabled={blockers.length > 0 || action.isPending} onClick={() => run('finalize', action.mutateAsync('Finalize'))}>
          ✓ {t('bill.finalize')}
        </button>,
      )
    if (canCancel(bill.status))
      actions.push(
        <button key="cancel" type="button" className="btn-danger" disabled={action.isPending} onClick={() => setConfirmCancel(true)}>
          {t(isExchange ? 'bill.cancelExchange' : 'bill.cancel')}
        </button>,
      )
  }

  return (
    <>
      <Link to="/bills" className="mb-3 inline-flex gap-1 text-sm muted hover:text-fg">
        ← {t('pages.bills.title')}
      </Link>
      <div className="flex flex-wrap items-center gap-3">
        <h1 className="min-w-0 text-[1.4rem] font-bold break-words">{title}</h1>
        <StatusPill status={bill.status} />
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

      {isExchange && (
        <ExchangeBanner
          bill={bill}
          other={other.data}
          canAccept={mine && bill.status === 'Draft' && !processing && other.data?.status === 'Draft'}
          busy={accept.isPending}
          onAccept={() => run('accept', accept.mutateAsync({ forwardBillId: bill.id, backwardBillId: bill.exchangeBillId! }))}
        />
      )}

      {editable && (
        <section className="card mb-4 px-4 py-3">
          <PeoplePicker
            people={people}
            selected={billPeopleList}
            locked={new Set(onBill)}
            onAdd={(p) => setExtras([...extraIds.filter((x) => x !== p.id), p.id])}
            onRemove={(p) => setExtras(extraIds.filter((x) => x !== p.id))}
          />
          <p className="mt-1.5 text-[0.8rem] muted">{t('people.onBillHint')}</p>
        </section>
      )}

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
            {editable && mode !== 'add-line' && (
              <Link to={`${base}/add-line${qs}`} className="btn-secondary px-2.5 py-1 text-[0.8rem]">
                ＋ {t('editor.addLine')}
              </Link>
            )}
          </div>
          {bill.lines.length === 0 && mode !== 'add-line' && <p className="px-4 py-3.5 text-sm muted">{t('bill.noLines')}</p>}
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
          {editable && mode === 'add-line' && (
            <div className="px-3 pb-3 md:px-4 md:pb-4">
              <LineEditor
                title={t('editor.addLine')}
                people={billPeopleList}
                currency={bill.currencyCode}
                busy={addLines.isPending}
                error={addLines.error && <div className="alert-danger">{message(addLines.error)}</div>}
                hint={tips === 0n ? t('editor.totalGrows') : t('editor.totalStays', { total: f.money(bill.totalWithTips, bill.currencyCode) })}
                onCancel={backToBill}
                onSubmit={(line) => addLines.mutate([line], { onSuccess: backToBill })}
              />
            </div>
          )}
        </section>

        <div className="grid gap-4 md:grid-cols-2">
          <section className="card">
            <div className="flex items-center gap-2 border-b border-line px-4 py-3">
              <h2 className="flex-1 font-bold">{t('bill.payments')}</h2>
              {editable && mode !== 'add-payment' && (
                <Link to={`${base}/add-payment${qs}`} className="btn-secondary px-2.5 py-1 text-[0.8rem]">
                  ＋ {t('editor.addPayment')}
                </Link>
              )}
            </div>
            {bill.payments.length === 0 && mode !== 'add-payment' && <p className="px-4 py-3.5 text-sm muted">{t('bill.noPayments')}</p>}
            <ul className="divide-y divide-line">
              {bill.payments.map((p) => (
                <li key={p.user.id} className="flex items-baseline gap-2 px-4 py-2.5 text-[0.9rem]">
                  <span className="flex-1">{names.long(p.user)}</span>
                  <span className="font-semibold tabular-nums whitespace-nowrap">{f.money(p.amount, bill.paymentCurrencyCode)}</span>
                </li>
              ))}
            </ul>
            {editable && mode === 'add-payment' && (
              <div className="px-3 pb-3 md:px-4 md:pb-4">
                <PaymentEditor
                  title={t('editor.addPayment')}
                  people={billPeopleList}
                  currency={bill.paymentCurrencyCode}
                  busy={addPayments.isPending}
                  error={addPayments.error && <div className="alert-danger">{message(addPayments.error)}</div>}
                  hint={t('editor.paymentReplaces')}
                  onCancel={backToBill}
                  onSubmit={(p) => addPayments.mutate([p], { onSuccess: backToBill })}
                />
              </div>
            )}
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
  )
}

/** One half of an exchange: who gives whom what, a link to the other half, and Accept. */
function ExchangeBanner({ bill, other, canAccept, busy, onAccept }: { bill: BillModel; other?: BillModel; canAccept: boolean; busy: boolean; onAccept: () => void }) {
  const { t } = useTranslation()
  const f = useFormat()
  const names = useNameOf()
  // Each half: the payer hands over the payment; the line's participant hands back the line's subtotal.
  const payment = bill.payments[0]
  const line = bill.lines[0]
  const taker = line?.participants[0]?.user
  return (
    <div className="mb-4 flex flex-wrap items-center gap-x-4 gap-y-3 rounded-[10px] border border-accent/40 bg-accent-faint p-4">
      <span className="text-[1.4rem]" aria-hidden>
        🔁
      </span>
      <div className="min-w-0 flex-[1_1_18rem] text-[0.9rem]">
        <b>{t('bill.exchangePair')}</b>{' '}
        {payment && line && taker
          ? t('bill.exchangeDeal', {
              giver: names.short(payment.user),
              taker: names.short(taker),
              give: f.money(payment.amount, bill.paymentCurrencyCode),
              back: f.money(line.subtotal, bill.currencyCode),
            })
          : null}
        <br />
        <span className="muted">{t('bill.otherHalf')}</span>{' '}
        <Link to={`/bills/${bill.exchangeBillId}`} className="link">
          {other ? `${firstLine(other.description)} · ${f.money(other.totalWithTips, other.currencyCode)} →` : `${t('bill.open')} →`}
        </Link>
      </div>
      {canAccept && (
        <button type="button" className="btn-primary" disabled={busy} onClick={onAccept}>
          ✓ {t('bill.acceptExchange')}
        </button>
      )}
    </div>
  )
}

function NotFoundBill() {
  const { t } = useTranslation()
  return (
    <>
      <Link to="/bills" className="mb-3 inline-flex gap-1 text-sm muted hover:text-fg">
        ← {t('pages.bills.title')}
      </Link>
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

/** Everyone already on the bill (you, line participants, payers), in that order, once each. */
function billPeople(bill: BillModel, meId: string | undefined): string[] {
  const ids = new Set<string>()
  if (meId) ids.add(meId)
  bill.lines.forEach((l) => l.participants.forEach((p) => ids.add(p.user.id)))
  bill.payments.forEach((p) => ids.add(p.user.id))
  return [...ids]
}

function findUser(bill: BillModel, id: string) {
  return bill.lines.flatMap((l) => l.participants.map((p) => p.user)).concat(bill.payments.map((p) => p.user)).find((u) => u.id === id)
}
