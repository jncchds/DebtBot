import clsx from 'clsx'

/** Props for a Profile form input: the error border, and the help or error text as its description. */
export const inputProps = (id: string, error?: string) => ({
  id,
  className: clsx('input', error && 'border-danger focus:border-danger focus:ring-danger/25'),
  'aria-invalid': !!error,
  'aria-describedby': `${id}-msg`,
})
