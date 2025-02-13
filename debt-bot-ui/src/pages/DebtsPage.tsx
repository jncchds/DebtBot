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
    MenuItem,
    Select,
    FormControl,
    InputLabel,
} from "@mui/material";
import debtBotApi from "../debtBotApi";
import { Debt } from "../types/Debt";
import { PagingResult } from "../types/PagingResult";

const DebtsPage: React.FC = () => {
    const [debts, setDebts] = useState<PagingResult<Debt> | null>(null);
    const [page, setPage] = useState(0);
    const [rowsPerPage, setRowsPerPage] = useState(10);
    const [selectedCreditor, setSelectedCreditor] = useState<string>("");

    useEffect(() => {
        const fetchDebts = async () => {
            try {
                const response = await debtBotApi.getDebts(page, rowsPerPage);
                setDebts(response || null);
                setSelectedCreditor(response?.items[0]?.creditorUser.displayName || "");
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

    const handleCreditorChange = (event: React.ChangeEvent<{ value: unknown }>) => {
        setSelectedCreditor(event.target.value as string);
        setPage(0);
    };

    const groupedDebts = debts?.items.reduce((acc, debt) => {
        const creditor = debt.creditorUser.displayName || "Unknown";
        if (!acc[creditor]) {
            acc[creditor] = [];
        }
        acc[creditor].push(debt);
        return acc;
    }, {} as Record<string, Debt[]>);

    const creditors = Object.keys(groupedDebts || {});

    const filteredDebts = selectedCreditor
        ? groupedDebts?.[selectedCreditor] || []
        : debts?.items || [];

    return (
        <Container>
            <Typography variant="h4" gutterBottom>
                Debts to {selectedCreditor || "N/A" }
            </Typography>

            {((creditors?.length ?? 0) > 1) ?
                <FormControl fullWidth margin="normal">
                    <InputLabel id="creditor-select-label">Select Creditor</InputLabel>
                    <Select
                        labelId="creditor-select-label"
                        value={selectedCreditor}
                        onChange={handleCreditorChange}
                    >
                        {/*<MenuItem value="">*/}
                        {/*    <em>All</em>*/}
                        {/*</MenuItem>*/}
                        {creditors.map((creditor) => (
                            <MenuItem key={creditor} value={creditor}>
                                {creditor}
                            </MenuItem>
                        ))}
                    </Select>
                </FormControl> : <></>
            }

            {((debts == null) || (!debts.items)) ? (
                <CircularProgress />
            ) : (
                <TableContainer component={Paper}>
                    <Table>
                        <TableHead>
                            <TableRow>
                                <TableCell>Debtor</TableCell>
                                <TableCell>Amount</TableCell>
                                <TableCell>Currency</TableCell>
                            </TableRow>
                        </TableHead>
                        <TableBody>
                            {filteredDebts.map((debt, index) => (
                                <TableRow key={index}>
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
                        count={filteredDebts.length}
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
