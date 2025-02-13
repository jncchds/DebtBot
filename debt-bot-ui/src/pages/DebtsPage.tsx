import React, { useState, useEffect } from "react";
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
    TablePagination,
} from "@mui/material";
import debtBotApi from "../debtBotApi";
import { Debt } from "../types/Debt";
import { PagingResult } from "../types/PagingResult"

const DebtsPage: React.FC = () => {
    const [debts, setDebts] = useState<PagingResult<Debt> | null>(null);
    const [page, setPage] = useState(0);
    const [rowsPerPage, setRowsPerPage] = useState(10);

    useEffect(() => {
        const fetchDebts = async () => {
            try {
                const response = await debtBotApi.getDebts(page, rowsPerPage);
                setDebts(response || null);
            } catch (error) {
                console.error("Error fetching debts:", error);
            }
        };

        fetchDebts();
    }, [page, rowsPerPage]);

    const handleChangePage = (event: unknown, newPage: number) => {
        setPage(newPage);
    };

    const handleChangeRowsPerPage = (event: React.ChangeEvent<HTMLInputElement>) => {
        setRowsPerPage(parseInt(event.target.value, 10));
        setPage(0);
    };

    return (
        <Container>
            <Typography variant="h4" gutterBottom>
                Your Debts
            </Typography>

            {((debts == null) || (!debts.items)) ? (
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
                    <TablePagination
                        rowsPerPageOptions={[5, 10, 25]}
                        component="div"
                        count={debts.totalCount}
                        rowsPerPage={rowsPerPage}
                        page={page}
                        onPageChange={handleChangePage}
                        onRowsPerPageChange={handleChangeRowsPerPage}
                    />
                </TableContainer>
            )}
        </Container>
    );
};

export default DebtsPage;
