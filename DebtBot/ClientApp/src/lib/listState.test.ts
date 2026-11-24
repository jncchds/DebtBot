import { describe, expect, it } from 'vitest'
import {
  activeFilterCount,
  listSearch,
  pageWindow,
  parseListState,
  withFilter,
  withOrderToggled,
  withoutFilters,
  withPage,
  withSort,
  type ListSpec,
} from './listState'

const spec: ListSpec = {
  filters: {
    q: { kind: 'text' },
    with: { kind: 'id' },
    currency: { kind: 'currency' },
    from: { kind: 'date' },
    mine: { kind: 'flag' },
    status: { kind: 'choice', values: ['draft', 'finalized'] },
  },
  sorts: { date: 'desc', description: 'asc', total: 'desc' },
}

const parse = (s: string) => parseListState(new URLSearchParams(s), spec)

describe('parseListState', () => {
  it('gives the defaults for an empty URL', () => {
    expect(parse('')).toEqual({ filters: {}, sort: 'date', order: 'desc', page: 1 })
  })

  it('reads and cleans every kind of filter', () => {
    const s = parse('q=%20pizza%20&with=1b2c-3d&currency=eur&from=2026-11-12&mine=1&status=draft&page=3&sort=description&order=desc')
    expect(s).toEqual({
      filters: { q: 'pizza', with: '1b2c-3d', currency: 'EUR', from: '2026-11-12', mine: '1', status: 'draft' },
      sort: 'description',
      order: 'desc',
      page: 3,
    })
  })

  it('drops invalid values', () => {
    const s = parse('with=<script>&currency=EURO&from=12.11.2026&mine=0&status=nope&sort=evil&order=up&page=-4')
    expect(s).toEqual({ filters: {}, sort: 'date', order: 'desc', page: 1 })
  })

  it('takes the field’s own starting order when the URL has none', () => {
    expect(parse('sort=description').order).toBe('asc')
    expect(parse('sort=total').order).toBe('desc')
  })
})

describe('listSearch', () => {
  it('leaves defaults out', () => {
    expect(listSearch(parse(''), spec)).toBe('')
    expect(listSearch(parse('sort=date&order=desc&page=1'), spec)).toBe('')
  })

  it('round-trips', () => {
    const url = '?with=abc&currency=PLN&mine=1&sort=total&order=asc&page=2'
    expect(listSearch(parse(url), spec)).toBe(url)
  })

  it('keeps parameters that aren’t the list’s', () => {
    expect(listSearch(parse('page=2'), spec, new URLSearchParams('tab=x&page=9'))).toBe('?tab=x&page=2')
  })
})

describe('changes', () => {
  const base = parse('currency=EUR&page=4&sort=total')

  it('filter changes go back to page 1', () => {
    const s = withFilter(base, 'mine', true)
    expect(s.filters).toEqual({ currency: 'EUR', mine: '1' })
    expect(s.page).toBe(1)
    expect(withFilter(s, 'currency', '').filters).toEqual({ mine: '1' })
    expect(withFilter(s, 'mine', false).filters).toEqual({ currency: 'EUR' })
    expect(activeFilterCount(s)).toBe(2)
    expect(withoutFilters(s)).toMatchObject({ filters: {}, page: 1, sort: 'total' })
  })

  it('sorting picks the field’s order and toggles', () => {
    expect(withSort(base, spec, 'description')).toMatchObject({ sort: 'description', order: 'asc', page: 1 })
    expect(withSort(withOrderToggled(base), spec, 'total')).toMatchObject({ sort: 'total', order: 'asc' })
    expect(withSort(base, spec, 'nope')).toBe(base)
  })

  it('pages', () => {
    expect(withPage(base, 7).page).toBe(7)
    expect(withPage(base, 0).page).toBe(1)
  })
})

describe('pageWindow', () => {
  it('shows first, last and the neighbours, with gaps', () => {
    expect(pageWindow(1, 1)).toEqual([1])
    expect(pageWindow(1, 8)).toEqual([1, 2, null, 8])
    expect(pageWindow(5, 8)).toEqual([1, null, 4, 5, 6, null, 8])
    expect(pageWindow(3, 4)).toEqual([1, 2, 3, 4])
  })
})
