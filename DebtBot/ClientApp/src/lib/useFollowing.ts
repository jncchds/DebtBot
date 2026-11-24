import { useMemo } from 'react'
import { useSearchParams } from 'react-router'
import { useContacts } from '../api/bills'
import { ApiError } from '../api/client'
import { useAuth } from '../auth/AuthContext'
import { followedPeople, parseOf, viewingFor, type Viewing } from './following'

export type FollowPage = 'debts' | 'bills' | 'spendings'

/** Whose list the current URL asks for, and the people you may view (mockups/following). */
export function useFollowing() {
  const [params] = useSearchParams()
  const { user } = useAuth()
  const contacts = useContacts()
  const of = parseOf(params, user?.id)
  const followed = useMemo(() => followedPeople(contacts.data ?? [], user?.id), [contacts.data, user?.id])
  const viewing = useMemo<Viewing>(() => viewingFor(of, contacts.data ?? []), [of, contacts.data])
  return { of, viewing, followed }
}

/** Whether a list query failed because you may not see that person's lists (403, or 404). */
export function isNoAccess(error: unknown): boolean {
  return error instanceof ApiError && (error.status === 403 || error.status === 404)
}
