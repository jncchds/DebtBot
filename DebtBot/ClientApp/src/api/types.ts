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

// Lists: every paged list takes `sortBy` and `sortOrder` next to its filters (mockups/shell/README.md, "List views").

export type SortOrder = 'Asc' | 'Desc'
export type BillSortField = 'Date' | 'Description' | 'Total' | 'Status'
export type DebtSortField = 'Person' | 'Amount' | 'Currency'
export type DebtDirection = 'All' | 'OwedToMe' | 'IOwe'
export type ExchangeSortField = 'Date' | 'Amount'

// Bills (api/v1/Bills)

export interface LedgerRecord {
  creditorUser: UserDisplay
  debtorUser: UserDisplay
  amount: number
  currencyCode: string
}

export interface BillLineParticipant {
  billLineId: string
  user: UserDisplay
  part: number
  userDisplayName: string
}

export interface BillLine {
  id: string
  billId: string
  itemDescription: string
  subtotal: number
  participants: BillLineParticipant[]
}

export interface BillPayment {
  user: UserDisplay
  amount: number
}

export interface Spending {
  description: string
  amount: number
  currencyCode: string
  paymentAmount: number
  paymentCurrencyCode: string
  /** 0..1 share of the bill. */
  portion: number
  billId: string
  date: string
  isCanceled: boolean
  user: UserDisplay
}

/** GET Bills/{id}, and the bill returned by lines/payments. */
export interface Bill {
  id: string
  creator: UserDisplay
  currencyCode: string
  paymentCurrencyCode: string
  description: string
  date: string
  status: ProcessingState
  totalWithTips: number
  /** Set on both bills of an exchange: the id of the other bill. */
  exchangeBillId: string | null
  lines: BillLine[]
  payments: BillPayment[]
  spendings: Spending[]
  ledgerRecords: LedgerRecord[]
}

/** GET Bills items. */
export interface BillListItem {
  id: string
  currencyCode: string
  paymentCurrencyCode: string
  description: string
  date: string
  status: ProcessingState
  totalWithTips: number
  creator: UserDisplay
  exchangeBillId: string | null
  ledgerRecords: LedgerRecord[]
}

/** GET Bills query. Dates are `yyyy-mm-dd` (whole days, UTC). */
export interface BillListQuery extends Paging {
  withUserId?: string
  currencyCode?: string
  status?: ProcessingState
  createdByMe?: boolean
  dateFrom?: string
  dateTo?: string
  search?: string
  sortBy?: BillSortField
  sortOrder?: SortOrder
}

export interface BillLineCreation {
  itemDescription: string
  /** > 0, bill currency, without tips. */
  subtotal: number
  /** Each user once; leave `part` out for an equal split (counts as 1). */
  participants: { userId: string; part?: number | null }[]
}

/** amount > 0, payment currency; each user once. */
export interface BillPaymentCreation {
  userId: string
  amount: number
}

/** POST Bills: always a draft. */
export interface BillCreation {
  /** 3 letters, stored upper case. */
  currencyCode: string
  /** Defaults to currencyCode. */
  paymentCurrencyCode?: string
  description?: string
  /** ISO; defaults to now. */
  date?: string
  /** > 0; defaults to the sum of line subtotals. */
  totalWithTips?: number
  lines?: BillLineCreation[]
  payments?: BillPaymentCreation[]
}

/** A person in the bot's text format, as typed (`@name`, a contact's name, a phone or an email). */
export interface UserSearch {
  id: string | null
  displayName: string | null
  telegramId: number | null
  telegramUserName: string | null
  phone: string | null
  email: string | null
  queryString: string | null
}

/** POST Bills/text/preview `result`: the bill as parsed; parts the parser didn't reach are null. */
export interface ParsedBill {
  currencyCode: string | null
  paymentCurrencyCode: string | null
  description: string | null
  date: string | null
  totalWithTips: number | null
  lines: { itemDescription: string | null; subtotal: number | null; participants: { part: number | null; user: UserSearch }[] }[] | null
  payments: { amount: number | null; user: UserSearch }[] | null
}

/** POST Bills/text/preview: always 200. Errors may hold `<pre>…</pre>` around the text they quote. */
export interface TextPreview {
  result: ParsedBill | null
  errors: string[]
  isValid: boolean
}

export interface Exchange {
  forwardBillId: string
  backwardBillId: string
}

/** POST Bills/exchange: "from" hands over fromAmount, "to" hands back toAmount. */
export interface ExchangeCreation {
  fromUserId: string
  fromAmount: number
  fromCurrencyCode: string
  toUserId: string
  toAmount: number
  toCurrencyCode: string
  /** Defaults to "Exchange". */
  description?: string
  date?: string
}

/** GET Bills/exchanges items: one per pair. When you're one of the two people, "from" is you. */
export interface ExchangeListItem {
  /** The bill "from" pays in. Send both ids to exchange/finalize to accept. */
  forwardBillId: string
  backwardBillId: string
  date: string
  status: ProcessingState
  description: string
  creator: UserDisplay
  fromUser: UserDisplay | null
  fromAmount: number
  fromCurrencyCode: string
  toUser: UserDisplay | null
  toAmount: number
  toCurrencyCode: string
  /** fromAmount / toAmount. */
  rate: number
}

/** GET Bills/exchanges query. `status`: Draft = waiting to accept, Processed = accepted (includes Ready), Cancelled. */
export interface ExchangeListQuery extends Paging {
  withUserId?: string
  currencyCode?: string
  status?: ProcessingState
  sortBy?: ExchangeSortField
  sortOrder?: SortOrder
}

// Debts (api/v1/Debts)

/** From the creditor's side: amount > 0 means debtorUser owes creditorUser; < 0 means creditorUser owes debtorUser. */
export interface Debt {
  creditorUser: UserDisplay
  debtorUser: UserDisplay
  amount: number
  currencyCode: string
}

/** GET Debts/Own query. */
export interface DebtListQuery extends Paging {
  withUserId?: string
  currencyCode?: string
  direction?: DebtDirection
  sortBy?: DebtSortField
  sortOrder?: SortOrder
}

// Contacts (api/v1/UserContacts)

export interface Contact {
  /** The contact's user id (use it as userId in bills). */
  id: string
  /** Your name for them. */
  displayName: string
  /** Their own display name. */
  userDisplayName: string
  telegramId: number | null
  telegramUserName: string | null
  /** False: a placeholder who never started the bot. */
  telegramBotEnabled: boolean
  mySubscription: SubscriptionState
  theirSubscription: SubscriptionState
}
