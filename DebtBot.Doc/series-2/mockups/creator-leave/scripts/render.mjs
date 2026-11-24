// Renders ../creator-leave.html to PNGs next to it: node render.mjs (any cwd).
import { launch } from '../../../../web-ui/e2e/lib/cdp.mjs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

const dir = join(dirname(fileURLToPath(import.meta.url)), '..')
const b = await launch(9341)
try {
  for (const state of ['newbill-leave', 'newbill-left', 'bill-leave', 'bill-left'])
    for (const [w, phone] of [[1280, false], [390, true]])
      for (const theme of ['light', 'dark']) {
        await b.viewport(w, 900, phone)
        await b.goto(`file://${dir}/creator-leave.html?state=${state}&theme=${theme}${phone ? '&phone=1' : ''}`)
        await b.shot(join(dir, `${state}-${w}-${theme}.png`))
      }
} finally {
  await b.close()
}
