import React, { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import {
  Alert, Box, Button, Divider, IconButton, MenuItem,
  Paper, Select, Stack, TextField, Typography
} from '@mui/material'
import ArrowBackIcon from '@mui/icons-material/ArrowBack'
import AddIcon from '@mui/icons-material/Add'
import DeleteIcon from '@mui/icons-material/Delete'
import api from '../api'
import { BillCreation } from '../types'

interface LineForm {
  itemDescription: string
  subtotal: string
  participants: { userId: string; part: string }[]
}

interface PaymentForm {
  userId: string
  amount: string
}

export default function CreateBillPage() {
  const navigate = useNavigate()
  const [description, setDescription] = useState('')
  const [currencyCode, setCurrencyCode] = useState('USD')
  const [paymentCurrencyCode, setPaymentCurrencyCode] = useState('USD')
  const [date, setDate] = useState(new Date().toISOString().slice(0, 10))
  const [totalWithTips, setTotalWithTips] = useState('')
  const [lines, setLines] = useState<LineForm[]>([])
  const [payments, setPayments] = useState<PaymentForm[]>([])
  const [error, setError] = useState('')

  const addLine = () => setLines([...lines, { itemDescription: '', subtotal: '', participants: [] }])
  const removeLine = (i: number) => setLines(lines.filter((_, idx) => idx !== i))
  const updateLine = (i: number, field: keyof LineForm, value: string) => {
    const next = [...lines]
    ;(next[i] as any)[field] = value
    setLines(next)
  }
  const addParticipant = (li: number) => {
    const next = [...lines]
    next[li].participants.push({ userId: '', part: '' })
    setLines(next)
  }
  const updateParticipant = (li: number, pi: number, field: 'userId' | 'part', value: string) => {
    const next = [...lines]
    next[li].participants[pi][field] = value
    setLines(next)
  }
  const removeParticipant = (li: number, pi: number) => {
    const next = [...lines]
    next[li].participants = next[li].participants.filter((_, i) => i !== pi)
    setLines(next)
  }

  const addPayment = () => setPayments([...payments, { userId: '', amount: '' }])
  const removePayment = (i: number) => setPayments(payments.filter((_, idx) => idx !== i))
  const updatePayment = (i: number, field: keyof PaymentForm, value: string) => {
    const next = [...payments]
    next[i][field] = value
    setPayments(next)
  }

  const submit = async (asDraft: boolean) => {
    setError('')
    const body: BillCreation = {
      description,
      currencyCode,
      paymentCurrencyCode,
      date: new Date(date).toISOString(),
      totalWithTips: parseFloat(totalWithTips),
      status: asDraft ? 0 : 2,
      lines: lines.map((l) => ({
        itemDescription: l.itemDescription,
        subtotal: parseFloat(l.subtotal),
        participants: l.participants.map((p) => ({ userId: p.userId, part: parseFloat(p.part) })),
      })),
      payments: payments.map((p) => ({ userId: p.userId, amount: parseFloat(p.amount) })),
    }
    try {
      const res = await api.post<string>('/bills', body)
      if (!asDraft) {
        await api.post(`/bills/${res.data}/Finalize`)
      }
      navigate(`/bills/${res.data}`)
    } catch (e: any) {
      setError(e.response?.data || 'Failed to create bill')
    }
  }

  return (
    <Box>
      <Box display="flex" alignItems="center" gap={1} mb={2}>
        <IconButton onClick={() => navigate('/bills')}><ArrowBackIcon /></IconButton>
        <Typography variant="h6" fontWeight={700}>New Bill</Typography>
      </Box>
      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
      <Paper sx={{ p: 2, mb: 2 }}>
        <Stack spacing={2}>
          <TextField label="Description" value={description} onChange={(e) => setDescription(e.target.value)} fullWidth />
          <Stack direction="row" spacing={2}>
            <TextField label="Currency" value={currencyCode} onChange={(e) => setCurrencyCode(e.target.value)} sx={{ width: 120 }} />
            <TextField label="Payment Currency" value={paymentCurrencyCode} onChange={(e) => setPaymentCurrencyCode(e.target.value)} sx={{ width: 160 }} />
          </Stack>
          <Stack direction="row" spacing={2}>
            <TextField label="Date" type="date" value={date} onChange={(e) => setDate(e.target.value)} InputLabelProps={{ shrink: true }} />
            <TextField label="Total (with tips)" value={totalWithTips} onChange={(e) => setTotalWithTips(e.target.value)} sx={{ width: 180 }} />
          </Stack>
        </Stack>
      </Paper>

      <Box display="flex" justifyContent="space-between" alignItems="center" mb={1}>
        <Typography variant="subtitle1" fontWeight={600}>Lines</Typography>
        <Button size="small" startIcon={<AddIcon />} onClick={addLine}>Add Line</Button>
      </Box>
      {lines.map((line, li) => (
        <Paper key={li} sx={{ p: 2, mb: 1 }}>
          <Stack spacing={1}>
            <Stack direction="row" spacing={1} alignItems="center">
              <TextField label="Description" value={line.itemDescription} onChange={(e) => updateLine(li, 'itemDescription', e.target.value)} sx={{ flexGrow: 1 }} size="small" />
              <TextField label="Subtotal" value={line.subtotal} onChange={(e) => updateLine(li, 'subtotal', e.target.value)} sx={{ width: 120 }} size="small" />
              <IconButton size="small" onClick={() => removeLine(li)}><DeleteIcon fontSize="small" /></IconButton>
            </Stack>
            {line.participants.map((p, pi) => (
              <Stack key={pi} direction="row" spacing={1} pl={2} alignItems="center">
                <TextField label="User ID" value={p.userId} onChange={(e) => updateParticipant(li, pi, 'userId', e.target.value)} sx={{ flexGrow: 1 }} size="small" />
                <TextField label="Part (0-1)" value={p.part} onChange={(e) => updateParticipant(li, pi, 'part', e.target.value)} sx={{ width: 100 }} size="small" />
                <IconButton size="small" onClick={() => removeParticipant(li, pi)}><DeleteIcon fontSize="small" /></IconButton>
              </Stack>
            ))}
            <Button size="small" onClick={() => addParticipant(li)} sx={{ alignSelf: 'flex-start', ml: 2 }}>+ Participant</Button>
          </Stack>
        </Paper>
      ))}

      <Box display="flex" justifyContent="space-between" alignItems="center" mt={2} mb={1}>
        <Typography variant="subtitle1" fontWeight={600}>Payments</Typography>
        <Button size="small" startIcon={<AddIcon />} onClick={addPayment}>Add Payment</Button>
      </Box>
      {payments.map((p, pi) => (
        <Paper key={pi} sx={{ p: 2, mb: 1 }}>
          <Stack direction="row" spacing={1} alignItems="center">
            <TextField label="User ID" value={p.userId} onChange={(e) => updatePayment(pi, 'userId', e.target.value)} sx={{ flexGrow: 1 }} size="small" />
            <TextField label="Amount" value={p.amount} onChange={(e) => updatePayment(pi, 'amount', e.target.value)} sx={{ width: 120 }} size="small" />
            <IconButton size="small" onClick={() => removePayment(pi)}><DeleteIcon fontSize="small" /></IconButton>
          </Stack>
        </Paper>
      ))}

      <Divider sx={{ my: 2 }} />
      <Stack direction="row" spacing={2} justifyContent="flex-end">
        <Button variant="outlined" onClick={() => submit(true)}>Save as Draft</Button>
        <Button variant="contained" onClick={() => submit(false)}>Create &amp; Finalize</Button>
      </Stack>
    </Box>
  )
}
