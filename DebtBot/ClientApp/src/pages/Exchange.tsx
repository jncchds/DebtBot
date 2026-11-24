import { useEffect, useId, useMemo, useState, type ReactNode } from 'react'
import { useSearchParams } from 'react-router'
import { useTranslation } from 'react-i18next'
import clsx from 'clsx'
import { useAcceptExchange, useAllOwnDebts, useBill, useCancelBill, useCreateExchange, useExchanges } from '../api/bills'
import { ApiError } from '../api/client'
import type { ExchangeListItem } from '../api/types'
import { useAuth } from '../auth/AuthContext'
import { DealText, ExchangeBanner, ExchangeStatusPill, RateBothWays } from '../components/exchange/ExchangeBits'
import { FilterChoice, FilterCurrency, FilterPerson } from '../components/list/filters'
import { ListView, type Column } from '../components/list/ListView'
import { PersonSearch } from '../components/people/PersonSearch'
import { PageTitle, Spinner } from '../components/ui'
import { canChange } from '../lib/bills'
import {
  dealFromBill,
  dealFromListItem,
  emptyExchangeForm,
  exchangeAmounts,
  exchangeProblems,
  orientDeal,
  swapSides,
  toExchangeCreation,
  type ExchangeForm,
  type RateSide,
} from '../lib/exchange'
import { withFilter } from '../lib/listState'
import { exchangesQuery, exchangesSpec, PAGE_SIZE } from '../lib/lists'
import { currencyDigits, fromUnits } from '../lib/money'
import { loadStored, store } from '../lib/newBill'
import { useFormat } from '../lib/useFormat'
import { useListState } from '../lib/useListState'
import { useNameOf, usePeople } from '../lib/usePeople'
import { POLL_MS, usePollWindow } from '../lib/usePollWindow'

const EXCHANGE_KEY = 'debtbot.exchange'

/**
 * 🔁 Exchange: /exchange (mockups/exchange). An exchange swaps amounts in two currencies between two people:
 * two draft bills, accepted with a separate step. The form's person and rate side are in the URL
 * (`with`, `rate=give|get`), the draft just made is `draft=<billId>`, and the list below keeps its own
 * filters, sort and page (exchangesSpec). The typed amounts are kept in sessionStorage.
 */
export default function Exchange() {
  const { t } = useTranslation()
  const [search, setSearch] = useSearchParams()
  const draft = search.get('draft') ?? undefined

  const setParams = (patch: Record<string, string | null>, replace = false) =>
    setSearch(
      (prev) => {
        const next = new URLSearchParams(prev)
        for (const [k, v] of Object.entries(patch)) {
          if (v) next.set(k, v)
          else next.delete(k)
        }
        return next
      },
      { replace },
    )

  return (
    <>
      <PageTitle subtitle={t('exchange.subtitle')}>{t('pages.exchange.title')}</PageTitle>
      {draft && <DraftPanel key={draft} billId={draft} onClose={() => setParams({ draft: null })} />}
      <NewExchange
        otherId={search.get('with') ?? undefined}
        side={(['give', 'get'] as const).find((s) => s === search.get('rate')) ?? null}
        setParams={setParams}
      />
      <h2 className="mt-6 mb-3 text-[1.1rem] font-bold">{t('exchange.list')}</h2>
      <ExchangeList />
    </>
  )
}

