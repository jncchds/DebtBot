import React, { Component, ErrorInfo, ReactNode } from 'react';
import { Alert, Box, Button } from '@mui/material';

interface Props {
    children: ReactNode;
}

interface State {
    hasError: boolean;
    error?: Error;
}

class ErrorBoundary extends Component<Props, State> {
    public state: State = {
        hasError: false
    };

    public static getDerivedStateFromError(error: Error): State {
        return { hasError: true, error };
    }

    public componentDidCatch(error: Error, errorInfo: ErrorInfo) {
        console.error('Uncaught error:', error, errorInfo);
    }

    public render() {
        if (this.state.hasError) {
            return (
                <Box sx={{ m: 2 }}>
                    <Alert severity="error">
                        Something went wrong. Please try again later.
                        <Button 
                            color="inherit" 
                            size="small"
                            onClick={() => this.setState({ hasError: false })}
                        >
                            Try again
                        </Button>
                    </Alert>
                </Box>
            );
        }

        return this.props.children;
    }
}

export default ErrorBoundary;
