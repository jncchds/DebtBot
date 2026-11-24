import { useMutation, useQueryClient } from '@tanstack/react-query'
import { api, refreshSession } from './client'
import { keys } from './bills'
import { queryKeys } from './hooks'
import type { Contact, PasswordChange, Profile, ProfileUpdate } from './types'

/** Contacts, subscriptions and the profile. Every change refetches the contacts (Contacts, Subscriptions and the badge share them). */

function useInvalidateContacts() {
  const qc = useQueryClient()
  return () => qc.invalidateQueries({ queryKey: keys.contacts })
}

/** `POST UserContacts/Own/by-telegram`: the `@` is optional. */
export function useAddContact() {
  const invalidate = useInvalidateContacts()
  return useMutation({
    mutationFn: (username: string) => api<Contact>('/UserContacts/Own/by-telegram', { method: 'POST', query: { username } }),
    onSuccess: () => invalidate(),
  })
}

export function useRenameContact() {
  const invalidate = useInvalidateContacts()
  return useMutation({
    mutationFn: ({ id, displayName }: { id: string; displayName: string }) => api<Contact>(`/UserContacts/Own/${id}`, { method: 'PUT', json: { displayName } }),
    onSuccess: () => invalidate(),
  })
}

export function useRemoveContact() {
  const invalidate = useInvalidateContacts()
  return useMutation({
    mutationFn: (id: string) => api<void>(`/UserContacts/Own/${id}`, { method: 'DELETE' }),
    onSuccess: () => invalidate(),
  })
}

/**
 * The subscription actions, all for the signed-in user:
 * request = ask for their bills; confirm / decline = their request or subscription to you; cancel = your own.
 */
export type SubscriptionAction = 'request' | 'confirm' | 'decline' | 'cancel'

export function useSubscriptionAction() {
  const invalidate = useInvalidateContacts()
  return useMutation({
    mutationFn: ({ action, contactId }: { action: SubscriptionAction; contactId: string }) =>
      api<void>(`/UserContacts/subscription/${action}`, { method: 'POST', query: { contactId } }),
    onSettled: () => invalidate(),
  })
}

export function useSaveProfile() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (update: ProfileUpdate) => api<Profile>('/profile', { method: 'PUT', json: update }),
    onSuccess: async (profile) => {
      qc.setQueryData(queryKeys.profile, profile)
      // The sidebar's "Sign out (Name)" comes from the session: a refresh brings the new name.
      await refreshSession()
    },
  })
}

export function useSetPassword() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (change: PasswordChange) => api<void>('/auth/password', { method: 'PUT', json: change }),
    onSuccess: () => qc.invalidateQueries({ queryKey: queryKeys.profile }),
  })
}
