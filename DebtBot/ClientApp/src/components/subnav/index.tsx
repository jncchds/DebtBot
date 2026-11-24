import type { ReactNode } from 'react'
import { matchPath } from 'react-router'
import { BillsSubNav, DebtsSubNav, ExchangeSubNav, NewBillSubNav } from './money'
import { ContactsSubNav, SpendingsSubNav, SubscriptionsSubNav } from './people'
import { AdminSubNav, ProfileSubNav } from './static'

/**
 * Page-specific sidebar sections below the divider (mockups/shell/README.md).
 * The first entry whose pattern matches the current path wins, so list specific paths first.
 * T8 adds the admin badge.
 */
const SUBNAVS: { path: string; element: ReactNode }[] = [
  { path: '/', element: <DebtsSubNav /> },
  { path: '/bills/new/*', element: <NewBillSubNav /> },
  { path: '/bills/*', element: <BillsSubNav /> },
  { path: '/exchange/*', element: <ExchangeSubNav /> },
  { path: '/spendings/*', element: <SpendingsSubNav /> },
  { path: '/contacts/*', element: <ContactsSubNav /> },
  { path: '/subscriptions/*', element: <SubscriptionsSubNav /> },
  { path: '/profile/*', element: <ProfileSubNav /> },
  { path: '/admin/*', element: <AdminSubNav /> },
]

export function subNavFor(pathname: string): ReactNode {
  return SUBNAVS.find((s) => matchPath(s.path, pathname))?.element ?? null
}
