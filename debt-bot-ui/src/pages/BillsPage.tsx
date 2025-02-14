import React, { useEffect, useState } from 'react';
import { useSearchParams, useNavigate } from 'react-router';
import { BillModel } from '../types/BillTypes';
import { billService, BillsFilter } from '../services/billService';
import {
    Paper,
    Button,
    Typography,
    TextField,
    MenuItem,
    Table,
    TableBody,
    TableCell,
    TableContainer,
    TableHead,
    TableRow,
    TablePagination,
    Stack,
} from '@mui/material';
import { ProcessingState, getStatusLabel, getStatusColor } from '../types/ProcessingState';
import { TableColumnWidth } from '../utils/TableStyles';

const DEFAULT_PAGE_SIZE = 10;

export const BillsPage: React.FC = () => {
    const navigate = useNavigate();
    const [searchParams, setSearchParams] = useSearchParams();
    const [bills, setBills] = useState<BillModel[]>([]);
    const [totalCount, setTotalCount] = useState(0);
    const [loading, setLoading] = useState(false);

    // Get filter from URL parameters or use defaults
    const getFilterFromUrl = (): BillsFilter => ({
        pageNumber: parseInt(searchParams.get('page') ?? '0'),
        countPerPage: parseInt(searchParams.get('pageSize') ?? DEFAULT_PAGE_SIZE.toString()),
        currencyCode: searchParams.get('currency') ?? undefined,
        debtorId: searchParams.get('debtor') ?? undefined,
    });

    // Update URL with current filter state
    const updateUrlParams = (newFilter: BillsFilter) => {
        const params = new URLSearchParams();
        if (newFilter.pageNumber) params.set('page', newFilter.pageNumber.toString());
        if (newFilter.countPerPage) params.set('pageSize', newFilter.countPerPage.toString());
        if (newFilter.currencyCode) params.set('currency', newFilter.currencyCode);
        if (newFilter.debtorId) params.set('debtor', newFilter.debtorId);
        setSearchParams(params);
    };

    const fetchBills = async (filter: BillsFilter) => {
        setLoading(true);
        try {
            const data = await billService.getOwnBills(filter);
            setBills(data.items);
            setTotalCount(data.totalCount);
        } catch (error) {
            console.error('Failed to fetch bills:', error);
        } finally {
            setLoading(false);
        }
    };

    const viewBill = (billId: string) => {
        navigate(`/bills/${billId}`);
    };

    useEffect(() => {
        const filter = getFilterFromUrl();
        fetchBills(filter);
    }, [searchParams]); // Re-fetch when URL parameters change

    const handleFilterChange = (changes: Partial<BillsFilter>) => {
        const currentFilter = getFilterFromUrl();
        const newFilter = { ...currentFilter, ...changes };
        updateUrlParams(newFilter);
    };

    const getCaption = () => {
        const filter = getFilterFromUrl();
        const parts = [];
        
        if (filter.debtorId) parts.push(`to ${filter.debtorId}`);
        if (filter.currencyCode) parts.push(`in ${filter.currencyCode}`);
        
        return parts.length > 0 ? `My bills ${parts.join(' ')}` : 'My Bills';
    };

    const getColumns = () => {
        const baseColumns = [
            {
                id: 'date',
                label: 'Date',
                width: TableColumnWidth.date,
                format: (date: string) => new Date(date).toLocaleDateString()
            },
            {
                id: 'description',
                label: 'Description',
                width: TableColumnWidth.xl,
            },
            {
                id: 'totalWithTips',
                label: 'Total',
                width: TableColumnWidth.amount,
                format: (total: number, record: BillModel) => `${total} ${record.currencyCode}`
            },
            {
                id: 'status',
                label: 'Status',
                width: TableColumnWidth.status,
                format: (status: ProcessingState) => (
                    <Typography
                        component="span"
                        sx={{
                            color: 'white',
                            bgcolor: getStatusColor(status),
                            px: 1,
                            py: 0.5,
                            borderRadius: 1,
                            display: 'inline-block',
                        }}
                    >
                        {getStatusLabel(status)}
                    </Typography>
                ),
            },
        ];

        return baseColumns;
    };

    // Replace the static columns with getColumns()
    const columns = getColumns();

    return (
        <Paper sx={{ p: 3 }}>
            <Stack spacing={3}>
                <Typography variant="h4">{getCaption()}</Typography>

                <TableContainer>
                    <Table sx={{ tableLayout: 'fixed' }}>
                        <TableHead>
                            <TableRow>
                                {columns.map((column) => (
                                    <TableCell 
                                        key={column.id} 
                                        sx={{ 
                                            width: column.width,
                                            whiteSpace: 'nowrap',
                                            overflow: 'hidden',
                                            textOverflow: 'ellipsis'
                                        }}
                                    >
                                        {column.label}
                                    </TableCell>
                                ))}
                                <TableCell sx={{ width: TableColumnWidth.actions }}>Actions</TableCell>
                            </TableRow>
                        </TableHead>
                        <TableBody>
                            {bills.map((bill) => (
                                <TableRow 
                                    key={bill.id}
                                    sx={{ '&:hover': { bgcolor: 'rgba(0, 0, 0, 0.04)' } }}
                                >
                                    {columns.map((column) => (
                                        <TableCell 
                                            key={column.id}
                                            sx={{ 
                                                width: column.width,
                                                whiteSpace: 'nowrap',
                                                overflow: 'hidden',
                                                textOverflow: 'ellipsis'
                                            }}
                                        >
                                            {column.format
                                                ? column.format(bill[column.id as keyof BillModel], bill)
                                                : column.getValue
                                                    ? column.getValue(bill)
                                                    : bill[column.id as keyof BillModel]}
                                        </TableCell>
                                    ))}
                                    <TableCell sx={{ width: TableColumnWidth.actions }}>
                                        <Button
                                            type="primary"
                                            onClick={() => viewBill(bill.id)}
                                        >
                                            View Bill
                                        </Button>
                                    </TableCell>
                                </TableRow>
                            ))}
                        </TableBody>
                    </Table>
                </TableContainer>

                <TablePagination
                    component="div"
                    count={totalCount}
                    page={getFilterFromUrl().pageNumber!}
                    rowsPerPage={getFilterFromUrl().countPerPage!}
                    onPageChange={(_, page) => handleFilterChange({ pageNumber: page })}
                    onRowsPerPageChange={(e) => handleFilterChange({
                        pageNumber: 0,
                        countPerPage: parseInt(e.target.value)
                    })}
                    rowsPerPageOptions={[5, 10, 25]}
                />
            </Stack>
        </Paper>
    );
};

export default BillsPage;