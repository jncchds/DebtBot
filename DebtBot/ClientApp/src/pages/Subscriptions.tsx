import { useEffect, useState, type ReactNode } from 'react'
import { Link, useLocation } from 'react-router'
import { Trans, useTranslation } from 'react-i18next'
import { useContacts } from '../api/bills'
import { ApiError } from '../api/client'
import { useSubscriptionAction, type SubscriptionAction } from '../api/people'
import type { Contact } from '../api/types'
import { ErrorBox, Loading, PageTitle } from '../components/ui'
import { handle } from '../lib/bills'
import { groupSubscriptions } from '../lib/contacts'

/**
 * 🔔 Subscriptions: who the bot tells about whose bills and debts in Telegram (mockups/subscriptions).
 * Built from GET UserContacts/Own; the web UI never shows other people's debts here.
 */
export default function Subscriptions() {
  const { t } = useTranslation()
  const contacts = useContacts()
  const action = useSubscriptionAction()
  const [error, setError] = useState<string>()
  const { hash } = useLocation()

  // The sidebar's Summary entries link to #requests, #theirs and #yours.
  useEffect(() => {
    if (hash && contacts.isSuccess) document.getElementById(hash.slice(1))?.scrollIntoView({ behavior: 'smooth', block: 'start' })
  }, [hash, contacts.isSuccess])

  if (contacts.isLoading) return <Loading />
  if (contacts.error) return <ErrorBox error={contacts.error} />

  const g = groupSubscriptions(contacts.data ?? [])
  const busy = (c: Contact) => action.isPending && action.variables?.contactId === c.id

  const run = (act: SubscriptionAction, c: Contact) => {
    setError(undefined)
    action.mutate(
      { action: act, contactId: c.id },
      { onError: (e) => setError(e instanceof ApiError && e.serverMessage ? e.serverMessage : t('subscriptions.failed')) },
    )
  }

  const button = (act: SubscriptionAction, c: Contact, label: string, style: string) => (
    <button type="button" className={`${style} px-2.5 py-1 text-[0.8rem] whitespace-nowrap`} disabled={busy(c)} onClick={() => run(act, c)}>
      {label}
    </button>
  )

  return (
    <>
      <PageTitle subtitle={t('pages.subscriptions.subtitle')}>{t('pages.subscriptions.title')}</PageTitle>
      {error && (
        <div className="alert-danger mb-4" role="alert">
          {error}
        </div>
      )}
      <div className="space-y-4">
        <Group id="requests" title={t('subscriptions.requests')} hint={t('subscriptions.requestsHint')} empty={t('subscriptions.noRequests')} people={g.requests}>
          {(c) => (
            <>
              {button('confirm', c, `✓ ${t('subscriptions.confirm')}`, 'btn-primary')}
              {button('decline', c, t('subscriptions.decline'), 'btn-secondary')}
            </>
          )}
        </Group>
        <Group id="theirs" title={t('subscriptions.theyGetYours')} hint={t('subscriptions.theyGetYoursHint')} empty={t('subscriptions.nobody')} people={g.theyGetYours}>
          {(c) => (
            <>
              <span className="pill-success whitespace-nowrap">{t('subscriptions.confirmed')}</span>
              {button('decline', c, t('subscriptions.stop'), 'btn-ghost')}
            </>
          )}
        </Group>
        <Group
          id="yours"
          title={t('subscriptions.yours')}
          hint={t('subscriptions.yoursHint')}
          empty={<Trans i18nKey="subscriptions.noneYours" components={{ a: <Link to="/contacts" className="link" /> }} />}
          people={g.yours}
        >
          {(c) =>
            c.mySubscription === 'Confirmed' ? (
              <>
                <span className="pill-success whitespace-nowrap">🔔 {t('subscriptions.youGetTheirs')}</span>
                {button('cancel', c, t('subscriptions.stop'), 'btn-ghost')}
              </>
            ) : (
              <>
                <span className="pill-muted max-w-full break-words">⏳ {t('subscriptions.waitingFor', { name: c.displayName })}</span>
                {button('cancel', c, t('subscriptions.withdraw'), 'btn-ghost')}
              </>
            )
          }
        </Group>
      </div>
    </>
  )
}

function Group({ id, title, hint, empty, people, children }: { id: string; title: string; hint: string; empty: ReactNode; people: Contact[]; children: (c: Contact) => ReactNode }) {
  return (
    <section id={id} className="card scroll-mt-4 overflow-hidden" aria-labelledby={`${id}-title`}>
      <header className="flex flex-wrap items-baseline gap-x-2 border-b border-line px-4 py-3">
        <h2 id={`${id}-title`} className="font-bold">
          {title}
        </h2>
        <span className="text-[0.8rem] muted">{hint}</span>
      </header>
      {people.length === 0 ? (
        <p className="px-4 py-3 text-sm muted">{empty}</p>
      ) : (
        <ul>
          {people.map((c) => (
            <li key={c.id} className="flex flex-wrap items-center gap-x-3 gap-y-2 border-b border-line px-4 py-3 last:border-b-0">
              <span className="min-w-0 flex-1">
                <span className="block font-semibold break-words">{c.displayName}</span>
                {c.telegramUserName && <span className="block text-[0.8rem] muted">{handle(c.telegramUserName)}</span>}
              </span>
              <span className="ml-auto flex max-w-full flex-wrap items-center justify-end gap-2">{children(c)}</span>
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}
