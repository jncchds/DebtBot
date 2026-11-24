---
name: web-ui-palette
description: DebtBot web UI uses the home-family nopds/abook style with the lime-on-graphite palette chosen on 2026-11-23
metadata:
  node_type: memory
  type: project
  originSessionId: b85fe091-00b4-4715-be20-d611e12ba132
  modified: 2026-11-23T16:24:48.880Z
---

DebtBot's web UI follows the user's cross-project home style: nopds/abook sidebar shell, flat cards and pills, Inter, and nopds CSS tokens on Tailwind 4. The cross-project rule lives in `~/.claude/projects/-mnt-git-ANalyze/memory/web-visual-style.md`.

On 2026-11-23 the user chose **Lime on graphite** from four rendered image options.
- **Light:** bg `#f6f8f1`, surface `#ffffff`, border `#dfe5d3`, text `#1c2416`, muted `#5f6b55`, accent `#4d7c0f`, accent-contrast `#f7fee7`.
- **Dark:** bg `#121510`, surface `#1b2017`, border `#2c3424`, text `#e6edd9`, muted `#9aa68c`, accent `#a3e635`, accent-contrast `#14200a`.
- "You owe" uses danger (`#b42318` light / `#f07167` dark) and "owed to you" uses success (`#2f7a45` / `#6fcf8a`).

**Why:** the user's apps should look like one family, but each one needs its own palette.
**How to apply:** use these tokens in `DebtBot/ClientApp/src/index.css` and don't introduce new colours. The approved plan is in `~/.claude/plans/i-want-to-extend-unified-quail-of-b85fe091-00b4-4715-be20-d611e12ba132.md`.
