import type { BillListItem, Contact } from '../api/types'

/**
 * Following (T7b, mockups/following): `?of=<userId>` on `/`, `/bills` and `/spendings` shows the Debts, Bills or
 * Spendings of someone you have a confirmed subscription to, read-only. Left out = yours.
 * `of` isn't a list filter: list changes (filters, sort, page) keep it, and changing it starts the list afresh.
 */

const ID = /^[0-9a-fA-F-]{1,64}$/

/** The `of` parameter, if it's a well-formed id and not you (your own id is the same as leaving it out). */
export function parseOf(params: URLSearchParams, meId?: string): string | undefined {
  const v = params.get('of')?.trim()
  return v && ID.test(v) && v.toLowerCase() !== meId?.toLowerCase() ? v : undefined
}

/** `path` (which may have a query) with `of` as its first parameter, or without `of` for yours. */
export function withOf(path: string, of?: string): string {
  const [p, q = ''] = path.split('?', 2)
  const params = new URLSearchParams(q)
  params.delete('of')
  const rest = params.toString()
  const all = of ? `of=${encodeURIComponent(of)}${rest ? `&${rest}` : ''}` : rest
  return all ? `${p}?${all}` : p
}

/** The query string for switching whose list you see: filters, sort and page start afresh. */
export function ofSearch(of?: string): string {
  return of ? `?of=${encodeURIComponent(of)}` : ''
}

/** Everyone whose lists you may see (`mySubscription = Confirmed`), A–Z by your name for them. */
export function followedPeople(contacts: Contact[], meId?: string): Contact[] {
  return contacts
    .filter((c) => c.mySubscription === 'Confirmed' && c.id !== meId)
    .sort((a, b) => a.displayName.localeCompare(b.displayName, undefined, { sensitivity: 'base' }) || a.id.localeCompare(b.id))
}

/** The dropdown's search: any word of your name for them, their own name or their @username starts with it. */
export function searchFollowed(people: Contact[], query: string): Contact[] {
  const q = query.trim().toLowerCase().replace(/^@/, '')
  if (!q) return people
  return people.filter((c) => {
    const hay = [c.displayName, c.userDisplayName, (c.telegramUserName ?? '').replace(/^@/, '')].join(' ').toLowerCase()
    return hay.split(/[\s()]+/).some((w) => w.startsWith(q)) || hay.includes(q)
  })
}

/** Whose list a page shows. */
export type Viewing =
  | { kind: 'mine' }
  /** Someone else's: `name` is your name for them (or their own name when they aren't your contact). */
  | { kind: 'of'; id: string; name: string }

export function viewingFor(of: string | undefined, contacts: Contact[]): Viewing {
  if (!of) return { kind: 'mine' }
  const c = contacts.find((x) => x.id.toLowerCase() === of.toLowerCase())
  return { kind: 'of', id: of, name: c?.displayName ?? c?.userDisplayName ?? '…' }
}

/** Read-only switches: someone else's list never offers actions (✓ Accept on Bills). */
export function listActionsAllowed(v: Viewing): boolean {
  return v.kind === 'mine'
}

/** Whose side balances are worked out from: yours, or the viewed person's. */
export function balanceOwner(v: Viewing, meId: string | undefined): string | undefined {
  return v.kind === 'of' ? v.id : meId
}

/** "you're on it": in someone else's list, a bill you take part in too. */
export function youreOnIt(v: Viewing, bill: Pick<BillListItem, 'participantIds' | 'creator'>, meId: string | undefined): boolean {
  return v.kind === 'of' && !!meId && (bill.creator.id === meId || (bill.participantIds ?? []).includes(meId))
}

/** Sidebar "Following": the first five, and whether "… All subscriptions" follows (always, when there's anyone). */
export const FOLLOWING_MAX = 5
