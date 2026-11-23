import { Navigate, Route, Routes } from 'react-router'
import { RequireAdmin, RequireAuth } from './auth/guards'
import { Layout } from './components/Layout'
import Login from './pages/Login'
import NotFound from './pages/NotFound'
import Placeholder from './pages/Placeholder'

/**
 * Every view has its own URL so Back/Forward, reload and bookmarks work (mockups/shell/README.md).
 * Filters, paging and tabs belong in the URL too. T5–T8 replace the placeholders.
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
        <Route index element={<Placeholder page="debts" />} />
        <Route path="debts" element={<Navigate to="/" replace />} />
        <Route path="bills">
          <Route index element={<Placeholder page="bills" />} />
          <Route path="new" element={<Placeholder page="newBill" />} />
          <Route path="new/paste" element={<Placeholder page="pasteBill" />} />
          <Route path=":id" element={<Placeholder page="bill" />} />
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
