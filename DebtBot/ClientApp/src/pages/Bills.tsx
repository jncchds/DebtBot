import { useMemo, useState } from 'react'
import { Link } from 'react-router'
import { useTranslation } from 'react-i18next'
import { useAcceptExchange, useAllOwnDebts, useBills } from '../api/bills'
import { ApiError } from '../api/client'
import { MyBalance, StatusPill } from '../components/bill/bits'
import { ExchangeStatusPill } from '../components/exchange/ExchangeBits'
import { FilterChoice, FilterCurrency, FilterDate, FilterFlag, FilterPerson, FilterText } from '../components/list/filters'
import { ListView, type Column } from '../components/list/ListView'
import { PageTitle } from '../components/ui'
import { useAuth } from '../auth/AuthContext'
import { FollowingBanner, NoAccess, WhoseSelect } from '../components/following/Following'
import { isNoAccess, useFollowing } from '../lib/useFollowing'
import { balanceOwner, listActionsAllowed, youreOnIt } from '../lib/following'
import { canChange, firstLine, pairExchanges, type BillRow } from '../lib/bills'
import { withFilter } from '../lib/listState'
import { billsQuery, billsSpec, PAGE_SIZE } from '../lib/lists'
import { useFormat } from '../lib/useFormat'
import { POLL_MS, usePollWindow } from '../lib/usePollWindow'
import { useListState } from '../lib/useListState'
import { useNameOf, usePeople } from '../lib/usePeople'
import { useOrigin } from '../lib/useOrigin'

/** A row's status; an exchange uses the Exchange page's words ("Waiting to accept", "Accepted"). */
function RowStatus({ r }: { r: BillRow }) {
  return r.bill.exchangeBillId ? <ExchangeStatusPill status={r.bill.status} /> : <StatusPill status={r.bill.status} />
}

/**
 * 🧾 Bills: the bills you take part in, as the shared list (mockups/bills).
 * With `?of=<userId>`, the bills of someone you're subscribed to, read-only (mockups/following).
 */
export default function Bills() {
  const { t } = useTranslation()
  const origin = useOrigin()
  const f = useFormat()
  const { user, isAdmin } = useAuth()
  const names = useNameOf()
  const { people } = usePeople()
  const { of, viewing } = useFollowing()
  const name = viewing.kind === 'of' ? viewing.name : ''
  const owner = balanceOwner(viewing, user?.id)
  // Someone else's list: the person filter and currencies come from their balances (their counterparts).
  const debts = useAllOwnDebts(of)
  const [state, setState] = useListState(billsSpec)
  const query = useMemo(() => ({ ...billsQuery(state), ofUserId: of }), [state, of])
  // After Accept, the two bills are finalized in the background: poll for a while so the status catches up.
  const [pollUntil, setPollUntil] = useState(0)
  const polling = usePollWindow(pollUntil)
  const bills = useBills(query, { poll: polling })
  const accept = useAcceptExchange()
  const [acceptError, setAcceptError] = useState<string>()
  const rows = useMemo(() => pairExchanges(bills.data?.items ?? []), [bills.data])

  const theirPeople = useMemo(() => {
    const seen = new Map<string, string>()
    for (const d of debts.data ?? []) seen.set(d.debtorUser.id, d.debtorUser.id === user?.id ? t('common.youNamed', { name: d.debtorUser.displayName }) : d.debtorUser.displayName)
    return [...seen].map(([id, n]) => ({ id, name: n, isMe: id === user?.id })).sort((a, b) => a.name.localeCompare(b.name))
  }, [debts.data, user?.id, t])
  const others = of ? theirPeople : people.filter((p) => !p.isMe)
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
  const canAccept = (r: BillRow) => listActionsAllowed(viewing) && isExchangeDraft(r) && canChange(r.bill, user?.id, isAdmin)
  const onIt = (r: BillRow) =>
    youreOnIt(viewing, r.bill, user?.id) && (
      <>
        {' · '}
        <span className="pill-muted px-1.5 py-0">{t('following.bills.onIt')}</span>
      </>
    )

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
    <Link to={`/bills/${r.bill.id}`} state={origin} className="font-semibold hover:underline">
      {r.bill.exchangeBillId && <span aria-hidden>🔁 </span>}
      {firstLine(r.bill.description) || t('bill.untitled')}
    </Link>
  )

  const by = (r: BillRow) => (
    <>
      {t('bills.by', { name: names.short(r.bill.creator) })}
      {onIt(r)}
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
        ✓ {t('exchange.accept')}
      </button>
    )

  const columns: Column<BillRow>[] = [
    { key: 'date', header: t('bills.date'), sort: 'date', width: '6rem', cell: (r) => <span className="tabular-nums muted">{f.date(r.bill.date)}</span> },
    { key: 'bill', header: t('bills.bill'), sort: 'description', cell: (r) => (<><div>{title(r)}</div><div className="text-[0.8rem] muted">{by(r)}</div></>) },
    { key: 'total', header: t('bills.total'), sort: 'total', align: 'right', cell: total },
    {
      key: 'balance',
      header: of ? t('following.bills.balance', { name }) : t('bills.yourBalance'),
      align: 'right',
      cell: (r) => <MyBalance records={[...r.bill.ledgerRecords, ...(r.pair?.ledgerRecords ?? [])]} meId={owner} name={of && name} />,
    },
    { key: 'status', header: t('bills.status'), sort: 'status', width: '7.5rem', cell: (r) => <RowStatus r={r} /> },
    { key: 'action', header: <span className="sr-only">{t('bills.actions')}</span>, align: 'right', width: '7.5rem', cell: acceptButton },
  ]

  if (of && isNoAccess(bills.error))
    return (
      <>
        <PageTitle subtitle={t('pages.bills.subtitle')}>{t('pages.bills.title')}</PageTitle>
        <WhoseSelect />
        <NoAccess error={bills.error} />
      </>
    )

  return (
    <>
      {of ? (
        <PageTitle subtitle={t('following.subtitle.bills', { name })}>{t('following.title.bills', { name })}</PageTitle>
      ) : (
        <PageTitle subtitle={`${subtitle}.`}>{t('pages.bills.title')}</PageTitle>
      )}
      <WhoseSelect />
      {of && <FollowingBanner page="bills" name={name} />}
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
            <FilterFlag label={of ? t('following.bills.mine', { name }) : t('bills.mine')} value={!!state.filters.mine} onChange={(v) => setState(withFilter(state, 'mine', v))} />
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
              <RowStatus r={r} />
            </div>
            <div className="mt-0.5 flex items-baseline gap-2 text-[0.8rem]">
              <span className="min-w-0 flex-1 muted">
                {f.date(r.bill.date)} · {t('bills.by', { name: names.short(r.bill.creator) })}
                {onIt(r)}
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
                  <MyBalance records={r.bill.ledgerRecords} meId={owner} name={of && name} />
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
            <p className="font-semibold">{of ? t('following.bills.empty') : t('bills.empty')}</p>
            <p className="mt-1 text-sm muted">{of ? t('following.bills.emptyHint', { name }) : t('bills.emptyHint')}</p>
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
