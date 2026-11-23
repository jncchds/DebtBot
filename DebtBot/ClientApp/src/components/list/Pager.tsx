import { useTranslation } from 'react-i18next'
import clsx from 'clsx'
import { pageWindow } from '../../lib/listState'

const pg = 'inline-flex h-8 min-w-8 items-center justify-center rounded-md border px-2 text-sm disabled:pointer-events-none disabled:opacity-45'
const idle = 'border-line text-muted hover:border-accent hover:text-fg'
const current = 'border-accent bg-accent font-bold text-accent-contrast'

/** "1–20 of 57" and page buttons; `compact` shows "Page 1 of 3" instead of numbers (phones). Pages are 1-based. */
export function Pager({ page, pageSize, total, onPage, compact }: { page: number; pageSize: number; total: number; onPage: (page: number) => void; compact?: boolean }) {
  const { t } = useTranslation()
  const last = Math.max(1, Math.ceil(total / pageSize))
  const from = total === 0 ? 0 : (page - 1) * pageSize + 1
  const to = Math.min(total, page * pageSize)
  return (
    <nav className="mt-4 flex flex-wrap items-center gap-2 text-sm" aria-label={t('list.pages')}>
      <span className="muted">{t('list.range', { from, to, total })}</span>
      <span className="flex-1" />
      <button className={clsx(pg, idle)} disabled={page <= 1} onClick={() => onPage(page - 1)} aria-label={t('list.previous')}>
        ‹
      </button>
      {compact ? (
        <span>{t('list.pageOf', { page, last })}</span>
      ) : (
        pageWindow(page, last).map((p, i) =>
          p === null ? (
            <span key={`gap${i}`} className="muted">
              …
            </span>
          ) : (
            <button
              key={p}
              className={clsx(pg, p === page ? current : idle)}
              aria-current={p === page ? 'page' : undefined}
              onClick={() => onPage(p)}
            >
              {p}
            </button>
          ),
        )
      )}
      <button className={clsx(pg, idle)} disabled={page >= last} onClick={() => onPage(page + 1)} aria-label={t('list.next')}>
        ›
      </button>
    </nav>
  )
}
