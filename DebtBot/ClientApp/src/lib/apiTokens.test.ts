import { describe, expect, it } from 'vitest'
import { activeCount, canOpen, checkTokenForm, closeOpen, curlExample, DEFAULT_EXPIRY, emptyTokenForm, expiryDate, maskedToken, toggleOpen, tokenRequest } from './apiTokens'

describe('the New token form', () => {
  it('starts read-only with 90 days preselected', () => {
    expect(emptyTokenForm()).toEqual({ name: '', scope: 'Read', expiry: '90' })
    expect(DEFAULT_EXPIRY).toBe('90')
  })

  it('needs a name of 1–100 characters', () => {
    expect(checkTokenForm({ ...emptyTokenForm(), name: '  ' })).toEqual({ name: 'tokens.nameRule' })
    expect(checkTokenForm({ ...emptyTokenForm(), name: 'x'.repeat(101) })).toEqual({ name: 'tokens.nameRule' })
    expect(checkTokenForm({ ...emptyTokenForm(), name: ` ${'x'.repeat(100)} ` })).toEqual({})
  })

  it('sends the trimmed name, the scope and the days (null for never)', () => {
    expect(tokenRequest({ name: ' Home Assistant ', scope: 'ReadWrite', expiry: '365' })).toEqual({ name: 'Home Assistant', scope: 'ReadWrite', expiresInDays: 365 })
    expect(tokenRequest({ name: 'CI', scope: 'Read', expiry: 'never' })).toEqual({ name: 'CI', scope: 'Read', expiresInDays: null })
  })

  it('shows when the token would expire', () => {
    const now = new Date('2026-11-24T10:00:00Z')
    expect(expiryDate('90', now)?.toISOString()).toBe('2027-02-22T10:00:00.000Z')
    expect(expiryDate('30', now)?.toISOString()).toBe('2026-12-24T10:00:00.000Z')
    expect(expiryDate('never', now)).toBeNull()
  })
})

describe('the token list', () => {
  it('masks a token by its prefix', () => {
    expect(maskedToken('a1b2c3d4')).toBe('dbt_a1b2c3d4…')
  })

  it('builds the curl example for this origin', () => {
    expect(curlExample('https://debts.example.com/', 'dbt_x')).toBe('curl -H "Authorization: Bearer dbt_x" \\\n  https://debts.example.com/api/v1/Debts/Own')
  })

  it('opens only active tokens and counts them', () => {
    expect(canOpen({ status: 'Active' })).toBe(true)
    expect(canOpen({ status: 'Expired' })).toBe(false)
    expect(canOpen({ status: 'Revoked' })).toBe(false)
    expect(activeCount([{ status: 'Active' }, { status: 'Revoked' }, { status: 'Active' }, { status: 'Expired' }])).toBe(2)
    expect(activeCount(undefined)).toBe(0)
  })

  it('keeps the open row in the URL with the other parameters', () => {
    expect(toggleOpen('', 'a')).toBe('?open=a')
    expect(toggleOpen('?search=bor&page=2', 'a')).toBe('?search=bor&page=2&open=a')
    expect(toggleOpen('?search=bor&open=a', 'b')).toBe('?search=bor&open=b')
    expect(toggleOpen('?search=bor&open=a', 'a')).toBe('?search=bor')
    expect(toggleOpen('?open=a', 'a')).toBe('')
    expect(closeOpen('?role=admin&open=a')).toBe('?role=admin')
    expect(closeOpen('')).toBe('')
  })
})
