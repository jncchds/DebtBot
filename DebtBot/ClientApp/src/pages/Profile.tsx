import { useState, type FormEvent, type ReactNode } from 'react'
import { Trans, useTranslation } from 'react-i18next'
import clsx from 'clsx'
import { ApiError } from '../api/client'
import { useAuthConfig, useProfile } from '../api/hooks'
import { useSaveProfile, useSetPassword } from '../api/people'
import type { ApiErrorBody, Profile as ProfileData } from '../api/types'
import { Card, Field } from '../components/profile/bits'
import { inputProps } from '../components/profile/inputProps'
import { ErrorBox, Loading, PageTitle } from '../components/ui'
import { handle } from '../lib/bills'
import {
  checkDetails,
  checkPassword,
  detailsRequest,
  detailsServerErrors,
  passwordRequest,
  type DetailsErrors,
  type DetailsForm,
  type PasswordErrors,
  type PasswordForm,
} from '../lib/profile'

export type ProfileSection = 'details' | 'password' | 'telegram'

/** 👤 Profile: Details (/profile), Login & password (/profile/password), Telegram (/profile/telegram) (mockups/profile). */
export default function Profile({ section }: { section: ProfileSection }) {
  const { t } = useTranslation()
  const profile = useProfile()

  const title = { details: t('pages.profile.title'), password: t('pages.password.title'), telegram: t('pages.telegram.title') }[section]
  const subtitle = { details: t('pages.profile.subtitle'), password: t('pages.password.subtitle'), telegram: t('pages.telegram.subtitle') }[section]

  return (
    <>
      <PageTitle subtitle={subtitle}>{title}</PageTitle>
      <div className="max-w-[34rem] space-y-4">
        {profile.isLoading ? (
          <Loading />
        ) : profile.error || !profile.data ? (
          <ErrorBox error={profile.error} />
        ) : section === 'details' ? (
          <Details key={profile.data.id} profile={profile.data} />
        ) : section === 'password' ? (
          <Password key={profile.data.id} profile={profile.data} />
        ) : (
          <Telegram profile={profile.data} />
        )}
      </div>
    </>
  )
}

function Details({ profile }: { profile: ProfileData }) {
  const { t } = useTranslation()
  const save = useSaveProfile()
  const loaded: DetailsForm = { displayName: profile.displayName, phone: profile.phone ?? '', email: profile.email ?? '' }
  const [form, setForm] = useState(loaded)
  const [errors, setErrors] = useState<DetailsErrors>({})
  const [saved, setSaved] = useState(false)
  const [failure, setFailure] = useState<string>()

  const set = (k: keyof DetailsForm) => (e: { target: { value: string } }) => {
    setForm({ ...form, [k]: e.target.value })
    setSaved(false)
  }

  const submit = (e: FormEvent) => {
    e.preventDefault()
    setFailure(undefined)
    const found = checkDetails(form)
    setErrors(found)
    if (Object.keys(found).length) return
    save.mutate(detailsRequest(form), {
      onSuccess: (p) => {
        setForm({ displayName: p.displayName, phone: p.phone ?? '', email: p.email ?? '' })
        setSaved(true)
      },
      onError: (err) => {
        const server = err instanceof ApiError ? detailsServerErrors((err.body as ApiErrorBody | undefined)?.errors) : {}
        setErrors(server)
        if (!Object.keys(server).length) setFailure(err instanceof ApiError && err.serverMessage ? err.serverMessage : t('profile.saveFailed'))
      },
    })
  }

  const err = (k: keyof DetailsForm) => (errors[k] ? t(errors[k]) : undefined)

  return (
    <Card title={`🪪 ${t('profile.details')}`}>
      <form onSubmit={submit} noValidate>
        {failure && (
          <div className="alert-danger mb-4" role="alert">
            {failure}
          </div>
        )}
        <Field id="p-name" label={t('profile.displayName')} help={t('profile.displayNameHelp')} error={err('displayName')}>
          <input {...inputProps('p-name', err('displayName'))} autoComplete="name" value={form.displayName} onChange={set('displayName')} />
        </Field>
        <Field id="p-phone" label={t('profile.phone')} error={err('phone')}>
          <input {...inputProps('p-phone', err('phone'))} type="tel" autoComplete="tel" placeholder={t('profile.optional')} value={form.phone} onChange={set('phone')} />
        </Field>
        <Field id="p-email" label={t('profile.email')} error={err('email')}>
          <input {...inputProps('p-email', err('email'))} type="email" autoComplete="email" placeholder={t('profile.optional')} value={form.email} onChange={set('email')} />
        </Field>
        <div className="flex flex-wrap items-center gap-2">
          <button type="submit" className="btn-primary" disabled={save.isPending}>
            {t('profile.save')}
          </button>
          <button
            type="button"
            className="btn-ghost"
            onClick={() => {
              setForm(loaded)
              setErrors({})
              setSaved(false)
              setFailure(undefined)
            }}
          >
            {t('profile.reset')}
          </button>
          {saved && (
            <span className="text-sm font-semibold text-success" role="status">
              ✓ {t('profile.saved')}
            </span>
          )}
        </div>
      </form>
    </Card>
  )
}

