import { useTranslation } from 'react-i18next'

/** ▾ at the end of a row: opens its action strip (▴ closes it). The open row is in the URL (`?open=<id>`). */
export function ExpandButton({ open, onClick, label }: { open: boolean; onClick: () => void; label: string }) {
  const { t } = useTranslation()
  return (
    <button
      type="button"
      className="inline-flex h-7 w-7 items-center justify-center rounded text-muted hover:bg-accent-faint hover:text-fg"
      aria-expanded={open}
      aria-label={`${open ? t('list.hideActions') : t('list.actions')}: ${label}`}
      title={open ? t('list.hideActions') : t('list.actions')}
      onClick={onClick}
    >
      <span aria-hidden>{open ? '▴' : '▾'}</span>
    </button>
  )
}
