import axios from "axios";

const API_BASE_URL = "https://debtbot.local";

export const apiClient = axios.create({
    baseURL: API_BASE_URL,
    headers: {
        "Content-Type": "application/json",
        'Access-Control-Allow-Origin': 'https://debtbot.local'
    },
});

apiClient.interceptors.request.use((config) => {
    const token = localStorage.getItem("token");
    if (token) {
        config.headers["Authorization"] = `Bearer ${token}`;
    }
    return config;
});

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
