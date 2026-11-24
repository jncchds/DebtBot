import { describe, expect, it } from 'vitest'
import { listSearch, parseListState, withFilter } from './listState'
import { exchangesQuery, exchangesSpec } from './lists'

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
