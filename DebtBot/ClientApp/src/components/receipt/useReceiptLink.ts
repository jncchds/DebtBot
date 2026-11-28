import { useEffect, useRef, useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router'
import { useProfile } from '../../api/hooks'
import { getReceipt } from '../../api/receipts'
import type { ReceiptDraft } from '../../api/types'
import { loadLinkedReceipt, RECEIPT_PARAM, withoutReceiptParam, type ReceiptFailure } from '../../lib/receipt'

export type ReceiptLinkState = { kind: 'loading' } | { kind: 'failed'; failure: ReceiptFailure } | undefined

/**
 * The bot's link `/bills/new?receipt=<id>` (receipts T2): loads the draft and hands it to the page, then takes
 * `receipt` out of the URL so a reload doesn't fill the page again. On the form, with Profile's "New bill opens:
 * Paste text", it moves on to `/bills/new/paste?receipt=<id>` first.
 */
export function useReceiptLink(page: 'form' | 'text', onDraft: (draft: ReceiptDraft) => void): [ReceiptLinkState, () => void] {
  const [search, setSearch] = useSearchParams()
  const navigate = useNavigate()
  const profile = useProfile()
  const id = search.get(RECEIPT_PARAM)
  const [state, setState] = useState<ReceiptLinkState>()
  const handler = useRef(onDraft)
  useEffect(() => {
    handler.current = onDraft
  })

  const waitForProfile = page === 'form' && profile.isLoading
  const openIn = profile.data?.receiptOpenIn

  useEffect(() => {
    if (!id || waitForProfile) return
    if (page === 'form' && openIn === 'Text') {
      navigate(`/bills/new/paste?${RECEIPT_PARAM}=${encodeURIComponent(id)}`, { replace: true })
      return
    }
    let live = true
    void loadLinkedReceipt(id, getReceipt).then((r) => {
      if (!live) return
      setSearch((prev) => withoutReceiptParam(prev), { replace: true })
      if ('draft' in r) handler.current(r.draft)
      else setState({ kind: 'failed', failure: r.failure })
    })
    return () => {
      live = false
    }
  }, [id, page, waitForProfile, openIn, navigate, setSearch])

  // Loading while the link's id is still in the URL (it leaves once the draft or the failure is here).
  return [id ? { kind: 'loading' } : state, () => setState(undefined)]
}
