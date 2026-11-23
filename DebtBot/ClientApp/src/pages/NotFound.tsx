import { Link } from 'react-router'
import { useTranslation } from 'react-i18next'
import { PageTitle } from '../components/ui'

export default function NotFound() {
  const { t } = useTranslation()
  return (
    <>
      <PageTitle subtitle={t('notFound.text')}>{t('notFound.title')}</PageTitle>
      <Link to="/" className="btn-secondary">
        {t('notFound.home')}
      </Link>
    </>
  )
}
