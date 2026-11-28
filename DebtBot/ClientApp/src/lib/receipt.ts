import { ApiError } from '../api/client'
import type { BillLineCreation, ReceiptDraft, ReceiptErrorBody, ReceiptOpenIn, ReceiptWarning } from '../api/types'
import { localDateTime, type NewBillForm } from './newBill'

/**
 * Receipts on the web (receipts T3, receipts/mockups/receipt): a photo is shrunk, sent to `POST Bills/receipt`, and
 * the draft fills the page it was read on (the New bill form, or the text on Paste text). The bot's link
 * `/bills/new?receipt=<id>` loads a draft with `GET Bills/receipt/{id}`. Pure logic here; the pages own the state.
 */

// ---- The draft into the form ----

/** Whether the form has anything a receipt would overwrite (people alone don't count: they're kept). */
export function formHasContent(f: NewBillForm): boolean {
  return !!(f.description.trim() || f.currency.trim() || f.paymentCurrency.trim() || f.total.trim() || f.lines.length || f.payments.length)
}

/**
 * The form with the receipt in it (user, mockup round 1): the description, date, currency, total and lines are the
 * receipt's, every line has everyone on the bill ticked (as LineEditor starts), and the payments are cleared
 * because they belonged to the old bill. The people on the bill stay; with nobody on it, it's you.
 */
export function draftToForm(draft: ReceiptDraft, f: NewBillForm, meId: string | undefined): NewBillForm {
  const people = f.people.length ? f.people : meId ? [meId] : []
  const date = draft.date ? new Date(draft.date) : null
  const lines: BillLineCreation[] = draft.lines.map((l) => ({
    itemDescription: l.description,
    subtotal: l.subtotal,
    participants: people.map((userId) => ({ userId })),
  }))
  return {
    ...f,
    description: draft.description ?? '',
    date: date && !Number.isNaN(date.getTime()) ? localDateTime(date) : f.date,
    currency: draft.currencyCode ?? '',
    paymentCurrency: '',
    total: draft.totalWithTips != null ? String(draft.totalWithTips) : '',
    people,
    lines,
    payments: [],
  }
}

/** Warnings are shown above the lines (or the text); each can be dismissed. */
export const withoutWarning = (ws: ReceiptWarning[], index: number) => ws.filter((_, i) => i !== index)

// ---- Errors ----

/** Codes the client produces itself, besides the API's (web-ui/api.md, "Receipts"). */
export type ClientCode = 'network' | 'heicNotSupported' | 'cancelled'

/** A failed read: the API's `{ code, message }`, or one of the client's own codes. */
export class ReceiptFailure extends Error {
  code: string
  serverMessage?: string
  retryAfterSeconds?: number
  status?: number
  constructor(code: string, opts: { message?: string; retryAfterSeconds?: number; status?: number } = {}) {
    super(opts.message ?? code)
    this.name = 'ReceiptFailure'
    this.code = code
    this.serverMessage = opts.message
    this.retryAfterSeconds = opts.retryAfterSeconds
    this.status = opts.status
  }
}

/** Over this the server refuses the request (5 MB + 64 KB); a network error then most likely means "too large". */
export const MAX_UPLOAD_BYTES = 5 * 1024 * 1024

/** Any error from an upload as a ReceiptFailure. `size` is what was sent. */
export function toFailure(e: unknown, size: number): ReceiptFailure {
  if (e instanceof ReceiptFailure) return e
  if (e instanceof DOMException && e.name === 'AbortError') return new ReceiptFailure('cancelled')
  if (e instanceof ApiError) {
    const b = (typeof e.body === 'object' && e.body ? e.body : {}) as Partial<ReceiptErrorBody>
    const code = typeof b.code === 'string' && b.code ? b.code : e.status === 413 ? 'tooLarge' : e.status === 504 ? 'timeout' : e.status >= 500 ? 'upstreamError' : 'error'
    return new ReceiptFailure(code, { message: typeof b.message === 'string' && b.message ? b.message : undefined, retryAfterSeconds: b.retryAfterSeconds, status: e.status })
  }
  // fetch's TypeError: the connection broke. The server closes it early on a body over its limit (receipts T1).
  return new ReceiptFailure(size > MAX_UPLOAD_BYTES ? 'tooLarge' : 'network')
}

/** The client retries once on a 5xx or a timeout (spec), never on busy or a feature that's off; and on a broken connection. */
export function isRetryable(f: ReceiptFailure): boolean {
  if (f.code === 'network') return true
  if (f.code === 'busy' || f.code === 'receiptUnavailable') return false
  return f.code === 'timeout' || (f.status !== undefined && f.status >= 500)
}

/** "in 42 minutes" / "in 2 hours", rounded up, as the bot words it. */
export function retryWhen(seconds: number): { key: 'receipt.retryMinutes' | 'receipt.retryHours'; count: number } {
  const minutes = Math.max(1, Math.ceil(seconds / 60))
  return minutes < 60 ? { key: 'receipt.retryMinutes', count: minutes } : { key: 'receipt.retryHours', count: Math.ceil(minutes / 60) }
}

/**
 * What to show for a failure: the API's own message when it sent one (the same as the bot's), else the client's
 * wording for the code; plus when to try again for `rateLimited`.
 */
