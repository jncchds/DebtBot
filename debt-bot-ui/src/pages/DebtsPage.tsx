import React, { useEffect, useState } from "react";
import {
    Container,
    Typography,
    Table,
    TableBody,
    TableCell,
    TableHead,
    TableRow,
    CircularProgress,
    TableContainer,
    Paper,
} from "@mui/material";
import debtBotApi from "../debtBotApi";
import { Debt } from "../types/Debt";
import { PagingResult } from "../types/PagingResult"

const DebtsPage: React.FC = () => {
    const [debts, setDebts] = useState<PagingResult<Debt> | null>(null);
    const [loading, setLoading] = useState<boolean>(true);

    useEffect(() => {
        const fetchDebts = async () => {
            try {
                const response = await debtBotApi.getDebts(0, 10);
                setDebts(response || null);
            } catch (error) {
                console.error("Error fetching debts:", error);
            } finally {
                setLoading(false);
            }
        };

        fetchDebts();
    }, []);

    return (
        <Container>
            <Typography variant="h4" gutterBottom>
                Your Debts
            </Typography>

            {loading || debts == null || !debts.items ? (
                <CircularProgress />
            ) : (
                <TableContainer component={Paper}>
                    <Table>
                        <TableHead>
                            <TableRow>
                                <TableCell>Creditor</TableCell>
                                <TableCell>Debtor</TableCell>
                                <TableCell>Amount</TableCell>
                                <TableCell>Currency</TableCell>
                            </TableRow>
                        </TableHead>
                        <TableBody>
                            {debts.items.map((debt, index) => (
                                <TableRow key={index}>
                                    <TableCell>{debt.creditorUser.displayName || "Unknown"}</TableCell>
                                    <TableCell>{debt.debtorUser.displayName || "Unknown"}</TableCell>
                                    <TableCell>${debt.amount.toFixed(2)}</TableCell>
                                    <TableCell>{debt.currencyCode || "N/A"}</TableCell>
                                </TableRow>
                            ))}
                        </TableBody>
                    </Table>
                </TableContainer>
            )}
        </Container>
    );
};

export default DebtsPage;
