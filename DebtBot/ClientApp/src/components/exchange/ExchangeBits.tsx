import type { ReactNode } from 'react'
import { Link } from 'react-router'
import { useTranslation } from 'react-i18next'
import type { ProcessingState } from '../../api/types'
import { formatRate, unitRate, type ExchangeDeal } from '../../lib/exchange'
import { useFormat } from '../../lib/useFormat'
import { useNameOf } from '../../lib/usePeople'

/**
 * The pieces every view of an exchange shares, so the Exchange page, the Bill page and the Bills list use the
 * same words (mockups/exchange): the status pill, the rate both ways and the banner with Accept.
 */

const PILL: Record<ProcessingState, [string, string]> = {
  Draft: ['pill-accent', 'exchange.status.waiting'],
  Ready: ['pill-muted', 'exchange.status.accepting'],
  Processed: ['pill-muted', 'exchange.status.accepted'],
  Cancelled: ['pill-danger', 'exchange.status.cancelled'],
}

export function ExchangeStatusPill({ status }: { status: ProcessingState }) {
  const { t } = useTranslation()
  return <span className={`${PILL[status][0]} whitespace-nowrap`}>{t(PILL[status][1])}</span>
}

/** "1 EUR = 4.3000 PLN" and "1 PLN = 0.2326 EUR": always both, always in full (user, mockup round 4). */
export function RateBothWays({ a, aCurrency, b, bCurrency, stacked, className }: { a: number | bigint; aCurrency: string; b: number | bigint; bCurrency: string; stacked?: boolean; className?: string }) {
  const { i18n } = useTranslation()
  const ab = unitRate(a, b)
  const ba = unitRate(b, a)
  if (!ab || !ba) return null
  const one = `1 ${aCurrency} = ${formatRate(ab, i18n.language)} ${bCurrency}`
  const two = `1 ${bCurrency} = ${formatRate(ba, i18n.language)} ${aCurrency}`
  if (stacked)
    return (
      <span className={`inline-flex flex-col text-[0.8rem] leading-snug tabular-nums whitespace-nowrap ${className ?? ''}`}>
        <span>{one}</span>
        <span className="muted">{two}</span>
      </span>
    )
  return (
    <span className={`inline-flex flex-wrap gap-x-4 gap-y-0.5 tabular-nums ${className ?? ''}`}>
      <b className="whitespace-nowrap">{one}</b>
      <b className="whitespace-nowrap">{two}</b>
    </span>
  )
}

/** "You give Boris 100.00 EUR, Boris gives you 430.00 PLN." (or between two other people). `deal` is already from your side. */
export function DealText({ deal }: { deal: ExchangeDeal }) {
  const { t } = useTranslation()
  const f = useFormat()
  const names = useNameOf()
  const give = f.money(deal.fromAmount, deal.fromCurrency)
  const back = f.money(deal.toAmount, deal.toCurrency)
  return names.isMe(deal.from.id)
    ? t('exchange.dealYou', { name: names.short(deal.to), give, back })
    : t('exchange.dealOthers', { giver: names.short(deal.from), taker: names.short(deal.to), give, back })
}

/**
 * A draft or finished exchange: who gives whom what, the rate both ways, both bills, and the actions.
 * Used on the Exchange page (after Create, or from "Waiting to accept") and on each of its bills.
 */
export function ExchangeBanner({ deal, status, actions, note }: { deal: ExchangeDeal; status?: ProcessingState; actions?: ReactNode; note?: ReactNode }) {
  const { t } = useTranslation()
  const names = useNameOf()
  const other = names.isMe(deal.from.id) ? deal.to : names.isMe(deal.to.id) ? deal.from : null
  return (
    <div className="mb-4 flex flex-wrap items-center gap-x-4 gap-y-3 rounded-[10px] border border-accent/40 bg-accent-faint p-4">
      <span className="text-[1.4rem]" aria-hidden>
        🔁
      </span>
      <div className="min-w-0 flex-[1_1_18rem] text-[0.9rem] leading-relaxed">
        <b>{other ? t('exchange.withName', { name: names.short(other) }) : t('exchange.between', { a: names.short(deal.from), b: names.short(deal.to) })}</b>{' '}
        {status && <ExchangeStatusPill status={status} />} <DealText deal={deal} />
        <br />
        <span className="muted">{t('exchange.rate')}</span> <RateBothWays a={deal.fromAmount} aCurrency={deal.fromCurrency} b={deal.toAmount} bCurrency={deal.toCurrency} className="align-top" />
        <br />
        <span className="muted">{t('exchange.bills')}</span>{' '}
        <Link to={`/bills/${deal.fromBillId}`} className="link">
          {t('exchange.billLabel', { from: names.short(deal.from), to: names.short(deal.to), currency: deal.fromCurrency })}
        </Link>
        {' · '}
        <Link to={`/bills/${deal.toBillId}`} className="link">
          {t('exchange.billLabel', { from: names.short(deal.to), to: names.short(deal.from), currency: deal.toCurrency })}
        </Link>
        {note && <div className="mt-1">{note}</div>}
      </div>
      {actions && <div className="flex flex-wrap gap-2 max-md:w-full max-md:[&>*]:flex-1">{actions}</div>}
    </div>
  )
}
