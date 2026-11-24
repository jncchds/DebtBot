import type { ReactNode } from 'react'
import { NavLink } from 'react-router'
import clsx from 'clsx'

/** Hides sidebar text when the sidebar is collapsed to the icon rail. */
export function Label({ collapsed, children }: { collapsed: boolean; children: ReactNode }) {
  return <span className={clsx('min-w-0 flex-1 truncate', collapsed && 'w-0 opacity-0')}>{children}</span>
}

export function Icon({ children }: { children: ReactNode }) {
  return (
    <span className="w-[22px] shrink-0 text-center" aria-hidden>
      {children}
    </span>
  )
}

export function SidebarDivider() {
  return <div className="mx-0.5 my-1 h-px bg-line" />
}

export function SidebarSection({ children }: { children: ReactNode }) {
  return <div className="sidebar-section">{children}</div>
}

/** A page's own entry below the divider: icon, bold title, muted subtitle, optional badge (abeat style). */
export function SidebarEntry({ to, icon, title, subtitle, badge, end = true, wrap, active }: { to: string; icon: string; title: string; subtitle?: ReactNode; badge?: number; end?: boolean; /** Let the subtitle wrap instead of cutting it off (amounts, rates). */ wrap?: boolean; /** Overrides the highlight (when more than one path belongs to the entry). */ active?: boolean }) {
  return (
    <NavLink to={to} end={end} title={title} className={({ isActive }) => clsx('sidebar-entry', (active ?? isActive) && 'bg-accent-faint text-accent')}>
      <span className="shrink-0 text-[0.85rem]" aria-hidden>
        {icon}
      </span>
      <span className="min-w-0 flex-1">
        <span className="block truncate font-semibold">{title}</span>
        {subtitle && <span className={clsx('mt-px block text-[0.76rem] text-muted', wrap ? 'break-words' : 'truncate')}>{subtitle}</span>}
      </span>
      {!!badge && <span className="ml-auto shrink-0 rounded-full bg-accent px-1.5 text-[0.68rem] leading-normal font-bold text-accent-contrast">{badge}</span>}
    </NavLink>
  )
}

/** "… All bills" style link at the end of a list. */
export function SidebarMore({ to, label }: { to: string; label: string }) {
  return (
    <NavLink to={to} end className="sidebar-btn">
      <Icon>…</Icon>
      <span className="min-w-0 flex-1 truncate">{label}</span>
    </NavLink>
  )
}
