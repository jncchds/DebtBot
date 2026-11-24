import { useState, type FormEvent } from 'react'
import { Navigate, useLocation, useNavigate } from 'react-router'
import { useTranslation } from 'react-i18next'
import { useAuth } from '../auth/AuthContext'
import { ApiError } from '../api/client'
import { useAuthConfig } from '../api/hooks'
import type { TelegramAuthData } from '../api/types'
import { TelegramLogin } from '../components/TelegramLogin'
import { Loading } from '../components/ui'

export default function Login() {
  const { t } = useTranslation()
  const { user, ready, signIn, signInWithTelegram } = useAuth()
  const config = useAuthConfig()
  const navigate = useNavigate()
  const location = useLocation()
  const from = (location.state as { from?: string } | null)?.from
  const target = from && !from.startsWith('/login') ? from : '/'
  const [login, setLogin] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const run = async (action: () => Promise<void>) => {
    setBusy(true)
    setError(null)
    try {
      await action()
      navigate(target, { replace: true })
    } catch (err) {
      // One message per failure: the server's { message } when it sent one, a generic one otherwise.
      setError((err instanceof ApiError && err.serverMessage) || t('auth.failed'))
    } finally {
      setBusy(false)
    }
  }

  const submit = (e: FormEvent) => {
    e.preventDefault()
    run(() => signIn({ login: login.trim(), password }))
  }

  const onTelegram = (data: TelegramAuthData) => run(() => signInWithTelegram(data))

  if (!ready) return <Loading />
  if (user) return <Navigate to={target} replace />

  const bot = config.data?.telegramBotUsername

  return (
    <div className="flex min-h-dvh items-center justify-center px-4 py-8">
      <form onSubmit={submit} className="card w-full max-w-sm space-y-4 p-6">
        <div className="flex flex-col items-center gap-2 text-center">
          <div className="text-[2.5rem] leading-none" aria-hidden>
            💸
          </div>
          <h1 className="text-2xl font-semibold">DebtBot</h1>
          <p className="text-sm muted">{t('auth.prompt')}</p>
        </div>
        <label className="block">
          <span className="label">{t('auth.login')}</span>
          <input className="input" autoComplete="username" autoCapitalize="none" value={login} onChange={(e) => setLogin(e.target.value)} required autoFocus />
        </label>
        <label className="block">
          <span className="label">{t('auth.password')}</span>
          <input className="input" type="password" autoComplete="current-password" value={password} onChange={(e) => setPassword(e.target.value)} required />
        </label>
        {error && (
          <div className="alert-danger" role="alert">
            {error}
          </div>
        )}
        <button className="btn-primary w-full" disabled={busy}>
          {t('auth.signIn')}
        </button>
        {bot && (
          <>
            <div className="flex items-center gap-3 text-xs muted">
              <span className="h-px flex-1 bg-line" />
              {t('auth.or')}
              <span className="h-px flex-1 bg-line" />
            </div>
            <TelegramLogin botUsername={bot} onAuth={onTelegram} />
          </>
        )}
        {config.isSuccess && <p className="text-center text-xs muted">{bot ? t('auth.hintTelegram') : t('auth.hintAdmin')}</p>}
      </form>
    </div>
  )
}
