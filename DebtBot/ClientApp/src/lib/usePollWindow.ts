import { useEffect, useState } from 'react'

/** How long the UI waits for a background finalize or cancel before it says "still processing". */
export const POLL_MS = 30_000

/** True until the time `until` (ms since epoch) has passed; re-renders when it does. */
export function usePollWindow(until: number): boolean {
  const [now, setNow] = useState(() => Date.now())
  useEffect(() => {
    const left = until - Date.now()
    if (left <= 0) return
    const id = setTimeout(() => setNow(Date.now()), left + 10)
    return () => clearTimeout(id)
  }, [until])
  return until > now
}
