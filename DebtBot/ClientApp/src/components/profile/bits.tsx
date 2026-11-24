import type { ReactNode } from 'react'

/** The Profile pages' card and labelled field (mockups/profile). */

export function Card({ title, badge, children }: { title: ReactNode; badge?: ReactNode; children: ReactNode }) {
  return (
    <section className="card overflow-hidden">
      <header className="flex items-center gap-2 border-b border-line px-4 py-3">
        <h2 className="min-w-0 flex-1 font-bold">{title}</h2>
        {badge}
      </header>
      <div className="p-4">{children}</div>
    </section>
  )
}

export function Field({ id, label, help, error, children }: { id: string; label: string; help?: string; error?: string; children: ReactNode }) {
  return (
    <div className="mb-4">
      <label htmlFor={id} className="label">
        {label}
      </label>
      {children}
      {error ? (
        <p id={`${id}-msg`} className="mt-1 text-[0.8rem] text-danger">
          {error}
        </p>
      ) : (
        help && (
          <p id={`${id}-msg`} className="mt-1 text-[0.8rem] muted">
            {help}
          </p>
        )
      )}
    </div>
  )
}
