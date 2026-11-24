import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, type Query } from './client'
import type {
  Bill,
  BillCreation,
  BillLineCreation,
  BillListItem,
  BillListQuery,
  BillPaymentCreation,
  Contact,
  Debt,
  DebtListQuery,
  Exchange,
  ExchangeCreation,
  ExchangeListItem,
  ExchangeListQuery,
  PagingResult,
  Spending,
  SpendingListQuery,
  SpendingSummary,
  TextPreview,
} from './types'

/** Query keys; everything about bills and debts is invalidated together after a change (exchanges are bills too). */
export const keys = {
  bills: (q?: BillListQuery) => (q ? (['bills', 'list', q] as const) : (['bills'] as const)),
  bill: (id: string) => ['bills', 'one', id] as const,
  exchanges: (q: ExchangeListQuery) => ['bills', 'exchanges', q] as const,
  debts: (q?: DebtListQuery | 'all') => (q ? (['debts', q] as const) : (['debts'] as const)),
  contacts: ['contacts'] as const,
  spendings: (q?: SpendingListQuery) => (q ? (['spendings', 'list', q] as const) : (['spendings'] as const)),
  spendingSummary: (from?: string, to?: string) => ['spendings', 'summary', from ?? '', to ?? ''] as const,
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

/** A page of your spendings (your share of finalized bills). */
export function useSpendings(q: SpendingListQuery) {
  return useQuery({
    queryKey: keys.spendings(q),
    queryFn: () => api<PagingResult<Spending>>('/Spendings/Own', { query: q as Query }),
    placeholderData: keepPreviousData,
  })
}

/** Non-cancelled totals per bill currency between two days (yyyy-mm-dd, both optional). */
export function useSpendingSummary(from?: string, to?: string) {
  return useQuery({
    queryKey: keys.spendingSummary(from, to),
    queryFn: () => api<SpendingSummary[]>('/Spendings/Own/summary', { query: { dateFrom: from, dateTo: to } }),
  })
}

export function useContacts() {
  return useQuery({ queryKey: keys.contacts, queryFn: () => api<Contact[]>('/UserContacts/Own'), staleTime: 60_000 })
}

function useInvalidateMoney() {
  const qc = useQueryClient()
  return () =>
    Promise.all([qc.invalidateQueries({ queryKey: keys.bills() }), qc.invalidateQueries({ queryKey: keys.debts() }), qc.invalidateQueries({ queryKey: keys.spendings() })])
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

/** A page of exchanges, one row per pair. `poll` as in useBills. */
export function useExchanges(q: ExchangeListQuery, opts?: { poll?: boolean }) {
  return useQuery({
    queryKey: keys.exchanges(q),
    queryFn: () => api<PagingResult<ExchangeListItem>>('/Bills/exchanges', { query: q as Query }),
    placeholderData: keepPreviousData,
    refetchInterval: opts?.poll ? 1000 : false,
  })
}

/** Creates an exchange as two draft bills. */
export function useCreateExchange() {
  const invalidate = useInvalidateMoney()
  return useMutation({
    mutationFn: (x: ExchangeCreation) => api<Exchange>('/Bills/exchange', { method: 'POST', json: x }),
    onSuccess: () => invalidate(),
  })
}

/** Cancels a bill by id (for an exchange, both bills); it finishes in the background. */
export function useCancelBill() {
  const invalidate = useInvalidateMoney()
  return useMutation({
    mutationFn: (id: string) => api<void>(`/Bills/${id}/Cancel`, { method: 'POST' }),
    onSuccess: () => invalidate(),
  })
}

/**
 * New bill from the form: creates the draft, then finalizes it when asked. Resolves to the new bill's id.
 * `onCreated` runs as soon as the draft exists, so a failed finalize still leads to the bill.
 */
export function useCreateBill() {
  const invalidate = useInvalidateMoney()
  return useMutation({
    mutationFn: async ({ bill, finalize, onCreated }: { bill: BillCreation; finalize: boolean; onCreated?: (id: string) => void }) => {
      const id = await api<string>('/Bills', { method: 'POST', json: bill })
      onCreated?.(id)
      if (finalize) await api<void>(`/Bills/${id}/Finalize`, { method: 'POST' })
      return id
    },
    onSettled: () => invalidate(),
  })
}

const textInit = (text: string) => ({ method: 'POST', body: text, headers: { 'Content-Type': 'text/plain' } })

/** Parses the bot's bill text without saving it (always 200). */
export function previewBillText(text: string) {
  return api<TextPreview>('/Bills/text/preview', textInit(text))
}

/** New bill from the bot's text: a draft, or finalized straight away. Resolves to the new bill's id. */
export function useCreateBillFromText() {
  const invalidate = useInvalidateMoney()
  return useMutation({
    mutationFn: ({ text, finalize }: { text: string; finalize: boolean }) => api<string>('/Bills/text', { ...textInit(text), query: { createDrafted: !finalize } }),
    onSettled: () => invalidate(),
  })
}
