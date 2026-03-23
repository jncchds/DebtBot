import React from 'react'
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom'
import { ThemeProvider, createTheme } from '@mui/material'
import { AuthProvider } from './AuthContext'
import PrivateRoute from './PrivateRoute'
import AppLayout from './AppLayout'
import LoginPage from './pages/LoginPage'
import BillsPage from './pages/BillsPage'
import BillDetailPage from './pages/BillDetailPage'
import CreateBillPage from './pages/CreateBillPage'
import DebtsPage from './pages/DebtsPage'
import SpendingsPage from './pages/SpendingsPage'
import ContactsPage from './pages/ContactsPage'
import ProfilePage from './pages/ProfilePage'

const theme = createTheme()

export default function App() {
  return (
    <ThemeProvider theme={theme}>
      <AuthProvider>
        <BrowserRouter>
          <Routes>
            <Route path="/login" element={<LoginPage />} />
            <Route
              path="/*"
              element={
                <PrivateRoute>
                  <AppLayout>
                    <Routes>
                      <Route index element={<Navigate to="/bills" replace />} />
                      <Route path="bills" element={<BillsPage />} />
                      <Route path="bills/new" element={<CreateBillPage />} />
                      <Route path="bills/:id" element={<BillDetailPage />} />
                      <Route path="debts" element={<DebtsPage />} />
                      <Route path="spendings" element={<SpendingsPage />} />
                      <Route path="contacts" element={<ContactsPage />} />
                      <Route path="profile" element={<ProfilePage />} />
                    </Routes>
                  </AppLayout>
                </PrivateRoute>
              }
            />
          </Routes>
        </BrowserRouter>
      </AuthProvider>
    </ThemeProvider>
  )
}
