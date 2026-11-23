import { useMemo } from 'react'
import { Link } from 'react-router'
import { useTranslation } from 'react-i18next'
import { useAllOwnDebts, useOwnDebts } from '../api/bills'
import type { Debt } from '../api/types'
import { FilterChoice, FilterCurrency, FilterPerson } from '../components/list/filters'
import { ListView, type Column } from '../components/list/ListView'
import { PageTitle } from '../components/ui'
import { withFilter } from '../lib/listState'
import { debtsQuery, debtsSpec, PAGE_SIZE } from '../lib/lists'
import { handle } from '../lib/bills'
import { toUnits } from '../lib/money'
import { useFormat } from '../lib/useFormat'
import { useListState } from '../lib/useListState'

/** 💸 Debts (home): your balance with each person per currency, as the shared list (mockups/debts). */
export default function Debts() {
  const { t } = useTranslation()
  const f = useFormat()
  const [state, setState] = useListState(debtsSpec)
  const query = useMemo(() => debtsQuery(state), [state])
  const page = useOwnDebts(query)
  const all = useAllOwnDebts()

  // Totals and filter options come from all balances, not just this page.
  const { owed, owe, people, currencies } = useMemo(() => {
    const owed = new Map<string, bigint>()
    const owe = new Map<string, bigint>()
    const people = new Map<string, string>()
    for (const d of all.data ?? []) {
      const u = toUnits(d.amount)
      const m = u > 0n ? owed : owe
      if (u !== 0n) m.set(d.currencyCode, (m.get(d.currencyCode) ?? 0n) + u)
      people.set(d.debtorUser.id, d.debtorUser.displayName)
    }
    const sorted = (m: Map<string, bigint>) => [...m].sort(([a], [b]) => a.localeCompare(b))
    return {
      owed: sorted(owed),
      owe: sorted(owe),
      people: [...people].map(([id, name]) => ({ id, name, isMe: false })).sort((a, b) => a.name.localeCompare(b.name)),
      currencies: [...new Set((all.data ?? []).map((d) => d.currencyCode))].sort(),
    }
  }, [all.data])

  const settled = all.isSuccess && all.data.length === 0

  const direction = (d: Debt) =>
    d.amount > 0 ? <span className="text-success">{t('debts.owesYou')}</span> : <span className="text-danger">{t('debts.youOwe')}</span>
  const amount = (d: Debt) => <span className={`font-bold tabular-nums whitespace-nowrap ${d.amount > 0 ? 'text-success' : 'text-danger'}`}>{f.money(d.amount, d.currencyCode)}</span>
  const who = (d: Debt) => (
    <>
      <span className="font-semibold">{d.debtorUser.displayName}</span>
      {d.debtorUser.telegramUserName && <span className="ml-1.5 text-[0.8rem] muted">{handle(d.debtorUser.telegramUserName)}</span>}
    </>
  )
  const actions = (d: Debt) => (
    <span className="inline-flex flex-wrap gap-2">
      <Link className="btn-secondary px-2.5 py-1 text-[0.8rem]" to={`/bills?with=${d.debtorUser.id}&currency=${d.currencyCode}`}>
        🧾 {t('debts.bills')}
      </Link>
    </span>
  )

  const columns: Column<Debt>[] = [
    { key: 'person', header: t('debts.person'), sort: 'person', cell: who },
    { key: 'currency', header: t('debts.currency'), sort: 'currency', width: '6rem', cell: (d) => d.currencyCode },
    { key: 'dir', header: '', width: '7rem', cell: direction },
    { key: 'amount', header: t('debts.amount'), sort: 'amount', align: 'right', width: '9rem', cell: amount },
    { key: 'actions', header: <span className="sr-only">{t('debts.actions')}</span>, align: 'right', width: '7rem', cell: actions },
  ]

  return (
    <>
      <PageTitle subtitle={t('pages.debts.subtitle')}>{t('pages.debts.title')}</PageTitle>

      {settled ? (
        <div className="card px-5 py-10 text-center">
          <div className="mb-2 text-[2rem]" aria-hidden>
            🤝
          </div>
          <p className="font-semibold">{t('debts.settled')}</p>
          <p className="mt-1 text-sm muted">{t('debts.settledHint')}</p>
        </div>
      ) : (
        <>
          <div className="mb-4 grid gap-2 md:grid-cols-2 md:gap-4">
            {[
              { key: 'owed', label: t('debts.totalOwed'), items: owed, cls: 'text-success' },
              { key: 'owe', label: t('debts.totalOwe'), items: owe, cls: 'text-danger' },
            ].map((box) => (
              <div key={box.key} className="card px-4 py-3">
                <div className={`text-[0.72rem] font-semibold tracking-wide uppercase ${box.cls}`}>{box.label}</div>
                <div className={`mt-1 flex flex-wrap gap-x-4 gap-y-1 text-[1.1rem] font-bold tabular-nums ${box.cls}`}>
                  {box.items.length ? box.items.map(([c, u]) => <span key={c}>{f.money(u, c)}</span>) : <span className="muted">—</span>}
                </div>
              </div>
            ))}
          </div>

          <ListView<Debt>
            spec={debtsSpec}
            state={state}
            onChange={setState}
            filters={
              <>
                <FilterPerson label={t('debts.person')} placeholder={t('list.everyone')} value={state.filters.with} people={people} onChange={(v) => setState(withFilter(state, 'with', v))} />
                <FilterCurrency label={t('debts.currency')} placeholder={t('list.anyCurrency')} value={state.filters.currency} suggestions={currencies} onChange={(v) => setState(withFilter(state, 'currency', v))} />
                <FilterChoice
                  label={t('debts.direction')}
                  value={state.filters.dir}
                  options={[
                    { value: '', label: t('list.all') },
                    { value: 'owed', label: t('debts.dirOwed') },
                    { value: 'owe', label: t('debts.dirOwe') },
                  ]}
                  onChange={(v) => setState(withFilter(state, 'dir', v))}
                />
              </>
            }
            sorts={[
              { value: 'person', label: t('debts.sortPerson'), asc: t('list.az'), desc: t('list.za') },
              { value: 'amount', label: t('debts.sortAmount'), asc: t('debts.amountAsc'), desc: t('debts.amountDesc') },
              { value: 'currency', label: t('debts.sortCurrency'), asc: t('list.az'), desc: t('list.za') },
            ]}
            columns={columns}
            card={(d) => (
              <>
                <div className="flex items-baseline gap-2">
                  <span className="min-w-0 flex-1 truncate font-semibold">{d.debtorUser.displayName}</span>
                  {amount(d)}
                </div>
                <div className="mt-0.5 flex items-baseline gap-2 text-[0.8rem]">
                  <span className="min-w-0 flex-1 truncate muted">{d.debtorUser.telegramUserName ? handle(d.debtorUser.telegramUserName) : ''}</span>
                  {direction(d)}
                </div>
                <div className="mt-2">{actions(d)}</div>
              </>
            )}
            rowKey={(d) => `${d.debtorUser.id}:${d.currencyCode}`}
            items={page.data?.items}
            total={page.data?.totalCount ?? 0}
            pageSize={PAGE_SIZE}
            loading={page.isLoading}
            fetching={page.isFetching && page.isPlaceholderData}
            error={page.error}
            empty={<p className="font-semibold">{t('debts.settled')}</p>}
            filteredEmpty={<p className="font-semibold">{t('debts.noMatch')}</p>}
            tableMinWidth={760}
          />
        </>
      )}
    </>
  )
}
