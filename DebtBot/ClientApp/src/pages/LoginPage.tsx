import React, { useEffect, useRef } from 'react'
import { useNavigate } from 'react-router-dom'
import { Box, Button, CircularProgress, Paper, Typography } from '@mui/material'
import { useAuth } from '../AuthContext'
import api from '../api'

declare global {
  interface Window {
    onTelegramAuth: (user: TelegramUser) => void
  }
}

interface TelegramUser {
  id: number
  first_name: string
  last_name?: string
  username?: string
  photo_url?: string
  auth_date: number
  hash: string
}

export default function LoginPage() {
  const { login, token } = useAuth()
  const navigate = useNavigate()
  const widgetRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    if (token) {
      navigate('/', { replace: true })
      return
    }

    window.onTelegramAuth = async (tgUser: TelegramUser) => {
      const payload = {
        id: tgUser.id,
        firstName: tgUser.first_name,
        lastName: tgUser.last_name,
        username: tgUser.username,
        photoUrl: tgUser.photo_url,
        authDate: tgUser.auth_date,
        hash: tgUser.hash,
      }
      const res = await api.post<string>('/identity/telegram-login', payload)
      const jwtToken = res.data
      // Decode user info from JWT payload
      const parts = jwtToken.split('.')
      const claims = JSON.parse(atob(parts[1].replace(/-/g, '+').replace(/_/g, '/')))
      const authUser = {
        id: claims['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier'] ?? claims.sub,
        displayName: claims['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name'] ?? tgUser.first_name,
        telegramUserName: tgUser.username,
      }
      login(jwtToken, authUser)
      navigate('/', { replace: true })
    }

    // Inject Telegram Login Widget
    if (widgetRef.current && widgetRef.current.childNodes.length === 0) {
      const script = document.createElement('script')
      script.src = 'https://telegram.org/js/telegram-widget.js?22'
      // Replace 'YOUR_BOT_USERNAME' with your bot's @username (without the @)
      // This must match the Telegram bot configured in appsettings.json
      script.setAttribute('data-telegram-login', 'YOUR_BOT_USERNAME')
      script.setAttribute('data-size', 'large')
      script.setAttribute('data-onauth', 'onTelegramAuth(user)')
      script.setAttribute('data-request-access', 'write')
      script.async = true
      widgetRef.current.appendChild(script)
    }
  }, [token])

  return (
    <Box display="flex" justifyContent="center" alignItems="center" minHeight="100vh" bgcolor="background.default">
      <Paper elevation={3} sx={{ p: 4, maxWidth: 380, width: '100%', textAlign: 'center' }}>
        <Typography variant="h5" gutterBottom fontWeight={700}>DebtBot</Typography>
        <Typography variant="body2" color="text.secondary" mb={3}>
          Sign in with your Telegram account
        </Typography>
        <Box display="flex" justifyContent="center" ref={widgetRef} />
        <Typography variant="caption" color="text.secondary" mt={2} display="block">
          After signing in via Telegram, you'll be redirected automatically.
        </Typography>
      </Paper>
    </Box>
  )
}
