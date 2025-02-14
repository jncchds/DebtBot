import React from 'react';
import { AppBar, Toolbar, Typography, Button, Container, CssBaseline } from '@mui/material';
import { BrowserRouter as Router, Routes, Route, Link } from "react-router";
import { ThemeProvider, createTheme } from '@mui/material/styles';
import { LoginButton, TelegramAuthData } from '@telegram-auth/react';
import HomePage from "./pages/HomePage"; // Import the home page
import DebtsPage from "./pages/DebtsPage"; // Import the debts page
import BillsPage from './pages/BillsPage'; // Import the bills page
import BillDetailsPage from './pages/BillDetailsPage'; // Import the bill details page
import { authService } from "./services/authService"; // Import the auth service
import ErrorBoundary from './components/ErrorBoundary'; // Import the ErrorBoundary component

const theme = createTheme({
    palette: {
        primary: {
            main: '#1976d2',
        },
        secondary: {
            main: '#dc004e',
        },
    },
});

const authCallback = (data: TelegramAuthData) => {
    authService.authenticateUser(data);
};

const App: React.FC = () => {
    return (
        <ThemeProvider theme={theme}>
            <CssBaseline />
            <ErrorBoundary>
                <Router>
                    <AppBar position="static">
                        <Toolbar>
                            <Typography variant="h6" component="div" sx={{ flexGrow: 1 }}>
                                My App
                            </Typography>
                            <Button color="inherit" component={Link} to="/">
                                Home
                            </Button>
                            <Button color="inherit" component={Link} to="/debts">
                                Debts
                            </Button>
                            <Button color="inherit" component={Link} to="/bills">
                                Bills
                            </Button>
                            <Button color="inherit" component={Link} onClick={authService.cheatAuth} to="#">
                                Cheat
                            </Button>
                            <LoginButton
                                botUsername="CHDSTestBot"
                                onAuthCallback={authCallback}
                                buttonSize="large" // "large" | "medium" | "small"
                                cornerRadius={5} // 0 - 20
                                showAvatar={true} // true | false
                                lang="en"
                            />
                        </Toolbar>
                    </AppBar>
                    <Routes>
                        <Route path="/" element={<HomePage />} />
                        <Route path="/debts" element={<DebtsPage />} />
                        <Route path="/bills" element={<BillsPage />} />
                        <Route path="/bills/:id" element={<BillDetailsPage />} />
                    </Routes>
                </Router>
            </ErrorBoundary>
        </ThemeProvider>
    );
};

export default App;
