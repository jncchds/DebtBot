import { useMemo } from 'react'
import { useTranslation } from 'react-i18next'
import { useContacts } from '../api/bills'
import { useAuth } from '../auth/AuthContext'

export interface Person {
  id: string
  /** Your name for them; for yourself "Anna (you)". */
  name: string
  isMe: boolean
}

/**
 * The people you can put on a bill or filter by: yourself first, then your contacts by name (api.md: the picker
 * is contacts plus yourself; there's no global user search).
 */
export function usePeople() {
  const { t } = useTranslation()
  const { user } = useAuth()
  const contacts = useContacts()
  const people = useMemo<Person[]>(() => {
    const me: Person[] = user ? [{ id: user.id, name: t('common.youNamed', { name: user.displayName }), isMe: true }] : []
    const others = (contacts.data ?? [])
      .filter((c) => c.id !== user?.id)
      .map((c) => ({ id: c.id, name: c.displayName, isMe: false }))
    return [...me, ...others]
  }, [contacts.data, user, t])
  return { people, loading: contacts.isLoading, error: contacts.error }
}

/** A user's name as you see it: "you" for yourself, else your contact name, else their own name. */
export function useNameOf() {
  const { t } = useTranslation()
  const { user } = useAuth()
  const contacts = useContacts()
  return useMemo(() => {
    const names = new Map((contacts.data ?? []).map((c) => [c.id, c.displayName]))
    return {
      /** For sentences: "you". */
      short: (u: { id: string; displayName: string }) => (u.id === user?.id ? t('common.you') : (names.get(u.id) ?? u.displayName)),
      /** For lists: "Anna (you)". */
      long: (u: { id: string; displayName: string }) => (u.id === user?.id ? t('common.youNamed', { name: user.displayName }) : (names.get(u.id) ?? u.displayName)),
      isMe: (id: string) => id === user?.id,
    }
  }, [contacts.data, user, t])
}
