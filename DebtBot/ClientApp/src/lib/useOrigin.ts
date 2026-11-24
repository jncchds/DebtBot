import { useLocation } from 'react-router'
import { originState } from './backToList'

/** The router state to put on links to a bill from this page (see `backToList.ts`). */
export function useOrigin() {
  const location = useLocation()
  return originState(location.pathname, location.search, location.state)
}
