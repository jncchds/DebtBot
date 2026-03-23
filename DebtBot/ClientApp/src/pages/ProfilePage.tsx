import React, { useEffect, useState } from 'react'
import {
  Alert, Box, Button, CircularProgress, Paper, Stack, TextField, Typography
} from '@mui/material'
import api from '../api'
import { UserDisplay } from '../types'

interface UserUpdateForm {
  displayName: string
  phone: string
  email: string
}

export default function ProfilePage() {
  const [user, setUser] = useState<UserDisplay | null>(null)
  const [form, setForm] = useState<UserUpdateForm>({ displayName: '', phone: '', email: '' })
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [success, setSuccess] = useState(false)
  const [error, setError] = useState('')

  useEffect(() => {
    api.get<UserDisplay>('/users/own')
      .then((res) => {
        setUser(res.data)
        setForm({ displayName: res.data.displayName ?? '', phone: '', email: '' })
      })
      .catch(() => setError('Failed to load profile'))
      .finally(() => setLoading(false))
  }, [])

  const save = async () => {
    setSaving(true)
    setSuccess(false)
    setError('')
    try {
      await api.put('/users/own', form)
      setSuccess(true)
    } catch {
      setError('Failed to save changes')
    } finally {
      setSaving(false)
    }
  }

  if (loading) return <CircularProgress />

  return (
    <Box maxWidth={480}>
      <Typography variant="h5" fontWeight={700} mb={2}>Profile</Typography>
      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
      {success && <Alert severity="success" sx={{ mb: 2 }}>Saved!</Alert>}
      <Paper sx={{ p: 2 }}>
        {user?.telegramUserName && (
          <Typography variant="body2" color="text.secondary" mb={2}>
            Telegram: @{user.telegramUserName}
          </Typography>
        )}
        <Stack spacing={2}>
          <TextField
            label="Display Name"
            value={form.displayName}
            onChange={(e) => setForm({ ...form, displayName: e.target.value })}
            fullWidth
          />
          <TextField
            label="Phone"
            value={form.phone}
            onChange={(e) => setForm({ ...form, phone: e.target.value })}
            fullWidth
          />
          <TextField
            label="Email"
            value={form.email}
            onChange={(e) => setForm({ ...form, email: e.target.value })}
            fullWidth
          />
          <Button variant="contained" onClick={save} disabled={saving}>Save Changes</Button>
        </Stack>
      </Paper>
    </Box>
  )
}
