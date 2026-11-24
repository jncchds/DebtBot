import { useEffect, useRef } from 'react'
import type { TelegramAuthData } from '../api/types'

declare global {
  interface Window {
    onDebtBotTelegramAuth?: (data: TelegramAuthData) => void
  }
}

/**
 * The Telegram Login Widget (https://core.telegram.org/widgets/login). Telegram's script replaces itself with an
 * iframe button; its callback gets the signed payload, which goes as is to POST auth/telegram.
 * The widget only works on the domain set with BotFather's /setdomain (see web-sign-in.md).
 */
export function TelegramLogin({ botUsername, onAuth }: { botUsername: string; onAuth: (data: TelegramAuthData) => void }) {
  const ref = useRef<HTMLDivElement>(null)
  const onAuthRef = useRef(onAuth)
  useEffect(() => {
    onAuthRef.current = onAuth
  })

  useEffect(() => {
    const host = ref.current
    if (!host) return
    window.onDebtBotTelegramAuth = (data) => onAuthRef.current(data)
    const script = document.createElement('script')
    script.src = 'https://telegram.org/js/telegram-widget.js?22'
    script.async = true
    script.setAttribute('data-telegram-login', botUsername)
    script.setAttribute('data-size', 'large')
    script.setAttribute('data-request-access', 'write')
    script.setAttribute('data-onauth', 'onDebtBotTelegramAuth(user)')
    host.appendChild(script)
    return () => {
      host.replaceChildren()
      delete window.onDebtBotTelegramAuth
    }
  }, [botUsername])

  return <div ref={ref} className="flex min-h-10 justify-center" />
}
