// Renders ../profile-tokens.html and ../admin-users-tokens.html to PNGs next to them: node render.mjs (any cwd).
import { launch } from '../../../../web-ui/e2e/lib/cdp.mjs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

const dir = join(dirname(fileURLToPath(import.meta.url)), '..')
const views = [['profile-tokens', 'tokens'], ['profile-tokens', 'new'], ['profile-tokens', 'created'], ['admin-users-tokens', 'main'], ['admin-users-tokens', 'revoke']]
const b = await launch(9343)
try {
  for (const [file, state] of views)
    for (const [w, phone] of [[1280, false], [390, true]])
      for (const theme of ['light', 'dark']) {
        await b.viewport(w, 900, phone)
        await b.goto(`file://${dir}/${file}.html?state=${state}&theme=${theme}${phone ? '&phone=1' : ''}`)
        const name = file === 'admin-users-tokens' ? (state === 'main' ? 'admin-users' : `admin-users-${state}`) : state === 'tokens' ? 'tokens' : `tokens-${state}`
        await b.shot(join(dir, `${name}-${w}-${theme}.png`))
      }
} finally {
  await b.close()
}
