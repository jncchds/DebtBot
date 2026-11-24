import type { BillLineCreation } from '../api/types'
import type { Person } from './usePeople'

/** Who PaymentEditor starts with: you when you're on the bill, else its first person, else nobody (''). */
export function defaultPayer(people: Person[]): string {
  return people.find((p) => p.isMe)?.id ?? people[0]?.id ?? ''
}

export interface LineStart {
  item: string
  subtotal: string
  mode: 'equal' | 'parts'
  /** The bill's people who start unticked. */
  off: string[]
  /** Parts as typed, by user id. */
  parts: Record<string, string>
}

/**
 * How LineEditor starts: a new line ticks everyone on the bill (and only them, so you're ticked only while you're
 * on the bill); ✎ Edit line starts from the line, with the bill's other people unticked.
 */
export function lineStart(people: Person[], initial?: BillLineCreation): LineStart {
  if (!initial) return { item: '', subtotal: '', mode: 'equal', off: [], parts: {} }
  const first = initial.participants[0]?.part ?? 1
  return {
    item: initial.itemDescription,
    subtotal: String(initial.subtotal),
    mode: initial.participants.every((p) => (p.part ?? 1) === first) ? 'equal' : 'parts',
    off: people.filter((p) => !initial.participants.some((x) => x.userId === p.id)).map((p) => p.id),
    parts: Object.fromEntries(initial.participants.filter((p) => p.part != null).map((p) => [p.userId, String(p.part)])),
  }
}
