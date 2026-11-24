import { useTranslation } from 'react-i18next'
import { useMergeSuggestions } from '../../api/admin'
import { useAuth } from '../../auth/AuthContext'
import { SidebarEntry, SidebarSection } from '../Sidebar'

/** Profile's sections, and Admin's with the merge-suggestion badge. (New bill and Exchange are in money.tsx.) */

export function ProfileSubNav() {
  const { t } = useTranslation()
  return (
    <>
      <SidebarSection>{t('subnav.profile.title')}</SidebarSection>
      <SidebarEntry to="/profile" icon="🪪" title={t('subnav.profile.details')} subtitle={t('subnav.profile.detailsHint')} />
      <SidebarEntry to="/profile/password" icon="🔑" title={t('subnav.profile.password')} subtitle={t('subnav.profile.passwordHint')} />
      <SidebarEntry to="/profile/telegram" icon="✈️" title={t('subnav.profile.telegram')} subtitle={t('subnav.profile.telegramHint')} />
    </>
  )
}

export function AdminSubNav() {
  const { t } = useTranslation()
  const { isAdmin } = useAuth()
  // The number of merge suggestions; no badge at 0.
  const suggestions = useMergeSuggestions().data?.length ?? 0
  if (!isAdmin) return null
  return (
    <>
      <SidebarSection>{t('subnav.admin.title')}</SidebarSection>
      <SidebarEntry to="/admin/users" icon="👥" title={t('subnav.admin.users')} subtitle={t('subnav.admin.usersHint')} />
      <SidebarEntry to="/admin/merge" icon="🔀" title={t('subnav.admin.merge')} subtitle={t('subnav.admin.mergeHint')} badge={suggestions} />
      <SidebarEntry to="/admin/bills" icon="🧾" title={t('subnav.admin.bills')} subtitle={t('subnav.admin.billsHint')} />
    </>
  )
}
