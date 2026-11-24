import type { PasswordChange, ProfileUpdate } from '../api/types'

/** Profile form checks, the same limits as the API (UserService.UpdateProfile, AuthService). Values are i18n keys. */

export const MAX_NAME = 100
export const MAX_PHONE = 32
export const MAX_EMAIL = 254
export const MIN_PASSWORD = 8

export interface DetailsForm {
  displayName: string
  phone: string
  email: string
}

export type DetailsErrors = Partial<Record<keyof DetailsForm, string>>

export function checkDetails(f: DetailsForm): DetailsErrors {
  const e: DetailsErrors = {}
  const name = f.displayName.trim()
  if (!name || name.length > MAX_NAME) e.displayName = 'profile.nameRule'
  if (f.phone.trim().length > MAX_PHONE) e.phone = 'profile.phoneRule'
  const email = f.email.trim()
  if (email && !email.includes('@')) e.email = 'profile.emailAt'
  else if (email.length > MAX_EMAIL) e.email = 'profile.emailRule'
  return e
}

/** `PUT profile`: empty phone or email clears it. */
export function detailsRequest(f: DetailsForm): ProfileUpdate {
  return { displayName: f.displayName.trim(), phone: f.phone.trim() || null, email: f.email.trim() || null }
}

/** The API's 400 `errors` (plain sentences) placed under their fields. */
export function detailsServerErrors(errors: readonly string[] | undefined): DetailsErrors {
  const e: DetailsErrors = {}
  for (const s of errors ?? []) {
    const l = s.toLowerCase()
    if (l.includes('display name')) e.displayName = 'profile.nameRule'
    else if (l.includes('phone')) e.phone = 'profile.phoneRule'
    else if (l.includes('email')) e.email = 'profile.emailRule'
  }
  return e
}

export interface PasswordForm {
  login: string
  current: string
  next: string
  repeat: string
}

export type PasswordErrors = Partial<Record<keyof PasswordForm, string>>

/**
 * First time (no password yet): a login and a password are both needed.
 * Later: the current password is needed for any change; a new password is optional.
 */
export function checkPassword(f: PasswordForm, firstTime: boolean): PasswordErrors {
  const e: PasswordErrors = {}
  if (!f.login.trim()) e.login = 'profile.loginRequired'
  if (!firstTime && !f.current) e.current = 'profile.currentRequired'
  if (firstTime && !f.next) e.next = 'profile.passwordRequired'
  else if (f.next && f.next.length < MIN_PASSWORD) e.next = 'profile.passwordShort'
  if (f.next && f.repeat !== f.next) e.repeat = 'profile.mismatch'
  return e
}

/** `PUT auth/password`: login (+ current password later), plus the new password when one was typed. */
export function passwordRequest(f: PasswordForm, firstTime: boolean): PasswordChange {
  const r: PasswordChange = { login: f.login.trim() }
  if (!firstTime) r.currentPassword = f.current
  if (f.next) r.newPassword = f.next
  return r
}
