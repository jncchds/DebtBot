import { useMemo } from 'react'
import { Link } from 'react-router'
import { useTranslation } from 'react-i18next'
import { useAdminUser, useAllBills } from '../api/admin'
import { StatusPill } from '../components/bill/bits'
import { FilterUser } from '../components/admin/UserSearch'
import { ExchangeStatusPill } from '../components/exchange/ExchangeBits'
import { FilterChoice, FilterCurrency, FilterDate, FilterText } from '../components/list/filters'
import { ListView, type Column } from '../components/list/ListView'
import { PageTitle } from '../components/ui'
import { firstLine, pairExchanges, type BillRow } from '../lib/bills'
import { withFilter } from '../lib/listState'
import { adminBillsQuery, adminBillsSpec, PAGE_SIZE } from '../lib/lists'
import { useFormat } from '../lib/useFormat'
import { useListState } from '../lib/useListState'

function RowStatus({ r }: { r: BillRow }) {
  return r.bill.exchangeBillId ? <ExchangeStatusPill status={r.bill.status} /> : <StatusPill status={r.bill.status} />
}

/**
 * 🛠 Admin › All bills (mockups/admin-bills): every bill, read-only, as the shared list. Rows open `/bills/<id>`,
 * where admins already have full access. Created by and Person search all users.
 */
export default function AdminBills() {
  const { t } = useTranslation()
  const f = useFormat()
  const [state, setState] = useListState(adminBillsSpec)
  const query = useMemo(() => adminBillsQuery(state), [state])
  const bills = useAllBills(query)
  const rows = useMemo(() => pairExchanges(bills.data?.items ?? []), [bills.data])
  const creator = useAdminUser(state.filters.by).data
  const person = useAdminUser(state.filters.with).data
  const currencies = [...new Set((bills.data?.items ?? []).flatMap((b) => [b.currencyCode, b.paymentCurrencyCode]))].sort()

  // "Drafts with Clara in USD, created by Boris Ivanov." — or the plain subtitle without filters.
  const filtered = Object.keys(state.filters).length > 0
  const summary = [
    state.filters.status ? t(`bills.statusFilter.${state.filters.status}`) : undefined,
    person && t('bills.withPerson', { name: person.displayName }),
    state.filters.currency && t('bills.inCurrency', { currency: state.filters.currency }),
    creator && t('admin.bills.createdBy', { name: creator.displayName }),
  ].filter(Boolean)
  const subtitle = filtered && summary.length ? `${summary.join(' ').replace(/^./, (c) => c.toUpperCase())}.` : t('pages.adminBills.subtitle')

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

  const people = (r: BillRow) => Math.max(r.bill.participantCount, r.pair?.participantCount ?? 0)

  const by = (r: BillRow) => (
    <>
      {t('bills.by', { name: r.bill.creator.displayName })}
      {r.bill.exchangeBillId && (
        <>
          {' · '}
          <span className="pill-accent px-1.5 py-0">{t('bills.exchangePair')}</span>
        </>
      )}
    </>
  )

  const columns: Column<BillRow>[] = [
    { key: 'date', header: t('bills.date'), sort: 'date', width: '6rem', cell: (r) => <span className="tabular-nums muted">{f.date(r.bill.date)}</span> },
    { key: 'bill', header: t('bills.bill'), sort: 'description', cell: (r) => (<><div>{title(r)}</div><div className="text-[0.8rem] muted">{by(r)}</div></>) },
    { key: 'people', header: t('admin.bills.people'), align: 'right', width: '6rem', cell: (r) => <span className="tabular-nums">{people(r)}</span> },
    { key: 'total', header: t('bills.total'), sort: 'total', align: 'right', cell: total },
    { key: 'status', header: t('bills.status'), sort: 'status', width: '8rem', cell: (r) => <RowStatus r={r} /> },
  ]

  return (
    <>
      <PageTitle subtitle={subtitle}>{t('pages.adminBills.title')}</PageTitle>
      <ListView<BillRow>
        spec={adminBillsSpec}
        state={state}
        onChange={setState}
        filters={
          <>
            <FilterText label={t('bills.search')} placeholder={t('bills.searchHint')} value={state.filters.q} onChange={(v) => setState(withFilter(state, 'q', v))} />
            <FilterUser label={t('admin.bills.createdByLabel')} placeholder={t('admin.bills.typeName')} value={state.filters.by} onChange={(v) => setState(withFilter(state, 'by', v))} />
            <FilterUser label={t('bills.person')} placeholder={t('admin.bills.typeName')} value={state.filters.with} onChange={(v) => setState(withFilter(state, 'with', v))} />
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
                {f.date(r.bill.date)} · {t('bills.by', { name: r.bill.creator.displayName })} · {t('admin.bills.peopleCount', { count: people(r) })}
              </span>
              <span className="text-right">{total(r)}</span>
            </div>
            {r.bill.exchangeBillId && (
              <div className="mt-1 text-[0.8rem]">
                <span className="pill-accent">{t('bills.exchangePair')}</span>
              </div>
            )}
          </>
        )}
        rowKey={(r) => r.bill.id}
        rowHref={(r) => `/bills/${r.bill.id}`}
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
            <p className="mt-1 text-sm muted">{t('admin.bills.emptyHint')}</p>
          </>
        }
        filteredEmpty={
          <>
            <div className="mb-2 text-[2rem]" aria-hidden>
              🔍
            </div>
            <p className="font-semibold">{t('bills.noMatch')}</p>
            <p className="mt-1 text-sm muted">{t('admin.bills.noMatchHint')}</p>
          </>
        }
        tableMinWidth={720}
      />
    </>
  )
}
