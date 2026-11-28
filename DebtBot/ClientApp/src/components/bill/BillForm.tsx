import { useId, useMemo, type Dispatch, type ReactNode, type SetStateAction } from 'react'
import { Link, useNavigate } from 'react-router'
import { useTranslation } from 'react-i18next'
import clsx from 'clsx'
import { useAllOwnDebts } from '../../api/bills'
import { isEqualSplit } from '../../lib/bills'
import { formatRate } from '../../lib/exchange'
import { formatNumber } from '../../lib/money'
import {
  billCurrency,
  checkNewBill,
  impliedRate,
  linesUnits,
  lockedPeople,
  paymentCurrency,
  paymentsUnits,
  previewBill,
  removePerson,
  totalUnits,
  withLine,
  withPayment,
  type FormEditor,
  type NewBillForm,
} from '../../lib/newBill'
import { useFormat } from '../../lib/useFormat'
import { useNameOf, type Person } from '../../lib/usePeople'
import { PeoplePicker } from '../people/PeoplePicker'
import { LineEditor } from './LineEditor'
import { PaymentEditor } from './PaymentEditor'

export interface BillFormProps {
  form: NewBillForm
  setForm: Dispatch<SetStateAction<NewBillForm>>
  /** `/bills/new`, or `/bills/<id>` for a draft; the editors live under it. */
  base: string
  editor?: FormEditor
  /** Show field errors (after a save was tried). */
  tried: boolean
  /** Who can be added: you and your contacts, plus anyone already on the bill. */
  people: Person[]
  /** A person on the bill by id (chips, lines, payments, preview). */
  personOf: (id: string) => Person
  /** Router state kept on the editor links (the list a bill was opened from). */
  linkState?: unknown
  /** Shown at the top of the Lines card (a receipt's warnings). */
  linesNotice?: ReactNode
  /** The actions card at the end. */
  children: ReactNode
}

/**
 * The bill form: Bill, People on this bill, Lines, Payments and the preview of who owes whom (mockups/new-bill,
 * series-2 mockups/creator-leave). New bill and a draft bill both use it; the caller owns the state and saving.
 * Everything here changes the form only.
 */
