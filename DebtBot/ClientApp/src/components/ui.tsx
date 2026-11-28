import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import clsx from 'clsx'

export function Spinner({ className }: { className?: string }) {
  return <span className={clsx('inline-block animate-spin rounded-full border-2 border-accent/25 border-t-accent', className ?? 'h-6 w-6')} aria-hidden />
}

export function Loading() {
  const { t } = useTranslation()
  return (
    <div className="flex items-center justify-center gap-3 py-16 muted" role="status">
      <Spinner />
      <span>{t('common.loading')}</span>
    </div>
  )
}

export function ErrorBox({ error }: { error: unknown }) {
  const { t } = useTranslation()
  const message = error instanceof Error ? error.message : String(error)
  return (
    <div className="alert-danger" role="alert">
      <strong>{t('common.error')}:</strong> {message}
    </div>
  )
}

/** `actionsEnd` puts the actions at the right end of the title row. */
export function PageTitle({ children, actions, actionsEnd, subtitle }: { children: ReactNode; actions?: ReactNode; actionsEnd?: boolean; subtitle?: ReactNode }) {
  return (
    <div className="mb-6">
      <div className="flex flex-wrap items-center gap-3">
        <h1 className="min-w-0 text-[1.4rem] font-bold">{children}</h1>
        {actions && <div className={clsx('flex flex-wrap items-center gap-2', actionsEnd && 'ml-auto')}>{actions}</div>}
      </div>
      {subtitle && <p className="mt-1 text-sm muted">{subtitle}</p>}
    </div>
  )
}
