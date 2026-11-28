import { describe, expect, it, vi } from 'vitest'
import { ApiError } from '../api/client'
import type { ReceiptDraft } from '../api/types'
import { emptyNewBill, type NewBillForm } from './newBill'
import {
  draftToForm,
  failureText,
  fitWithin,
  formHasContent,
  fromReceiptTarget,
  isRetryable,
  jpegName,
  loadLinkedReceipt,
  MAX_UPLOAD_BYTES,
  newBillPath,
  readReceipt,
  retryWhen,
  shrink,
  toFailure,
  withoutReceiptParam,
  withoutWarning,
  type Encoder,
} from './receipt'

// The supermarket-discount fixture after the server folded its loyalty discount (receipts T2).
const SUPERMARKET: ReceiptDraft = {
  id: 'd1',
  description: 'FreshMart 2026-11-21',
  date: '2026-11-21T12:00:00Z',
  currencyCode: 'EUR',
  totalWithTips: 12.06,
  lines: [
    { description: 'Milk 1L', subtotal: 1.19 },
    { description: 'Rye bread', subtotal: 2.3 },
    { description: 'Gouda cheese', subtotal: 5.16 },
    { description: 'Apples ×1.235', subtotal: 3.41 },
  ],
  warnings: [{ code: 'discountSpread', params: { name: 'Loyalty discount', amount: '1.00' }, message: 'The discount “Loyalty discount” (1.00 EUR) was spread over all lines.' }],
  model: 'fake/receipt-fixtures',
  text: 'FreshMart 2026-11-21\n\n12.06\nEUR\n\n12.06 @anna_p\n\nMilk 1L\n1.19\n1 @anna_p',
  expiresAt: '2026-11-21T12:15:00Z',
}

const apiError = (status: number, body?: unknown) => new ApiError(status, 'x', body)
const photo = (name = 'supermarket-discount.png', size = 3000, type = 'image/png') => new File([new Uint8Array(size)], name, { type })
const jpegOf = (size: number) => new Blob([new Uint8Array(size)], { type: 'image/jpeg' })

