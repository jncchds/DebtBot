import { useEffect, useState } from 'react'
import { Navigate, useNavigate, useParams } from 'react-router'
import { useTranslation } from 'react-i18next'
import { useCreateBill } from '../api/bills'
import { ApiError } from '../api/client'
import { useAuthConfig } from '../api/hooks'
import type { ReceiptDraft, ReceiptWarning } from '../api/types'
import { useAuth } from '../auth/AuthContext'
import { BillForm } from '../components/bill/BillForm'
import { ReceiptLinkNotice } from '../components/receipt/ReceiptLinkNotice'
import { FromReceiptButton, ReceiptPanel, ReceiptWarnings } from '../components/receipt/Receipt'
import { useReceiptLink } from '../components/receipt/useReceiptLink'
import { PageTitle, Spinner } from '../components/ui'
import { checkNewBill, emptyNewBill, formEditor, loadStored, store, toBillCreation, type NewBillForm } from '../lib/newBill'
import { draftToForm, formHasContent, fromReceiptTarget, withoutWarning } from '../lib/receipt'
import { usePeople, type Person } from '../lib/usePeople'

const NEW_BILL_KEY = 'debtbot.newBill'
/** The receipt's warnings shown above the lines, kept with the form. */
const WARNINGS_KEY = 'debtbot.newBill.receiptWarnings'

/**
 * ➕ New bill, form mode: /bills/new, with the editors at /bills/new/add-line, /bills/new/add-payment and
 * /bills/new/edit-line/<n> (mockups/new-bill, series-2 mockups/creator-leave). The form lives in sessionStorage, so a
 * reload keeps it; it's cleared once the bill is created.
 *
 * Receipts (receipts T3, receipts/mockups/receipt): /bills/new/receipt opens the 🧾 From a receipt panel above the
 * form, and the bot's link /bills/new?receipt=<id> loads a read receipt. Either fills the form, asking "Replace the
 * form?" first when it has content.
 */
export default function NewBill({ mode, receipt }: { mode?: 'add-line' | 'add-payment' | 'edit-line'; receipt?: boolean }) {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const { n } = useParams()
  const { user } = useAuth()
  const { people } = usePeople()
  const [form, setForm] = useState<NewBillForm>(() => loadStored(NEW_BILL_KEY, emptyNewBill(user?.id)))
  const [tried, setTried] = useState(false)
  const [error, setError] = useState<string>()
  const [phase, setPhase] = useState<'idle' | 'creating' | 'finalizing'>('idle')
  const create = useCreateBill()
  const config = useAuthConfig()
  const receiptsOn = !!config.data?.receiptParse
  const fromButton = fromReceiptTarget(config.data, 'form', !!receipt)
  const [warnings, setWarnings] = useState<ReceiptWarning[]>(() => loadStored(WARNINGS_KEY, { items: [] as ReceiptWarning[] }).items)
  const [pending, setPending] = useState<ReceiptDraft>()

  useEffect(() => store(NEW_BILL_KEY, form), [form])
  useEffect(() => store(WARNINGS_KEY, warnings.length ? { items: warnings } : undefined), [warnings])

  const fill = (draft: ReceiptDraft) => {
    setForm((f) => draftToForm(draft, f, user?.id))
    setWarnings(draft.warnings)
    setPending(undefined)
    setTried(false)
    setError(undefined)
    if (receipt) navigate('/bills/new', { replace: true })
  }
  const onRead = (draft: ReceiptDraft) => (formHasContent(form) ? setPending(draft) : fill(draft))
  const [link, clearLink] = useReceiptLink('form', onRead)

  const check = checkNewBill(form)
  const byId = new Map(people.map((p) => [p.id, p]))
  const personOf = (pid: string): Person => byId.get(pid) ?? { id: pid, name: pid === user?.id ? t('common.youNamed', { name: user.displayName }) : '…', isMe: pid === user?.id }

  const submit = (finalize: boolean) => {
    setTried(true)
    setError(undefined)
    if (!check.canSave || (finalize && check.finalizeBlockers.length > 0)) return
    setPhase('creating')
    create.mutate(
      {
        bill: toBillCreation(form),
        finalize,
        onCreated: () => {
          // The bill exists now: the form is done with, even if finalizing fails below.
          store(NEW_BILL_KEY, undefined)
          store(WARNINGS_KEY, undefined)
          if (finalize) setPhase('finalizing')
        },
      },
      {
        onSuccess: (billId) => navigate(`/bills/${billId}`),
        onError: (e) => {
          setPhase('idle')
          setError(e instanceof ApiError && e.serverMessage ? e.serverMessage : e.message)
        },
      },
    )
  }

  const busy = phase !== 'idle'

  if (receipt && config.data && !receiptsOn) return <Navigate to="/bills/new" replace />

  return (
    <>
      <PageTitle subtitle={t('newBill.subtitle')} actionsEnd actions={fromButton && <FromReceiptButton to={fromButton} />}>
        {t('pages.newBill.title')}
      </PageTitle>
      {receipt && receiptsOn ? (
        <ReceiptPanel target="form" model={config.data?.receiptModel} closeTo="/bills/new" onRead={onRead} pending={pending} onReplace={() => fill(pending!)} onKeep={() => setPending(undefined)} />
      ) : (
        <ReceiptLinkNotice target="form" link={link} onDismiss={clearLink} pending={pending} onReplace={() => fill(pending!)} onKeep={() => setPending(undefined)} />
      )}
      <BillForm
        form={form}
        setForm={setForm}
        base="/bills/new"
        editor={formEditor(mode, n)}
        tried={tried}
        people={people}
        personOf={personOf}
        linesNotice={<ReceiptWarnings className="mx-4 mt-3" warnings={warnings} onDismiss={(i) => setWarnings((w) => withoutWarning(w, i))} />}
      >
        <section className="card">
          {tried && (!check.canSave || error) && (
            <div className="px-4 pt-3.5">
              <div className="alert-danger" role="alert">
                {error ?? (
                  <>
                    <b>{t('newBill.cantSave')}</b> {t('newBill.fixFields')}
                  </>
                )}
              </div>
            </div>
          )}
          <div className="flex flex-wrap items-center gap-2 px-4 py-3.5">
            <span className="text-[0.8rem] muted">{t('newBill.kept')}</span>
            <button
              type="button"
              className="btn-ghost px-2 py-1 text-[0.8rem]"
              disabled={busy}
              onClick={() => {
                setForm(emptyNewBill(user?.id))
                setWarnings([])
                setTried(false)
                setError(undefined)
              }}
            >
              {t('newBill.clear')}
            </button>
            <span className="flex-1" />
            {busy && (
              <span className="inline-flex items-center gap-2 text-sm" role="status">
                <Spinner className="h-4 w-4" />
                {t(phase === 'finalizing' ? 'bill.finalizing' : 'newBill.creating')}
              </span>
            )}
            {!busy && check.canSave && check.finalizeBlockers.length > 0 && <span className="text-[0.8rem] muted">{t(check.finalizeBlockers[0])}</span>}
            <div className="flex flex-wrap gap-2 max-md:w-full max-md:[&>*]:flex-1">
              <button type="button" className="btn-secondary" disabled={busy} onClick={() => submit(false)}>
                {t('newBill.saveDraft')}
              </button>
              <button type="button" className="btn-primary" disabled={busy || (check.canSave && check.finalizeBlockers.length > 0)} onClick={() => submit(true)}>
                ✓ {t('newBill.createFinalize')}
              </button>
            </div>
          </div>
        </section>
      </BillForm>
    </>
  )
}
