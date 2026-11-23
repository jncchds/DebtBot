import { useTranslation } from 'react-i18next'
import { useAuth } from '../../auth/AuthContext'
import { SidebarEntry, SidebarSection } from '../Sidebar'

/** Sections that need no data. T6 adds "Continue a draft" to New bill; T8 adds the merge badge. */

export function NewBillSubNav() {
  const { t } = useTranslation()
  return (
    <>
      <SidebarSection>{t('subnav.newBill.mode')}</SidebarSection>
      <SidebarEntry to="/bills/new" icon="📝" title={t('subnav.newBill.form')} subtitle={t('subnav.newBill.formHint')} />
      <SidebarEntry to="/bills/new/paste" icon="📋" title={t('subnav.newBill.paste')} subtitle={t('subnav.newBill.pasteHint')} />
    </>
  )
}

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
  if (!isAdmin) return null
  return (
    <>
      <SidebarSection>{t('subnav.admin.title')}</SidebarSection>
      <SidebarEntry to="/admin/users" icon="👥" title={t('subnav.admin.users')} subtitle={t('subnav.admin.usersHint')} />
      <SidebarEntry to="/admin/merge" icon="🔀" title={t('subnav.admin.merge')} subtitle={t('subnav.admin.mergeHint')} />
      <SidebarEntry to="/admin/bills" icon="🧾" title={t('subnav.admin.bills')} subtitle={t('subnav.admin.billsHint')} />
    </>
  )
}
