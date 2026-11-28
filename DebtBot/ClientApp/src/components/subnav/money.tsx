import { Fragment, useMemo } from 'react'
import { useLocation } from 'react-router'
import { useTranslation } from 'react-i18next'
import { useAllOwnDebts, useBills, useExchanges } from '../../api/bills'
import type { ExchangeListItem } from '../../api/types'
import { useAuth } from '../../auth/AuthContext'
import { firstLine, pairExchanges } from '../../lib/bills'
import { dealFromListItem } from '../../lib/exchange'
import { withOf } from '../../lib/following'
import { useFormat } from '../../lib/useFormat'
import { FollowingSubNav } from '../following/Following'
import { isNoAccess, useFollowing } from '../../lib/useFollowing'
import { useNameOf } from '../../lib/usePeople'
import { SidebarEntry, SidebarMore, SidebarSection } from '../Sidebar'

/**
 * 💸 Debts: Balances, one entry per person (green "owes you", red "you owe"), each opening their bills, then Following.
 * With `?of=`, "Boris's balances" ("owes Boris", "Boris owes"), people under their own names (mockups/following).
 */
export function DebtsSubNav() {
  const { t } = useTranslation()
  const f = useFormat()
  const { user } = useAuth()
  const { of, viewing } = useFollowing()
  const name = viewing.kind === 'of' ? viewing.name : ''
  const debts = useAllOwnDebts(of)
  const people = useMemo(() => {
    const byPerson = new Map<string, { name: string; items: { amount: number; currency: string }[] }>()
    for (const d of debts.data ?? []) {
      if (d.amount === 0) continue
      const own = d.debtorUser.displayName
      const p = byPerson.get(d.debtorUser.id) ?? { name: of && d.debtorUser.id === user?.id ? t('common.youNamed', { name: own }) : own, items: [] }
      p.items.push({ amount: d.amount, currency: d.currencyCode })
      byPerson.set(d.debtorUser.id, p)
    }
    return [...byPerson].sort(([, a], [, b]) => a.name.localeCompare(b.name))
  }, [debts.data, of, user?.id, t])
  const words = (amount: number, money: string) =>
    of
      ? t(amount > 0 ? 'following.subnav.owesThem' : 'following.subnav.theyOwe', { amount: money, name })
      : t(amount > 0 ? 'subnav.debts.owesYou' : 'subnav.debts.youOwe', { amount: money })

  // Someone you may not see: no section of theirs, only Following.
  if (of && isNoAccess(debts.error)) return <FollowingSubNav page="debts" path="/" />

  return (
    <>
      <SidebarSection>{of ? t('following.subnav.balances', { name }) : t('subnav.debts.title')}</SidebarSection>
      {debts.isSuccess && people.length === 0 && <SidebarEntry to={withOf('/', of)} icon="✅" title={t('subnav.debts.settled')} subtitle={t('subnav.debts.settledHint')} />}
      {people.map(([id, p]) => (
        <SidebarEntry
          key={id}
          to={withOf(`/bills?with=${id}`, of)}
          icon="👤"
          title={p.name}
          subtitle={p.items.map((x, i) => (
            <Fragment key={x.currency}>
              {i > 0 && ' · '}
              <span className={x.amount > 0 ? 'text-success' : 'text-danger'}>{words(x.amount, f.money(x.amount, x.currency))}</span>
            </Fragment>
          ))}
        />
      ))}
      <FollowingSubNav page="debts" path="/" />
    </>
  )
}

/** ➕ New bill: Mode (Form, Paste text), then Continue a draft (your latest drafts that aren't exchanges). */
export function NewBillSubNav() {
  const { t } = useTranslation()
  const f = useFormat()
  const { user } = useAuth()
  const { pathname } = useLocation()
  const drafts = useBills({ status: 'Draft', createdByMe: true, countPerPage: 15 })
  const mine = (drafts.data?.items ?? []).filter((b) => !b.exchangeBillId && b.creator.id === user?.id).slice(0, 5)
  return (
    <>
      <SidebarSection>{t('subnav.newBill.mode')}</SidebarSection>
      {/* The form's editors (/bills/new/add-line, /add-payment) and its receipt panel belong to Form too. */}
      <SidebarEntry to="/bills/new" end={false} icon="📝" title={t('subnav.newBill.form')} subtitle={t('subnav.newBill.formHint')} active={!pathname.startsWith('/bills/new/paste')} />
      <SidebarEntry to="/bills/new/paste" end={false} icon="📋" title={t('subnav.newBill.paste')} subtitle={t('subnav.newBill.pasteHint')} />
      {mine.length > 0 && <SidebarSection>{t('subnav.newBill.drafts')}</SidebarSection>}
      {mine.map((b) => (
        <SidebarEntry
          key={b.id}
          to={`/bills/${b.id}`}
          icon="✏️"
          title={firstLine(b.description) || t('bill.untitled')}
          subtitle={`${f.date(b.date)} · ${b.totalWithTips > 0 ? f.money(b.totalWithTips, b.currencyCode) : t('subnav.newBill.noLines')}`}
        />
      ))}
    </>
  )
}

