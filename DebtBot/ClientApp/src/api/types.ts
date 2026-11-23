// API shapes from DebtBot.Doc/web-ui/api.md. Enums are strings. Later tasks add the rest.

export type ProcessingState = 'Draft' | 'Ready' | 'Processed' | 'Cancelled'
export type UserRole = 'Regular' | 'Admin'
export type SubscriptionState = 'None' | 'Pending' | 'Confirmed'
export type MergeReason = 'SameTelegramId' | 'SameTelegramUserName'

/** Error body: `{ message, errors }`; auth endpoints send `{ message }` only. */
export interface ApiErrorBody {
  message?: string
  errors?: string[]
}

export interface PagingResult<T> {
  countPerPage: number | null
  pageNumber: number
  totalCount: number
  lastPageNumber: number
  isFirstPage: boolean
  isLastPage: boolean
  moreThenFivePassed: boolean
  moreThenFiveLeft: boolean
  items: T[]
}

/** Query for paged endpoints; page numbers start at 0. */
export interface Paging {
  pageNumber?: number
  countPerPage?: number
}

export interface UserDisplay {
  id: string
  displayName: string
  telegramId: number | null
  telegramUserName: string | null
}

// Auth (api/v1/auth)

export interface SessionUser {
  id: string
  displayName: string
  telegramUserName: string | null
  isAdmin: boolean
}

export interface Session {
  user: SessionUser
  accessToken: string
  accessTokenExpiresAt: string
}

export interface AuthConfig {
  telegramBotUsername: string | null
}

export interface LoginRequest {
  login: string
  password: string
}

/** The Telegram Login Widget's callback data, posted as is to `POST auth/telegram`. */
export interface TelegramAuthData {
  id: number
  first_name?: string
  last_name?: string
  username?: string
  photo_url?: string
  auth_date: number
  hash: string
}

export interface PasswordChange {
  login?: string
  currentPassword?: string
  newPassword?: string
}

// Profile (api/v1/profile)

export interface Profile {
  id: string
  displayName: string
  phone: string | null
  email: string | null
  telegramId: number | null
  telegramUserName: string | null
  /** The user has started the bot. */
  telegramBotEnabled: boolean
  role: UserRole
  login: string | null
  hasPassword: boolean
}

export interface ProfileUpdate {
  displayName: string
  phone?: string | null
  email?: string | null
}
