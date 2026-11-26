import { useEffect, useState } from 'react'
import { useNavigate, useParams } from 'react-router'
import { useTranslation } from 'react-i18next'
import { useCreateBill } from '../api/bills'
import { ApiError } from '../api/client'
import { useAuth } from '../auth/AuthContext'
import { BillForm } from '../components/bill/BillForm'
import { PageTitle, Spinner } from '../components/ui'
import { checkNewBill, emptyNewBill, formEditor, loadStored, store, toBillCreation, type NewBillForm } from '../lib/newBill'
import { usePeople, type Person } from '../lib/usePeople'

const NEW_BILL_KEY = 'debtbot.newBill'

/**
 * ➕ New bill, form mode: /bills/new, with the editors at /bills/new/add-line, /bills/new/add-payment and
 * /bills/new/edit-line/<n> (mockups/new-bill, series-2 mockups/creator-leave). The form lives in sessionStorage, so a
 * reload keeps it; it's cleared once the bill is created.
 */
export default function NewBill({ mode }: { mode?: 'add-line' | 'add-payment' | 'edit-line' }) {
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

  useEffect(() => store(NEW_BILL_KEY, form), [form])

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

  return (
    <>
      <PageTitle subtitle={t('newBill.subtitle')}>{t('pages.newBill.title')}</PageTitle>
      <BillForm form={form} setForm={setForm} base="/bills/new" editor={formEditor(mode, n)} tried={tried} people={people} personOf={personOf}>
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
            <div className="flex flex-wrap gap-2">
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
