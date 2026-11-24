import { describe, expect, it } from 'vitest'
import { matchesPerson, type Person } from './usePeople'

const boris: Person = { id: 'b', name: 'Boris', isMe: false, handle: '@boris_iv', ownName: 'Boris Ivanov' }
const me: Person = { id: 'a', name: 'Anna Petrova (you)', isMe: true }

describe('matchesPerson', () => {
  it('matches the start of any name word, the own name and the handle, ignoring case', () => {
    expect(matchesPerson(boris, 'bor')).toBe(true)
    expect(matchesPerson(boris, 'IVAN')).toBe(true)
    expect(matchesPerson(boris, '@boris_')).toBe(true)
    expect(matchesPerson(me, 'pet')).toBe(true)
    expect(matchesPerson(me, 'you')).toBe(true)
  })

  it('finds text inside, and everyone for an empty query', () => {
    expect(matchesPerson(boris, 'ris_i')).toBe(true)
    expect(matchesPerson(boris, '')).toBe(true)
    expect(matchesPerson(boris, 'clara')).toBe(false)
  })
})
