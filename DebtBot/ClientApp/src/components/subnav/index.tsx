import type { ReactNode } from 'react'
import { matchPath } from 'react-router'
import { AdminSubNav, NewBillSubNav, ProfileSubNav } from './static'

/**
 * Page-specific sidebar sections below the divider (mockups/shell/README.md).
 * The first entry whose pattern matches the current path wins, so list specific paths first.
 * T5–T8 add their sections here: Debts (balances), Bills (drafts + recent), Exchange, Spendings, Contacts.
 */
const SUBNAVS: { path: string; element: ReactNode }[] = [
  { path: '/bills/new/*', element: <NewBillSubNav /> },
  { path: '/profile/*', element: <ProfileSubNav /> },
  { path: '/admin/*', element: <AdminSubNav /> },
]

export function subNavFor(pathname: string): ReactNode {
  return SUBNAVS.find((s) => matchPath(s.path, pathname))?.element ?? null
}
