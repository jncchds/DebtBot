import { describe, expect, it } from 'vitest'
import { checkDetails, checkPassword, detailsRequest, detailsServerErrors, newBillOpensRequest, passwordRequest } from './profile'

describe('profile details', () => {
  it('checks the name, phone and email like the API', () => {
    expect(checkDetails({ displayName: 'Anna', phone: '', email: '' })).toEqual({})
    expect(checkDetails({ displayName: '  ', phone: 'x'.repeat(33), email: 'anna.example.com' })).toEqual({
      displayName: 'profile.nameRule',
      phone: 'profile.phoneRule',
      email: 'profile.emailAt',
    })
    expect(checkDetails({ displayName: 'x'.repeat(101), phone: '', email: `${'a'.repeat(250)}@b.cd` }).email).toBe('profile.emailRule')
  })

  it('sends trimmed values, and empty phone or email as null', () => {
    expect(detailsRequest({ displayName: ' Anna ', phone: ' ', email: 'a@b.c ' })).toEqual({ displayName: 'Anna', phone: null, email: 'a@b.c' })
  })

  it('New bill opens: sends the saved details unchanged with the choice (receipts T3)', () => {
    const saved = { displayName: 'Anna Petrova', phone: '+48 600 123 456', email: null }
    expect(newBillOpensRequest(saved, 'Text')).toEqual({ displayName: 'Anna Petrova', phone: '+48 600 123 456', email: null, receiptOpenIn: 'Text' })
    expect(newBillOpensRequest(saved, 'Form').receiptOpenIn).toBe('Form')
    // The Details form leaves it out, so saving the details keeps the choice.
    expect(detailsRequest({ displayName: 'Anna', phone: '', email: '' })).not.toHaveProperty('receiptOpenIn')
  })

  it('places the API errors under their fields', () => {
    expect(detailsServerErrors(['A display name is required', "The email doesn't look right"])).toEqual({ displayName: 'profile.nameRule', email: 'profile.emailRule' })
    expect(detailsServerErrors(undefined)).toEqual({})
  })
})

describe('login and password', () => {
  const f = { login: 'anna.p', current: '', next: '', repeat: '' }

  it('first time needs a login and a password of 8+, repeated', () => {
    expect(checkPassword({ ...f, login: '' }, true)).toEqual({ login: 'profile.loginRequired', next: 'profile.passwordRequired' })
    expect(checkPassword({ ...f, next: 'short', repeat: 'short' }, true)).toEqual({ next: 'profile.passwordShort' })
    expect(checkPassword({ ...f, next: 'long enough', repeat: 'long enougH' }, true)).toEqual({ repeat: 'profile.mismatch' })
    expect(passwordRequest({ ...f, next: 'long enough', repeat: 'long enough' }, true)).toEqual({ login: 'anna.p', newPassword: 'long enough' })
  })

  it('later needs the current password; the new one is optional', () => {
    expect(checkPassword(f, false)).toEqual({ current: 'profile.currentRequired' })
    expect(checkPassword({ ...f, current: 'old secret' }, false)).toEqual({})
    expect(passwordRequest({ ...f, login: ' anna ', current: 'old secret' }, false)).toEqual({ login: 'anna', currentPassword: 'old secret' })
    expect(passwordRequest({ ...f, current: 'old secret', next: 'new secret', repeat: 'new secret' }, false)).toEqual({
      login: 'anna.p',
      currentPassword: 'old secret',
      newPassword: 'new secret',
    })
  })
})
