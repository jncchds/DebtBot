import React, { useEffect, useState } from 'react'
import {
  Alert, Box, Button, CircularProgress, Dialog, DialogActions, DialogContent,
  DialogTitle, Divider, Paper, Stack, TextField, Typography
} from '@mui/material'
import api from '../api'
import { DebtModel, ExchangeCreation, ExchangeResult, UserDisplay } from '../types'
import { useAuth } from '../AuthContext'

interface GroupedDebt {
  counterpart: UserDisplay
  asDebtor: DebtModel[]
  asCreditor: DebtModel[]
}

export default function DebtsPage() {
  const { user } = useAuth()
  const [debts, setDebts] = useState<DebtModel[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [exchangeOpen, setExchangeOpen] = useState(false)
  const [exchange, setExchange] = useState<Partial<ExchangeCreation>>({})
  const [exchangeResult, setExchangeResult] = useState<ExchangeResult | null>(null)

  const load = () => {
    setLoading(true)
    api.get<DebtModel[]>('/debts/Own')
      .then((res) => setDebts(res.data))
      .catch(() => setError('Failed to load debts'))
      .finally(() => setLoading(false))
  }

  useEffect(() => { load() }, [])

  const grouped: GroupedDebt[] = []
  debts.forEach((d) => {
    const isDebtor = d.debtorUser.id === user?.id
    const counterpart = isDebtor ? d.creditorUser : d.debtorUser
    let existing = grouped.find((g) => g.counterpart.id === counterpart.id)
    if (!existing) {
      existing = { counterpart, asDebtor: [], asCreditor: [] }
      grouped.push(existing)
    }
    if (isDebtor) existing.asDebtor.push(d)
    else existing.asCreditor.push(d)
  })

  const submitExchange = async () => {
    const res = await api.post<ExchangeResult>('/bills/exchange', exchange)
    setExchangeResult(res.data)
  }

  return (
    <Box>
      <Box display="flex" justifyContent="space-between" alignItems="center" mb={2}>
        <Typography variant="h5" fontWeight={700}>Debts</Typography>
        <Button variant="outlined" onClick={() => setExchangeOpen(true)}>Exchange Debt</Button>
      </Box>
      {loading ? <CircularProgress /> : error ? <Alert severity="error">{error}</Alert> : (
        grouped.length === 0 ? (
          <Typography color="text.secondary">No debts. All settled!</Typography>
        ) : (
          grouped.map((g) => (
            <Paper key={g.counterpart.id} sx={{ p: 2, mb: 2 }}>
              <Typography fontWeight={600} mb={1}>{g.counterpart.displayName}</Typography>
              {g.asDebtor.length > 0 && (
                <>
                  <Typography variant="caption" color="error.main">You owe:</Typography>
                  {g.asDebtor.map((d, i) => (
                    <Typography key={i} variant="body2" color="error.main" pl={1}>
                      {d.amount.toFixed(2)} {d.currencyCode}
                    </Typography>
                  ))}
                </>
              )}
              {g.asCreditor.length > 0 && (
                <>
                  <Typography variant="caption" color="success.main">You are owed:</Typography>
                  {g.asCreditor.map((d, i) => (
                    <Typography key={i} variant="body2" color="success.main" pl={1}>
                      {d.amount.toFixed(2)} {d.currencyCode}
                    </Typography>
                  ))}
                </>
              )}
            </Paper>
          ))
        )
      )}

      <Dialog open={exchangeOpen} onClose={() => { setExchangeOpen(false); setExchangeResult(null) }} fullWidth>
        <DialogTitle>Exchange Debt</DialogTitle>
        <DialogContent>
          {exchangeResult ? (
            <Alert severity="success">
              Exchange created!<br />
              Forward bill: {exchangeResult.forwardBillId}<br />
              Backward bill: {exchangeResult.backwardBillId}
            </Alert>
          ) : (
            <Stack spacing={2} mt={1}>
              <TextField label="Description" fullWidth onChange={(e) => setExchange({ ...exchange, description: e.target.value })} />
              <TextField label="From User ID" fullWidth onChange={(e) => setExchange({ ...exchange, fromUserId: e.target.value })} />
              <Stack direction="row" spacing={1}>
                <TextField label="From Amount" onChange={(e) => setExchange({ ...exchange, fromAmount: parseFloat(e.target.value) })} />
                <TextField label="From Currency" onChange={(e) => setExchange({ ...exchange, fromCurrencyCode: e.target.value })} sx={{ width: 120 }} />
              </Stack>
              <TextField label="To User ID" fullWidth onChange={(e) => setExchange({ ...exchange, toUserId: e.target.value })} />
              <Stack direction="row" spacing={1}>
                <TextField label="To Amount" onChange={(e) => setExchange({ ...exchange, toAmount: parseFloat(e.target.value) })} />
                <TextField label="To Currency" onChange={(e) => setExchange({ ...exchange, toCurrencyCode: e.target.value })} sx={{ width: 120 }} />
              </Stack>
            </Stack>
          )}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => { setExchangeOpen(false); setExchangeResult(null) }}>Close</Button>
          {!exchangeResult && <Button variant="contained" onClick={submitExchange}>Create Exchange</Button>}
        </DialogActions>
      </Dialog>
    </Box>
  )
}