/** The New exchange card. */
function NewExchange({ otherId, side, setParams }: { otherId?: string; side: RateSide; setParams: (patch: Record<string, string | null>, replace?: boolean) => void }) {
  const { t } = useTranslation()
  const id = useId()
  const f = useFormat()
  const { user } = useAuth()
  const { people } = usePeople()
  const debts = useAllOwnDebts()
  const create = useCreateExchange()
  const [form, setForm] = useState<ExchangeForm>(() => loadStored(EXCHANGE_KEY, emptyExchangeForm()))
  const [tried, setTried] = useState(false)
  const [error, setError] = useState<string>()
  useEffect(() => store(EXCHANGE_KEY, form), [form])

  const others = people.filter((p) => !p.isMe)
  const other = others.find((p) => p.id === otherId)
  const amounts = exchangeAmounts(form, side)
  const problems = exchangeProblems(form, side, other?.id)
  const valid = Object.keys(problems).length === 0
  const currencies = [...new Set((debts.data ?? []).map((d) => d.currencyCode))].sort()
  const set = (patch: Partial<ExchangeForm>) => setForm((x) => ({ ...x, ...patch }))
  const giveCur = form.giveCurrency.trim().toUpperCase()
  const getCur = form.getCurrency.trim().toUpperCase()
  const err = (key?: string) => tried && key && <p className="mt-1 text-xs text-danger">{t(key)}</p>

  // Only one side can be a rate: choosing Rate on one side switches the other back to Amount.
  const setSide = (s: RateSide) => {
    if (side && s !== side) {
      // The side leaving rate mode keeps the amount that was worked out for it.
      const known = exchangeAmounts(form, side)
      if (side === 'give' && known.give !== null) set({ giveAmount: String(fromUnits(known.give)) })
      if (side === 'get' && known.get !== null) set({ getAmount: String(fromUnits(known.get)) })
    }
    setParams({ rate: s })
  }

  const submit = () => {
    setTried(true)
    setError(undefined)
    if (!valid || !user || !other) return
    const body = toExchangeCreation(user.id, other.id, form, side)
    if (!body) return
    create.mutate(body, {
      onSuccess: (pair) => {
        setForm(emptyExchangeForm())
        setTried(false)
        setParams({ draft: pair.forwardBillId, with: null, rate: null })
      },
      onError: (e) => setError(e instanceof ApiError && e.serverMessage ? e.serverMessage : e.message),
    })
  }

  const modeSwitch = (which: 'give' | 'get') => (
    <span className="inline-flex gap-1" role="group" aria-label={t('exchange.modeFor', { side: t(which === 'give' ? 'exchange.youGive' : 'exchange.theyGive', { name: other?.name ?? t('exchange.they') }) })}>
      {(['amount', 'rate'] as const).map((m) => {
        const on = m === 'rate' ? side === which : side !== which
        return (
          <button
            key={m}
            type="button"
            aria-pressed={on}
            onClick={() => setSide(m === 'rate' ? which : side === which ? null : side)}
            className={clsx('rounded-full border px-2.5 py-0.5 text-[0.8rem]', on ? 'border-accent/40 bg-accent-faint font-semibold text-accent' : 'border-line text-muted hover:border-accent')}
          >
            {t(m === 'rate' ? 'exchange.rateMode' : 'exchange.amountMode')}
          </button>
        )
      })}
    </span>
  )

  const currencyInput = (which: 'give' | 'get', className?: string) => (
    <input
      className={clsx('input w-20 shrink-0 uppercase tabular-nums', tried && problems[which === 'give' ? 'giveCurrency' : 'getCurrency'] && 'border-danger', className)}
      aria-label={t('exchange.currencyOf', { side: t(which === 'give' ? 'exchange.youGive' : 'exchange.theyGive', { name: other?.name ?? t('exchange.they') }) })}
      list={`${id}-currencies`}
      maxLength={3}
      placeholder={which === 'give' ? 'EUR' : 'PLN'}
      value={which === 'give' ? form.giveCurrency : form.getCurrency}
      onChange={(e) => set(which === 'give' ? { giveCurrency: e.target.value.toUpperCase() } : { getCurrency: e.target.value.toUpperCase() })}
    />
  )

  /** "1 [EUR] = [4.30] PLN": the rate reads from `flip`; this side's own currency box sits where its currency is. */
  const rateRow = (which: 'give' | 'get') => {
    const first = form.flip ? 'get' : 'give'
    const second = form.flip ? 'give' : 'get'
    const label = (w: 'give' | 'get') => (w === which ? currencyInput(w) : <span className="shrink-0 text-sm tabular-nums muted">{(w === 'give' ? giveCur : getCur) || '…'}</span>)
    const worked = which === 'give' ? amounts.give : amounts.get
    const cur = which === 'give' ? giveCur : getCur
    return (
      <>
        <div className="flex items-center gap-2">
          <span className="text-sm muted">1</span>
          {label(first)}
          <span className="muted">=</span>
          <input
            className={clsx('input min-w-0 flex-1 text-right tabular-nums', tried && problems.rate && 'border-danger')}
            inputMode="decimal"
            aria-label={t('exchange.rateInput')}
            placeholder="0.00"
            value={form.rate}
            onChange={(e) => set({ rate: e.target.value })}
          />
          {label(second)}
        </div>
        <div className="mt-2 flex flex-wrap items-center gap-x-2 gap-y-1 text-[0.85rem]">
          {worked !== null && cur ? (
            <>
              <span className="muted">{t('exchange.thats')}</span>
              <b className="tabular-nums">{f.money(worked, cur)}</b>
              <span className="text-[0.76rem] muted">{t('exchange.rounded', { step: (10 ** -currencyDigits(cur)).toFixed(currencyDigits(cur)) })}</span>
            </>
          ) : (
            <span className="muted">{t('exchange.thatsLater')}</span>
          )}
          <span className="flex-1" />
          <button type="button" className="btn-ghost px-2 py-0.5 text-[0.8rem]" title={t('exchange.flipHint')} onClick={() => set({ flip: !form.flip })}>
            ⇅ {t('exchange.flip')}
          </button>
        </div>
      </>
    )
  }

  const sideBox = (which: 'give' | 'get', title: string) => (
    <div className="rounded-lg border border-line bg-surface-2 p-3">
      <div className="mb-2 flex flex-wrap items-center gap-2">
        <span className="flex-1 text-sm font-semibold">{title}</span>
        {modeSwitch(which)}
      </div>
      {side === which ? (
        rateRow(which)
      ) : (
        <div className="flex gap-2">
          <input
            className={clsx('input min-w-0 flex-1 text-right tabular-nums', tried && problems[which] && 'border-danger')}
            inputMode="decimal"
            aria-label={t('exchange.amountOf', { side: title })}
            placeholder="0.00"
            value={which === 'give' ? form.giveAmount : form.getAmount}
            onChange={(e) => set(which === 'give' ? { giveAmount: e.target.value } : { getAmount: e.target.value })}
          />
          {currencyInput(which)}
        </div>
      )}
      {err(problems[which])}
      {err(problems[which === 'give' ? 'giveCurrency' : 'getCurrency'])}
      {side === which && err(problems.rate)}
    </div>
  )

  return (
    <section className="card">
      <datalist id={`${id}-currencies`}>
        {currencies.map((c) => (
          <option key={c} value={c} />
        ))}
      </datalist>
      <h2 className="border-b border-line px-4 py-3 font-bold">{t('exchange.new')}</h2>
      <div className="p-4">
        <label className="label" htmlFor={`${id}-with`}>
          {t('exchange.with')}
        </label>
        {other ? (
          <span className="inline-flex items-center gap-1 rounded bg-accent-faint py-1 pr-1 pl-2.5 text-sm text-accent">
            {other.name}
            {other.handle && <span className="text-[0.78rem] muted">{other.handle}</span>}
            <button type="button" className="rounded px-1 hover:bg-accent/20" aria-label={t('people.remove', { name: other.name })} onClick={() => setParams({ with: null })}>
              ×
            </button>
          </span>
        ) : (
          <>
            <PersonSearch id={`${id}-with`} className={clsx('w-full', tried && problems.person && '[&_input]:border-danger')} people={others} onPick={(p) => setParams({ with: p.id })} placeholder={t('exchange.withPlaceholder')} />
            {err(problems.person)}
          </>
        )}

        <div className="mt-4 grid items-stretch gap-3 md:grid-cols-[minmax(0,1fr)_auto_minmax(0,1fr)]">
          {sideBox('give', t('exchange.youGive'))}
          <button
            type="button"
            className="btn-secondary self-center justify-self-center"
            title={t('exchange.swapHint')}
            onClick={() => {
              const s = swapSides(form, side)
              setForm(s.form)
              setParams({ rate: s.side })
            }}
          >
            ⇄<span className="md:sr-only"> {t('exchange.swap')}</span>
          </button>
          {sideBox('get', other ? t('exchange.theyGive', { name: other.name }) : t('exchange.theyGiveYou'))}
        </div>

        <div className="mt-3.5 flex flex-wrap items-baseline gap-x-4 gap-y-1 rounded-md border border-dashed border-line px-3 py-2.5 text-sm">
          {amounts.give !== null && amounts.get !== null && giveCur && getCur ? (
            <>
              <span className="muted">{t('exchange.rate')}</span>
              <RateBothWays a={amounts.give} aCurrency={giveCur} b={amounts.get} bCurrency={getCur} />
            </>
          ) : (
            <span className="muted">{t('exchange.rateLater')}</span>
          )}
        </div>

        <div className="mt-3.5">
          <label className="label" htmlFor={`${id}-desc`}>
            {t('exchange.description')} <span className="font-normal">{t('exchange.optional')}</span>
          </label>
          <input id={`${id}-desc`} className="input" maxLength={200} placeholder={t('exchange.descriptionPlaceholder')} value={form.description} onChange={(e) => set({ description: e.target.value })} />
        </div>

        {error && (
          <div className="alert-danger mt-3.5" role="alert">
            {error}
          </div>
        )}
        <div className="mt-4 flex flex-wrap items-center gap-2">
          <button type="button" className="btn-primary" disabled={create.isPending || (tried && !valid)} onClick={submit}>
            🔁 {t('exchange.create')}
          </button>
          {create.isPending && <Spinner className="h-4 w-4" />}
          <span className="text-[0.8rem] muted">{t('exchange.createHint')}</span>
        </div>
      </div>
    </section>
  )
}

