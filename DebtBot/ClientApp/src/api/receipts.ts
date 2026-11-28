import { api } from './client'
import type { ReceiptDraft } from './types'

/** `POST Bills/receipt`: one multipart field `image` (receipts T1). */
export function uploadReceipt(image: File, signal?: AbortSignal): Promise<ReceiptDraft> {
  const body = new FormData()
  body.append('image', image, image.name)
  return api<ReceiptDraft>('/Bills/receipt', { method: 'POST', body, signal })
}

/** `GET Bills/receipt/{id}`: the owner's draft for 15 minutes, else 404. */
export function getReceipt(id: string): Promise<ReceiptDraft> {
  return api<ReceiptDraft>(`/Bills/receipt/${encodeURIComponent(id)}`)
}
