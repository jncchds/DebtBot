1) Get all the debts of user to one user:
	- CreditorId (one user)
	- DebtorId (another)
	- List of:
		- Amount
		- Currency
2) Get all the debts of all users to one user:
	- List of:
		- CreditorId (one user)
		- DebtorId (another)
		- List of:
			- Amount
			- Currency
3) Get all the debt entries to one user
4) Store information about a transaction:
	- Currency
	- Description
	- List of:
		- Person Id
		- Amount they've paid
	- List of:
		- Person Id
		- Fraction over absolute amount they've consumed
5) Store information about a bill as transaction:
	- Currency
	- Description
	- List of:
		- Person Id
		- Amount they've paid
	- List of:
		- Item description
		- Item total
		- LIst of:
			- Person Id
			- Fraction of the item they've consumed
6) Editing should be similar to 4-5 but with an old transaction id
7) We will do circular debts, we swear
8) Source management (view/add/edit/setup)
9) Store information about a currency conversion transaction
	- Creditor
	- Debtor
	- First currency
	- Amount in first currency that creditor gives to debtor
	- Second currency
	- Amount in second currency that debtor gives to creditor