export function failureText(f: ReceiptFailure): { message?: string; key: string; retry?: ReturnType<typeof retryWhen> } {
  const known = ['network', 'heicNotSupported', 'tooLarge', 'unsupportedType', 'notAReceipt', 'unreadable', 'busy', 'timeout', 'upstreamError', 'rateLimited', 'receiptUnavailable', 'noImage', 'notFound']
  return {
    message: f.code === 'notFound' ? undefined : f.serverMessage,
    key: `receipt.errors.${known.includes(f.code) ? f.code : 'other'}`,
    retry: f.code === 'rateLimited' && f.retryAfterSeconds ? retryWhen(f.retryAfterSeconds) : undefined,
  }
}

// ---- Shrinking (longest side 1600 px, JPEG about 0.8, so under about 1 MB) ----

export const MAX_SIDE = 1600
export const TARGET_BYTES = 1024 * 1024
const QUALITIES = [0.8, 0.7, 0.6]

/** The size that fits within `max` on its longest side, never bigger than the original. */
export function fitWithin(width: number, height: number, max = MAX_SIDE): { width: number; height: number } {
  const scale = Math.min(1, max / Math.max(width, height))
  return { width: Math.max(1, Math.round(width * scale)), height: Math.max(1, Math.round(height * scale)) }
}

/** The file name's stem with `.jpg`: the fake provider picks its fixture by the name (receipts T1). */
export function jpegName(name: string): string {
  const stem = name.replace(/^.*[\\/]/, '').replace(/\.[^.]*$/, '')
  return `${stem || 'receipt'}.jpg`
}

export const isHeic = (file: { name: string; type: string }) => /^image\/hei[cf]/i.test(file.type) || /\.(heic|heif)$/i.test(file.name)

/** Draws the image at most `maxSide` on its longest side and encodes it as JPEG; throws when it can't decode it. */
export type Encoder = (file: Blob, maxSide: number, quality: number) => Promise<Blob>

/**
 * The photo to send: re-encoded as a JPEG with the original name's stem. If the browser can't decode it, a HEIC
 * photo fails with `heicNotSupported` (some browsers can draw HEIC, so it's converted when it can be), and anything
 * else goes as it is, for the server to judge.
 */
export async function shrink(file: File, encode: Encoder): Promise<File> {
  let blob: Blob | undefined
  for (const q of QUALITIES) {
    try {
      blob = await encode(file, MAX_SIDE, q)
    } catch {
      if (isHeic(file)) throw new ReceiptFailure('heicNotSupported')
      return file
    }
    if (blob.size <= TARGET_BYTES) break
  }
  return new File([blob!], jpegName(file.name), { type: 'image/jpeg' })
}

/** The browser's encoder: createImageBitmap (EXIF orientation applied) on a canvas. */
export const canvasEncoder: Encoder = async (file, maxSide, quality) => {
  const bitmap = await createImageBitmap(file, { imageOrientation: 'from-image' })
  try {
    const { width, height } = fitWithin(bitmap.width, bitmap.height, maxSide)
    const canvas = document.createElement('canvas')
    canvas.width = width
    canvas.height = height
    const ctx = canvas.getContext('2d')
    if (!ctx) throw new Error('No canvas')
    ctx.drawImage(bitmap, 0, 0, width, height)
    const blob = await new Promise<Blob | null>((resolve) => canvas.toBlob(resolve, 'image/jpeg', quality))
    if (!blob) throw new Error('No JPEG')
    return blob
  } finally {
    bitmap.close()
  }
}

// ---- Reading ----

export type Uploader = (image: File, signal?: AbortSignal) => Promise<ReceiptDraft>

/** Shrinks and uploads the photo, retrying once on a 5xx, a timeout or a broken connection. Throws ReceiptFailure. */
export async function readReceipt(file: File, opts: { upload: Uploader; encode: Encoder; signal?: AbortSignal }): Promise<ReceiptDraft> {
  const image = await shrink(file, opts.encode)
  let tried = false
  for (;;) {
    try {
      return await opts.upload(image, opts.signal)
    } catch (e) {
      const f = toFailure(e, image.size)
      if (opts.signal?.aborted) throw new ReceiptFailure('cancelled')
      if (tried || !isRetryable(f)) throw f
      tried = true
    }
  }
}

// ---- The bot's link: /bills/new?receipt=<id> ----

export const RECEIPT_PARAM = 'receipt'

/** Where ➕ New bill goes (Profile "New bill opens"). */
export const newBillPath = (mode: ReceiptOpenIn | undefined) => (mode === 'Text' ? '/bills/new/paste' : '/bills/new')

/** The search without `receipt`, so a reload doesn't fill the page again. */
export function withoutReceiptParam(search: URLSearchParams): URLSearchParams {
  const next = new URLSearchParams(search)
  next.delete(RECEIPT_PARAM)
  return next
}

/** Loads the link's draft: the draft, or `expired` for a 404 (someone else's, unknown or older than 15 minutes). */
export async function loadLinkedReceipt(id: string, get: (id: string) => Promise<ReceiptDraft>): Promise<{ draft: ReceiptDraft } | { failure: ReceiptFailure }> {
  try {
    return { draft: await get(id) }
  } catch (e) {
    if (e instanceof ApiError && e.status === 404) return { failure: new ReceiptFailure('notFound', { status: 404 }) }
    return { failure: toFailure(e, 0) }
  }
}

/** The "🧾 From a receipt" button's target, or null: only when receipts are on and the panel isn't open already. */
export function fromReceiptTarget(config: { receiptParse?: boolean } | undefined, page: 'form' | 'text', panelOpen: boolean): string | null {
  if (!config?.receiptParse || panelOpen) return null
  return page === 'form' ? '/bills/new/receipt' : '/bills/new/paste/receipt'
}