export function BillForm({ form, setForm, base, editor, tried, people, personOf, linkState, linesNotice, children }: BillFormProps) {
  const { t, i18n } = useTranslation()
  const f = useFormat()
  const navigate = useNavigate()
  const names = useNameOf()
  const debts = useAllOwnDebts()
  const id = useId()

  const check = checkNewBill(form)
  const cur = billCurrency(form)
  const payCur = paymentCurrency(form)
  const locked = lockedPeople(form)
  const onBill = form.people.map(personOf)
  const preview = useMemo(() => previewBill(form), [form])
  const currencies = [...new Set((debts.data ?? []).map((d) => d.currencyCode))].sort()
  const lines = linesUnits(form)
  const total = totalUnits(form)
  const tips = total !== null && total > lines ? total - lines : 0n
  const fx = payCur !== cur && !!cur && payCur.length === 3
  const rate = fx ? impliedRate(form) : null
  const editing = editor?.kind === 'edit-line' && editor.index < form.lines.length ? editor.index : -1

  const set = (patch: Partial<NewBillForm>) => setForm((x) => ({ ...x, ...patch }))
  const closeEditor = () => navigate(base, { replace: true, state: linkState })
  const fieldError = (key?: string) => tried && key && <p className="mt-1 text-xs text-danger">{t(key)}</p>
  const hint = (text: ReactNode) => <p className="mt-1 text-xs muted">{text}</p>
  const totalHint = form.total.trim() ? t('editor.totalStays', { total: total !== null && cur ? f.money(total, cur) : form.total }) : t('editor.totalGrows')

  return (
    <>
      <datalist id={`${id}-currencies`}>
        {currencies.map((c) => (
          <option key={c} value={c} />
        ))}
      </datalist>

      <div className="space-y-4 [&>*]:min-w-0">
        <section className="card">
          <h2 className="border-b border-line px-4 py-3 font-bold">{t('newBill.bill')}</h2>
          <div className="grid grid-cols-2 gap-x-4 gap-y-3.5 p-4 md:grid-cols-[2fr_1fr_1fr]">
            <div className="col-span-full">
              <label className="label" htmlFor={`${id}-desc`}>
                {t('newBill.description')}
              </label>
              <textarea id={`${id}-desc`} className="input resize-y" rows={2} maxLength={1000} value={form.description} onChange={(e) => set({ description: e.target.value })} />
              {hint(t('newBill.descriptionHint'))}
            </div>
            <div className="col-span-full md:col-span-1">
              <label className="label" htmlFor={`${id}-date`}>
                {t('newBill.date')}
              </label>
              <input id={`${id}-date`} type="datetime-local" className="input tabular-nums" value={form.date} onChange={(e) => set({ date: e.target.value })} />
              {fieldError(check.fields.date)}
            </div>
            <div>
              <label className="label" htmlFor={`${id}-cur`}>
                {t('newBill.currency')}
              </label>
              <input
                id={`${id}-cur`}
                className={clsx('input uppercase tabular-nums', tried && check.fields.currency && 'border-danger')}
                list={`${id}-currencies`}
                maxLength={3}
                placeholder="EUR"
                value={form.currency}
                onChange={(e) => set({ currency: e.target.value.toUpperCase() })}
              />
              {tried && check.fields.currency ? fieldError(check.fields.currency) : hint(t('newBill.currencyHint'))}
            </div>
            <div>
              <label className="label" htmlFor={`${id}-pay`}>
                {t('newBill.paidIn')}
              </label>
              <input
                id={`${id}-pay`}
                className={clsx('input uppercase tabular-nums', fx && 'border-accent/60 font-semibold text-accent', tried && check.fields.paymentCurrency && 'border-danger')}
                list={`${id}-currencies`}
                maxLength={3}
                placeholder={cur || 'EUR'}
                value={form.paymentCurrency}
                onChange={(e) => set({ paymentCurrency: e.target.value.toUpperCase() })}
              />
              {tried && check.fields.paymentCurrency ? fieldError(check.fields.paymentCurrency) : hint(fx ? t('newBill.paidInOther', { currency: payCur }) : t('newBill.paidInHint'))}
            </div>
            <div className="col-span-full md:col-span-1">
              <label className="label" htmlFor={`${id}-total`}>
                {t('newBill.total', { currency: cur || '…' })}
              </label>
              <input
                id={`${id}-total`}
                className={clsx('input text-right tabular-nums', tried && check.fields.total && 'border-danger')}
                inputMode="decimal"
                placeholder={formatNumber(lines, cur || 'EUR', i18n.language)}
                value={form.total}
                onChange={(e) => set({ total: e.target.value })}
              />
              {tried && check.fields.total
                ? fieldError(check.fields.total)
                : hint(
                    !form.total.trim()
                      ? t('newBill.totalEmpty', { total: cur ? f.money(lines, cur) : '0' })
                      : t('newBill.totalTips', { lines: cur ? f.money(lines, cur) : '…', tips: cur ? f.money(tips, cur) : '…' }),
                  )}
            </div>
          </div>
        </section>

        <section className="card px-4 py-3">
          <PeoplePicker
            people={people}
            selected={onBill}
            locked={locked}
            onAdd={(p) => set({ people: [...form.people.filter((x) => x !== p.id), p.id] })}
            onRemove={(p) => setForm((x) => removePerson(x, p.id))}
          />
          <p className="mt-1.5 text-[0.8rem] muted">{t('newBill.peopleHint')}</p>
        </section>

        <section className="card">
          <div className="flex flex-wrap items-center gap-2 border-b border-line px-4 py-3">
            <h2 className="flex-1 font-bold">{t('bill.lines')}</h2>
            {lines > 0n && cur && <span className="text-[0.85rem] tabular-nums muted">{f.money(lines, cur)}</span>}
            {editor?.kind !== 'add-line' && (
              <Link to={`${base}/add-line`} state={linkState} className="btn-secondary px-2.5 py-1 text-[0.8rem]">
                ＋ {t('editor.addLine')}
              </Link>
            )}
          </div>
          {linesNotice}
          {form.lines.length === 0 && editor?.kind !== 'add-line' && <p className="px-4 py-3.5 text-sm muted">{t('bill.noLines')}</p>}
          <ul className="divide-y divide-line">
            {form.lines.map((l, i) => {
              if (i === editing)
                return (
                  <li key={`edit-${i}`} className="px-3 py-3 md:px-4">
                    <LineEditor
                      title={t('editor.editLine')}
                      submitLabel={t('editor.saveLine')}
                      initial={l}
                      people={onBill}
                      currency={cur || '…'}
                      onCancel={closeEditor}
                      onSubmit={(line) => {
                        setForm((x) => withLine(x, i, line))
                        closeEditor()
                      }}
                    />
                  </li>
                )
              const equal = isEqualSplit(l.participants.map((p) => p.part ?? 1))
              return (
                <li key={i} className="flex flex-wrap items-start gap-x-3 px-4 py-3">
                  <div className="min-w-0 flex-1">
                    <b className="break-words">{l.itemDescription}</b> <span className="text-[0.8rem] whitespace-nowrap muted">{t(equal ? 'bill.splitEqually' : 'bill.byParts')}</span>
                  </div>
                  <span className="font-semibold tabular-nums whitespace-nowrap">{cur ? f.money(l.subtotal, cur) : l.subtotal}</span>
                  <Link
                    to={`${base}/edit-line/${i + 1}`}
                    replace={editor?.kind === 'edit-line'}
                    state={linkState}
                    className="px-1 text-sm muted hover:text-accent"
                    aria-label={t('newBill.editLine', { name: l.itemDescription })}
                    title={t('editor.editLine')}
                  >
                    ✎
                  </Link>
                  <button
                    type="button"
                    className="px-1 text-sm muted hover:text-danger"
                    aria-label={t('newBill.removeLine', { name: l.itemDescription })}
                    onClick={() => {
                      set({ lines: form.lines.filter((_, j) => j !== i) })
                      // An open editor's line number would now point at another line.
                      if (editing >= 0) closeEditor()
                    }}
                  >
                    ✕
                  </button>
                  {/* Full width under the line, so the people don't squeeze into a narrow column on phones. */}
                  <div className="mt-1.5 flex basis-full flex-wrap gap-1.5">
                    {l.participants.map((p) => (
                      <span key={p.userId} className="chip">
                        {personOf(p.userId).name}
                        {!equal && ` ×${p.part ?? 1}`}
                      </span>
                    ))}
                  </div>
                </li>
              )
            })}
          </ul>
          {editor?.kind === 'add-line' && (
            <div className="px-3 pb-3 md:px-4 md:pb-4">
              <LineEditor
                title={t('editor.addLine')}
                people={onBill}
                currency={cur || '…'}
                hint={totalHint}
                onCancel={closeEditor}
                onSubmit={(line) => {
                  set({ lines: [...form.lines, line] })
                  closeEditor()
                }}
              />
            </div>
          )}
        </section>

        <div className="grid gap-4 md:grid-cols-2 [&>*]:min-w-0">
          <section className="card">
            <div className="flex flex-wrap items-center gap-2 border-b border-line px-4 py-3">
              <h2 className="flex-1 font-bold">{t('bill.payments')}</h2>
              {form.payments.length > 0 && payCur && <span className="text-[0.85rem] tabular-nums muted">{f.money(paymentsUnits(form), payCur)}</span>}
              {editor?.kind !== 'add-payment' && (
                <Link to={`${base}/add-payment`} state={linkState} className="btn-secondary px-2.5 py-1 text-[0.8rem]">
                  ＋ {t('editor.addPayment')}
                </Link>
              )}
            </div>
            {form.payments.length === 0 && editor?.kind !== 'add-payment' && <p className="px-4 py-3.5 text-sm muted">{t('newBill.noPayments')}</p>}
            <ul className="divide-y divide-line">
              {form.payments.map((p) => (
                <li key={p.userId} className="flex items-baseline gap-2 px-4 py-2.5 text-[0.9rem]">
                  <span className="min-w-0 flex-1">{personOf(p.userId).name}</span>
                  <span className="font-semibold tabular-nums whitespace-nowrap">{payCur ? f.money(p.amount, payCur) : p.amount}</span>
                  <button type="button" className="px-1 text-sm muted hover:text-danger" aria-label={t('newBill.removePayment', { name: personOf(p.userId).name })} onClick={() => set({ payments: form.payments.filter((x) => x.userId !== p.userId) })}>
                    ✕
                  </button>
                </li>
              ))}
            </ul>
            {fx && rate && total !== null && (
              <div className="px-4 pb-3.5">
                <div className="rounded-md border border-accent/30 bg-accent-faint px-3 py-2 text-[0.82rem]">
                  💱 {t('newBill.paidAs', { total: f.money(total, cur), paid: f.money(paymentsUnits(form), payCur) })}
                  <br />
                  <b className="tabular-nums whitespace-nowrap">1 {payCur} = {formatRate(rate, i18n.language)} {cur}</b> ·{' '}
                  <b className="tabular-nums whitespace-nowrap">1 {cur} = {formatRate(1 / rate, i18n.language)} {payCur}</b>
                </div>
              </div>
            )}
            {editor?.kind === 'add-payment' && (
              <div className="px-3 pb-3 md:px-4 md:pb-4">
                <PaymentEditor
                  title={t('editor.addPayment')}
                  people={onBill}
                  currency={payCur || '…'}
                  hint={t('editor.paymentReplaces')}
                  onCancel={closeEditor}
                  onSubmit={(p) => {
                    setForm((x) => withPayment(x, p))
                    closeEditor()
                  }}
                />
              </div>
            )}
          </section>

          <PreviewCard preview={preview} fx={fx} cur={cur} payCur={payCur} tips={tips} personName={(pid) => personOf(pid).name} isMe={names.isMe} />
        </div>

        {children}
      </div>
    </>
  )
}

