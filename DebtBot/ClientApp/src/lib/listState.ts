/**
 * URL state for the shared list pattern (mockups/shell/README.md, "List views").
 * A list's filters, sort field, sort order and page live in the query string, so Back/Forward, reload
 * and bookmarks restore them. Defaults are left out of the URL, so `/bills` is the default view.
 *
 * URL convention: one parameter per filter (its name in the spec), plus `sort=<field>`, `order=asc|desc`
 * and `page=<n>` (1-based). Unknown or invalid values fall back to the defaults.
 */

export type FilterSpec =
  /** Free text, trimmed. */
  | { kind: 'text' }
  /** An id (contact). */
  | { kind: 'id' }
  /** A 3-letter currency code, stored upper case. */
  | { kind: 'currency' }
  /** `yyyy-mm-dd`. */
  | { kind: 'date' }
  /** Present as `=1` when on. */
  | { kind: 'flag' }
  /** One of the listed values. */
  | { kind: 'choice'; values: readonly string[] }

export type Order = 'asc' | 'desc'

export interface ListSpec {
  filters: Record<string, FilterSpec>
  /** Sort fields, each with the order it starts in when picked. The first one is the default sort. */
  sorts: Record<string, Order>
}

export interface ListState {
  /** Only filters that are set; flags are `'1'`. */
  filters: Record<string, string>
  sort: string
  order: Order
  /** 1-based. */
  page: number
}

const RESERVED = ['sort', 'order', 'page']

function cleanFilter(spec: FilterSpec, raw: string | null): string | undefined {
  if (raw === null) return undefined
  const v = raw.trim()
  if (!v) return undefined
  switch (spec.kind) {
    case 'text':
      return v.slice(0, 100)
    case 'id':
      return /^[0-9a-fA-F-]{1,64}$/.test(v) ? v : undefined
    case 'currency':
      return /^[a-zA-Z]{3}$/.test(v) ? v.toUpperCase() : undefined
    case 'date':
      return /^\d{4}-\d{2}-\d{2}$/.test(v) && !Number.isNaN(Date.parse(v)) ? v : undefined
    case 'flag':
      return v === '1' || v === 'true' ? '1' : undefined
    case 'choice':
      return spec.values.includes(v) ? v : undefined
  }
}

export function defaultSort(spec: ListSpec): string {
  return Object.keys(spec.sorts)[0]
}

export function parseListState(params: URLSearchParams, spec: ListSpec): ListState {
  const filters: Record<string, string> = {}
  for (const [name, f] of Object.entries(spec.filters)) {
    const v = cleanFilter(f, params.get(name))
    if (v !== undefined) filters[name] = v
  }
  const rawSort = params.get('sort') ?? ''
  const sort = rawSort in spec.sorts ? rawSort : defaultSort(spec)
  const rawOrder = params.get('order')
  const order: Order = rawOrder === 'asc' || rawOrder === 'desc' ? rawOrder : spec.sorts[sort]
  const page = Math.max(1, Math.min(100_000, Math.floor(Number(params.get('page')) || 1)))
  return { filters, sort, order, page }
}

/** The query string for a state, without the defaults; other parameters in `keep` are kept as they are. */
export function listSearch(state: ListState, spec: ListSpec, keep?: URLSearchParams): string {
  const p = new URLSearchParams()
  if (keep) for (const [k, v] of keep) if (!(k in spec.filters) && !RESERVED.includes(k)) p.append(k, v)
  for (const name of Object.keys(spec.filters)) {
    const v = state.filters[name]
    if (v) p.set(name, v)
  }
  if (state.sort !== defaultSort(spec)) p.set('sort', state.sort)
  if (state.order !== spec.sorts[state.sort]) p.set('order', state.order)
  if (state.page > 1) p.set('page', String(state.page))
  const s = p.toString()
  return s ? `?${s}` : ''
}

/** Sets or clears one filter; any filter change goes back to page 1. */
export function withFilter(state: ListState, name: string, value: string | undefined | null | boolean): ListState {
  const filters = { ...state.filters }
  const v = value === true ? '1' : value === false || value == null ? '' : value.trim()
  if (v) filters[name] = v
  else delete filters[name]
  return { ...state, filters, page: 1 }
}

export function withoutFilters(state: ListState): ListState {
  return { ...state, filters: {}, page: 1 }
}

/** Picks a sort field in its starting order; picking the current field keeps the order. Back to page 1. */
export function withSort(state: ListState, spec: ListSpec, sort: string): ListState {
  if (!(sort in spec.sorts)) return state
  return { ...state, sort, order: sort === state.sort ? state.order : spec.sorts[sort], page: 1 }
}

export function withOrderToggled(state: ListState): ListState {
  return { ...state, order: state.order === 'asc' ? 'desc' : 'asc', page: 1 }
}

export function withPage(state: ListState, page: number): ListState {
  return { ...state, page: Math.max(1, Math.floor(page)) }
}

export function activeFilterCount(state: ListState): number {
  return Object.keys(state.filters).length
}

/** Page numbers to show: first, last, and two around the current one, with gaps as null. */
export function pageWindow(page: number, last: number): (number | null)[] {
  const pages = new Set([1, last, page - 1, page, page + 1].filter((p) => p >= 1 && p <= last))
  const sorted = [...pages].sort((a, b) => a - b)
  const out: (number | null)[] = []
  sorted.forEach((p, i) => {
    if (i > 0 && p - sorted[i - 1] > 1) out.push(null)
    out.push(p)
  })
  return out
}
