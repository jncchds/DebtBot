import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter } from 'react-router'
import { QueryClientProvider } from '@tanstack/react-query'
import '@fontsource-variable/inter'
import './index.css'
import './i18n'
import { AuthProvider } from './auth/AuthContext'
import { createQueryClient } from './api/hooks'
import { applyTheme, storedTheme } from './hooks/useTheme'
import App from './App'

// index.html already applied it before the first paint; this also sets the theme-color meta.
applyTheme(storedTheme())

const queryClient = createQueryClient()

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <AuthProvider>
          <App />
        </AuthProvider>
      </BrowserRouter>
    </QueryClientProvider>
  </StrictMode>,
)
