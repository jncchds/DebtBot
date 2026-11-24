// Minimal headless Chrome driver over the DevTools protocol (Node 22's built-in WebSocket).
import { spawn } from 'node:child_process'
import { mkdtempSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

const sleep = (ms) => new Promise((r) => setTimeout(r, ms))

export async function launch(port = 9333) {
  const dir = mkdtempSync(join(process.env.TMPDIR ?? tmpdir(), 'chrome-'))
  const proc = spawn('google-chrome', ['--headless=new', '--no-sandbox', '--disable-gpu', '--hide-scrollbars', `--remote-debugging-port=${port}`, `--user-data-dir=${dir}`, 'about:blank'], { stdio: 'ignore' })
  let ver
  for (let i = 0; i < 50 && !ver; i++) {
    await sleep(200)
    ver = await fetch(`http://127.0.0.1:${port}/json/version`).then((r) => r.json()).catch(() => null)
  }
  const tabs = await fetch(`http://127.0.0.1:${port}/json`).then((r) => r.json())
  const page = tabs.find((t) => t.type === 'page')
  const ws = new WebSocket(page.webSocketDebuggerUrl)
  await new Promise((r) => ws.addEventListener('open', r, { once: true }))
  let id = 0
  const pending = new Map()
  const events = []
  ws.addEventListener('message', (m) => {
    const msg = JSON.parse(m.data)
    if (msg.id && pending.has(msg.id)) {
      const { res, rej } = pending.get(msg.id)
      pending.delete(msg.id)
      msg.error ? rej(new Error(JSON.stringify(msg.error))) : res(msg.result)
    } else if (msg.method) {
      events.push(msg)
      for (const h of handlers) if (h.method === msg.method) h.fn(msg.params)
    }
  })
  const handlers = []
  const send = (method, params = {}) => new Promise((res, rej) => { const i = ++id; pending.set(i, { res, rej }); ws.send(JSON.stringify({ id: i, method, params })) })
  await send('Page.enable')
  await send('Runtime.enable')

  const b = {
    send,
    events,
    /** Calls `fn(params)` for every later event named `method` (e.g. Fetch.requestPaused). */
    on(method, fn) {
      handlers.push({ method, fn })
    },
    async viewport(width, height, mobile = false) {
      await send('Emulation.setDeviceMetricsOverride', { width, height, deviceScaleFactor: 1, mobile })
    },
    async theme(dark) {
      await send('Emulation.setEmulatedMedia', { features: [{ name: 'prefers-color-scheme', value: dark ? 'dark' : 'light' }] })
    },
    async eval(expr) {
      const r = await send('Runtime.evaluate', { expression: expr, awaitPromise: true, returnByValue: true })
      if (r.exceptionDetails) throw new Error(`eval failed: ${expr.slice(0, 80)}: ${JSON.stringify(r.exceptionDetails).slice(0, 300)}`)
      return r.result.value
    },
    async goto(url) {
      await send('Page.navigate', { url })
      await sleep(400)
      await b.waitFor('document.readyState === "complete"')
    },
    async waitFor(expr, timeout = 15000) {
      const end = Date.now() + timeout
      while (Date.now() < end) {
        try {
          if (await b.eval(`!!(${expr})`)) return true
        } catch { /* page navigating */ }
        await sleep(150)
      }
      throw new Error(`timeout waiting for ${expr}`)
    },
    /** Waits until some element's text includes `text`. */
    async waitText(text, timeout) {
      return b.waitFor(`document.body && document.body.innerText.includes(${JSON.stringify(text)})`, timeout)
    },
    text: () => b.eval('document.body.innerText'),
    path: () => b.eval('location.pathname + location.search'),
    /** Clicks the first element matching the selector whose text includes `text` (if given). */
    async click(selector, text) {
      const ok = await b.eval(`(() => { const els = [...document.querySelectorAll(${JSON.stringify(selector)})]; const el = els.find(e => ${text === undefined ? 'true' : `e.innerText.includes(${JSON.stringify(text)})`}); if (!el) return false; el.scrollIntoView({block:'center'}); el.click(); return true })()`)
      if (!ok) throw new Error(`no element ${selector} ${text ?? ''}`)
      await sleep(250)
    },
    /** Sets an input's value the React way and fires input/change. */
    async fill(selector, value) {
      const ok = await b.eval(`(() => { const el = document.querySelector(${JSON.stringify(selector)}); if (!el) return false;
        const proto = el.tagName === 'SELECT' ? HTMLSelectElement.prototype : el.tagName === 'TEXTAREA' ? HTMLTextAreaElement.prototype : HTMLInputElement.prototype;
        Object.getOwnPropertyDescriptor(proto, 'value').set.call(el, ${JSON.stringify(value)});
        el.dispatchEvent(new Event('input', { bubbles: true })); el.dispatchEvent(new Event('change', { bubbles: true })); return true })()`)
      if (!ok) throw new Error(`no input ${selector}`)
      await sleep(250)
    },
    async back() { await b.eval('history.back()'); await sleep(600) },
    async forward() { await b.eval('history.forward()'); await sleep(600) },
    async reload() { await send('Page.reload'); await sleep(500); await b.waitFor('document.readyState === "complete"') },
    async shot(file, fullHeight = true) {
      let clip
      if (fullHeight) {
        const h = await b.eval('Math.max(document.documentElement.scrollHeight, ...[...document.querySelectorAll("main, nav")].map(e => e.scrollHeight + e.getBoundingClientRect().top))')
        const w = await b.eval('window.innerWidth')
        const vh = await b.eval('window.innerHeight')
        if (h > vh) {
          await send('Emulation.setDeviceMetricsOverride', { width: w, height: Math.ceil(h), deviceScaleFactor: 1, mobile: w < 500 })
          await sleep(400)
        }
        const r = await send('Page.captureScreenshot', { format: 'png' })
        writeFileSync(file, Buffer.from(r.data, 'base64'))
        if (h > vh) await send('Emulation.setDeviceMetricsOverride', { width: w, height: vh, deviceScaleFactor: 1, mobile: w < 500 })
        return
      }
      const r = await send('Page.captureScreenshot', { format: 'png', clip })
      writeFileSync(file, Buffer.from(r.data, 'base64'))
    },
    async close() { ws.close(); proc.kill('SIGTERM') },
  }
  return b
}