/** The exchange just created (or picked in "Waiting to accept"): the deal with Accept and Cancel. */
function DraftPanel({ billId, onClose }: { billId: string; onClose: () => void }) {
  const { t } = useTranslation()
  const { user, isAdmin } = useAuth()
  const [waiting, setWaiting] = useState<{ action: 'accept' | 'cancel'; until: number } | null>(null)
  const [confirm, setConfirm] = useState(false)
  const [error, setError] = useState<string>()
  const pollWindow = usePollWindow(waiting?.until ?? 0)
  const first = useBill(billId)
  const bill = first.data
  const settled = !!bill && bill.status !== 'Draft' && bill.status !== 'Ready'
  useBill(billId, pollWindow && !settled)
  const accept = useAcceptExchange()
  const cancel = useCancelBill()

  if (first.isLoading) return null
  const raw = bill ? dealFromBill(bill) : null
  if (!bill || !raw)
    return (
      <div className="alert-danger mb-4 flex items-center gap-3" role="alert">
        <span className="flex-1">{t('exchange.draftNotFound')}</span>
        <button type="button" className="btn-ghost px-2 py-1" onClick={onClose}>
          ✕
        </button>
      </div>
    )
  const deal = orientDeal(raw, user?.id)
  const mine = canChange(bill, user?.id, isAdmin)
  const busy = (!!waiting && pollWindow && !settled) || bill.status === 'Ready'
  const fail = (e: Error) => setError(e instanceof ApiError && e.serverMessage ? e.serverMessage : e.message)

  const actions: ReactNode[] = []
  if (mine && bill.status === 'Draft' && !busy && !confirm) {
    actions.push(
      <button
        key="accept"
        type="button"
        className="btn-primary"
        disabled={accept.isPending}
        onClick={() => {
          setError(undefined)
          accept.mutate({ forwardBillId: bill.id, backwardBillId: bill.exchangeBillId! }, { onSuccess: () => setWaiting({ action: 'accept', until: Date.now() + POLL_MS }), onError: fail })
        }}
      >
        ✓ {t('exchange.accept')}
      </button>,
      <button key="cancel" type="button" className="btn-danger" onClick={() => setConfirm(true)}>
        {t('exchange.cancel')}
      </button>,
    )
  }
  actions.push(
    <button key="close" type="button" className="btn-ghost" aria-label={t('exchange.close')} title={t('exchange.close')} onClick={onClose}>
      ✕
    </button>,
  )

  return (
    <>
      {error && (
        <div className="alert-danger mb-3" role="alert">
          {error}
        </div>
      )}
      <ExchangeBanner
        deal={deal}
        status={bill.status}
        actions={actions}
        note={
          busy ? (
            <span className="inline-flex items-center gap-2" role="status">
              <Spinner className="h-4 w-4" />
              {t(waiting?.action === 'cancel' ? 'bill.cancelling' : 'exchange.accepting')}
            </span>
          ) : confirm ? (
            <span className="inline-flex flex-wrap items-center gap-2" role="alertdialog" aria-label={t('bill.cancelExchangeConfirm')}>
              <span className="text-danger">{t('bill.cancelExchangeConfirm')}</span>
              <button
                type="button"
                className="btn-danger px-2.5 py-1 text-[0.8rem]"
                onClick={() => {
                  setConfirm(false)
                  setError(undefined)
                  cancel.mutate(bill.id, { onSuccess: () => setWaiting({ action: 'cancel', until: Date.now() + POLL_MS }), onError: fail })
                }}
              >
                {t('bill.cancelYes')}
              </button>
              <button type="button" className="btn-ghost px-2.5 py-1 text-[0.8rem]" onClick={() => setConfirm(false)}>
                {t('bill.cancelNo')}
              </button>
            </span>
          ) : null
        }
      />
    </>
  )
}

