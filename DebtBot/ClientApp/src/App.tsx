import { lazy, Suspense, type ReactNode } from 'react'
import { Navigate, Route, Routes } from 'react-router'
import { RequireAdmin, RequireAuth } from './auth/guards'
import { Layout } from './components/Layout'
import { Loading } from './components/ui'
import Login from './pages/Login'
import NotFound from './pages/NotFound'

// Route-level code splitting: each real page is its own chunk.
const Debts = lazy(() => import('./pages/Debts'))
const Bills = lazy(() => import('./pages/Bills'))
const Bill = lazy(() => import('./pages/Bill'))
const NewBill = lazy(() => import('./pages/NewBill'))
const PasteBill = lazy(() => import('./pages/PasteBill'))
const Exchange = lazy(() => import('./pages/Exchange'))
const Spendings = lazy(() => import('./pages/Spendings'))
const Contacts = lazy(() => import('./pages/Contacts'))
const Subscriptions = lazy(() => import('./pages/Subscriptions'))
const Profile = lazy(() => import('./pages/Profile'))
const ProfileTokens = lazy(() => import('./pages/ProfileTokens'))
const AdminUsers = lazy(() => import('./pages/AdminUsers'))
const AdminMerge = lazy(() => import('./pages/AdminMerge'))
const AdminBills = lazy(() => import('./pages/AdminBills'))

const page = (el: ReactNode) => <Suspense fallback={<Loading />}>{el}</Suspense>

/**
 * Every view has its own URL so Back/Forward, reload and bookmarks work (mockups/shell/README.md).
 * Filters, paging and tabs belong in the URL too.
 */
export default function App() {
  return (
    <Routes>
      <Route path="/login" element={<Login />} />
      <Route
        element={
          <RequireAuth>
            <Layout />
          </RequireAuth>
        }
      >
        <Route index element={page(<Debts />)} />
        <Route path="debts" element={<Navigate to="/" replace />} />
        <Route path="bills">
          <Route index element={page(<Bills />)} />
          <Route path="new" element={page(<NewBill key="form" />)} />
          <Route path="new/add-line" element={page(<NewBill key="form" mode="add-line" />)} />
          <Route path="new/add-payment" element={page(<NewBill key="form" mode="add-payment" />)} />
          <Route path="new/edit-line/:n" element={page(<NewBill key="form" mode="edit-line" />)} />
          <Route path="new/paste" element={page(<PasteBill />)} />
          <Route path=":id" element={page(<Bill key="view" />)} />
          <Route path=":id/add-line" element={page(<Bill key="view" mode="add-line" />)} />
          <Route path=":id/add-payment" element={page(<Bill key="view" mode="add-payment" />)} />
          <Route path=":id/edit-line/:n" element={page(<Bill key="view" mode="edit-line" />)} />
        </Route>
        <Route path="exchange" element={page(<Exchange />)} />
        <Route path="spendings" element={page(<Spendings />)} />
        <Route path="contacts" element={page(<Contacts />)} />
        {/* Requests moved to their own page (T7). */}
        <Route path="contacts/requests" element={<Navigate to="/subscriptions" replace />} />
        <Route path="subscriptions" element={page(<Subscriptions />)} />
        <Route path="profile" element={page(<Profile key="details" section="details" />)} />
        <Route path="profile/password" element={page(<Profile key="password" section="password" />)} />
        <Route path="profile/telegram" element={page(<Profile key="telegram" section="telegram" />)} />
        {/* One component for the three, so the shown-once secret survives the move from /new to /created. */}
        <Route path="profile/tokens" element={page(<ProfileTokens key="tokens" mode="list" />)} />
        <Route path="profile/tokens/new" element={page(<ProfileTokens key="tokens" mode="new" />)} />
        <Route path="profile/tokens/created" element={page(<ProfileTokens key="tokens" mode="created" />)} />
        <Route
          path="admin"
          element={
            <RequireAdmin>
              <Navigate to="/admin/users" replace />
            </RequireAdmin>
          }
        />
        {/* The password dialog sits over the list; both paths render the same page so the list stays put. */}
        <Route path="admin/users" element={<RequireAdmin>{page(<AdminUsers key="users" />)}</RequireAdmin>} />
        <Route path="admin/users/:id/password" element={<RequireAdmin>{page(<AdminUsers key="users" />)}</RequireAdmin>} />
        <Route path="admin/merge" element={<RequireAdmin>{page(<AdminMerge />)}</RequireAdmin>} />
        <Route path="admin/bills" element={<RequireAdmin>{page(<AdminBills />)}</RequireAdmin>} />
        <Route path="*" element={<NotFound />} />
      </Route>
    </Routes>
  )
}
