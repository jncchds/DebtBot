import { describe, expect, it } from 'vitest'
import { currencyDigits, formatNumber, fromUnits, parseAmount, parsePart, sumUnits, toUnits } from './money'

describe('money units', () => {
  it('adds without float errors', () => {
    expect(0.1 + 0.2).not.toBe(0.3)
    expect(fromUnits(sumUnits([0.1, 0.2]))).toBe(0.3)
    expect(fromUnits(toUnits(84) - sumUnits([48, 28]))).toBe(8)
  })

  it('keeps 4 decimals and signs', () => {
    expect(toUnits(33.3333)).toBe(333333n)
    expect(fromUnits(-125000n)).toBe(-12.5)
    expect(fromUnits(toUnits(-0.0001))).toBe(-0.0001)
    expect(fromUnits(0n)).toBe(0)
  })
})

describe('parseAmount', () => {
  it('accepts plain decimals with . or ,', () => {
    expect(parseAmount('12')).toBe(12)
    expect(parseAmount(' 12.5 ')).toBe(12.5)
    expect(parseAmount('12,30')).toBe(12.3)
    expect(parseAmount('0.0001')).toBe(0.0001)
  })

  it('rejects the rest', () => {
    for (const bad of ['', '-1', '1e3', '12.34567', 'abc', '1.2.3', '+5'])
      expect(parseAmount(bad)).toBeNull()
  })

  it('parts must be above zero', () => {
    expect(parsePart('0')).toBeNull()
    expect(parsePart('3')).toBe(3)
  })
})

describe('formatNumber', () => {
  it('uses the currency digits', () => {
    expect(currencyDigits('EUR')).toBe(2)
    expect(currencyDigits('JPY')).toBe(0)
    expect(currencyDigits('XYZ1')).toBe(2)
    expect(formatNumber(1234.5, 'EUR')).toBe('1,234.50')
    expect(formatNumber(1234.5, 'JPY')).toBe('1,235')
  })

  it('is always positive and takes units', () => {
    expect(formatNumber(-12.5, 'USD')).toBe('12.50')
    expect(formatNumber(-1200000n, 'PLN')).toBe('120.00')
  })
})
