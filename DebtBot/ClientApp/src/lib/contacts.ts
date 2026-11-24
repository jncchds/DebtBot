import type { Contact } from '../api/types'

/**
 * Contacts and subscriptions in the browser: `GET UserContacts/Own` returns every contact, so search, sort,
 * "Recently added" and the subscription groups are worked out here (mockups/contacts, mockups/subscriptions).
 */

const bare = (s: string | null | undefined) => (s ?? '').trim().toLowerCase().replace(/^@+/, '')

/** Your name for them, A–Z, ignoring case (then by id, so the order is stable). */
export function sortContacts(contacts: readonly Contact[]): Contact[] {
  return [...contacts].sort((a, b) => a.displayName.localeCompare(b.displayName, undefined, { sensitivity: 'base' }) || a.id.localeCompare(b.id))
}

/** Whether a contact matches the search: your name, their own name or the username contains it, ignoring case and `@`. */
export function matchesContact(c: Contact, query: string): boolean {
  const q = bare(query)
  if (!q) return true
  return [c.displayName, c.userDisplayName, c.telegramUserName].some((s) => bare(s).includes(q))
}

/** The contact list as shown: matching contacts, A–Z. */
export function searchContacts(contacts: readonly Contact[], query: string): Contact[] {
  return sortContacts(contacts.filter((c) => matchesContact(c, query)))
}

/** The newest contacts by `addedAt`; contacts without a date (added before it was recorded) come last. */
export function recentContacts(contacts: readonly Contact[], count = 3): Contact[] {
  return [...contacts]
    .sort((a, b) => {
      if (a.addedAt && b.addedAt) return b.addedAt.localeCompare(a.addedAt)
      if (a.addedAt) return -1
      if (b.addedAt) return 1
      return a.displayName.localeCompare(b.displayName, undefined, { sensitivity: 'base' })
    })
    .slice(0, count)
}

/** A placeholder never started the bot: they can be on bills but can't confirm a subscription. */
export const isPlaceholder = (c: Contact) => !c.telegramBotEnabled

export interface SubscriptionGroups {
  /** They asked for your bills and debts; you can confirm or decline. */
  requests: Contact[]
  /** They get your bills and debts (you confirmed). */
  theyGetYours: Contact[]
  /** You get theirs, or asked and are waiting. */
  yours: Contact[]
}

/** The three cards on /subscriptions, each A–Z by your name for them. */
export function groupSubscriptions(contacts: readonly Contact[]): SubscriptionGroups {
  const sorted = sortContacts(contacts)
  return {
    requests: sorted.filter((c) => c.theirSubscription === 'Pending'),
    theyGetYours: sorted.filter((c) => c.theirSubscription === 'Confirmed'),
    yours: sorted.filter((c) => c.mySubscription !== 'None'),
  }
}

/** The Subscriptions nav badge: how many people are waiting for you to confirm. */
export const pendingRequestCount = (contacts: readonly Contact[] | undefined) => (contacts ?? []).filter((c) => c.theirSubscription === 'Pending').length

/** `yyyy-mm-dd` of a local date. */
export function isoDay(d: Date): string {
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`
}

/** The first and last day of the calendar month of `now` (local time), for "This month". */
export function monthRange(now: Date): { from: string; to: string } {
  return { from: isoDay(new Date(now.getFullYear(), now.getMonth(), 1)), to: isoDay(new Date(now.getFullYear(), now.getMonth() + 1, 0)) }
}
