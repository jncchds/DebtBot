import type { AdminSetPassword, AdminUser, MergeSuggestion } from '../api/types'
import { MIN_PASSWORD } from './profile'

/**
 * 🛠 Admin helpers (mockups/admin-users, admin-merge): the Merge users URL state, the duplicate marker and the
 * admin's Login & password form.
 */

const ID = /^[0-9a-fA-F-]{1,64}$/

const cleanId = (v: string | null | undefined) => {
  const s = v?.trim()
  return s && ID.test(s) ? s : undefined
}

/**
 * `/admin/merge?swap=<mergeId>,<mergeId>&keep=<id>&merge=<id>`:
 * `swap` lists the suggestions shown swapped, by the suggested merge user's id (so a swap survives a reload and
 * Back undoes it); `keep` and `merge` fill the manual picker.
 */
export interface MergeUrl {
  swap: string[]
  keep?: string
  merge?: string
}

export function parseMergeUrl(params: URLSearchParams): MergeUrl {
  const swap = [...new Set((params.get('swap') ?? '').split(',').map(cleanId).filter((v): v is string => !!v))]
  return { swap, keep: cleanId(params.get('keep')), merge: cleanId(params.get('merge')) }
}

/** The query string, without empty parts; ids keep the order they were swapped in. */
export function mergeSearch(s: MergeUrl): string {
  const p = new URLSearchParams()
  if (s.swap.length) p.set('swap', s.swap.join(','))
  if (s.keep) p.set('keep', s.keep)
  if (s.merge) p.set('merge', s.merge)
  const q = p.toString().replace(/%2C/gi, ',')
  return q ? `?${q}` : ''
}

/** Shows a suggestion swapped (on) or as suggested (off). */
export function withSwap(s: MergeUrl, mergeId: string, on: boolean): MergeUrl {
  const rest = s.swap.filter((id) => id !== mergeId)
  return { ...s, swap: on ? [...rest, mergeId] : rest }
}

/** Sets one side of the manual picker (undefined clears it). */
export function withManual(s: MergeUrl, side: 'keep' | 'merge', id: string | undefined): MergeUrl {
  return { ...s, [side]: id }
}

/** Trades the manual picker's two sides. */
export function withManualSwapped(s: MergeUrl): MergeUrl {
  return { ...s, keep: s.merge, merge: s.keep }
}

/** After a merge: the merged-away user is gone, so drop it from `swap` and the picker. */
export function afterMerge(s: MergeUrl, mergedAwayId: string, keptId: string): MergeUrl {
  const gone = (id?: string) => id === mergedAwayId || id === keptId
  return { swap: s.swap.filter((id) => id !== mergedAwayId), keep: gone(s.keep) ? undefined : s.keep, merge: gone(s.merge) ? undefined : s.merge }
}

export const isSwapped = (s: MergeUrl, suggestion: MergeSuggestion) => s.swap.includes(suggestion.merge.id)

/** The two users as shown: swapped suggestions keep the suggested merge user. */
export function shownPair(s: MergeUrl, suggestion: MergeSuggestion): { keep: AdminUser; merge: AdminUser; swapped: boolean } {
  const swapped = isSwapped(s, suggestion)
  return swapped ? { keep: suggestion.merge, merge: suggestion.keep, swapped } : { keep: suggestion.keep, merge: suggestion.merge, swapped }
}

/** Everyone in any suggestion: the Users list marks them "possible duplicate". */
export function duplicateIds(suggestions: MergeSuggestion[] | undefined): Set<string> {
  const out = new Set<string>()
  for (const s of suggestions ?? []) {
    out.add(s.keep.id)
    out.add(s.merge.id)
  }
  return out
}

/**
 * What happens to web sign-in (api.md, "What a merge does"): the kept user keeps theirs, or takes the old user's
 * when they have neither a login nor a password.
 */
export const keptSignIn = (kept: Pick<AdminUser, 'login' | 'hasPassword'>): 'keeps' | 'takes' => (kept.login || kept.hasPassword ? 'keeps' : 'takes')

/** Whether the manual picker has two different users. */
export const canMergeManual = (s: MergeUrl) => !!s.keep && !!s.merge && s.keep.toLowerCase() !== s.merge.toLowerCase()

// Login & password (POST Users/{id}/password)

export const MIN_LOGIN = 3
export const MAX_LOGIN = 64
const LOGIN = /^[a-z0-9._@-]+$/

export interface AdminPasswordForm {
  login: string
  next: string
  repeat: string
}

export type AdminPasswordErrors = Partial<Record<keyof AdminPasswordForm, string>>

/** The API's rules: a login is needed when the user has none (3–64 of a–z 0–9 . _ - @, any case); a password of 8+. */
export function checkAdminPassword(f: AdminPasswordForm, hasLogin: boolean): AdminPasswordErrors {
  const e: AdminPasswordErrors = {}
  const login = f.login.trim().toLowerCase()
  if (!login) {
    if (!hasLogin) e.login = 'admin.password.loginRequired'
  } else if (login.length < MIN_LOGIN || login.length > MAX_LOGIN || !LOGIN.test(login)) e.login = 'admin.password.loginRule'
  if (!f.next) e.next = 'admin.password.passwordRequired'
  else if (f.next.length < MIN_PASSWORD) e.next = 'admin.password.passwordShort'
  if (f.next && f.repeat !== f.next) e.repeat = 'admin.password.mismatch'
  return e
}

/** The request body: the login only when one was typed (an empty login keeps the current one). */
export function adminPasswordRequest(f: AdminPasswordForm): AdminSetPassword {
  const login = f.login.trim().toLowerCase()
  return login ? { login, password: f.next } : { password: f.next }
}