/** Who owes whom, worked out in the browser as a preview (lib/newBill.ts previewBill). A table when wide, blocks on a phone. */
function PreviewCard({
  preview,
  fx,
  cur,
  payCur,
  tips,
  personName,
  isMe,
}: {
  preview: ReturnType<typeof previewBill>
  fx: boolean
  cur: string
  payCur: string
  tips: bigint
  personName: (id: string) => string
  isMe: (id: string) => boolean
}) {
  const { t, i18n } = useTranslation()
  const f = useFormat()
  const n = (u: bigint, c: string) => formatNumber(u, c, i18n.language)
  const sentence = (d: { debtorId: string; creditorId: string }) =>
    isMe(d.creditorId)
      ? t('newBill.owesYou', { name: personName(d.debtorId) })
      : isMe(d.debtorId)
        ? t('bill.youOwe', { name: personName(d.creditorId) })
        : t('bill.owes', { debtor: personName(d.debtorId), creditor: personName(d.creditorId) })
  const heads = fx ? [t('newBill.shareIn', { currency: cur }), t('newBill.shareIn', { currency: payCur }), t('newBill.paidCol'), t('newBill.balanceIn', { currency: payCur })] : [t('newBill.share'), t('newBill.paidCol'), t('newBill.balance')]
  const values = (p: NonNullable<typeof preview>['people'][number]) =>
    fx ? [n(p.shareBill, cur), n(p.sharePay, payCur), p.paid ? n(p.paid, payCur) : '—'] : [n(p.sharePay, payCur), p.paid ? n(p.paid, payCur) : '—']
  const balance = (u: bigint) => (
    <span className={clsx('font-semibold tabular-nums', u > 0n ? 'text-success' : u < 0n ? 'text-danger' : 'muted')}>{u === 0n ? '—' : `${u > 0n ? '+' : '−'}${n(u < 0n ? -u : u, payCur)}`}</span>
  )

  return (
    <section className="card">
      <div className="flex items-center gap-2 border-b border-line px-4 py-3">
        <h2 className="flex-1 font-bold">{t('bill.ledger')}</h2>
        <span className="pill-muted">{t('newBill.preview')}</span>
      </div>
      {!preview || !payCur ? (
        <p className="px-4 py-3.5 text-sm muted">{t('newBill.previewLater')}</p>
      ) : (
        <>
          <ul className="divide-y divide-line">
            {preview.debts.map((d) => (
              <li key={`${d.debtorId}:${d.creditorId}`} className="flex items-baseline gap-2 px-4 py-2.5 text-[0.9rem]">
                <span className="min-w-0 flex-1">{sentence(d)}</span>
                <span className={clsx('font-bold tabular-nums whitespace-nowrap', isMe(d.creditorId) ? 'text-success' : isMe(d.debtorId) ? 'text-danger' : '')}>{f.money(d.amount, payCur)}</span>
              </li>
            ))}
          </ul>
          {/* Wide: a table. Narrow (the card itself, so it follows the two-column grid too): one block per person. */}
          <div className="@container border-t border-line">
            <table className="hidden w-full text-sm @[26rem]:table">
              <thead>
                <tr className="border-b border-line">
                  <th className="px-4 py-2 text-left text-[0.72rem] font-semibold tracking-wide text-muted uppercase">{t('newBill.person')}</th>
                  {heads.map((h) => (
                    <th key={h} className="px-4 py-2 text-right text-[0.72rem] font-semibold tracking-wide whitespace-nowrap text-muted uppercase">
                      {h}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-line">
                {preview.people.map((p) => (
                  <tr key={p.userId}>
                    <td className="px-4 py-2">{personName(p.userId)}</td>
                    {values(p).map((v, i) => (
                      <td key={i} className="px-4 py-2 text-right tabular-nums">
                        {v}
                      </td>
                    ))}
                    <td className="px-4 py-2 text-right">{balance(p.balance)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
            <ul className="divide-y divide-line @[26rem]:hidden">
              {preview.people.map((p) => (
                <li key={p.userId} className="px-4 py-2.5">
                  <div className="flex items-baseline gap-2">
                    <span className="min-w-0 flex-1">{personName(p.userId)}</span>
                    {balance(p.balance)}
                  </div>
                  <div className="mt-0.5 flex flex-wrap gap-x-3 text-[0.8rem] muted">
                    {values(p).map((v, i) => (
                      <span key={i}>
                        {heads[i]} <span className="text-fg tabular-nums">{v}</span>
                      </span>
                    ))}
                  </div>
                </li>
              ))}
            </ul>
          </div>
          <p className="px-4 pt-1 pb-3 text-xs muted">
            {fx ? t('newBill.previewFx', { bill: cur, pay: payCur }) : tips > 0n ? t('newBill.previewTips', { tips: f.money(tips, cur) }) : t('newBill.previewNote')}
            {' '}{t('newBill.previewServer')}
          </p>
        </>
      )}
    </section>
  )
}
