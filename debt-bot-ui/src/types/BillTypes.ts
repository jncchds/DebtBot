import { UserDisplayModel } from './UserTypes';

export interface BillModel {
    id: string;
    creator: UserDisplayModel;
    currencyCode: string | null;
    paymentCurrencyCode: string | null;
    description: string | null;
    date: string;
    status: ProcessingState;
    totalWithTips: number;
    lines: BillLineModel[];
    payments: BillPaymentModel[];
    spendings: SpendingModel[];
}

export interface BillLineModel {
    id: string;
    billId: string;
    itemDescription: string | null;
    subtotal: number;
    participants: BillLineParticipantModel[];
}

export interface BillLineParticipantModel {
    billLineId: string;
    user: UserDisplayModel;
    part: number;
    userDisplayName: string | null;
}

export interface BillPaymentModel {
    user: UserDisplayModel;
    amount: number;
}

export interface SpendingModel {
    description: string | null;
    amount: number;
    currencyCode: string | null;
    paymentAmount: number;
    paymentCurrencyCode: string | null;
    portion: number;
    billId: string;
    user: UserDisplayModel;
}

export enum ProcessingState {
    Draft = 0,
    Active = 1,
    Cancelled = 3,
    Finalized = 4
}
