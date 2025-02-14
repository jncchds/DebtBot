import React, { useEffect, useState } from 'react';
import { useParams } from 'react-router';
import {
    Paper,
    Typography,
    Stack,
    Divider,
    Chip,
    Table,
    TableBody,
    TableCell,
    TableContainer,
    TableHead,
    TableRow,
    CircularProgress,
    Alert,
    Box
} from '@mui/material';
import { BillModel, BillLineModel } from '../types/BillTypes';
import { billService } from '../services/billService';
import { getStatusColor, getStatusLabel } from '../types/ProcessingState';

export const BillDetailsPage: React.FC = () => {
    const { id } = useParams<{ id: string }>();
    const [bill, setBill] = useState<BillModel | null>(null);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);

    useEffect(() => {
        const fetchBill = async () => {
            try {
                const data = await billService.getBill(id!);
                setBill(data);
            } catch (err) {
                setError('Failed to load bill details');
            } finally {
                setLoading(false);
            }
        };

        fetchBill();
    }, [id]);

    const renderHeaderRow = (label: string, value: React.ReactNode) => (
        <Box sx={{
            display: 'flex',
            py: 1,
            borderBottom: '1px solid',
            borderColor: 'grey.200',
            alignItems: 'center'
        }}>
            <Box sx={{ width: '200px' }}>
                <Typography variant="subtitle2" component="div">{label}</Typography>
            </Box>
            <Box component="div">
                {typeof value === 'string' ? (
                    <Typography component="div">{value}</Typography>
                ) : (
                    value
                )}
            </Box>
        </Box>
    );

    const calculateExchangeRate = (bill: BillModel) => {
        if (!bill.paymentCurrencyCode || !bill.currencyCode || bill.paymentCurrencyCode === bill.currencyCode) {
            return null;
        }
        const paymentsTotal = bill.payments.reduce((sum, p) => sum + p.amount, 0);
        const rate = paymentsTotal / bill.totalWithTips;
        return `1 ${bill.currencyCode} = ${rate.toFixed(4)} ${bill.paymentCurrencyCode}`;
    };

    if (loading) return <CircularProgress />;
    if (error) return <Alert severity="error">{error}</Alert>;
    if (!bill) return <Alert severity="info">Bill not found</Alert>;

    return (
        <Paper sx={{ p: 3 }}>
            <Stack spacing={3}>
                {/* Header */}
                <Box sx={{
                    bgcolor: 'grey.100',
                    p: 2,
                    borderRadius: 1
                }}>
                    <Typography variant="h4" component="div" sx={{ mb: 2 }}>Bill Details</Typography>
                    {renderHeaderRow('Status',
                        <Chip
                            label={getStatusLabel(bill.status)}
                            sx={{
                                bgcolor: getStatusColor(bill.status),
                                color: 'white'
                            }}
                        />
                    )}
                    {renderHeaderRow('Created by', bill.creator.displayName)}
                    {renderHeaderRow('Date', new Date(bill.date).toLocaleDateString())}
                    {renderHeaderRow('Description', bill.description || '-')}
                    {renderHeaderRow('Bill Amount', `${bill.totalWithTips} ${bill.currencyCode}`)}
                    {bill.paymentCurrencyCode !== bill.currencyCode && (
                        <>
                            {renderHeaderRow('Payments Total',
                                `${bill.payments.reduce((sum, p) => sum + p.amount, 0)} ${bill.paymentCurrencyCode}`
                            )}
                            {renderHeaderRow('Exchange Rate', calculateExchangeRate(bill))}
                        </>
                    )}

                    <Divider />

                    {/* Payments */}
                    <Typography variant="h6">Payments</Typography>
                    <TableContainer>
                        <Table size="small">
                            <TableHead>
                                <TableRow>
                                    <TableCell>Paid By</TableCell>
                                    <TableCell align="right">Amount</TableCell>
                                </TableRow>
                            </TableHead>
                            <TableBody>
                                {bill.payments.map((payment) => (
                                    <TableRow key={payment.user.id}>
                                        <TableCell>{payment.user.displayName}</TableCell>
                                        <TableCell align="right">
                                            {payment.amount} {bill.paymentCurrencyCode}
                                        </TableCell>
                                    </TableRow>
                                ))}
                                <TableRow>
                                    <TableCell sx={{ fontWeight: 'bold' }}>Total Paid</TableCell>
                                    <TableCell align="right" sx={{ fontWeight: 'bold' }}>
                                        {bill.payments.reduce((sum, p) => sum + p.amount, 0)} {bill.paymentCurrencyCode}
                                    </TableCell>
                                </TableRow>
                            </TableBody>
                        </Table>
                    </TableContainer>
                </Box>

                {/* Bill Lines */}
                <Typography variant="h6">Bill Items</Typography>
                <BillLinesList lines={bill.lines} />
            </Stack>
        </Paper>
    );
};

interface BillLinesListProps {
    lines: BillLineModel[];
}

const BillLinesList: React.FC<BillLinesListProps> = ({ lines }) => {
    return (
        <TableContainer>
            <Table>
                <TableHead>
                    <TableRow>
                        <TableCell>Description</TableCell>
                        <TableCell align="right">Subtotal</TableCell>
                        <TableCell>Participants</TableCell>
                    </TableRow>
                </TableHead>
                <TableBody>
                    {lines.map((line) => {
                        const ratioSum = line.participants.reduce((sum, p) => sum + p.part, 0);
                        return (
                            <TableRow key={line.id}>
                                <TableCell>{line.itemDescription}</TableCell>
                                <TableCell align="right">{line.subtotal}</TableCell>
                                <TableCell>
                                    <Box sx={{ 
                                        display: 'flex', 
                                        flexWrap: 'wrap', 
                                        gap: 1,
                                        '& > *': { my: 0.5 }  // Add vertical margin to chips
                                    }}>
                                        {line.participants.map((participant) => (
                                            <Chip
                                                key={`${line.id}-${participant.user.id}`}
                                                label={`${participant.userDisplayName || participant.user.displayName} (${participant.part}/${ratioSum})`}
                                                size="small"
                                                variant="outlined"
                                            />
                                        ))}
                                    </Box>
                                </TableCell>
                            </TableRow>
                        );
                    })}
                </TableBody>
            </Table>
        </TableContainer>
    );
};

export default BillDetailsPage;
