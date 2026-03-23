import React, { useEffect, useState } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import {
  Alert, Box, Button, Chip, CircularProgress, Collapse, Dialog,
  DialogActions, DialogContent, DialogTitle, Divider, IconButton,
  Paper, Table, TableBody, TableCell, TableContainer, TableHead,
  TableRow, TextField, Typography
} from '@mui/material'
import ArrowBackIcon from '@mui/icons-material/ArrowBack'
import AddIcon from '@mui/icons-material/Add'
import api from '../api'
import { BillModel, BillLineCreation, BillPaymentCreation } from '../types'

const STATUS_COLOR: Record<string, 'default' | 'warning' | 'success' | 'error'> = {
  Draft: 'warning', Ready: 'default', Processed: 'success', Cancelled: 'error',
}

export default function BillDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const [bill, setBill] = useState<BillModel | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [addLinesOpen, setAddLinesOpen] = useState(false)
  const [addPaymentsOpen, setAddPaymentsOpen] = useState(false)
  const [linesJson, setLinesJson] = useState('')
  const [paymentsJson, setPaymentsJson] = useState('')

  const load = () => {
    setLoading(true)
    api.get<BillModel>(`/bills/${id}`)
      .then((res) => setBill(res.data))
      .catch(() => setError('Bill not found'))
      .finally(() => setLoading(false))
  }

  useEffect(() => { load() }, [id])

  const finalize = async () => {
    await api.post(`/bills/${id}/Finalize`)
    load()
  }

  const cancel = async () => {
    await api.post(`/bills/${id}/Cancel`)
    load()
  }

  const submitLines = async () => {
    const lines: BillLineCreation[] = JSON.parse(linesJson)
    await api.post(`/bills/${id}/lines`, lines)
    setAddLinesOpen(false)
    load()
  }

  const submitPayments = async () => {
    const payments: BillPaymentCreation[] = JSON.parse(paymentsJson)
    await api.post(`/bills/${id}/payments`, payments)
    setAddPaymentsOpen(false)
    load()
  }

  if (loading) return <CircularProgress />
  if (error || !bill) return <Alert severity="error">{error || 'Not found'}</Alert>

  const isDraft = bill.status === 'Draft'

  return (
    <Box>
      <Box display="flex" alignItems="center" gap={1} mb={2}>
        <IconButton onClick={() => navigate('/bills')}><ArrowBackIcon /></IconButton>
        <Typography variant="h6" fontWeight={700} sx={{ flexGrow: 1 }}>{bill.description}</Typography>
        <Chip label={bill.status} color={STATUS_COLOR[bill.status] ?? 'default'} />
      </Box>
      <Paper sx={{ p: 2, mb: 2 }}>
        <Typography variant="body2">Date: {new Date(bill.date).toLocaleDateString()}</Typography>
        <Typography variant="body2">Total: {bill.totalWithTips} {bill.currencyCode}</Typography>
        <Typography variant="body2">Payment Currency: {bill.paymentCurrencyCode}</Typography>
        <Typography variant="body2">Creator: {bill.creator.displayName}</Typography>
      </Paper>

      {isDraft && (
        <Box display="flex" gap={1} mb={2} flexWrap="wrap">
          <Button variant="contained" color="success" onClick={finalize}>Finalize</Button>
          <Button variant="outlined" color="error" onClick={cancel}>Cancel</Button>
          <Button variant="outlined" startIcon={<AddIcon />} onClick={() => setAddLinesOpen(true)}>Add Lines</Button>
          <Button variant="outlined" startIcon={<AddIcon />} onClick={() => setAddPaymentsOpen(true)}>Add Payments</Button>
        </Box>
      )}

      <Typography variant="subtitle1" fontWeight={600} mb={1}>Lines</Typography>
      <TableContainer component={Paper} sx={{ mb: 2 }}>
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell>Description</TableCell>
              <TableCell>Subtotal</TableCell>
              <TableCell>Participants</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {bill.lines.map((line) => (
              <TableRow key={line.id}>
                <TableCell>{line.itemDescription}</TableCell>
                <TableCell>{line.subtotal}</TableCell>
                <TableCell>{line.participants.map((p) => `${p.userDisplayName} (${(p.part * 100).toFixed(0)}%)`).join(', ')}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </TableContainer>

      <Typography variant="subtitle1" fontWeight={600} mb={1}>Payments</Typography>
      <TableContainer component={Paper} sx={{ mb: 2 }}>
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell>User</TableCell>
              <TableCell>Amount</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {bill.payments.map((p, i) => (
              <TableRow key={i}>
                <TableCell>{p.user.displayName}</TableCell>
                <TableCell>{p.amount} {bill.paymentCurrencyCode}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </TableContainer>

      {/* Add Lines Dialog */}
      <Dialog open={addLinesOpen} onClose={() => setAddLinesOpen(false)} fullWidth>
        <DialogTitle>Add Lines (JSON)</DialogTitle>
        <DialogContent>
          <Typography variant="caption" color="text.secondary" component="pre" mb={1}>{`Example:
[{
  "itemDescription": "Food",
  "subtotal": 100,
  "participants": [
    { "userId": "<guid>", "part": 0.5 },
    { "userId": "<guid>", "part": 0.5 }
  ]
}]`}</Typography>
          <TextField multiline rows={8} fullWidth value={linesJson} onChange={(e) => setLinesJson(e.target.value)} />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setAddLinesOpen(false)}>Cancel</Button>
          <Button variant="contained" onClick={submitLines}>Add</Button>
        </DialogActions>
      </Dialog>

      {/* Add Payments Dialog */}
      <Dialog open={addPaymentsOpen} onClose={() => setAddPaymentsOpen(false)} fullWidth>
        <DialogTitle>Add Payments (JSON)</DialogTitle>
        <DialogContent>
          <Typography variant="caption" color="text.secondary" component="pre" mb={1}>{`Example:
[{ "userId": "<guid>", "amount": 100 }]`}</Typography>
          <TextField multiline rows={4} fullWidth value={paymentsJson} onChange={(e) => setPaymentsJson(e.target.value)} />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setAddPaymentsOpen(false)}>Cancel</Button>
          <Button variant="contained" onClick={submitPayments}>Add</Button>
        </DialogActions>
      </Dialog>
    </Box>
  )
}
