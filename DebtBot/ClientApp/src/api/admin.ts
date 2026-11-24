import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useAuth } from '../auth/AuthContext'
import { api, type Query } from './client'
import type { AdminBillListQuery, AdminSetPassword, AdminUser, AdminUserQuery, BillListItem, MergeSuggestion, PagingResult, UserModel, UserRole } from './types'

/** 🛠 Admin: users, merge suggestions and every bill. All of it is admin-only; the queries stay off for everyone else. */

export const adminKeys = {
  all: ['admin'] as const,
  users: (q?: AdminUserQuery) => (q ? (['admin', 'users', q] as const) : (['admin', 'users'] as const)),
  user: (id: string) => ['admin', 'user', id] as const,
  suggestions: ['admin', 'suggestions'] as const,
  bills: (q: AdminBillListQuery) => ['admin', 'bills', q] as const,
}

export function useAdminUsers(q: AdminUserQuery, opts?: { enabled?: boolean }) {
  const { isAdmin } = useAuth()
  return useQuery({
    queryKey: adminKeys.users(q),
    queryFn: () => api<PagingResult<AdminUser>>('/Users', { query: q as Query }),
    placeholderData: keepPreviousData,
    enabled: isAdmin && (opts?.enabled ?? true),
  })
}

/** One user by id (`GET Users/{id}`), e.g. to name a user picked in a filter. */
export function useAdminUser(id: string | undefined) {
  const { isAdmin } = useAuth()
  return useQuery({
    queryKey: adminKeys.user(id ?? ''),
    queryFn: () => api<UserModel>(`/Users/${id}`),
    enabled: isAdmin && !!id,
    staleTime: 5 * 60_000,
  })
}

/**
 * A user's full row (login, counts) by id: from `known` rows when it's there, or else looked up: `GET Users/{id}`
 * gives the name, and a search by that name finds the row.
 */
export function useAdminUserRow(id: string | undefined, known?: AdminUser[]) {
  const have = id ? known?.find((u) => u.id === id) : undefined
  const one = useAdminUser(have ? undefined : id)
  const found = useAdminUsers({ search: one.data?.displayName, countPerPage: 100 }, { enabled: !have && !!one.data })
  const row = have ?? (id ? found.data?.items.find((u) => u.id === id) : undefined)
  return { row, error: one.error ?? found.error, loading: !!id && !row && !one.error && !found.error && (one.isPending || found.isPending) }
}

/** Pairs that look like the same person; it feeds Merge users, the "possible duplicate" marker and the sidebar badge. */
export function useMergeSuggestions() {
  const { isAdmin } = useAuth()
  return useQuery({
    queryKey: adminKeys.suggestions,
    queryFn: () => api<MergeSuggestion[]>('/Users/merge/suggestions'),
    enabled: isAdmin,
  })
}

export function useAllBills(q: AdminBillListQuery) {
  const { isAdmin } = useAuth()
  return useQuery({
    queryKey: adminKeys.bills(q),
    queryFn: () => api<PagingResult<BillListItem>>('/Bills/All', { query: q as Query }),
    placeholderData: keepPreviousData,
    enabled: isAdmin,
  })
}

function useInvalidateAdmin() {
  const qc = useQueryClient()
  return () => qc.invalidateQueries({ queryKey: adminKeys.all })
}

export function useSetUserPassword() {
  const invalidate = useInvalidateAdmin()
  return useMutation({
    mutationFn: ({ id, ...body }: AdminSetPassword & { id: string }) => api<void>(`/Users/${id}/password`, { method: 'POST', json: body }),
    onSuccess: () => invalidate(),
  })
}

export function useSetRole() {
  const invalidate = useInvalidateAdmin()
  return useMutation({
    mutationFn: ({ id, role }: { id: string; role: UserRole }) => api<void>(`/Users/${id}/Role`, { method: 'POST', query: { role } }),
    onSettled: () => invalidate(),
  })
}

/** `POST Users/{id}/tokens/revoke-all`: returns how many were revoked. */
export function useRevokeAllTokens() {
  const invalidate = useInvalidateAdmin()
  return useMutation({
    mutationFn: (id: string) => api<{ revoked: number }>(`/Users/${id}/tokens/revoke-all`, { method: 'POST' }),
    onSettled: () => invalidate(),
  })
}

export function useDeleteUser() {
  const invalidate = useInvalidateAdmin()
  return useMutation({
    mutationFn: (id: string) => api<void>(`/Users/${id}`, { method: 'DELETE' }),
    onSuccess: () => invalidate(),
  })
}

/** `POST Users/merge`. A merge moves bills, debts and contacts, so everything is refetched. */
export function useMergeUsers() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (body: { oldUserId: string; newUserId: string }) => api<AdminUser>('/Users/merge', { method: 'POST', json: body }),
    onSuccess: () => qc.invalidateQueries(),
  })
}
