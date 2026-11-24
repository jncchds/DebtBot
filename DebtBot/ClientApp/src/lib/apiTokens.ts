import type { ApiToken, ApiTokenCreation, ApiTokenScope } from '../api/types'

/** API tokens on Profile (mockups series-2/api-tokens): the New token form, the shown-once secret, the open row. */

export const EXPIRY_CHOICES = ['30', '90', '365', 'never'] as const
export type ExpiryChoice = (typeof EXPIRY_CHOICES)[number]
export const DEFAULT_EXPIRY: ExpiryChoice = '90'
export const MAX_NAME = 100

export interface TokenForm {
  name: string
  scope: ApiTokenScope
  expiry: ExpiryChoice
}

export const emptyTokenForm = (): TokenForm => ({ name: '', scope: 'Read', expiry: DEFAULT_EXPIRY })

/** The form's problems, as locale keys (the API checks the same). */
export function checkTokenForm(f: TokenForm): { name?: string } {
  const name = f.name.trim()
  return name.length === 0 || name.length > MAX_NAME ? { name: 'tokens.nameRule' } : {}
}

export function tokenRequest(f: TokenForm): ApiTokenCreation {
  return { name: f.name.trim(), scope: f.scope, expiresInDays: f.expiry === 'never' ? null : Number(f.expiry) }
}

/** When a token made now with this choice would expire, or null for never (the form's "On 22 Feb 2027."). */
export function expiryDate(choice: ExpiryChoice, now: Date = new Date()): Date | null {
  if (choice === 'never') return null
  return new Date(now.getTime() + Number(choice) * 24 * 60 * 60 * 1000)
}

/** `dbt_a1b2c3d4…`: how a token shows in lists. */
export const maskedToken = (prefix: string) => `dbt_${prefix}…`

/** The "Try it" example for a new token. */
export const curlExample = (origin: string, token: string) => `curl -H "Authorization: Bearer ${token}" \\\n  ${origin.replace(/\/+$/, '')}/api/v1/Debts/Own`

/** Only active tokens can be revoked, so only they open (▾). */
export const canOpen = (t: Pick<ApiToken, 'status'>) => t.status === 'Active'

/** How many tokens are active ("2 active · up to 20"). */
export const activeCount = (tokens: Pick<ApiToken, 'status'>[] | undefined) => tokens?.filter((t) => t.status === 'Active').length ?? 0

/**
 * The open row (`?open=<id>`), kept with the page's other query parameters. Opening the open row again closes it.
 * Returns the new search string (with `?`, or empty).
 */
export function toggleOpen(search: string | URLSearchParams, id: string): string {
  const p = new URLSearchParams(search)
  if (p.get('open') === id) p.delete('open')
  else p.set('open', id)
  const s = p.toString()
  return s ? `?${s}` : ''
}

/** The search string without `open`. */
export function closeOpen(search: string | URLSearchParams): string {
  const p = new URLSearchParams(search)
  p.delete('open')
  const s = p.toString()
  return s ? `?${s}` : ''
}
