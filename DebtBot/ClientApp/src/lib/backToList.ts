/**
 * The bill page's "← Bills" link goes back to the list the bill was opened from, with its query
 * (`?of=`, filters, sort, page). The list's URL travels in the router state of the link to the bill, so it
 * survives reload and Back/Forward (the browser keeps history state); a bill opened in a new tab or from a
 * bookmark falls back to your own `/bills`.
 */
export type ListOrigin = { from: string }

/** The lists a bill can be opened from, and the label of the way back to each. */
const LISTS: { path: string; label: string }[] = [
  { path: '/bills', label: 'pages.bills.title' },
  { path: '/spendings', label: 'pages.spendings.title' },
  { path: '/exchange', label: 'pages.exchange.title' },
  { path: '/admin/bills', label: 'pages.adminBills.title' },
]

const DEFAULT = { to: '/bills', label: 'pages.bills.title' }

const listFor = (pathname: string) => LISTS.find((l) => l.path === pathname)

const isBillPage = (pathname: string) => /^\/bills\/(?!new(\/|$))[^/]+/.test(pathname)

function isOrigin(state: unknown): state is ListOrigin {
  if (typeof state !== 'object' || state === null) return false
  const from = (state as { from?: unknown }).from
  return typeof from === 'string' && from.startsWith('/') && !from.startsWith('//') && !!listFor(from.split(/[?#]/)[0])
}

/**
 * The router state for a link to a bill from the current location: the list's own URL, or on a bill page
 * (its paired exchange bill, add line, add payment) the origin it already has.
 */
export function originState(pathname: string, search: string, state: unknown): ListOrigin | undefined {
  if (isBillPage(pathname)) return isOrigin(state) ? state : undefined
  return listFor(pathname) ? { from: pathname + search } : undefined
}

/** Where the bill page's back link goes, and its label (an i18n key). */
export function backTarget(state: unknown): { to: string; label: string } {
  if (!isOrigin(state)) return DEFAULT
  return { to: state.from, label: listFor(state.from.split(/[?#]/)[0])!.label }
}
