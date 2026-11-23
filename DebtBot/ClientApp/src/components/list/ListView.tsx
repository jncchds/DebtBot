import { useEffect, useRef, useState, type MouseEvent, type ReactNode } from 'react'
import { useNavigate } from 'react-router'
import { useTranslation } from 'react-i18next'
import clsx from 'clsx'
import { activeFilterCount, withOrderToggled, withoutFilters, withPage, withSort, type ListSpec, type ListState, type Order } from '../../lib/listState'
import { ErrorBox, Spinner } from '../ui'
import { Pager } from './Pager'

/**
 * The shared list pattern for Bills, Debts, Spendings and Exchanges (mockups/shell/README.md, "List views"):
 * a header card with filters and "Sort by", then one page of results, as a table when the list is at least
 * `tableMinWidth` px wide and as cards below that. All state is in the URL (useListState); the page passes
 * the API result in.
 */

export interface Column<T> {
  key: string
  header: ReactNode
  /** The sort field this column shows; its header gets an arrow when it's the current sort. */
  sort?: string
  align?: 'right'
  width?: string
  cell: (item: T) => ReactNode
}

export interface SortOption {
  value: string
  label: string
  /** Labels for the two directions, e.g. "Newest first" / "Oldest first". */
  asc: string
  desc: string
}

export interface ListViewProps<T> {
  spec: ListSpec
  state: ListState
  onChange: (next: ListState) => void
  /** Filter controls (components/list/filters.tsx). */
  filters: ReactNode
  sorts: SortOption[]
  columns: Column<T>[]
  /** One item as a card on narrow screens. */
  card: (item: T) => ReactNode
  rowKey: (item: T) => string
  /** Clicking a row or card (outside its buttons and links) opens this. */
  rowHref?: (item: T) => string | undefined
  rowHighlight?: (item: T) => boolean
  items: T[] | undefined
  total: number
  pageSize: number
  loading: boolean
  /** Refetching with the previous page still shown. */
  fetching?: boolean
  error: unknown
  /** Shown when there's nothing at all (no filters). */
  empty: ReactNode
  /** Shown when the filters match nothing; it gets a Clear filters button. */
  filteredEmpty: ReactNode
  tableMinWidth?: number
}

/** Width of an element, following resizes. */
function useWidth<E extends HTMLElement>() {
  const ref = useRef<E>(null)
  const [width, setWidth] = useState(0)
  useEffect(() => {
    const el = ref.current
    if (!el) return
    setWidth(el.clientWidth)
    const ro = new ResizeObserver(([e]) => setWidth(e.contentRect.width))
    ro.observe(el)
    return () => ro.disconnect()
  }, [])
  return [ref, width] as const
}

const isInteractive = (e: MouseEvent) => !!(e.target as HTMLElement).closest('a,button,input,select,label,textarea')

