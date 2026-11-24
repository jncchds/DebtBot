import { lazy, Suspense, type ReactNode } from 'react'
import { Navigate, Route, Routes } from 'react-router'
import { RequireAdmin, RequireAuth } from './auth/guards'
import { Layout } from './components/Layout'
import { Loading } from './components/ui'
import Login from './pages/Login'
import NotFound from './pages/NotFound'
import Placeholder from './pages/Placeholder'

// Route-level code splitting: each real page is its own chunk.
const Debts = lazy(() => import('./pages/Debts'))
const Bills = lazy(() => import('./pages/Bills'))
const Bill = lazy(() => import('./pages/Bill'))

const page = (el: ReactNode) => <Suspense fallback={<Loading />}>{el}</Suspense>

/**
 * Every view has its own URL so Back/Forward, reload and bookmarks work (mockups/shell/README.md).
 * Filters, paging and tabs belong in the URL too. T6–T8 replace the remaining placeholders.
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
          <Route path="new" element={<Placeholder page="newBill" />} />
          <Route path="new/paste" element={<Placeholder page="pasteBill" />} />
          <Route path=":id" element={page(<Bill key="view" />)} />
          <Route path=":id/add-line" element={page(<Bill key="view" mode="add-line" />)} />
          <Route path=":id/add-payment" element={page(<Bill key="view" mode="add-payment" />)} />
        </Route>
        <Route path="exchange" element={<Placeholder page="exchange" />} />
        <Route path="spendings" element={<Placeholder page="spendings" />} />
        <Route path="contacts" element={<Placeholder page="contacts" />} />
        <Route path="contacts/requests" element={<Placeholder page="contactRequests" />} />
        <Route path="profile" element={<Placeholder page="profile" />} />
        <Route path="profile/password" element={<Placeholder page="password" />} />
        <Route path="profile/telegram" element={<Placeholder page="telegram" />} />
        <Route
          path="admin"
          element={
            <RequireAdmin>
              <Navigate to="/admin/users" replace />
            </RequireAdmin>
          }
        />
        <Route path="admin/users" element={<RequireAdmin><Placeholder page="adminUsers" /></RequireAdmin>} />
        <Route path="admin/merge" element={<RequireAdmin><Placeholder page="adminMerge" /></RequireAdmin>} />
        <Route path="admin/bills" element={<RequireAdmin><Placeholder page="adminBills" /></RequireAdmin>} />
        <Route path="*" element={<NotFound />} />
      </Route>
    </Routes>
  )
}
