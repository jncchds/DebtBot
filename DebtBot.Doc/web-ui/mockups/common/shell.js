// Shared mockup shell (sidebar from mockups/shell). Query: theme=dark, phone=1|open, state=<view state>.
// A page sets window.MOCK = { page: 'debts'|'bills', ctx: [...], render(state, phone) -> html } before loading this file.
const q = new URLSearchParams(location.search)
const NAV = [
  ['debts', '💸', 'Debts'], ['bills', '🧾', 'Bills'], ['new', '➕', 'New bill'], ['exchange', '🔁', 'Exchange'],
  ['spendings', '📊', 'Spendings'], ['contacts', '👥', 'Contacts'], ['subscriptions', '🔔', 'Subscriptions'], ['profile', '👤', 'Profile'], ['admin', '🛠', 'Admin'],
]
// Optional nav badges: MOCK.badges = { subscriptions: 1, admin: 3 } (hidden on the collapsed phone rail).
const navBadge = (k) => MOCK.badges?.[k] && q.get('phone') !== '1' ? `<span class="count">${MOCK.badges[k]}</span>` : ''
const phone = q.get('phone')
const state = q.get('state') ?? 'main'
const dark = q.get('theme') === 'dark'
if (dark) document.documentElement.classList.add('dark')

const ctxHtml = (MOCK.ctxFor ? MOCK.ctxFor(state) : MOCK.ctx).map((x) => {
  if (typeof x === 'string' && x.startsWith('S:')) return `<div class="section">${x.slice(2)}</div>`
  if (typeof x === 'string' && x.startsWith('A:')) return `<a class="sidebar-btn"><span class="s-icon">…</span><span class="s-label">${x.slice(2)}</span></a>`
  return `<div class="entry${x.a ? ' active' : ''}${x.w ? ' wrap' : ''}"><span class="i">${x.i}</span><span class="c"><span class="t">${x.t}</span><span class="s">${x.s}</span></span></div>`
}).join('')

document.body.innerHTML = `
<div class="app${phone ? ' phone' : ''}">
  <aside class="${phone === 'open' ? 'open' : phone ? 'collapsed' : ''}">
    <div class="top"><button class="sidebar-btn"><span class="s-icon">☰</span><span class="s-label" style="font-weight:600">DebtBot</span><span class="version-pill">v1.0.0</span></button></div>
    <nav>
      ${NAV.map(([k, i, l]) => `<a class="sidebar-btn${k === MOCK.page ? ' active' : ''}"><span class="s-icon">${i}</span><span class="s-label">${l}</span>${navBadge(k)}</a>`).join('')}
      <div class="ctx"><div class="divider"></div>${ctxHtml}</div>
    </nav>
    <div class="bottom">
      <button class="sidebar-btn"><span class="s-icon">🚪</span><span class="s-label">Sign out (Anna Petrova)</span></button>
      <button class="sidebar-btn"><span class="s-icon">${dark ? '🌙' : '☀️'}</span><span class="s-label">Theme: ${dark ? 'dark' : 'light'}</span></button>
    </div>
  </aside>
  ${phone === 'open' ? '<div class="backdrop"></div>' : ''}
  <main class="main"><div class="inner">${MOCK.render(state, !!phone)}</div></main>
</div>`
