# Event
- Id
- Date
- Event parameter
- Event type

# People

## User
- Id
- Display Name
- TelegramId
- Phone
- Email
- ActorId

## UserSubordinate
- UserId
- SubordinateUserId
- DisplayName

# Money records
## Ledger
- CreditorUserId
- DebtorUserId
- Amount
- Currency
- BillId

## BillLine
 - Id
 - BillId
 - Item Description
 - Subtotal

## BillLineParticipant
- BillLineId
- UserId
- Parts

## BillPayment
 - BillId
 - UserId
 - Amount
## Bill
- Id
- Currency
- Description
- Date

# View tables
## Debt
- CreditorUserId
- DebtorUserId
- Amount
- Currency

## Currency
- CurrencyCode
- FullName
- Character
