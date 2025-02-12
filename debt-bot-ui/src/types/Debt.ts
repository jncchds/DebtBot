export interface UserDisplayModel {
    id: string;
    displayName?: string;
    telegramId?: number;
    telegramUserName?: string;
}

export interface Debt {
    creditorUser: UserDisplayModel;
    debtorUser: UserDisplayModel;
    amount: number;
    currencyCode?: string;
}