function Password({ profile }: { profile: ProfileData }) {
  const { t } = useTranslation()
  const setPassword = useSetPassword()
  const firstTime = !profile.hasPassword && !profile.login
  const [form, setForm] = useState<PasswordForm>({ login: profile.login ?? '', current: '', next: '', repeat: '' })
  const [errors, setErrors] = useState<PasswordErrors>({})
  const [failure, setFailure] = useState<string>()
  const [saved, setSaved] = useState(false)
  // A 409: the message is in the alert; the login field is only marked.
  const [taken, setTaken] = useState(false)

  const set = (k: keyof PasswordForm) => (e: { target: { value: string } }) => {
    setForm({ ...form, [k]: e.target.value })
    setSaved(false)
  }

  const submit = (e: FormEvent) => {
    e.preventDefault()
    setFailure(undefined)
    setSaved(false)
    setTaken(false)
    const found = checkPassword(form, firstTime)
    setErrors(found)
    if (Object.keys(found).length) return
    setPassword.mutate(passwordRequest(form, firstTime), {
      onSuccess: () => {
        setForm({ login: form.login.trim().toLowerCase(), current: '', next: '', repeat: '' })
        setSaved(true)
      },
      onError: (err) => {
        if (err instanceof ApiError && err.status === 409) {
          setFailure(t('profile.loginTaken', { login: form.login.trim() }))
          setTaken(true)
        } else setFailure(err instanceof ApiError && err.serverMessage ? err.serverMessage : t('profile.saveFailed'))
      },
    })
  }

  const err = (k: keyof PasswordForm) => (errors[k] ? t(errors[k]) : undefined)

  return (
    <Card
      title={`🔑 ${firstTime ? t('profile.setTitle') : t('profile.changeTitle')}`}
      badge={!firstTime && profile.hasPassword && <span className="pill-success shrink-0">{t('profile.passwordSet')}</span>}
    >
      <form onSubmit={submit} noValidate>
        {firstTime && <div className="alert-info mb-4">{t('profile.firstTimeInfo')}</div>}
        {failure && (
          <div className="alert-danger mb-4" role="alert">
            {failure}
          </div>
        )}
        {saved && (
          <div className="mb-4 text-sm font-semibold text-success" role="status">
            ✓ {t('profile.passwordSaved')}
          </div>
        )}
        <Field id="pw-login" label={t('profile.login')} help={firstTime ? t('profile.loginHelp') : t('profile.loginChangeHelp')} error={err('login')}>
          <input {...inputProps('pw-login', err('login') ?? (taken ? ' ' : undefined))} autoComplete="username" autoCapitalize="none" spellCheck={false} value={form.login} onChange={set('login')} />
        </Field>
        {!firstTime && (
          <Field id="pw-current" label={t('profile.currentPassword')} help={t('profile.currentHelp')} error={err('current')}>
            <input {...inputProps('pw-current', err('current'))} type="password" autoComplete="current-password" value={form.current} onChange={set('current')} />
          </Field>
        )}
        <Field id="pw-next" label={firstTime ? t('profile.password') : t('profile.newPassword')} help={firstTime ? t('profile.passwordHelp') : undefined} error={err('next')}>
          <input
            {...inputProps('pw-next', err('next'))}
            type="password"
            autoComplete="new-password"
            placeholder={firstTime ? undefined : t('profile.keepEmpty')}
            value={form.next}
            onChange={set('next')}
          />
        </Field>
        <Field id="pw-repeat" label={firstTime ? t('profile.repeatPassword') : t('profile.repeatNewPassword')} error={err('repeat')}>
          <input {...inputProps('pw-repeat', err('repeat'))} type="password" autoComplete="new-password" value={form.repeat} onChange={set('repeat')} />
        </Field>
        <button type="submit" className="btn-primary" disabled={setPassword.isPending}>
          {firstTime ? t('profile.setButton') : t('profile.saveChanges')}
        </button>
      </form>
    </Card>
  )
}

