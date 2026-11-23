import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { api, onSessionChanged, refreshSession, setSession } from '../api/client'
import type { LoginRequest, Session, SessionUser, TelegramAuthData } from '../api/types'

interface AuthState {
  user: SessionUser | null
  isAdmin: boolean
  /** False until the first refresh from the cookie has finished. */
  ready: boolean
  signIn: (request: LoginRequest) => Promise<void>
  signInWithTelegram: (data: TelegramAuthData) => Promise<void>
  signOut: () => Promise<void>
}

const AuthContext = createContext<AuthState | null>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<SessionUser | null>(null)
  const [ready, setReady] = useState(false)
  const queryClient = useQueryClient()

  useEffect(() => {
    const off = onSessionChanged((s) => setUser(s?.user ?? null))
    // Restore the session from the refresh cookie on load.
    refreshSession().finally(() => setReady(true))
    return off
  }, [])

  const start = useCallback(
    (session: Session) => {
      queryClient.clear()
      setSession(session)
    },
    [queryClient],
  )

  const signIn = useCallback(
    async (request: LoginRequest) => start(await api<Session>('/auth/login', { method: 'POST', json: request })),
    [start],
  )

  const signInWithTelegram = useCallback(
    async (data: TelegramAuthData) => start(await api<Session>('/auth/telegram', { method: 'POST', json: data })),
    [start],
  )

  const signOut = useCallback(async () => {
    try {
      await api('/auth/logout', { method: 'POST' })
    } finally {
      setSession(null)
      queryClient.clear()
    }
  }, [queryClient])

  const value = useMemo<AuthState>(
    () => ({ user, isAdmin: !!user?.isAdmin, ready, signIn, signInWithTelegram, signOut }),
    [user, ready, signIn, signInWithTelegram, signOut],
  )
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

// eslint-disable-next-line react-refresh/only-export-components
export function useAuth() {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth outside AuthProvider')
  return ctx
}