/** Exchanges you created or are in, as the shared list. */
function ExchangeList() {
  const { t } = useTranslation()
  const f = useFormat()
  const names = useNameOf()
  const { user, isAdmin } = useAuth()
  const { people } = usePeople()
  const debts = useAllOwnDebts()
  const [state, setState] = useListState(exchangesSpec)
  const query = useMemo(() => exchangesQuery(state), [state])
  const [pollUntil, setPollUntil] = useState(0)
  const polling = usePollWindow(pollUntil)
  const list = useExchanges(query, { poll: polling })
  const accept = useAcceptExchange()
  const [acceptError, setAcceptError] = useState<string>()
  const others = people.filter((p) => !p.isMe)
  const currencies = [...new Set((debts.data ?? []).map((d) => d.currencyCode))].sort()

  const dealOf = (x: ExchangeListItem) => {
    const d = dealFromListItem(x)
    return d && orientDeal(d, user?.id)
  }
  const withWhom = (x: ExchangeListItem) => {
    const d = dealOf(x)
    if (!d) return names.short(x.creator)
    if (names.isMe(d.from.id)) return names.short(d.to)
    return `${names.short(d.from)} ⇄ ${names.short(d.to)}`
  }
  const acceptCell = (x: ExchangeListItem) => {
    if (x.status !== 'Draft') return null
    if (!canChange(x, user?.id, isAdmin)) return <span className="text-[0.78rem] muted">{t('exchange.accepts', { name: names.short(x.creator) })}</span>
    return (
      <button
        type="button"
        className="btn-primary px-2.5 py-1 text-[0.8rem] whitespace-nowrap"
        disabled={accept.isPending}
        onClick={() => {
          setAcceptError(undefined)
          accept.mutate(
            { forwardBillId: x.forwardBillId, backwardBillId: x.backwardBillId },
            { onSuccess: () => setPollUntil(Date.now() + POLL_MS), onError: (e) => setAcceptError(e instanceof ApiError && e.serverMessage ? e.serverMessage : t('bills.acceptFailed')) },
          )
        }}
      >
        ✓ {t('exchange.accept')}
      </button>
    )
  }
  const money = (x: ExchangeListItem, which: 'from' | 'to') => {
    const d = dealOf(x)
    return d ? f.money(which === 'from' ? d.fromAmount : d.toAmount, which === 'from' ? d.fromCurrency : d.toCurrency) : '—'
  }
  const rate = (x: ExchangeListItem) => {
    const d = dealOf(x)
    return d && <RateBothWays a={d.fromAmount} aCurrency={d.fromCurrency} b={d.toAmount} bCurrency={d.toCurrency} stacked />
  }

  const columns: Column<ExchangeListItem>[] = [
    { key: 'date', header: t('bills.date'), sort: 'date', width: '6rem', cell: (x) => <span className="tabular-nums muted">{f.date(x.date)}</span> },
    { key: 'with', header: t('exchange.withCol'), cell: (x) => <span className="font-semibold">🔁 {withWhom(x)}</span> },
    { key: 'give', header: t('exchange.youGiveCol'), sort: 'amount', align: 'right', cell: (x) => <span className="tabular-nums whitespace-nowrap">{money(x, 'from')}</span> },
    { key: 'get', header: t('exchange.youGetCol'), align: 'right', cell: (x) => <span className="tabular-nums whitespace-nowrap">{money(x, 'to')}</span> },
    { key: 'rate', header: t('exchange.rateCol'), cell: rate },
    { key: 'status', header: t('bills.status'), width: '9.5rem', cell: (x) => <ExchangeStatusPill status={x.status} /> },
    { key: 'action', header: <span className="sr-only">{t('bills.actions')}</span>, align: 'right', width: '7rem', cell: acceptCell },
  ]

  return (
    <>
      {acceptError && (
        <div className="alert-danger mb-4" role="alert">
          {acceptError}
        </div>
      )}
      <ListView<ExchangeListItem>
        spec={exchangesSpec}
        state={state}
        onChange={setState}
        filters={
          <>
            <FilterPerson label={t('bills.person')} placeholder={t('list.everyone')} value={state.filters.person} people={others} onChange={(v) => setState(withFilter(state, 'person', v))} />
            <FilterCurrency label={t('bills.currency')} placeholder={t('list.anyCurrency')} value={state.filters.currency} suggestions={currencies} onChange={(v) => setState(withFilter(state, 'currency', v))} />
            <FilterChoice
              label={t('bills.status')}
              value={state.filters.status}
              options={[
                { value: '', label: t('list.all') },
                { value: 'waiting', label: t('exchange.status.waiting') },
                { value: 'accepted', label: t('exchange.status.accepted') },
                { value: 'cancelled', label: t('exchange.status.cancelled') },
              ]}
              onChange={(v) => setState(withFilter(state, 'status', v))}
            />
          </>
        }
        sorts={[
          { value: 'date', label: t('bills.sortDate'), asc: t('bills.oldestFirst'), desc: t('bills.newestFirst') },
          { value: 'amount', label: t('exchange.sortAmount'), asc: t('bills.smallestFirst'), desc: t('bills.largestFirst') },
        ]}
        columns={columns}
        card={(x) => {
          const d = dealOf(x)
          return (
            <>
              <div className="flex items-baseline gap-2">
                <span className="min-w-0 flex-1 font-semibold">🔁 {withWhom(x)}</span>
                <ExchangeStatusPill status={x.status} />
              </div>
              <div className="mt-0.5 flex flex-wrap items-baseline gap-x-2 text-[0.8rem]">
                <span className="tabular-nums muted">{f.date(x.date)}</span>
                <span className="flex-1" />
                {d && names.isMe(d.from.id) ? (
                  <>
                    <span className="muted">{t('exchange.give')}</span>
                    <span className="tabular-nums">{money(x, 'from')}</span>
                    <span className="muted">{t('exchange.get')}</span>
                    <span className="tabular-nums">{money(x, 'to')}</span>
                  </>
                ) : (
                  d && <DealText deal={d} />
                )}
              </div>
              <div className="mt-1 flex items-center gap-2">
                {rate(x)}
                <span className="flex-1" />
                {acceptCell(x)}
              </div>
            </>
          )
        }}
        rowKey={(x) => x.forwardBillId}
        rowHref={(x) => `/bills/${x.forwardBillId}`}
        rowHighlight={(x) => x.status === 'Draft'}
        items={list.data?.items}
        total={list.data?.totalCount ?? 0}
        pageSize={PAGE_SIZE}
        loading={list.isLoading}
        fetching={list.isFetching && list.isPlaceholderData}
        error={list.error}
        empty={
          <>
            <div className="mb-2 text-[2rem]" aria-hidden>
              🔁
            </div>
            <p className="font-semibold">{t('exchange.empty')}</p>
            <p className="mt-1 text-sm muted">{t('exchange.emptyHint')}</p>
          </>
        }
        filteredEmpty={
          <>
            <div className="mb-2 text-[2rem]" aria-hidden>
              🔍
            </div>
            <p className="font-semibold">{t('exchange.noMatch')}</p>
            <p className="mt-1 text-sm muted">{t('bills.noMatchHint')}</p>
          </>
        }
        tableMinWidth={760}
      />
    </>
  )
}
