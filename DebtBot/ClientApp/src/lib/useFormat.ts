import { useMemo } from 'react'
import { useTranslation } from 'react-i18next'
import { formatNumber } from './money'

/** Amount and date formatting in the UI language (English for now). Amounts are shown without a sign. */
export function useFormat() {
  const { t, i18n } = useTranslation()
  const lang = i18n.language
  return useMemo(() => {
    // English dates as in the mockups: "18 Nov 2026, 20:40" (day first, 24-hour).
    const dateLocale = lang === 'en' ? 'en-GB' : lang
    const short = new Intl.DateTimeFormat(dateLocale, { day: 'numeric', month: 'short' })
    const shortYear = new Intl.DateTimeFormat(dateLocale, { day: 'numeric', month: 'short', year: 'numeric' })
    const long = new Intl.DateTimeFormat(dateLocale, { day: 'numeric', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' })
    return {
      /** "30.00 EUR" */
      money: (amount: number | bigint, currency: string) => t('money.amount', { amount: formatNumber(amount, currency, lang), currency }),
      /** "18 Nov", or "18 Nov 2025" for other years. */
      date: (iso: string) => {
        const d = new Date(iso)
        return (d.getFullYear() === new Date().getFullYear() ? short : shortYear).format(d)
      },
      /** "18 Nov 2026, 20:40" */
      dateTime: (iso: string) => long.format(new Date(iso)),
    }
  }, [t, lang])
}
