import { describe, expect, it } from 'vitest'
import { matchesPerson, matchTextPerson, type Person } from './usePeople'

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

describe('matchTextPerson (names in the bot text, as the server finds them)', () => {
  const people = [{ ...me, handle: '@anna_p' }, boris]
  it('matches @username ignoring case, and a contact name exactly ignoring case', () => {
    expect(matchTextPerson('@BORIS_IV', people)).toBe(boris)
    expect(matchTextPerson('@anna_p', people)?.id).toBe('a')
    expect(matchTextPerson('boris', people)).toBe(boris)
  })
  it('leaves the rest to the server: no @ on a username, your own name, partial names', () => {
    expect(matchTextPerson('boris_iv', people)).toBeUndefined()
    expect(matchTextPerson('Anna Petrova (you)', people)).toBeUndefined()
    expect(matchTextPerson('bor', people)).toBeUndefined()
    expect(matchTextPerson(' ', people)).toBeUndefined()
  })
})
