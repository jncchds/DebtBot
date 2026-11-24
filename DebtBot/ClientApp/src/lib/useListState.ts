import { useCallback, useMemo } from 'react'
import { useNavigate, useSearchParams } from 'react-router'
import { listSearch, parseListState, type ListSpec, type ListState } from './listState'

/**
 * A list's filters, sort and page, read from and written to the URL (see listState.ts).
 * Every change is a new history entry, so Back undoes it; pass `{ replace: true }` for in-between states.
 */
export function useListState(spec: ListSpec) {
  const [params] = useSearchParams()
  const navigate = useNavigate()
  const state = useMemo(() => parseListState(params, spec), [params, spec])
  const setState = useCallback(
    (next: ListState, opts?: { replace?: boolean }) => navigate({ search: listSearch(next, spec, params) }, { replace: opts?.replace }),
    [navigate, params, spec],
  )
  return [state, setState] as const
}
