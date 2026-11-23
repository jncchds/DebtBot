/**
 * Money in the UI. The API sends amounts as JSON numbers with up to 4 decimals (decimal(10, 4)).
 * We never add or subtract them as floats: sums and differences go through integer units of 1/10000.
 * Display goes through Intl.NumberFormat with the currency's own number of digits.
 */

const SCALE = 10_000n
const SCALE_DIGITS = 4

/** An API amount as integer units of 1/10000 (exact for decimal(10, 4) values). */
export function toUnits(amount: number): bigint {
  return BigInt(Math.round(amount * Number(SCALE)))
}

/** Units back to a number, for display or to send to the API. */
export function fromUnits(units: bigint): number {
  const neg = units < 0n
  const abs = neg ? -units : units
  const s = `${abs / SCALE}.${(abs % SCALE).toString().padStart(SCALE_DIGITS, '0')}`
  return Number(neg ? `-${s}` : s)
}

export function sumUnits(amounts: number[]): bigint {
  return amounts.reduce((acc, a) => acc + toUnits(a), 0n)
}

/**
 * Parses what a person typed into an amount field: digits with an optional "." or "," and at most 4 decimals.
 * Returns null for anything else (empty, signs, letters, more decimals).
 */
export function parseAmount(text: string): number | null {
  const t = text.trim().replace(',', '.')
  if (!/^\d{1,9}(\.\d{0,4})?$/.test(t)) return null
  const n = Number(t)
  return Number.isFinite(n) ? n : null
}

/** Parses a part (share weight) for a line: a positive number with at most 4 decimals. */
export function parsePart(text: string): number | null {
  const n = parseAmount(text)
  return n !== null && n > 0 ? n : null
}

const digitsCache = new Map<string, number>()

/** The currency's usual number of fraction digits (EUR 2, JPY 0); 2 for codes Intl doesn't know. */
export function currencyDigits(currency: string): number {
  const code = currency.toUpperCase()
  let d = digitsCache.get(code)
  if (d === undefined) {
    try {
      d = new Intl.NumberFormat('en', { style: 'currency', currency: code }).resolvedOptions().maximumFractionDigits ?? 2
    } catch {
      d = 2
    }
    digitsCache.set(code, d)
  }
  return d
}

/** The number part of an amount, e.g. "1,234.50". Always positive: callers say "owes you" / "you owe". */
export function formatNumber(amount: number | bigint, currency: string, locale = 'en'): string {
  const n = typeof amount === 'bigint' ? fromUnits(amount) : amount
  const digits = currencyDigits(currency)
  return new Intl.NumberFormat(locale, { minimumFractionDigits: digits, maximumFractionDigits: digits }).format(Math.abs(n))
}
