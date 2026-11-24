import { useMemo, type ReactNode } from 'react'
import { Link } from 'react-router'
import { useTranslation } from 'react-i18next'
import clsx from 'clsx'
import { useSpendings, useSpendingSummary } from '../api/bills'
import type { Spending } from '../api/types'
import { FollowingBanner, NoAccess, WhoseSelect } from '../components/following/Following'
import { isNoAccess, useFollowing } from '../lib/useFollowing'
import { FilterCurrency, FilterDate, FilterFlag } from '../components/list/filters'
import { ListView, type Column } from '../components/list/ListView'
import { PageTitle } from '../components/ui'
import { firstLine } from '../lib/bills'
import { withFilter } from '../lib/listState'
import { PAGE_SIZE, spendingsQuery, spendingsSpec } from '../lib/lists'
import { useFormat } from '../lib/useFormat'
import { useListState } from '../lib/useListState'
import { useOrigin } from '../lib/useOrigin'

/** "33 % of the bill": always a whole percentage of the bill (user, round 1). */
const sharePercent = (portion: number) => Math.round(portion * 100)

/**
 * 📊 Spendings: your share of every bill you take part in, as the shared list (mockups/spendings).
 * With `?of=<userId>`, the spendings of someone you're subscribed to, read-only (mockups/following).
 */
export default function Spendings() {
  const { t } = useTranslation()
  const origin = useOrigin()
  const f = useFormat()
  const { of, viewing } = useFollowing()
  const name = viewing.kind === 'of' ? viewing.name : ''
  const [state, setState] = useListState(spendingsSpec)
  const query = useMemo(() => ({ ...spendingsQuery(state), ofUserId: of }), [state, of])
  const list = useSpendings(query)
  // All-time totals give the currencies there are spendings in, for the currency suggestions.
  const summary = useSpendingSummary(undefined, undefined, of)
  const currencies = (summary.data ?? []).map((s) => s.currencyCode)

  /** Cancelled spendings are faded (shown only with "Show cancelled"). */
  const faded = (s: Spending, el: ReactNode) => <div className={clsx(s.isCanceled && 'opacity-60')}>{el}</div>

  const billLink = (s: Spending) => (
    <Link to={`/bills/${s.billId}`} state={origin} className="font-semibold text-accent hover:underline">
      {firstLine(s.description) || t('bill.untitled')}
    </Link>
  )

  const amount = (s: Spending) => <span className={clsx('font-bold tabular-nums whitespace-nowrap', s.isCanceled && 'line-through')}>{f.money(s.amount, s.currencyCode)}</span>

  const paidAs = (s: Spending) =>
    s.paymentCurrencyCode !== s.currencyCode && <div className="text-[0.8rem] whitespace-nowrap muted">{t('spendings.paidAs', { amount: f.money(s.paymentAmount, s.paymentCurrencyCode) })}</div>

  const cancelledPill = (s: Spending) => s.isCanceled && <span className="pill-danger">{t('spendings.cancelled')}</span>

  const columns: Column<Spending>[] = [
    { key: 'date', header: t('spendings.date'), sort: 'date', width: '6rem', cell: (s) => faded(s, <span className="tabular-nums muted">{f.date(s.date)}</span>) },
    {
      key: 'bill',
      header: t('spendings.bill'),
      cell: (s) =>
        faded(
          s,
          <>
            <div>{billLink(s)}</div>
            <div className="text-[0.8rem] muted">{t('spendings.share', { percent: sharePercent(s.portion) })}</div>
          </>,
        ),
    },
    {
      key: 'amount',
      header: t('spendings.amount'),
      sort: 'amount',
      align: 'right',
      cell: (s) =>
        faded(
          s,
          <>
            {amount(s)}
            {paidAs(s)}
          </>,
        ),
    },
    { key: 'status', header: <span className="sr-only">{t('spendings.status')}</span>, align: 'right', width: '7.5rem', cell: cancelledPill },
  ]

  if (of && isNoAccess(list.error))
    return (
      <>
        <PageTitle subtitle={t('pages.spendings.subtitle')}>{t('pages.spendings.title')}</PageTitle>
        <WhoseSelect />
        <NoAccess error={list.error} />
      </>
    )

  return (
    <>
      {of ? (
        <PageTitle subtitle={t('following.subtitle.spendings', { name })}>{t('following.title.spendings', { name })}</PageTitle>
      ) : (
        <PageTitle subtitle={t('pages.spendings.subtitle')}>{t('pages.spendings.title')}</PageTitle>
      )}
      <WhoseSelect />
      {of && <FollowingBanner page="spendings" name={name} />}
      <ListView<Spending>
        spec={spendingsSpec}
        state={state}
        onChange={setState}
        filters={
          <>
            <FilterCurrency label={t('spendings.currency')} placeholder={t('list.anyCurrency')} value={state.filters.currency} suggestions={currencies} onChange={(v) => setState(withFilter(state, 'currency', v))} />
            <FilterDate label={t('bills.from')} value={state.filters.from} onChange={(v) => setState(withFilter(state, 'from', v))} />
            <FilterDate label={t('bills.to')} value={state.filters.to} onChange={(v) => setState(withFilter(state, 'to', v))} />
            <FilterFlag label={t('spendings.showCancelled')} value={!!state.filters.cancelled} onChange={(v) => setState(withFilter(state, 'cancelled', v))} />
          </>
        }
        sorts={[
          { value: 'date', label: t('bills.sortDate'), asc: t('bills.oldestFirst'), desc: t('bills.newestFirst') },
          { value: 'amount', label: t('spendings.sortAmount'), asc: t('bills.smallestFirst'), desc: t('bills.largestFirst') },
        ]}
        columns={columns}
        card={(s) =>
          faded(
            s,
            <>
              <div className="flex items-baseline gap-2">
                <span className="min-w-0 flex-1 break-words">{billLink(s)}</span>
                {amount(s)}
              </div>
              <div className="mt-0.5 flex items-center gap-2 text-[0.8rem]">
                <span className="min-w-0 flex-1 muted">
                  {f.date(s.date)} · {t('spendings.share', { percent: sharePercent(s.portion) })}
                </span>
                {cancelledPill(s)}
              </div>
              {paidAs(s) && <div className="text-right">{paidAs(s)}</div>}
            </>,
          )
        }
        rowKey={(s) => s.billId}
        rowHref={(s) => `/bills/${s.billId}`}
        items={list.data?.items}
        total={list.data?.totalCount ?? 0}
        pageSize={PAGE_SIZE}
        loading={list.isLoading}
        fetching={list.isFetching && list.isPlaceholderData}
        error={list.error}
        empty={
          <>
            <div className="mb-2 text-[2rem]" aria-hidden>
              📊
            </div>
            <p className="font-semibold">{t('spendings.empty')}</p>
            <p className="mt-1 text-sm muted">{of ? t('following.spendings.emptyHint', { name }) : t('spendings.emptyHint')}</p>
          </>
        }
        filteredEmpty={
          <>
            <div className="mb-2 text-[2rem]" aria-hidden>
              🔍
            </div>
            <p className="font-semibold">{t('spendings.noMatch')}</p>
            <p className="mt-1 text-sm muted">{t('spendings.noMatchHint')}</p>
          </>
        }
        tableMinWidth={620}
      />
    </>
  )
}