describe('the draft into the form', () => {
  const now = new Date(2026, 10, 28, 10, 5)

  it('fills the description, date, currency, total and lines, with everyone on the bill ticked', () => {
    const before: NewBillForm = { ...emptyNewBill('me', now), people: ['me', 'boris', 'dmitry'] }
    const f = draftToForm(SUPERMARKET, before, 'me')
    expect(f.description).toBe('FreshMart 2026-11-21')
    // The printed time, whatever the browser's time zone.
    expect(f.date).toBe('2026-11-21T12:00')
    expect(f.currency).toBe('EUR')
    expect(f.paymentCurrency).toBe('')
    expect(f.total).toBe('12.06')
    expect(f.people).toEqual(['me', 'boris', 'dmitry'])
    expect(f.lines).toEqual([
      { itemDescription: 'Milk 1L', subtotal: 1.19, participants: [{ userId: 'me' }, { userId: 'boris' }, { userId: 'dmitry' }] },
      { itemDescription: 'Rye bread', subtotal: 2.3, participants: [{ userId: 'me' }, { userId: 'boris' }, { userId: 'dmitry' }] },
      { itemDescription: 'Gouda cheese', subtotal: 5.16, participants: [{ userId: 'me' }, { userId: 'boris' }, { userId: 'dmitry' }] },
      { itemDescription: 'Apples ×1.235', subtotal: 3.41, participants: [{ userId: 'me' }, { userId: 'boris' }, { userId: 'dmitry' }] },
    ])
  })

  it('replaces the old lines and payments but keeps the people', () => {
    const before: NewBillForm = {
      ...emptyNewBill('me', now),
      description: 'Groceries',
      currency: 'PLN',
      paymentCurrency: 'EUR',
      total: '15',
      people: ['me', 'boris'],
      lines: [{ itemDescription: 'Groceries', subtotal: 15, participants: [{ userId: 'boris' }] }],
      payments: [{ userId: 'boris', amount: 15 }],
    }
    const f = draftToForm(SUPERMARKET, before, 'me')
    expect(f.lines).toHaveLength(4)
    expect(f.payments).toEqual([])
    expect(f.people).toEqual(['me', 'boris'])
    expect(f.paymentCurrency).toBe('')
    expect(f.currency).toBe('EUR')
  })

  it('leaves the currency and total empty when the receipt has none, keeps the date without one, and puts you on the bill', () => {
    const before: NewBillForm = { ...emptyNewBill(undefined, now), people: [] }
    const f = draftToForm({ ...SUPERMARKET, currencyCode: null, totalWithTips: null, date: null }, before, 'me')
    expect(f.currency).toBe('')
    expect(f.total).toBe('')
    expect(f.date).toBe(before.date)
    expect(f.people).toEqual(['me'])
    expect(f.lines[0].participants).toEqual([{ userId: 'me' }])
  })

  it('asks before replacing only when the form has content; people alone don’t count', () => {
    const empty = { ...emptyNewBill('me', now), people: ['me', 'boris'] }
    expect(formHasContent(empty)).toBe(false)
    expect(formHasContent({ ...empty, description: 'x' })).toBe(true)
    expect(formHasContent({ ...empty, currency: 'EUR' })).toBe(true)
    expect(formHasContent({ ...empty, total: '3' })).toBe(true)
    expect(formHasContent({ ...empty, lines: [{ itemDescription: 'a', subtotal: 1, participants: [] }] })).toBe(true)
    expect(formHasContent({ ...empty, payments: [{ userId: 'me', amount: 1 }] })).toBe(true)
  })

  it('dismisses one warning at a time', () => {
    const ws = [SUPERMARKET.warnings[0], { code: 'tipsAdded', params: { amount: '3.50' }, message: 'Tips' }]
    expect(withoutWarning(ws, 0)).toEqual([ws[1]])
    expect(withoutWarning(ws, 1)).toEqual([ws[0]])
  })
})

describe('errors', () => {
  const codes: [number, string][] = [
    [413, 'tooLarge'],
    [415, 'unsupportedType'],
    [415, 'heicNotSupported'],
    [422, 'notAReceipt'],
    [422, 'unreadable'],
    [503, 'busy'],
    [504, 'timeout'],
    [502, 'upstreamError'],
    [429, 'rateLimited'],
    [503, 'receiptUnavailable'],
  ]

  it.each(codes)('%i %s shows the API’s message, else the client’s wording for the code', (status, code) => {
    const withMessage = toFailure(apiError(status, { code, message: `Server says ${code}` }), 1000)
    expect(withMessage.code).toBe(code)
    expect(failureText(withMessage).message).toBe(`Server says ${code}`)
    const bare = toFailure(apiError(status, { code }), 1000)
    expect(failureText(bare)).toMatchObject({ message: undefined, key: `receipt.errors.${code}` })
  })

  it('says when to try again after rateLimited', () => {
    const f = toFailure(apiError(429, { code: 'rateLimited', message: 'Too many', window: 'hour', retryAfterSeconds: 2501 }), 1000)
    expect(failureText(f).retry).toEqual({ key: 'receipt.retryMinutes', count: 42 })
    expect(retryWhen(30)).toEqual({ key: 'receipt.retryMinutes', count: 1 })
    expect(retryWhen(3600)).toEqual({ key: 'receipt.retryHours', count: 1 })
    expect(retryWhen(3601)).toEqual({ key: 'receipt.retryHours', count: 2 })
  })

  it('reads a status without a body by its status', () => {
    expect(toFailure(apiError(413), 1000).code).toBe('tooLarge')
    expect(toFailure(apiError(504), 1000).code).toBe('timeout')
    expect(toFailure(apiError(500), 1000).code).toBe('upstreamError')
    expect(failureText(toFailure(apiError(418), 1000)).key).toBe('receipt.errors.other')
  })

  it('a broken connection is tooLarge for a large upload, else a retryable network error', () => {
    expect(toFailure(new TypeError('Failed to fetch'), MAX_UPLOAD_BYTES + 1).code).toBe('tooLarge')
    const small = toFailure(new TypeError('Failed to fetch'), 900_000)
    expect(small.code).toBe('network')
    expect(isRetryable(small)).toBe(true)
  })

  it('retries 5xx and timeouts, never busy, an off feature or a 4xx', () => {
    expect(isRetryable(toFailure(apiError(502, { code: 'upstreamError' }), 1))).toBe(true)
    expect(isRetryable(toFailure(apiError(504, { code: 'timeout' }), 1))).toBe(true)
    expect(isRetryable(toFailure(apiError(503, { code: 'busy' }), 1))).toBe(false)
    expect(isRetryable(toFailure(apiError(503, { code: 'receiptUnavailable' }), 1))).toBe(false)
    expect(isRetryable(toFailure(apiError(422, { code: 'notAReceipt' }), 1))).toBe(false)
    expect(isRetryable(toFailure(apiError(429, { code: 'rateLimited' }), 1))).toBe(false)
  })

  it('an aborted upload is cancelled', () => {
    expect(toFailure(new DOMException('x', 'AbortError'), 1).code).toBe('cancelled')
  })
})