export function ListView<T>(p: ListViewProps<T>) {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const [ref, width] = useWidth<HTMLDivElement>()
  const [filtersOpen, setFiltersOpen] = useState(false)
  const wide = width >= (p.tableMinWidth ?? 720)
  const narrow = width > 0 && width < 480
  const count = activeFilterCount(p.state)
  const sort = p.sorts.find((s) => s.value === p.state.sort) ?? p.sorts[0]
  const orderLabel = (o: Order) => (o === 'asc' ? sort.asc : sort.desc)
  const arrow = p.state.order === 'asc' ? '↑' : '↓'

  const open = (item: T) => (e: MouseEvent) => {
    const href = p.rowHref?.(item)
    if (href && !isInteractive(e)) navigate(href)
  }

  const sortControl = (
    <div className="flex items-center gap-2">
      <label className="label mb-0 max-sm:sr-only" htmlFor="list-sort">
        {t('list.sortBy')}
      </label>
      <select id="list-sort" className="input w-auto min-w-0 max-sm:max-w-28" value={p.state.sort} onChange={(e) => p.onChange(withSort(p.state, p.spec, e.target.value))}>
        {p.sorts.map((s) => (
          <option key={s.value} value={s.value}>
            {s.label}
          </option>
        ))}
      </select>
      <button
        type="button"
        className="btn-secondary px-2.5 py-1 text-[0.8rem] whitespace-nowrap"
        title={t('list.changeOrder', { order: orderLabel(p.state.order === 'asc' ? 'desc' : 'asc') })}
        onClick={() => p.onChange(withOrderToggled(p.state))}
      >
        {arrow}
        {!narrow && <span> {orderLabel(p.state.order)}</span>}
        {narrow && <span className="sr-only">{orderLabel(p.state.order)}</span>}
      </button>
    </div>
  )

  const clear = count > 0 && (
    <button type="button" className="btn-ghost px-2.5 py-1 text-[0.8rem]" onClick={() => p.onChange(withoutFilters(p.state))}>
      ✕ {t('list.clearFilters')}
    </button>
  )

  const filtersBox = <div className="flex flex-wrap items-end gap-x-4 gap-y-3 px-4 py-3.5">{p.filters}</div>

  let body: ReactNode
  if (p.loading) {
    body = (
      <div className="card flex items-center justify-center gap-3 py-12 muted" role="status">
        <Spinner />
        <span>{t('common.loading')}</span>
      </div>
    )
  } else if (p.error) {
    body = <ErrorBox error={p.error} />
  } else if (!p.items?.length) {
    body =
      p.total > 0 ? (
        // A page past the end (an old bookmark, or items went away).
        <div className="card px-5 py-10 text-center">
          <p className="font-semibold">{t('list.pastEnd')}</p>
          <button type="button" className="btn-secondary mt-4" onClick={() => p.onChange(withPage(p.state, 1))}>
            {t('list.firstPage')}
          </button>
        </div>
      ) : count > 0 ? (
        <div className="card px-5 py-10 text-center">
          {p.filteredEmpty}
          <button type="button" className="btn-secondary mt-4" onClick={() => p.onChange(withoutFilters(p.state))}>
            ✕ {t('list.clearFilters')}
          </button>
        </div>
      ) : (
        <div className="card px-5 py-10 text-center">{p.empty}</div>
      )
  } else if (wide) {
    body = (
      <div className={clsx('card overflow-hidden transition-opacity', p.fetching && 'opacity-60')}>
        <table className="w-full border-collapse text-sm">
          <thead>
            <tr>
              {p.columns.map((c) => (
                <th
                  key={c.key}
                  scope="col"
                  style={c.width ? { width: c.width } : undefined}
                  className={clsx('border-b border-line px-4 py-2 text-[0.72rem] font-semibold tracking-wide text-muted uppercase', c.align === 'right' ? 'text-right' : 'text-left')}
                  aria-sort={c.sort && c.sort === p.state.sort ? (p.state.order === 'asc' ? 'ascending' : 'descending') : undefined}
                >
                  {c.header}
                  {c.sort && c.sort === p.state.sort && <span className="text-accent"> {arrow}</span>}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {p.items.map((item) => (
              <tr
                key={p.rowKey(item)}
                onClick={open(item)}
                className={clsx('border-b border-line last:border-b-0', p.rowHref?.(item) && 'cursor-pointer hover:bg-accent-faint', p.rowHighlight?.(item) && 'bg-accent-faint')}
              >
                {p.columns.map((c) => (
                  <td key={c.key} className={clsx('px-4 py-2.5 align-middle', c.align === 'right' && 'text-right')}>
                    {c.cell(item)}
                  </td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    )
  } else {
    body = (
      <ul className={clsx('card divide-y divide-line overflow-hidden transition-opacity', p.fetching && 'opacity-60')}>
        {p.items.map((item) => (
          <li
            key={p.rowKey(item)}
            onClick={open(item)}
            className={clsx('px-4 py-3', p.rowHref?.(item) && 'cursor-pointer hover:bg-accent-faint', p.rowHighlight?.(item) && 'bg-accent-faint')}
          >
            {p.card(item)}
          </li>
        ))}
      </ul>
    )
  }

  return (
    <div ref={ref}>
      <div className="card mb-4">
        {wide || width === 0 ? (
          <>
            {filtersBox}
            <div className="flex flex-wrap items-center gap-2 border-t border-line px-4 py-2.5">
              {sortControl}
              <span className="flex-1" />
              {clear}
            </div>
          </>
        ) : (
          <>
            <div className="flex items-center gap-2 px-3 py-2.5">
              <button type="button" className="btn-secondary px-2.5 py-1 text-[0.8rem]" aria-expanded={filtersOpen} onClick={() => setFiltersOpen((v) => !v)}>
                🔍 {t('list.filters')}
                {count > 0 && <span className="rounded-full bg-accent px-1.5 text-[0.68rem] leading-normal font-bold text-accent-contrast">{count}</span>}
                <span aria-hidden>{filtersOpen ? '▴' : '▾'}</span>
              </button>
              <span className="flex-1" />
              {sortControl}
            </div>
            {filtersOpen && (
              <div className="border-t border-line">
                {filtersBox}
                {clear && <div className="px-4 pb-3">{clear}</div>}
              </div>
            )}
          </>
        )}
      </div>
      {body}
      {!p.loading && !p.error && !!p.items?.length && (
        <Pager page={p.state.page} pageSize={p.pageSize} total={p.total} compact={!wide} onPage={(n) => p.onChange(withPage(p.state, n))} />
      )}
    </div>
  )
}
