import { apiClient } from "../utils/apiClient";
import { Debt } from "../types/Debt";
import { PagingResult } from "../types/PagingResult";
import { handleApiError } from "../utils/errorHandler";

export const debtService = {
    getDebts: async (pageNumber = 0, countPerPage = 10) => {
        try {
            const response = await apiClient.get<PagingResult<Debt>>("/api/v1/Debts/Own", {
                params: { pageNumber, countPerPage },
            });
            return response.data;
        } catch (error) {
            throw handleApiError(error);
        }
    },
};

export default debtService;
