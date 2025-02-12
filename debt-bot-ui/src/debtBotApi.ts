import { TelegramAuthData } from "@telegram-auth/react";
import axios, { AxiosResponse } from "axios";
import { Debt } from "./types/Debt";
import { PagingResult } from "./types/PagingResult";

// Define the base API URL
const API_BASE_URL = "https://debtbot.local"; // Replace with your actual API URL

// Create an Axios instance
const apiClient = axios.create({
    baseURL: API_BASE_URL,
    headers: {
        "Content-Type": "application/json",
        'Access-Control-Allow-Origin': 'https://debtbot.local'
    },
});

// Function to get the token from localStorage
const getAuthToken = () => localStorage.getItem("token");

// Attach an interceptor to add Authorization headers dynamically
apiClient.interceptors.request.use((config) => {
    const token = getAuthToken();
    if (token) {
        config.headers["Authorization"] = `Bearer ${token}`;
    }
    return config;
});

// Define API request functions
export const debtBotApi = {
    // Authenticate user with Telegram
    authenticateUser: async (telegramAuthData: TelegramAuthData) => {
        try {
            const data = {
                hash: telegramAuthData.hash,
                authDate: telegramAuthData.auth_date,
                telegramId: telegramAuthData.id,
            };
            const resp: AxiosResponse<string> = await apiClient.get(
                "/api/v1/Identity/token",
                { params: data }
            );

            if (resp.status === 200) {
                localStorage.setItem("token", resp.data); // Store the token
            }
        } catch (error) {
            console.error("Authentication failed:", error);
        }
    },

    // Fetch user debts
    getDebts: async (pageNumber = 0, countPerPage = 10) => {
        try {
            const response = await apiClient.get<PagingResult<Debt>>("/api/v1/Debts/Own", {
                params: { pageNumber, countPerPage },
            });
            return response.data;
        } catch (error) {
            console.error("Error fetching debts:", error);
            throw error;
        }
    },
};

export default debtBotApi;
