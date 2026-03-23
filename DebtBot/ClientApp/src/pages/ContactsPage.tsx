import React, { useEffect, useState } from 'react'
import {
  Alert, Box, Button, CircularProgress, Dialog, DialogActions, DialogContent,
  DialogTitle, IconButton, List, ListItem, ListItemText, Paper, TextField,
  Typography
} from '@mui/material'
import AddIcon from '@mui/icons-material/Add'
import PersonAddIcon from '@mui/icons-material/PersonAdd'
import CheckIcon from '@mui/icons-material/Check'
import CloseIcon from '@mui/icons-material/Close'
import api from '../api'
import { UserDisplay } from '../types'

interface ContactUser extends UserDisplay {
  subscriptionState?: string
}

export default function ContactsPage() {
  const [contacts, setContacts] = useState<UserDisplay[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [addOpen, setAddOpen] = useState(false)
  const [searchQ, setSearchQ] = useState('')
  const [searchResults, setSearchResults] = useState<UserDisplay[]>([])
  const [searching, setSearching] = useState(false)

  const load = () => {
    setLoading(true)
    api.get<UserDisplay[]>('/usercontacts/Own')
      .then((res) => setContacts(res.data))
      .catch(() => setError('Failed to load contacts'))
      .finally(() => setLoading(false))
  }

  useEffect(() => { load() }, [])

  const search = async () => {
    if (!searchQ.trim()) return
    setSearching(true)
    const res = await api.get<UserDisplay[]>('/users/search', { params: { q: searchQ } })
    setSearchResults(res.data)
    setSearching(false)
  }

  const addContact = async (contactId: string) => {
    await api.post('/usercontacts/', { id: contactId })
    setAddOpen(false)
    load()
  }

  const requestSubscription = async (contactId: string) => {
    await api.post('/usercontacts/subscription/request', null, { params: { contactId } })
  }

  return (
    <Box>
      <Box display="flex" justifyContent="space-between" alignItems="center" mb={2}>
        <Typography variant="h5" fontWeight={700}>Contacts</Typography>
        <Button variant="outlined" startIcon={<AddIcon />} onClick={() => setAddOpen(true)}>Add Contact</Button>
      </Box>
      {loading ? <CircularProgress /> : error ? <Alert severity="error">{error}</Alert> : (
        <Paper>
          <List>
            {contacts.map((c) => (
              <ListItem
                key={c.id}
                secondaryAction={
                  <IconButton edge="end" title="Request subscription" onClick={() => requestSubscription(c.id)}>
                    <PersonAddIcon fontSize="small" />
                  </IconButton>
                }
              >
                <ListItemText
                  primary={c.displayName}
                  secondary={c.telegramUserName ? `@${c.telegramUserName}` : undefined}
                />
              </ListItem>
            ))}
            {contacts.length === 0 && (
              <ListItem><ListItemText primary="No contacts yet" /></ListItem>
            )}
          </List>
        </Paper>
      )}

      <Dialog open={addOpen} onClose={() => setAddOpen(false)} fullWidth>
        <DialogTitle>Add Contact</DialogTitle>
        <DialogContent>
          <Box display="flex" gap={1} mt={1} mb={2}>
            <TextField
              label="Search by name / username"
              value={searchQ}
              onChange={(e) => setSearchQ(e.target.value)}
              onKeyDown={(e) => e.key === 'Enter' && search()}
              fullWidth
              size="small"
            />
            <Button variant="contained" onClick={search} disabled={searching}>Search</Button>
          </Box>
          <List dense>
            {searchResults.map((u) => (
              <ListItem
                key={u.id}
                secondaryAction={
                  <Button size="small" onClick={() => addContact(u.id)}>Add</Button>
                }
              >
                <ListItemText primary={u.displayName} secondary={u.telegramUserName ? `@${u.telegramUserName}` : undefined} />
              </ListItem>
            ))}
          </List>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setAddOpen(false)}>Close</Button>
        </DialogActions>
      </Dialog>
    </Box>
  )
}
