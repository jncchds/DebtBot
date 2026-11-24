import i18n, { type Resource } from 'i18next'
import { initReactI18next } from 'react-i18next'

/**
 * Every user-facing string goes through i18next. English only for now (no language switch yet).
 * Strings live in locales/<lang>/<group>.json; each file becomes a top-level key group,
 * so locales/en/auth.json's "signIn" is t('auth.signIn'). To add a language, add a folder
 * with the same files and list it in LANGUAGES and scripts/check-locales.mjs.
 */
export const LANGUAGES = ['en'] as const

const files = import.meta.glob<Record<string, unknown>>('../locales/*/*.json', { eager: true, import: 'default' })

const resources: Resource = {}
for (const [path, strings] of Object.entries(files)) {
  const [, lang, group] = /locales\/([^/]+)\/([^/]+)\.json$/.exec(path) ?? []
  if (!lang || !group) continue
  const translation = ((resources[lang] ??= { translation: {} }).translation as Record<string, unknown>)
  translation[group] = strings
}

i18n.use(initReactI18next).init({
  resources,
  lng: 'en',
  fallbackLng: 'en',
  interpolation: { escapeValue: false },
  returnNull: false,
})

document.documentElement.lang = i18n.language

export default i18n
