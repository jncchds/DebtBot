import { useTranslation } from 'react-i18next'
import clsx from 'clsx'
import type { ReceiptDraft } from '../../api/types'
import { Spinner } from '../ui'
import { FailureMessage, ReplaceQuestion } from './Receipt'
import type { ReceiptLinkState } from './useReceiptLink'

/**
 * Above the page when the panel isn't open: the bot's link loading, its failure ("This receipt has expired or
 * isn't yours."), or the replace question for a draft it brought.
 */
export function ReceiptLinkNotice({
  target,
  link,
  onDismiss,
  pending,
  onReplace,
  onKeep,
  className = 'mb-4',
}: {
  target: 'form' | 'text'
  link: ReceiptLinkState
  onDismiss: () => void
  pending?: ReceiptDraft
  onReplace: () => void
  onKeep: () => void
  className?: string
}) {
  const { t } = useTranslation()
  if (pending)
    return (
      <div className={className}>
        <ReplaceQuestion draft={pending} target={target} onReplace={onReplace} onKeep={onKeep} />
      </div>
    )
  if (link?.kind === 'loading')
    return (
      <div className={clsx('card flex items-center gap-2 px-4 py-3.5 text-sm muted', className)} role="status">
        <Spinner className="h-4 w-4" />
        {t('receipt.loading')}
      </div>
    )
  if (link?.kind === 'failed')
    return (
      <div className={clsx('alert-danger flex items-start gap-2', className)} role="alert">
        <span className="min-w-0 flex-1">
          <FailureMessage failure={link.failure} />
        </span>
        <button type="button" className="px-1" title={t('receipt.dismiss')} aria-label={t('receipt.dismiss')} onClick={onDismiss}>
          ✕
        </button>
      </div>
    )
  return null
}
