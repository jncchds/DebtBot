import { QueryClient, useQuery } from '@tanstack/react-query'
import { api, ApiError } from './client'
import type { AuthConfig, Profile } from './types'

export function createQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: {
        staleTime: 30_000,
        // Client errors (4xx) won't fix themselves; retry only network and server errors.
        retry: (count, err) => !(err instanceof ApiError && err.status < 500) && count < 2,
        refetchOnWindowFocus: false,
      },
      mutations: { retry: false },
    },
  })
}

export const queryKeys = {
  authConfig: ['auth', 'config'] as const,
  profile: ['profile'] as const,
}

export function useAuthConfig() {
  return useQuery({
    queryKey: queryKeys.authConfig,
    queryFn: () => api<AuthConfig>('/auth/config'),
    staleTime: Infinity,
  })
}

export function useProfile() {
  return useQuery({ queryKey: queryKeys.profile, queryFn: () => api<Profile>('/profile') })
}