describe('shrinking', () => {
  it('fits the longest side into 1600 px and never enlarges', () => {
    expect(fitWithin(4032, 3024)).toEqual({ width: 1600, height: 1200 })
    expect(fitWithin(3024, 4032)).toEqual({ width: 1200, height: 1600 })
    expect(fitWithin(800, 600)).toEqual({ width: 800, height: 600 })
  })

  it('keeps the file name stem (the fake provider picks its fixture by it)', () => {
    expect(jpegName('supermarket-discount.png')).toBe('supermarket-discount.jpg')
    expect(jpegName('IMG_1234.HEIC')).toBe('IMG_1234.jpg')
    expect(jpegName('C:\\fakepath\\cafe-eur.webp')).toBe('cafe-eur.jpg')
    expect(jpegName('noext')).toBe('noext.jpg')
    expect(jpegName('.jpg')).toBe('receipt.jpg')
  })

  it('re-encodes as JPEG 0.8 with the stem kept', async () => {
    const encode = vi.fn<Encoder>(async () => jpegOf(400_000))
    const out = await shrink(photo('supermarket-discount.png', 4_000_000), encode)
    expect(out.name).toBe('supermarket-discount.jpg')
    expect(out.type).toBe('image/jpeg')
    expect(out.size).toBe(400_000)
    expect(encode).toHaveBeenCalledTimes(1)
    expect(encode.mock.calls[0].slice(1)).toEqual([1600, 0.8])
  })

  it('lowers the quality while the JPEG is over 1 MB', async () => {
    const sizes = [1_500_000, 1_100_000, 900_000]
    const encode = vi.fn<Encoder>(async () => jpegOf(sizes.shift()!))
    const out = await shrink(photo(), encode)
    expect(encode.mock.calls.map((c) => c[2])).toEqual([0.8, 0.7, 0.6])
    expect(out.size).toBe(900_000)
  })

  it('converts HEIC when the browser can draw it, else heicNotSupported', async () => {
    const heic = photo('IMG_0042.heic', 2_000_000, 'image/heic')
    expect((await shrink(heic, async () => jpegOf(300_000))).name).toBe('IMG_0042.jpg')
    await expect(shrink(heic, async () => Promise.reject(new Error('decode')))).rejects.toMatchObject({ code: 'heicNotSupported' })
  })

  it('sends other files the browser can’t draw as they are, for the server to judge', async () => {
    const gif = photo('anim.gif', 1000, 'image/gif')
    expect(await shrink(gif, async () => Promise.reject(new Error('decode')))).toBe(gif)
  })
})

