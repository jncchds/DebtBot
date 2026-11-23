import { useTranslation } from 'react-i18next'
import { PageTitle } from '../components/ui'

/** Stand-in for the views T5–T8 build. `page` is a key group in locales/en/pages.json. */
export default function Placeholder({ page }: { page: string }) {
  const { t } = useTranslation()
  return (
    <>
      <PageTitle subtitle={t(`pages.${page}.subtitle`)}>{t(`pages.${page}.title`)}</PageTitle>
      <div className="card p-5">
        <p className="mb-1 font-semibold">{t('common.comingSoon')}</p>
        <p className="text-sm muted">{t('common.comingSoonHint')}</p>
      </div>
    </>
  )
}
