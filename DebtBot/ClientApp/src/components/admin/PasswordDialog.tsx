import { useEffect, useState, type FormEvent, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import clsx from 'clsx'
import { useAdminUserRow, useSetUserPassword } from '../../api/admin'
import { ApiError } from '../../api/client'
import type { AdminUser } from '../../api/types'
import { adminPasswordRequest, checkAdminPassword, type AdminPasswordErrors, type AdminPasswordForm } from '../../lib/admin'
import { ErrorBox, Spinner } from '../ui'

/** 🔑 Login & password for a user (`/admin/users/<id>/password`): `POST Users/{id}/password`. */
export function PasswordDialog({ id, pageUsers, onClose }: { id: string; pageUsers: AdminUser[] | undefined; onClose: () => void }) {
  const { t } = useTranslation()
  const { row, error, loading } = useAdminUserRow(id, pageUsers)

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => e.key === 'Escape' && onClose()
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [onClose])

  return (
    <div className="fixed inset-0 z-40 flex items-start justify-center overflow-y-auto bg-black/40 px-4 py-16" onMouseDown={(e) => e.target === e.currentTarget && onClose()}>
      <div className="card w-full max-w-[26rem] shadow-2xl" role="dialog" aria-modal="true" aria-labelledby="pw-title">
        <header className="flex items-center gap-2 border-b border-line px-4 py-3">
          <h2 id="pw-title" className="min-w-0 flex-1 truncate font-bold">
            🔑 {t('admin.password.title')}
            {row && ` · ${row.displayName}`}
          </h2>
          <button type="button" className="btn-ghost px-2 py-0.5" aria-label={t('common.close')} onClick={onClose}>
            ✕
          </button>
        </header>
        {row ? (
          <Form key={row.id} user={row} onDone={onClose} />
        ) : loading ? (
          <div className="flex justify-center p-8">
            <Spinner />
          </div>
        ) : (
          <div className="p-4">
            <ErrorBox error={error ?? new Error(t('admin.password.notFound'))} />
          </div>
        )}
      </div>
    </div>
  )
}

function Field({ id, label, help, error, children }: { id: string; label: string; help?: string; error?: string; children: ReactNode }) {
  return (
    <div className="mb-4">
      <label htmlFor={id} className="label">
        {label}
      </label>
      {children}
      {(error || help) && (
        <p id={`${id}-msg`} className={clsx('mt-1 text-[0.8rem]', error ? 'text-danger' : 'muted')}>
          {error ?? help}
        </p>
      )}
    </div>
  )
}

const inputProps = (id: string, error?: string | boolean) => ({
  id,
  className: clsx('input', error && 'border-danger focus:border-danger focus:ring-danger/25'),
  'aria-invalid': !!error,
  'aria-describedby': `${id}-msg`,
})

function Form({ user, onDone }: { user: AdminUser; onDone: () => void }) {
  const { t } = useTranslation()
  const save = useSetUserPassword()
  const [form, setForm] = useState<AdminPasswordForm>({ login: user.login ?? '', next: '', repeat: '' })
  const [errors, setErrors] = useState<AdminPasswordErrors>({})
  const [failure, setFailure] = useState<string>()
  const [taken, setTaken] = useState(false)
  const first = user.displayName.split(/\s+/)[0]

  const set = (k: keyof AdminPasswordForm) => (e: { target: { value: string } }) => setForm({ ...form, [k]: e.target.value })

  const submit = (e: FormEvent) => {
    e.preventDefault()
    setFailure(undefined)
    setTaken(false)
    const found = checkAdminPassword(form, !!user.login)
    setErrors(found)
    if (Object.keys(found).length) return
    save.mutate(
      { id: user.id, ...adminPasswordRequest(form) },
      {
        onSuccess: onDone,
        onError: (err) => {
          if (err instanceof ApiError && err.status === 409) {
            setTaken(true)
            setFailure(t('admin.password.taken', { login: form.login.trim().toLowerCase() }))
          } else setFailure(err instanceof ApiError && err.serverMessage ? err.serverMessage : t('admin.password.failed'))
        },
      },
    )
  }

  const err = (k: keyof AdminPasswordForm) => (errors[k] ? t(errors[k]) : undefined)

  return (
    <form onSubmit={submit} noValidate>
      <div className="p-4">
        {failure && (
          <div className="alert-danger mb-4" role="alert">
            {failure}
          </div>
        )}
        <Field id="apw-login" label={t('admin.password.login')} help={user.login ? undefined : t('admin.password.loginHelp', { name: first })} error={err('login')}>
          <input {...inputProps('apw-login', err('login') || taken)} autoComplete="off" autoCapitalize="none" spellCheck={false} autoFocus value={form.login} onChange={set('login')} />
        </Field>
        <Field id="apw-next" label={t('admin.password.newPassword')} help={t('admin.password.passwordHelp', { name: first })} error={err('next')}>
          <input {...inputProps('apw-next', err('next'))} type="password" autoComplete="new-password" value={form.next} onChange={set('next')} />
        </Field>
        <Field id="apw-repeat" label={t('admin.password.repeat')} error={err('repeat')}>
          <input {...inputProps('apw-repeat', err('repeat'))} type="password" autoComplete="new-password" value={form.repeat} onChange={set('repeat')} />
        </Field>
        <div className="alert-info flex gap-2">
          <span aria-hidden>ℹ️</span>
          <span>{t('admin.password.signsOut', { name: first })}</span>
        </div>
      </div>
      <footer className="flex justify-end gap-2 border-t border-line px-4 py-3">
        <button type="button" className="btn-ghost" onClick={onDone}>
          {t('common.cancel')}
        </button>
        <button type="submit" className="btn-primary" disabled={save.isPending}>
          {t('admin.password.save')}
        </button>
      </footer>
    </form>
  )
}