describe('reading', () => {
  const encode: Encoder = async () => jpegOf(500_000)

  it('uploads the shrunk photo and returns the draft', async () => {
    const upload = vi.fn(async (image: File) => ({ ...SUPERMARKET, description: image.name }))
    const draft = await readReceipt(photo('supermarket-discount.png'), { upload, encode })
    expect(draft.description).toBe('supermarket-discount.jpg')
    expect(upload).toHaveBeenCalledTimes(1)
  })

  it('retries once on a 5xx or a timeout, never more', async () => {
    for (const [status, code] of [
      [502, 'upstreamError'],
      [504, 'timeout'],
    ] as const) {
      const upload = vi.fn(async () => Promise.reject(apiError(status, { code, message: code })))
      await expect(readReceipt(photo(), { upload, encode })).rejects.toMatchObject({ code })
      expect(upload).toHaveBeenCalledTimes(2)
    }
    const flaky = vi.fn<(f: File) => Promise<ReceiptDraft>>().mockRejectedValueOnce(apiError(502, { code: 'upstreamError' })).mockResolvedValueOnce(SUPERMARKET)
    expect(await readReceipt(photo(), { upload: flaky, encode })).toBe(SUPERMARKET)
    expect(flaky).toHaveBeenCalledTimes(2)
  })

  it('doesn’t retry busy, not a receipt or the rate limit', async () => {
    for (const [status, code] of [
      [503, 'busy'],
      [422, 'notAReceipt'],
      [429, 'rateLimited'],
    ] as const) {
      const upload = vi.fn(async () => Promise.reject(apiError(status, { code })))
      await expect(readReceipt(photo(), { upload, encode })).rejects.toMatchObject({ code })
      expect(upload).toHaveBeenCalledTimes(1)
    }
  })

  it('a cancelled read is cancelled, without a retry', async () => {
    const ctrl = new AbortController()
    const upload = vi.fn(async () => {
      ctrl.abort()
      throw new DOMException('aborted', 'AbortError')
    })
    await expect(readReceipt(photo(), { upload, encode, signal: ctrl.signal })).rejects.toMatchObject({ code: 'cancelled' })
    expect(upload).toHaveBeenCalledTimes(1)
  })
})

describe('the bot’s link and the button', () => {
  it('loads the draft', async () => {
    const get = vi.fn(async () => SUPERMARKET)
    expect(await loadLinkedReceipt('d1', get)).toEqual({ draft: SUPERMARKET })
    expect(get).toHaveBeenCalledWith('d1')
  })

  it('a 404 is "This receipt has expired or isn’t yours."', async () => {
    const r = await loadLinkedReceipt('old', async () => Promise.reject(apiError(404, { code: 'notFound', message: 'Not found' })))
    expect('failure' in r && failureText(r.failure)).toEqual({ message: undefined, key: 'receipt.errors.notFound', retry: undefined })
  })

  it('takes receipt out of the URL and keeps the rest', () => {
    expect(withoutReceiptParam(new URLSearchParams('receipt=abc')).toString()).toBe('')
    expect(withoutReceiptParam(new URLSearchParams('receipt=abc&preview=1')).toString()).toBe('preview=1')
  })

  it('➕ New bill follows the Profile setting', () => {
    expect(newBillPath('Text')).toBe('/bills/new/paste')
    expect(newBillPath('Form')).toBe('/bills/new')
    expect(newBillPath(undefined)).toBe('/bills/new')
  })

  it('the From a receipt button shows only when receipts are on and the panel is closed', () => {
    expect(fromReceiptTarget({ receiptParse: false }, 'form', false)).toBeNull()
    expect(fromReceiptTarget(undefined, 'form', false)).toBeNull()
    expect(fromReceiptTarget({ receiptParse: true }, 'form', true)).toBeNull()
    expect(fromReceiptTarget({ receiptParse: true }, 'form', false)).toBe('/bills/new/receipt')
    expect(fromReceiptTarget({ receiptParse: true }, 'text', false)).toBe('/bills/new/paste/receipt')
  })
})
