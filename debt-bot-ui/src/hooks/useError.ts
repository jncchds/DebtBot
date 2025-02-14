import { useState, useCallback } from 'react';

export const useError = () => {
    const [error, setError] = useState<string | null>(null);

    const handleError = useCallback((error: unknown) => {
        if (error instanceof Error) {
            setError(error.message);
        } else if (typeof error === 'string') {
            setError(error);
        } else {
            setError('An unexpected error occurred');
        }
    }, []);

    const clearError = useCallback(() => {
        setError(null);
    }, []);

    return { error, handleError, clearError };
};
