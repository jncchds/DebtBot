// The shared list pattern (Bills, Debts, Spendings, Exchanges):
// a header card with filters and "Sort by", then a paged list: a table when the page is wide enough, cards on phones.
// list({ filters, activeCount, sort, columns, rows, card, pager, phone, open, empty })
function list(o) {
  const filters = o.filters.map((f) => {
    if (f.type === 'select') return `<div class="f"><label class="label">${f.label}</label><select class="input${f.on ? ' on' : ''}"><option>${f.value}</option></select></div>`
    if (f.type === 'seg') return `<div class="f f-seg"><label class="label">${f.label}</label><div class="seg">${f.options.map((x) => `<span class="opt${x === f.value ? ' on' : ''}">${x}</span>`).join('')}</div></div>`
    if (f.type === 'text') return `<div class="f f-wide"><label class="label">${f.label}</label><input class="input${f.on ? ' on' : ''}" placeholder="${f.placeholder ?? ''}" value="${f.value ?? ''}"></div>`
    if (f.type === 'date') return `<div class="f f-date"><label class="label">${f.label}</label><input class="input${f.on ? ' on' : ''}" value="${f.value ?? ''}" placeholder="dd.mm.yyyy"></div>`
    if (f.type === 'check') return `<label class="check"><span class="box${f.on ? ' on' : ''}">${f.on ? '✓' : ''}</span>${f.label}</label>`
    return ''
  }).join('')
  const sort = `<div class="sortby"><span class="label" style="margin:0">Sort by</span>
      <select class="input sort-sel"><option>${o.sort.field}</option></select>
      <a class="btn btn-secondary btn-sm" title="${o.sort.dir.slice(2)}">${o.phone ? o.sort.dir.split(' ')[0] : o.sort.dir}</a></div>`
  const clear = o.activeCount ? '<a class="btn btn-ghost btn-sm">✕ Clear filters</a>' : ''
  const header = o.phone
    ? `<div class="card lhead">
        <div class="lhead-top"><a class="btn btn-secondary btn-sm">🔍 Filters${o.activeCount ? ` <span class="count">${o.activeCount}</span>` : ''} ${o.open ? '▴' : '▾'}</a><span class="spacer"></span>${sort}</div>
        ${o.open ? `<div class="filters">${filters}</div><div class="row" style="padding:0 1rem .75rem">${clear}</div>` : ''}
      </div>`
    : `<div class="card lhead"><div class="filters">${filters}</div><div class="lhead-foot">${sort}<span class="spacer"></span>${clear}</div></div>`
  if (!o.rows.length) return header + o.empty
  const body = o.phone
    ? `<div class="card">${o.rows.map((r) => `<div class="item${r.hl ? ' hl' : ''}">${o.card(r)}</div>`).join('')}</div>`
    : `<div class="card"><table class="tbl"><thead><tr>${o.columns.map((c) => `<th class="${c.cls ?? ''}"${c.w ? ` style="width:${c.w}"` : ''}>${c.label}${c.sorted ? ` <span class="sorted">${c.sorted}</span>` : ''}</th>`).join('')}</tr></thead>
        <tbody>${o.rows.map((r) => `<tr${r.hl ? ' class="hl"' : ''}>${o.columns.map((c) => `<td class="${c.cls ?? ''}">${c.cell(r)}</td>`).join('')}</tr>`).join('')}</tbody></table></div>`
  const p = o.pager
  const pager = o.phone
    ? `<div class="pager"><span class="muted">${p.range}</span><span class="spacer"></span><a class="pg off">‹</a><span>Page 1 of ${p.pages}</span><a class="pg">›</a></div>`
    : `<div class="pager"><span class="muted">${p.range}</span><span class="spacer"></span><a class="pg off">‹</a><a class="pg on">1</a>${p.pages > 1 ? '<a class="pg">2</a>' : ''}${p.pages > 2 ? '<a class="pg">3</a>' : ''}${p.pages > 4 ? '<span class="muted">…</span>' : ''}${p.pages > 3 ? `<a class="pg">${p.pages}</a>` : ''}<a class="pg${p.pages > 1 ? '' : ' off'}">›</a></div>`
  return header + body + pager
}
