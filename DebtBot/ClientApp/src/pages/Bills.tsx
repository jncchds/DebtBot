import { useMemo, useState } from 'react'
import { Link } from 'react-router'
import { useTranslation } from 'react-i18next'
import { useAcceptExchange, useAllOwnDebts, useBills } from '../api/bills'
import { ApiError } from '../api/client'
import { MyBalance, StatusPill } from '../components/bill/bits'
import { FilterChoice, FilterCurrency, FilterDate, FilterFlag, FilterPerson, FilterText } from '../components/list/filters'
import { ListView, type Column } from '../components/list/ListView'
import { PageTitle } from '../components/ui'
import { useAuth } from '../auth/AuthContext'
import { canChange, firstLine, pairExchanges, type BillRow } from '../lib/bills'
import { withFilter } from '../lib/listState'
import { billsQuery, billsSpec, PAGE_SIZE } from '../lib/lists'
import { useFormat } from '../lib/useFormat'
import { POLL_MS, usePollWindow } from '../lib/usePollWindow'
import { useListState } from '../lib/useListState'
import { useNameOf, usePeople } from '../lib/usePeople'

/** 🧾 Bills: the bills you take part in, as the shared list (mockups/bills). */
export default function Bills() {
  const { t } = useTranslation()
  const f = useFormat()
  const { user, isAdmin } = useAuth()
  const names = useNameOf()
  const { people } = usePeople()
  const debts = useAllOwnDebts()
  const [state, setState] = useListState(billsSpec)
  const query = useMemo(() => billsQuery(state), [state])
  // After Accept, the two bills are finalized in the background: poll for a while so the status catches up.
  const [pollUntil, setPollUntil] = useState(0)
  const polling = usePollWindow(pollUntil)
  const bills = useBills(query, { poll: polling })
  const accept = useAcceptExchange()
  const [acceptError, setAcceptError] = useState<string>()
  const rows = useMemo(() => pairExchanges(bills.data?.items ?? []), [bills.data])

  const others = people.filter((p) => !p.isMe)
  const currencies = [...new Set((debts.data ?? []).map((d) => d.currencyCode))].sort()
  const statusLabel = state.filters.status ? t(`bills.statusFilter.${state.filters.status}`) : undefined
  const personLabel = state.filters.with ? (others.find((p) => p.id === state.filters.with)?.name ?? '') : undefined

  // "Drafts with Clara in USD, created by you."
  const subtitle = [
    statusLabel ?? t('bills.allBills'),
    personLabel && t('bills.withPerson', { name: personLabel }),
    state.filters.currency && t('bills.inCurrency', { currency: state.filters.currency }),
    state.filters.mine && t('bills.createdByYou'),
  ]
    .filter(Boolean)
    .join(' ')

  const isExchangeDraft = (r: BillRow) => !!r.bill.exchangeBillId && r.bill.status === 'Draft'
  const canAccept = (r: BillRow) => isExchangeDraft(r) && canChange(r.bill, user?.id, isAdmin)

  const total = (r: BillRow) =>
    r.pair ? (
      <span className="tabular-nums whitespace-nowrap">
        {f.money(r.bill.totalWithTips, r.bill.currencyCode)} ⇄ {f.money(r.pair.totalWithTips, r.pair.currencyCode)}
      </span>
    ) : (
      <>
        <span className="tabular-nums whitespace-nowrap">{f.money(r.bill.totalWithTips, r.bill.currencyCode)}</span>
        {r.bill.paymentCurrencyCode !== r.bill.currencyCode && <div className="text-[0.8rem] muted">{t('bills.paidIn', { currency: r.bill.paymentCurrencyCode })}</div>}
      </>
    )

  const title = (r: BillRow) => (
    <Link to={`/bills/${r.bill.id}`} className="font-semibold hover:underline">
      {r.bill.exchangeBillId && <span aria-hidden>🔁 </span>}
      {firstLine(r.bill.description) || t('bill.untitled')}
    </Link>
  )

  const by = (r: BillRow) => (
    <>
      {t('bills.by', { name: names.short(r.bill.creator) })}
      {r.bill.exchangeBillId && (
        <>
          {' · '}
          <span className="pill-accent px-1.5 py-0">{t('bills.exchangePair')}</span>
        </>
      )}
    </>
  )

  const acceptButton = (r: BillRow) =>
    canAccept(r) && (
      <button
        type="button"
        className="btn-primary px-2.5 py-1 text-[0.8rem] whitespace-nowrap"
        disabled={accept.isPending}
        onClick={() => {
          setAcceptError(undefined)
          accept.mutate(
            { forwardBillId: r.bill.id, backwardBillId: r.bill.exchangeBillId! },
            {
              onSuccess: () => setPollUntil(Date.now() + POLL_MS),
              onError: (e) => setAcceptError(e instanceof ApiError && e.serverMessage ? e.serverMessage : t('bills.acceptFailed')),
            },
          )
        }}
      >
        ✓ {t('bills.accept')}
      </button>
    )

  const columns: Column<BillRow>[] = [
    { key: 'date', header: t('bills.date'), sort: 'date', width: '6rem', cell: (r) => <span className="tabular-nums muted">{f.date(r.bill.date)}</span> },
    { key: 'bill', header: t('bills.bill'), sort: 'description', cell: (r) => (<><div>{title(r)}</div><div className="text-[0.8rem] muted">{by(r)}</div></>) },
    { key: 'total', header: t('bills.total'), sort: 'total', align: 'right', cell: total },
    { key: 'balance', header: t('bills.yourBalance'), align: 'right', cell: (r) => <MyBalance records={[...r.bill.ledgerRecords, ...(r.pair?.ledgerRecords ?? [])]} meId={user?.id} /> },
    { key: 'status', header: t('bills.status'), sort: 'status', width: '7.5rem', cell: (r) => <StatusPill status={r.bill.status} /> },
    { key: 'action', header: <span className="sr-only">{t('bills.actions')}</span>, align: 'right', width: '7.5rem', cell: acceptButton },
  ]

  return (
    <>
      <PageTitle subtitle={`${subtitle}.`}>{t('pages.bills.title')}</PageTitle>
      {acceptError && (
        <div className="alert-danger mb-4" role="alert">
          {acceptError}
        </div>
      )}
      <ListView<BillRow>
        spec={billsSpec}
        state={state}
        onChange={setState}
        filters={
          <>
            <FilterText label={t('bills.search')} placeholder={t('bills.searchHint')} value={state.filters.q} onChange={(v) => setState(withFilter(state, 'q', v))} />
            <FilterPerson label={t('bills.person')} placeholder={t('list.everyone')} value={state.filters.with} people={others} onChange={(v) => setState(withFilter(state, 'with', v))} />
            <FilterCurrency label={t('bills.currency')} placeholder={t('list.anyCurrency')} value={state.filters.currency} suggestions={currencies} onChange={(v) => setState(withFilter(state, 'currency', v))} />
            <FilterDate label={t('bills.from')} value={state.filters.from} onChange={(v) => setState(withFilter(state, 'from', v))} />
            <FilterDate label={t('bills.to')} value={state.filters.to} onChange={(v) => setState(withFilter(state, 'to', v))} />
            <FilterChoice
              label={t('bills.status')}
              value={state.filters.status}
              options={[
                { value: '', label: t('list.all') },
                { value: 'draft', label: t('bill.status.draft') },
                { value: 'processing', label: t('bill.status.processing') },
                { value: 'finalized', label: t('bill.status.finalized') },
                { value: 'cancelled', label: t('bill.status.cancelled') },
              ]}
              onChange={(v) => setState(withFilter(state, 'status', v))}
            />
            <FilterFlag label={t('bills.mine')} value={!!state.filters.mine} onChange={(v) => setState(withFilter(state, 'mine', v))} />
          </>
        }
        sorts={[
          { value: 'date', label: t('bills.sortDate'), asc: t('bills.oldestFirst'), desc: t('bills.newestFirst') },
          { value: 'description', label: t('bills.sortDescription'), asc: t('list.az'), desc: t('list.za') },
          { value: 'total', label: t('bills.sortTotal'), asc: t('bills.smallestFirst'), desc: t('bills.largestFirst') },
          { value: 'status', label: t('bills.sortStatus'), asc: t('bills.draftsFirst'), desc: t('bills.cancelledFirst') },
        ]}
        columns={columns}
        card={(r) => (
          <>
            <div className="flex items-baseline gap-2">
              <span className="min-w-0 flex-1">{title(r)}</span>
              <StatusPill status={r.bill.status} />
            </div>
            <div className="mt-0.5 flex items-baseline gap-2 text-[0.8rem]">
              <span className="min-w-0 flex-1 muted">
                {f.date(r.bill.date)} · {t('bills.by', { name: names.short(r.bill.creator) })}
              </span>
              <span className="text-right">{total(r)}</span>
            </div>
            {r.bill.exchangeBillId ? (
              <div className="mt-1 flex items-center gap-2 text-[0.8rem]">
                <span className="pill-accent">{t('bills.exchangePair')}</span>
                <span className="flex-1" />
                {acceptButton(r)}
              </div>
            ) : (
              r.bill.ledgerRecords.length > 0 && (
                <div className="mt-0.5 text-right text-[0.8rem]">
                  <MyBalance records={r.bill.ledgerRecords} meId={user?.id} />
                </div>
              )
            )}
          </>
        )}
        rowKey={(r) => r.bill.id}
        rowHref={(r) => `/bills/${r.bill.id}`}
        rowHighlight={isExchangeDraft}
        items={bills.data ? rows : undefined}
        total={bills.data?.totalCount ?? 0}
        pageSize={PAGE_SIZE}
        loading={bills.isLoading}
        fetching={bills.isFetching && bills.isPlaceholderData}
        error={bills.error}
        empty={
          <>
            <div className="mb-2 text-[2rem]" aria-hidden>
              🧾
            </div>
            <p className="font-semibold">{t('bills.empty')}</p>
            <p className="mt-1 text-sm muted">{t('bills.emptyHint')}</p>
          </>
        }
        filteredEmpty={
          <>
            <div className="mb-2 text-[2rem]" aria-hidden>
              🔍
            </div>
            <p className="font-semibold">{t('bills.noMatch')}</p>
            <p className="mt-1 text-sm muted">{t('bills.noMatchHint')}</p>
          </>
        }
        tableMinWidth={820}
      />
    </>
  )
}
