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
    Alert,
    Button,
} from "@mui/material";
import { useNavigate } from 'react-router';
import { Debt } from "../types/Debt";
import { PagingResult } from "../types/PagingResult";
import { useError } from "../hooks/useError";
import LoadingSpinner from "../components/LoadingSpinner";
import { debtService } from "../services/debtService";
import { TableColumnWidth } from '../utils/TableStyles';

const DebtsPage: React.FC = () => {
    const navigate = useNavigate();
    const [debts, setDebts] = useState<PagingResult<Debt> | null>(null);
    const [page, setPage] = useState(0);
    const [rowsPerPage, setRowsPerPage] = useState(10);
    const [selectedCreditor, setSelectedCreditor] = useState<string>("");
    const { error, handleError, clearError } = useError();
    const [isLoading, setIsLoading] = useState(true);

    useEffect(() => {
        const fetchDebts = async () => {
            try {
                setIsLoading(true);
                clearError();
                const response = await debtService.getDebts(page, rowsPerPage);
                setDebts(response || null);
                setSelectedCreditor(response?.items[0]?.creditorUser.displayName || "");
            } catch (error) {
                handleError(error);
            } finally {
                setIsLoading(false);
            }
        };

        fetchDebts();
    }, [page, rowsPerPage, handleError, clearError]);

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

    const viewBills = (debtorId: string, currencyCode: string) => {
        const params = new URLSearchParams({
            debtor: debtorId,
            currency: currencyCode
        });
        navigate(`/bills?${params.toString()}`);
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

    if (isLoading) {
        return <LoadingSpinner />;
    }

    return (
        <Container>
            {error && (
                <Alert severity="error" onClose={clearError} sx={{ mb: 2 }}>
                    {error}
                </Alert>
            )}
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
                    <Table sx={{ tableLayout: 'fixed' }}>
                        <TableHead>
                            <TableRow>
                                <TableCell sx={{ width: TableColumnWidth.lg }}>Debtor</TableCell>
                                <TableCell sx={{ width: TableColumnWidth.amount }}>Amount</TableCell>
                                <TableCell sx={{ width: TableColumnWidth.currency }}>Currency</TableCell>
                                <TableCell sx={{ width: TableColumnWidth.actions }}>Actions</TableCell>
                            </TableRow>
                        </TableHead>
                        <TableBody>
                            {filteredDebts.map((debt, index) => (
                                <TableRow key={index}
                                    sx={{ '&:hover': { bgcolor: 'rgba(0, 0, 0, 0.04)' } }}>
                                    <TableCell sx={{ 
                                        width: TableColumnWidth.lg,
                                        whiteSpace: 'nowrap',
                                        overflow: 'hidden',
                                        textOverflow: 'ellipsis'
                                    }}>
                                        {debt.debtorUser.displayName || "Unknown"}
                                    </TableCell>
                                    <TableCell sx={{ width: TableColumnWidth.amount }}>
                                        ${debt.amount.toFixed(2)}
                                    </TableCell>
                                    <TableCell sx={{ width: TableColumnWidth.currency }}>
                                        {debt.currencyCode || "N/A"}
                                    </TableCell>
                                    <TableCell sx={{ width: TableColumnWidth.actions }}>
                                        <Button
                                            type="primary"
                                            onClick={() => viewBills(debt.debtorUser.id, debt.currencyCode ?? '')}
                                        >
                                            View Bills
                                        </Button>
                                    </TableCell>
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
