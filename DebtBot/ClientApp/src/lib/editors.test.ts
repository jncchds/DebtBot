import { describe, expect, it } from 'vitest'
import { defaultPayer, lineStart } from './editors'
import { formEditor } from './newBill'
import type { Person } from './usePeople'

const me: Person = { id: 'anna', name: 'Anna (you)', isMe: true }
const boris: Person = { id: 'boris', name: 'Boris', isMe: false }
const dmitry: Person = { id: 'dmitry', name: 'Dmitry', isMe: false }

describe('editor defaults', () => {
  it('preselects you as the payer only while you are on the bill', () => {
    expect(defaultPayer([boris, me, dmitry])).toBe('anna')
    expect(defaultPayer([boris, dmitry])).toBe('boris')
    expect(defaultPayer([])).toBe('')
  })

  it('ticks everyone on the bill for a new line, so you only while you are on it', () => {
    expect(lineStart([boris, dmitry])).toEqual({ item: '', subtotal: '', mode: 'equal', off: [], parts: {} })
  })

  it('starts ✎ Edit line from the line: its people ticked, the others not, parts kept', () => {
    const s = lineStart([me, boris, dmitry], {
      itemDescription: 'Tiramisu',
      subtotal: 10,
      participants: [
        { userId: 'boris', part: 1 },
        { userId: 'dmitry', part: 2 },
      ],
    })
    expect(s).toEqual({ item: 'Tiramisu', subtotal: '10', mode: 'parts', off: ['anna'], parts: { boris: '1', dmitry: '2' } })
    expect(lineStart([boris, dmitry], { itemDescription: 'Pasta', subtotal: 48, participants: [{ userId: 'boris' }, { userId: 'dmitry' }] }).mode).toBe('equal')
  })
})

describe('the editor in the URL', () => {
  it('reads add-line, add-payment and edit-line/<n>', () => {
    expect(formEditor(undefined, undefined)).toBeUndefined()
    expect(formEditor('add-line', undefined)).toEqual({ kind: 'add-line' })
    expect(formEditor('add-payment', undefined)).toEqual({ kind: 'add-payment' })
    expect(formEditor('edit-line', '2')).toEqual({ kind: 'edit-line', index: 1 })
    expect(formEditor('edit-line', '0')).toBeUndefined()
    expect(formEditor('edit-line', 'x')).toBeUndefined()
  })
})
