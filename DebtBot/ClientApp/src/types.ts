export interface UserDisplay {
  id: string
  displayName: string
  telegramId?: number
  telegramUserName?: string
}

export interface PagingResult<T> {
  items: T[]
  pageNumber: number
  countPerPage: number
  totalCount: number
}

export interface BillPayment {
  user: UserDisplay
  amount: number
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

export interface SpendingModel {
  description: string
  amount: number
  currencyCode: string
  paymentAmount: number
  paymentCurrencyCode: string
  portion: number
  billId: string
  user: UserDisplay
}

export interface LedgerRecord {
  creditorUser: UserDisplay
  debtorUser: UserDisplay
  amount: number
  currencyCode: string
}

export interface BillModel {
  id: string
  creator: UserDisplay
  currencyCode: string
  paymentCurrencyCode: string
  description: string
  date: string
  status: 'Draft' | 'Ready' | 'Processed' | 'Cancelled'
  totalWithTips: number
  lines: BillLine[]
  payments: BillPayment[]
  spendings: SpendingModel[]
}

export interface BillListModel {
  id: string
  currencyCode: string
  paymentCurrencyCode: string
  description: string
  date: string
  status: 'Draft' | 'Ready' | 'Processed' | 'Cancelled'
  totalWithTips: number
  ledgerRecords: LedgerRecord[]
}

export interface DebtModel {
  creditorUser: UserDisplay
  debtorUser: UserDisplay
  amount: number
  currencyCode: string
}

export interface BillLineParticipantCreation {
  userId: string
  part: number
}

export interface BillLineCreation {
  itemDescription: string
  subtotal: number
  participants: BillLineParticipantCreation[]
}

export interface BillPaymentCreation {
  userId: string
  amount: number
}

export interface BillCreation {
  currencyCode: string
  paymentCurrencyCode: string
  description: string
  date: string
  status: number
  totalWithTips: number
  lines: BillLineCreation[]
  payments: BillPaymentCreation[]
}

export interface ExchangeCreation {
  fromUserId: string
  fromCurrencyCode: string
  fromAmount: number
  toUserId: string
  toCurrencyCode: string
  toAmount: number
  description: string
}

export interface ExchangeResult {
  forwardBillId: string
  backwardBillId: string
}
