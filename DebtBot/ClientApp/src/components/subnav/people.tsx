import { useState } from 'react'
import { useLocation } from 'react-router'
import { useTranslation } from 'react-i18next'
import { useContacts, useSpendingSummary } from '../../api/bills'
import { handle } from '../../lib/bills'
import { groupSubscriptions, isPlaceholder, isoDay, monthRange, recentContacts } from '../../lib/contacts'
import { withOf } from '../../lib/following'
import { useFormat } from '../../lib/useFormat'
import { FollowingSubNav } from '../following/Following'
import { isNoAccess, useFollowing } from '../../lib/useFollowing'
import { SidebarEntry, SidebarMore, SidebarSection } from '../Sidebar'

/**
 * 📊 Spendings: This month (non-cancelled totals per bill currency, local calendar month), … All spendings, then Following.
 * With `?of=`, "Boris this month" without … All spendings (mockups/following).
 */
export function SpendingsSubNav() {
  const { t, i18n } = useTranslation()
  const f = useFormat()
  const { pathname, search } = useLocation()
  // Taken once per mount; the sidebar remounts when you move between pages.
  const [now] = useState(() => new Date())
  const { from, to } = monthRange(now)
  const { of, viewing } = useFollowing()
  const summary = useSpendingSummary(from, to, of)
  const month = new Intl.DateTimeFormat(i18n.language === 'en' ? 'en-GB' : i18n.language, { month: 'long' }).format(now)
  const items = summary.data ?? []
  if (of && isNoAccess(summary.error)) return <FollowingSubNav page="spendings" path="/spendings" />
  return (
    <>
      <SidebarSection>{viewing.kind === 'of' ? t('following.subnav.thisMonth', { name: viewing.name }) : t('subnav.spendings.thisMonth')}</SidebarSection>
      {summary.isSuccess && items.length === 0 && (
        <SidebarEntry to={withOf('/spendings', of)} icon="📊" title={t('subnav.spendings.nothing')} subtitle={t('subnav.spendings.none', { month })} active={false} />
      )}
      {items.map((s) => {
        const to_ = withOf(`/spendings?currency=${s.currencyCode}&from=${from}&to=${to}`, of)
        return (
          <SidebarEntry
            key={s.currencyCode}
            to={to_}
            icon="📊"
            title={f.money(s.amount, s.currencyCode)}
            subtitle={t('subnav.spendings.count', { month, count: s.count })}
            wrap
            active={`${pathname}${search}` === to_}
          />
        )
      })}
      {!of && <SidebarMore to="/spendings" label={t('subnav.spendings.all')} />}
      <FollowingSubNav page="spendings" path="/spendings" />
    </>
  )
}

/** 👥 Contacts: Recently added (the 3 newest by addedAt), … All contacts. */
export function ContactsSubNav() {
  const { t } = useTranslation()
  const f = useFormat()
  const contacts = useContacts()
  const recent = recentContacts(contacts.data ?? [])
  const [today] = useState(() => isoDay(new Date()))
  const when = (iso: string | null) => (iso ? (isoDay(new Date(iso)) === today ? t('subnav.contacts.today') : f.date(iso)) : undefined)
  return (
    <>
      <SidebarSection>{t('subnav.contacts.recent')}</SidebarSection>
      {contacts.isSuccess && recent.length === 0 && <SidebarEntry to="/contacts" icon="👤" title={t('subnav.contacts.nobody')} subtitle={t('subnav.contacts.nobodyHint')} active={false} />}
      {recent.map((c) => {
        const addedToday = !!c.addedAt && isoDay(new Date(c.addedAt)) === today
        // A fresh placeholder: "today · never started the bot"; otherwise "@username · 20 Nov".
        const parts =
          addedToday && isPlaceholder(c)
            ? [t('subnav.contacts.today'), t('contacts.neverStarted')]
            : [c.telegramUserName ? handle(c.telegramUserName) : undefined, when(c.addedAt)].filter(Boolean)
        return <SidebarEntry key={c.id} to={`/contacts?q=${encodeURIComponent(c.displayName)}`} icon="👤" title={c.displayName} subtitle={parts.join(' · ')} active={false} />
      })}
      <SidebarMore to="/contacts" label={t('subnav.contacts.all')} />
    </>
  )
}

/** 🔔 Subscriptions: Summary, three plain entries that scroll to their cards (none highlighted). */
export function SubscriptionsSubNav() {
  const { t } = useTranslation()
  const contacts = useContacts()
  const g = groupSubscriptions(contacts.data ?? [])
  const confirmed = g.yours.filter((c) => c.mySubscription === 'Confirmed').length
  const waiting = g.yours.length - confirmed
  const yours = [confirmed > 0 && t('subnav.subscriptions.confirmed', { count: confirmed }), waiting > 0 && t('subnav.subscriptions.waiting', { count: waiting })].filter(Boolean).join(' · ')
  return (
    <>
      <SidebarSection>{t('subnav.subscriptions.title')}</SidebarSection>
      <SidebarEntry
        to="/subscriptions#requests"
        icon="🔔"
        title={t('subscriptions.requests')}
        subtitle={g.requests.length ? t('subnav.subscriptions.requests', { count: g.requests.length }) : t('subnav.subscriptions.noRequests')}
        active={false}
      />
      <SidebarEntry
        to="/subscriptions#theirs"
        icon="📤"
        title={t('subscriptions.theyGetYours')}
        subtitle={g.theyGetYours.length ? t('subnav.subscriptions.people', { count: g.theyGetYours.length }) : t('subnav.subscriptions.nobody')}
        active={false}
      />
      <SidebarEntry to="/subscriptions#yours" icon="📥" title={t('subscriptions.yours')} subtitle={yours || t('subnav.subscriptions.none')} active={false} />
    </>
  )
}
