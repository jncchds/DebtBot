import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, type Query } from './client'
import type { Bill, BillLineCreation, BillListItem, BillListQuery, BillPaymentCreation, Contact, Debt, DebtListQuery, Exchange, PagingResult } from './types'

/** Query keys; everything about bills and debts is invalidated together after a change. */
export const keys = {
  bills: (q?: BillListQuery) => (q ? (['bills', 'list', q] as const) : (['bills'] as const)),
  bill: (id: string) => ['bills', 'one', id] as const,
  debts: (q?: DebtListQuery | 'all') => (q ? (['debts', q] as const) : (['debts'] as const)),
  contacts: ['contacts'] as const,
}

/** A page of bills. `poll` refetches every second while it's true (e.g. right after accepting an exchange). */
export function useBills(q: BillListQuery, opts?: { enabled?: boolean; poll?: boolean }) {
  return useQuery({
    queryKey: keys.bills(q),
    queryFn: () => api<PagingResult<BillListItem>>('/Bills', { query: q as Query }),
    placeholderData: keepPreviousData,
    enabled: opts?.enabled,
    refetchInterval: opts?.poll ? 1000 : false,
  })
}

/** One bill. `poll` refetches every second while it's true (finalize and cancel run in the background). */
export function useBill(id: string | undefined, poll = false) {
  return useQuery({
    queryKey: keys.bill(id ?? ''),
    queryFn: () => api<Bill>(`/Bills/${id}`),
    enabled: !!id,
    refetchInterval: poll ? 1000 : false,
  })
}

export function useOwnDebts(q: DebtListQuery) {
  return useQuery({
    queryKey: keys.debts(q),
    queryFn: () => api<PagingResult<Debt>>('/Debts/Own', { query: q as Query }),
    placeholderData: keepPreviousData,
  })
}

/** All your balances at once (totals, sidebar, filter options). */
export function useAllOwnDebts() {
  return useQuery({
    queryKey: keys.debts('all'),
    queryFn: () => api<PagingResult<Debt>>('/Debts/Own').then((r) => r.items),
  })
}

export function useContacts() {
  return useQuery({ queryKey: keys.contacts, queryFn: () => api<Contact[]>('/UserContacts/Own'), staleTime: 60_000 })
}

function useInvalidateMoney() {
  const qc = useQueryClient()
  return () => Promise.all([qc.invalidateQueries({ queryKey: keys.bills() }), qc.invalidateQueries({ queryKey: keys.debts() })])
}

export function useAddLines(billId: string) {
  const qc = useQueryClient()
  const invalidate = useInvalidateMoney()
  return useMutation({
    mutationFn: (lines: BillLineCreation[]) => api<Bill>(`/Bills/${billId}/lines`, { method: 'POST', json: lines }),
    onSuccess: (bill) => {
      qc.setQueryData(keys.bill(billId), bill)
      return invalidate()
    },
  })
}

export function useAddPayments(billId: string) {
  const qc = useQueryClient()
  const invalidate = useInvalidateMoney()
  return useMutation({
    mutationFn: (payments: BillPaymentCreation[]) => api<Bill>(`/Bills/${billId}/payments`, { method: 'POST', json: payments }),
    onSuccess: (bill) => {
      qc.setQueryData(keys.bill(billId), bill)
      return invalidate()
    },
  })
}

/** Finalize or cancel: both return at once and finish in the background. */
export function useBillAction(billId: string) {
  const invalidate = useInvalidateMoney()
  return useMutation({
    mutationFn: (action: 'Finalize' | 'Cancel') => api<void>(`/Bills/${billId}/${action}`, { method: 'POST' }),
    onSuccess: () => invalidate(),
  })
}

export function useAcceptExchange() {
  const invalidate = useInvalidateMoney()
  return useMutation({
    mutationFn: (pair: Exchange) => api<Exchange>('/Bills/exchange/finalize', { method: 'POST', json: pair }),
    onSuccess: () => invalidate(),
  })
}
