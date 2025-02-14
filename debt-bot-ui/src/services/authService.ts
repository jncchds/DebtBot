import { TelegramAuthData } from "@telegram-auth/react";
import { apiClient } from "../utils/apiClient";
import { AxiosResponse } from "axios";

export const authService = {
    cheatAuth: async () => {
        const resp: AxiosResponse<string> = await apiClient.get("/api/v1/Identity");
        if (resp.status === 200) {
            localStorage.setItem("token", resp.data);
        }
    },

    authenticateUser: async (telegramAuthData: TelegramAuthData) => {
        try {
            const resp: AxiosResponse<string> = await apiClient.post(
                "/api/v1/Identity/Telegram",
                telegramAuthData
            );

            if (resp.status === 200) {
                localStorage.setItem("token", resp.data);
            }
        } catch (error) {
            console.error("Authentication failed:", error);
        }
    },
};

export default authService;
