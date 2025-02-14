import axios from "axios";

export const handleApiError = (error: unknown) => {
    if (axios.isAxiosError(error)) {
        if (error.response?.status === 401) {
            localStorage.removeItem('token');
            throw new Error('Authentication failed. Please log in again.');
        }
        throw new Error(error.response?.data?.message || 'An error occurred while fetching data');
    }
    throw error;
};
