import type { BillListQuery, BillSortField, DebtDirection, DebtListQuery, DebtSortField, ProcessingState, SortOrder } from '../api/types'
import type { ListSpec, ListState } from './listState'

/**
 * The URL specs of the list views and how their URL state maps to API queries.
 * URL values are short and lower case; the API takes the enum names.
 */

export const PAGE_SIZE = 20

const apiOrder = (s: ListState): SortOrder => (s.order === 'asc' ? 'Asc' : 'Desc')

/** 💸 Debts: /?with=<userId>&currency=EUR&dir=owed|owe&sort=person|amount|currency&order=asc|desc&page=2 */
export const debtsSpec: ListSpec = {
  filters: { with: { kind: 'id' }, currency: { kind: 'currency' }, dir: { kind: 'choice', values: ['owed', 'owe'] } },
  sorts: { person: 'asc', amount: 'asc', currency: 'asc' },
}

const DEBT_SORT: Record<string, DebtSortField> = { person: 'Person', amount: 'Amount', currency: 'Currency' }
const DEBT_DIR: Record<string, DebtDirection> = { owed: 'OwedToMe', owe: 'IOwe' }

export function debtsQuery(s: ListState): DebtListQuery {
  return {
    withUserId: s.filters.with,
    currencyCode: s.filters.currency,
    direction: s.filters.dir ? DEBT_DIR[s.filters.dir] : undefined,
    sortBy: DEBT_SORT[s.sort],
    sortOrder: apiOrder(s),
    pageNumber: s.page - 1,
    countPerPage: PAGE_SIZE,
  }
}

/**
 * 🧾 Bills: /bills?q=&with=<userId>&currency=EUR&from=yyyy-mm-dd&to=yyyy-mm-dd
 *   &status=draft|processing|finalized|cancelled&mine=1&sort=date|description|total|status&order=asc|desc&page=2
 */
export const billsSpec: ListSpec = {
  filters: {
    q: { kind: 'text' },
    with: { kind: 'id' },
    currency: { kind: 'currency' },
    from: { kind: 'date' },
    to: { kind: 'date' },
    status: { kind: 'choice', values: ['draft', 'processing', 'finalized', 'cancelled'] },
    mine: { kind: 'flag' },
  },
  sorts: { date: 'desc', description: 'asc', total: 'desc', status: 'asc' },
}

export const BILL_STATUS: Record<string, ProcessingState> = { draft: 'Draft', processing: 'Ready', finalized: 'Processed', cancelled: 'Cancelled' }
const BILL_SORT: Record<string, BillSortField> = { date: 'Date', description: 'Description', total: 'Total', status: 'Status' }

export function billsQuery(s: ListState): BillListQuery {
  return {
    search: s.filters.q,
    withUserId: s.filters.with,
    currencyCode: s.filters.currency,
    dateFrom: s.filters.from,
    dateTo: s.filters.to,
    status: s.filters.status ? BILL_STATUS[s.filters.status] : undefined,
    createdByMe: s.filters.mine ? true : undefined,
    sortBy: BILL_SORT[s.sort],
    sortOrder: apiOrder(s),
    pageNumber: s.page - 1,
    countPerPage: PAGE_SIZE,
  }
}
