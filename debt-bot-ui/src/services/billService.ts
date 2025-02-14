import { apiClient } from "../utils/apiClient";
import { BillModel } from "../types/BillTypes";
import { PagingResult } from "../types/PagingResult";
import { handleApiError } from "../utils/errorHandler";

export interface BillsFilter {
    pageNumber?: number;
    countPerPage?: number;
    debtorId?: string;
    currencyCode?: string;
}

export const billService = {
    getOwnBills: async (filter: BillsFilter) => {
        try {
            const response = await apiClient.get<PagingResult<BillModel>>("/api/v1/Bills", {
                params: filter,
            });
            return response.data;
        } catch (error) {
            throw handleApiError(error);
        }
    },

    getBill: async (id: string): Promise<BillModel> => {
        try {
            const response = await apiClient.get<BillModel>(`/api/v1/Bills/${id}`);
            return response.data;
        } catch (error) {
            throw handleApiError(error);
        }
    },
};
