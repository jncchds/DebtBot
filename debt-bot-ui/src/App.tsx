import React from 'react';
import { AppBar, Toolbar, Typography, Button, Container, CssBaseline } from '@mui/material';
import { BrowserRouter as Router, Routes, Route, Link } from "react-router";
import { ThemeProvider, createTheme } from '@mui/material/styles';
import { LoginButton, TelegramAuthData } from '@telegram-auth/react';
import HomePage from "./pages/HomePage"; // Import the home page
import DebtsPage from "./pages/DebtsPage"; // Import the debts page
import debtBotApi from "./debtBotApi"; // Import the API client

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
    debtBotApi.authenticateUser(data);
};

const App: React.FC = () => {
    return (
        <ThemeProvider theme={theme}>
            <CssBaseline />
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
                    <Button color="inherit" component={Link} onClick={debtBotApi.cheatAuth} to="#">
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
                    </Routes>
            </Router>
        </ThemeProvider>
    );
};

export default App;