function Telegram({ profile }: { profile: ProfileData }) {
  const { t } = useTranslation()
  const config = useAuthConfig()
  const botName = config.data?.telegramBotUsername?.replace(/^@+/, '')
  const bot = botName ? `@${botName}` : t('profile.theBot')
  const url = botName ? `https://t.me/${botName}` : undefined

  const row = (label: string, value: ReactNode) => (
    <div className="grid gap-x-4 gap-y-0.5 py-1 sm:grid-cols-[10rem_1fr]">
      <dt className="text-sm muted">{label}</dt>
      <dd className="min-w-0 text-sm break-words">{value}</dd>
    </div>
  )

  const openButton = (style: string) =>
    url && (
      <a href={url} target="_blank" rel="noopener noreferrer" className={clsx(style, 'mt-4')}>
        {t('profile.openBot', { bot })} ↗
      </a>
    )

  return (
    <>
      <Card title={`✈️ ${t('profile.account')}`}>
        <dl>
          {row(t('profile.username'), profile.telegramUserName ? handle(profile.telegramUserName) : <span className="muted">—</span>)}
          {row(t('profile.telegramId'), profile.telegramId ?? <span className="muted">—</span>)}
          {row(t('profile.role'), profile.role === 'Admin' ? <span className="pill-accent">{t('profile.admin')}</span> : <span className="pill-muted">{t('profile.regular')}</span>)}
        </dl>
      </Card>
      <Card
        title={`🤖 ${t('profile.bot')}`}
        badge={
          profile.telegramBotEnabled ? <span className="pill-success shrink-0">{t('profile.started')}</span> : <span className="pill-danger shrink-0">{t('profile.notStarted')}</span>
        }
      >
        {profile.telegramBotEnabled ? (
          <>
            <p>
              <Trans i18nKey="profile.startedText" values={{ bot }} components={{ b: <strong /> }} />
            </p>
            {openButton('btn-secondary')}
          </>
        ) : (
          <>
            <p>
              <Trans i18nKey="profile.notStartedText" values={{ bot }} components={{ b: <strong /> }} />
            </p>
            <ol className="mt-2 list-decimal space-y-1 pl-5 text-sm">
              <li>
                {url ? (
                  <Trans i18nKey="profile.step1" values={{ link: url.replace('https://', '') }} components={{ a: <a href={url} target="_blank" rel="noopener noreferrer" className="link" /> }} />
                ) : (
                  t('profile.step1NoLink')
                )}
              </li>
              <li>
                <Trans i18nKey="profile.step2" components={{ b: <strong />, code: <code className="rounded bg-muted/10 px-1 text-[0.8rem]" /> }} />
              </li>
              <li>{t('profile.step3')}</li>
            </ol>
            {openButton('btn-primary')}
          </>
        )}
      </Card>
    </>
  )
}
