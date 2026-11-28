// Renders ../receipt.html to PNGs next to it: node render.mjs [state ...] (any cwd).
import { launch } from '../../../../web-ui/e2e/lib/cdp.mjs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

const dir = join(dirname(fileURLToPath(import.meta.url)), '..')
const all = ['form-receipt', 'form-busy', 'paste-error', 'form-replace', 'form-filled', 'paste-filled', 'profile']
const states = process.argv.length > 2 ? process.argv.slice(2) : all
const b = await launch(9343)
try {
  for (const state of states)
    for (const [w, phone] of [[1280, false], [390, true]])
      for (const theme of ['light', 'dark']) {
        await b.viewport(w, 900, phone)
        await b.goto(`file://${dir}/receipt.html?state=${state}&theme=${theme}${phone ? '&phone=1' : ''}`)
        await b.shot(join(dir, `${state}-${w}-${theme}.png`))
      }
} finally {
  await b.close()
}