/** 🔁 Exchange: Waiting to accept, Recent exchanges. Amounts wrap instead of being cut off (user, mockup round 4). */
export function ExchangeSubNav() {
  const { t } = useTranslation()
  const f = useFormat()
  const names = useNameOf()
  const waiting = useExchanges({ status: 'Draft', countPerPage: 5 })
  const recent = useExchanges({ countPerPage: 15 })
  const recentItems = (recent.data?.items ?? []).filter((x) => x.status !== 'Draft').slice(0, 5)
  const entry = (x: ExchangeListItem, to: string, icon: string) => {
    const deal = dealFromListItem(x)
    const other = deal ? (names.isMe(deal.from.id) ? deal.to : deal.from) : x.creator
    return (
      <SidebarEntry
        key={x.forwardBillId}
        to={to}
        icon={icon}
        wrap
        title={deal && !names.isMe(deal.from.id) && !names.isMe(deal.to.id) ? `${names.short(deal.from)} ⇄ ${names.short(deal.to)}` : names.short(other)}
        subtitle={`${f.date(x.date)} · ${x.status === 'Cancelled' ? t('subnav.bills.cancelled') : `${f.money(x.fromAmount, x.fromCurrencyCode)} ⇄ ${f.money(x.toAmount, x.toCurrencyCode)}`}`}
      />
    )
  }
  return (
    <>
      <SidebarSection>{t('subnav.exchange.waiting')}</SidebarSection>
      {waiting.isSuccess && waiting.data.items.length === 0 && <p className="px-2 py-1 text-[0.8rem] muted">{t('subnav.exchange.noneWaiting')}</p>}
      {(waiting.data?.items ?? []).map((x) => entry(x, `/exchange?draft=${x.forwardBillId}`, '🔁'))}
      <SidebarSection>{t('subnav.exchange.recent')}</SidebarSection>
      {recent.isSuccess && recentItems.length === 0 && <p className="px-2 py-1 text-[0.8rem] muted">{t('subnav.exchange.none')}</p>}
      {recentItems.map((x) => entry(x, `/bills/${x.forwardBillId}`, x.status === 'Cancelled' ? '✖️' : '✅'))}
    </>
  )
}

/**
 * 🧾 Bills and a bill: Drafts (an exchange once), Recent bills (last 5), … All bills, then Following.
 * With `?of=`, "Boris's drafts" and "Boris's recent bills" without … All bills (mockups/following).
 */
export function BillsSubNav() {
  const { t } = useTranslation()
  const f = useFormat()
  const { of, viewing } = useFollowing()
  const name = viewing.kind === 'of' ? viewing.name : ''
  const drafts = useBills({ status: 'Draft', countPerPage: 10, ofUserId: of })
  const recent = useBills({ countPerPage: 15, ofUserId: of })
  const draftRows = pairExchanges(drafts.data?.items ?? [])
  const recentRows = pairExchanges((recent.data?.items ?? []).filter((b) => b.status !== 'Draft')).slice(0, 5)
  if (of && isNoAccess(recent.error)) return <FollowingSubNav page="bills" path="/bills" />

  return (
    <>
      {draftRows.length > 0 && <SidebarSection>{of ? t('following.subnav.drafts', { name }) : t('subnav.bills.drafts')}</SidebarSection>}
      {draftRows.map(({ bill: b, pair }) => (
        <SidebarEntry
          key={b.id}
          to={`/bills/${b.id}`}
          end={false}
          icon={b.exchangeBillId ? '🔁' : '✏️'}
          title={firstLine(b.description) || t('bill.untitled')}
          subtitle={`${f.date(b.date)} · ${f.money(b.totalWithTips, b.currencyCode)}${pair ? ` ⇄ ${f.money(pair.totalWithTips, pair.currencyCode)}` : ''}`}
        />
      ))}
      <SidebarSection>{of ? t('following.subnav.recent', { name }) : t('subnav.bills.recent')}</SidebarSection>
      {recent.isSuccess && recentRows.length === 0 && <p className="px-2 py-1 text-[0.8rem] muted">{t('subnav.bills.none')}</p>}
      {recentRows.map(({ bill: b, pair }) => (
        <SidebarEntry
          key={b.id}
          to={`/bills/${b.id}`}
          end={false}
          icon={b.exchangeBillId ? '🔁' : '🧾'}
          title={firstLine(b.description) || t('bill.untitled')}
          subtitle={`${f.date(b.date)} · ${b.status === 'Cancelled' ? t('subnav.bills.cancelled') : f.money(b.totalWithTips, b.currencyCode)}${pair && b.status !== 'Cancelled' ? ` ⇄ ${f.money(pair.totalWithTips, pair.currencyCode)}` : ''}`}
        />
      ))}
      {!of && <SidebarMore to="/bills" label={t('subnav.bills.all')} />}
      <FollowingSubNav page="bills" path="/bills" />
    </>
  )
}
