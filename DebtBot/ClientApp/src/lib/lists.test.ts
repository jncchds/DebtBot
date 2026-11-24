import { describe, expect, it } from 'vitest'
import { listSearch, parseListState, withFilter, withSort } from './listState'
import { exchangesQuery, exchangesSpec, spendingsQuery, spendingsSpec } from './lists'

const parse = (s: string) => parseListState(new URLSearchParams(s), exchangesSpec)

describe('exchangesSpec', () => {
  it('defaults to newest first, page 1, all statuses', () => {
    expect(exchangesQuery(parse(''))).toEqual({ withUserId: undefined, currencyCode: undefined, status: undefined, sortBy: 'Date', sortOrder: 'Desc', pageNumber: 0, countPerPage: 20 })
  })

  it('maps the URL to GET Bills/exchanges', () => {
    expect(exchangesQuery(parse('person=b0b&currency=pln&status=accepted&sort=amount&order=asc&page=3'))).toEqual({
      withUserId: 'b0b',
      currencyCode: 'PLN',
      status: 'Processed',
      sortBy: 'Amount',
      sortOrder: 'Asc',
      pageNumber: 2,
      countPerPage: 20,
    })
    expect(exchangesQuery(parse('status=waiting')).status).toBe('Draft')
    expect(exchangesQuery(parse('status=cancelled')).status).toBe('Cancelled')
    expect(exchangesQuery(parse('status=ready&sort=rate')).status).toBeUndefined()
  })

  it('keeps the form’s own parameters (with, rate, draft) when the list changes', () => {
    const params = new URLSearchParams('with=b0b&rate=get&draft=d1&page=2')
    const next = withFilter(parseListState(params, exchangesSpec), 'status', 'waiting')
    expect(listSearch(next, exchangesSpec, params)).toBe('?with=b0b&rate=get&draft=d1&status=waiting')
  })
})

describe('spendingsSpec', () => {
  const sp = (s: string) => parseListState(new URLSearchParams(s), spendingsSpec)

  it('defaults to newest first without cancelled', () => {
    expect(spendingsQuery(sp(''))).toEqual({
      currencyCode: undefined,
      dateFrom: undefined,
      dateTo: undefined,
      includeCancelled: undefined,
      sortBy: 'Date',
      sortOrder: 'Desc',
      pageNumber: 0,
      countPerPage: 20,
    })
  })

  it('maps the URL to GET Spendings/Own', () => {
    expect(spendingsQuery(sp('currency=eur&from=2026-11-01&to=2026-11-30&cancelled=1&sort=amount&order=asc&page=2'))).toEqual({
      currencyCode: 'EUR',
      dateFrom: '2026-11-01',
      dateTo: '2026-11-30',
      includeCancelled: true,
      sortBy: 'Amount',
      sortOrder: 'Asc',
      pageNumber: 1,
      countPerPage: 20,
    })
  })

  it('amount starts largest first, and bad values fall back', () => {
    const s = withSort(sp('page=3'), spendingsSpec, 'amount')
    expect(listSearch(s, spendingsSpec)).toBe('?sort=amount')
    expect(spendingsQuery(s).sortOrder).toBe('Desc')
    expect(spendingsQuery(sp('sort=total&from=yesterday&page=-1'))).toMatchObject({ sortBy: 'Date', dateFrom: undefined, pageNumber: 0 })
  })
})
