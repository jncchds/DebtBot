import { Fragment, useMemo } from 'react'
import { useTranslation } from 'react-i18next'
import { useAllOwnDebts, useBills } from '../../api/bills'
import { firstLine, pairExchanges } from '../../lib/bills'
import { useFormat } from '../../lib/useFormat'
import { SidebarEntry, SidebarMore, SidebarSection } from '../Sidebar'

/** 💸 Debts: Balances, one entry per person (green "owes you", red "you owe"), each opening their bills. */
export function DebtsSubNav() {
  const { t } = useTranslation()
  const f = useFormat()
  const debts = useAllOwnDebts()
  const people = useMemo(() => {
    const byPerson = new Map<string, { name: string; items: { amount: number; currency: string }[] }>()
    for (const d of debts.data ?? []) {
      if (d.amount === 0) continue
      const p = byPerson.get(d.debtorUser.id) ?? { name: d.debtorUser.displayName, items: [] }
      p.items.push({ amount: d.amount, currency: d.currencyCode })
      byPerson.set(d.debtorUser.id, p)
    }
    return [...byPerson].sort(([, a], [, b]) => a.name.localeCompare(b.name))
  }, [debts.data])

  return (
    <>
      <SidebarSection>{t('subnav.debts.title')}</SidebarSection>
      {debts.isSuccess && people.length === 0 && <SidebarEntry to="/" icon="✅" title={t('subnav.debts.settled')} subtitle={t('subnav.debts.settledHint')} />}
      {people.map(([id, p]) => (
        <SidebarEntry
          key={id}
          to={`/bills?with=${id}`}
          icon="👤"
          title={p.name}
          subtitle={p.items.map((x, i) => (
            <Fragment key={x.currency}>
              {i > 0 && ' · '}
              <span className={x.amount > 0 ? 'text-success' : 'text-danger'}>{t(x.amount > 0 ? 'subnav.debts.owesYou' : 'subnav.debts.youOwe', { amount: f.money(x.amount, x.currency) })}</span>
            </Fragment>
          ))}
        />
      ))}
    </>
  )
}

/** 🧾 Bills and a bill: Drafts (an exchange once), Recent bills (last 5), … All bills. */
export function BillsSubNav() {
  const { t } = useTranslation()
  const f = useFormat()
  const drafts = useBills({ status: 'Draft', countPerPage: 10 })
  const recent = useBills({ countPerPage: 15 })
  const draftRows = pairExchanges(drafts.data?.items ?? [])
  const recentRows = pairExchanges((recent.data?.items ?? []).filter((b) => b.status !== 'Draft')).slice(0, 5)

  return (
    <>
      {draftRows.length > 0 && <SidebarSection>{t('subnav.bills.drafts')}</SidebarSection>}
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
      <SidebarSection>{t('subnav.bills.recent')}</SidebarSection>
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
      <SidebarMore to="/bills" label={t('subnav.bills.all')} />
    </>
  )
}
