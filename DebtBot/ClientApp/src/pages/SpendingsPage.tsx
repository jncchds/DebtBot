import React, { useEffect, useState } from 'react'
import {
  Alert, Box, CircularProgress, Pagination, Paper, Table, TableBody,
  TableCell, TableContainer, TableHead, TableRow, Typography
} from '@mui/material'
import api from '../api'
import { PagingResult, SpendingModel } from '../types'
import { useNavigate } from 'react-router-dom'

const PAGE_SIZE = 10

export default function SpendingsPage() {
  const navigate = useNavigate()
  const [items, setItems] = useState<SpendingModel[]>([])
  const [total, setTotal] = useState(0)
  const [page, setPage] = useState(1)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  useEffect(() => {
    setLoading(true)
    api.get<PagingResult<SpendingModel>>('/spendings/Own', { params: { pageNumber: page - 1, countPerPage: PAGE_SIZE } })
      .then((res) => { setItems(res.data.items); setTotal(res.data.totalCount) })
      .catch(() => setError('Failed to load spendings'))
      .finally(() => setLoading(false))
  }, [page])

  return (
    <Box>
      <Typography variant="h5" fontWeight={700} mb={2}>My Spendings</Typography>
      {loading ? <CircularProgress /> : error ? <Alert severity="error">{error}</Alert> : (
        <TableContainer component={Paper}>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Description</TableCell>
                <TableCell>Amount</TableCell>
                <TableCell>Payment Amount</TableCell>
                <TableCell>Portion</TableCell>
                <TableCell>Bill</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {items.map((s, i) => (
                <TableRow key={i}>
                  <TableCell>{s.description}</TableCell>
                  <TableCell>{s.amount.toFixed(2)} {s.currencyCode}</TableCell>
                  <TableCell>{s.paymentAmount.toFixed(2)} {s.paymentCurrencyCode}</TableCell>
                  <TableCell>{(s.portion * 100).toFixed(1)}%</TableCell>
                  <TableCell>
                    <Typography
                      variant="body2"
                      color="primary"
                      sx={{ cursor: 'pointer', textDecoration: 'underline' }}
                      onClick={() => navigate(`/bills/${s.billId}`)}
                    >
                      View Bill
                    </Typography>
                  </TableCell>
                </TableRow>
              ))}
              {items.length === 0 && (
                <TableRow><TableCell colSpan={5} align="center">No spendings yet</TableCell></TableRow>
              )}
            </TableBody>
          </Table>
        </TableContainer>
      )}
      {Math.ceil(total / PAGE_SIZE) > 1 && (
        <Box display="flex" justifyContent="center" mt={2}>
          <Pagination count={Math.ceil(total / PAGE_SIZE)} page={page} onChange={(_, v) => setPage(v)} />
        </Box>
      )}
    </Box>
  )
}
