import React, { useEffect, useState } from 'react'
import {
  Box, Button, Chip, CircularProgress, Paper, Table, TableBody,
  TableCell, TableContainer, TableHead, TableRow, Typography, Pagination
} from '@mui/material'
import AddIcon from '@mui/icons-material/Add'
import { useNavigate } from 'react-router-dom'
import api from '../api'
import { BillModel, PagingResult } from '../types'

const STATUS_COLOR: Record<string, 'default' | 'warning' | 'success' | 'error'> = {
  Draft: 'warning',
  Ready: 'default',
  Processed: 'success',
  Cancelled: 'error',
}

const PAGE_SIZE = 10

export default function BillsPage() {
  const navigate = useNavigate()
  const [bills, setBills] = useState<BillModel[]>([])
  const [total, setTotal] = useState(0)
  const [page, setPage] = useState(1)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    setLoading(true)
    api.get<PagingResult<BillModel>>('/bills/Own', { params: { pageNumber: page - 1, countPerPage: PAGE_SIZE } })
      .then((res) => {
        setBills(res.data.items)
        setTotal(res.data.totalCount)
      })
      .finally(() => setLoading(false))
  }, [page])

  const pageCount = Math.ceil(total / PAGE_SIZE)

  return (
    <Box>
      <Box display="flex" justifyContent="space-between" alignItems="center" mb={2}>
        <Typography variant="h5" fontWeight={700}>My Bills</Typography>
        <Button variant="contained" startIcon={<AddIcon />} onClick={() => navigate('/bills/new')}>
          New Bill
        </Button>
      </Box>
      {loading ? (
        <CircularProgress />
      ) : (
        <TableContainer component={Paper}>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Date</TableCell>
                <TableCell>Description</TableCell>
                <TableCell>Amount</TableCell>
                <TableCell>Status</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {bills.map((bill) => (
                <TableRow key={bill.id} hover sx={{ cursor: 'pointer' }} onClick={() => navigate(`/bills/${bill.id}`)}>
                  <TableCell>{new Date(bill.date).toLocaleDateString()}</TableCell>
                  <TableCell>{bill.description}</TableCell>
                  <TableCell>{bill.totalWithTips} {bill.currencyCode}</TableCell>
                  <TableCell>
                    <Chip label={bill.status} color={STATUS_COLOR[bill.status] ?? 'default'} size="small" />
                  </TableCell>
                </TableRow>
              ))}
              {bills.length === 0 && (
                <TableRow>
                  <TableCell colSpan={4} align="center">No bills yet</TableCell>
                </TableRow>
              )}
            </TableBody>
          </Table>
        </TableContainer>
      )}
      {pageCount > 1 && (
        <Box display="flex" justifyContent="center" mt={2}>
          <Pagination count={pageCount} page={page} onChange={(_, v) => setPage(v)} />
        </Box>
      )}
    </Box>
  )
}
