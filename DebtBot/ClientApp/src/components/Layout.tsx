import { useEffect, useState } from 'react'
import { NavLink, Outlet, useLocation, useNavigate } from 'react-router'
import { useTranslation } from 'react-i18next'
import clsx from 'clsx'
import { useContacts } from '../api/bills'
import { useAuth } from '../auth/AuthContext'
import { pendingRequestCount } from '../lib/contacts'
import { THEMES, useTheme } from '../hooks/useTheme'
import { Icon, Label, SidebarDivider } from './Sidebar'
import { subNavFor } from './subnav'

const COLLAPSE_KEY = 'debtbot.sidebar'
const PHONE = '(max-width: 767px)'

function initialCollapsed() {
  try {
    const v = localStorage.getItem(COLLAPSE_KEY)
    if (v !== null) return v === '1'
  } catch {
    /* storage unavailable */
  }
  // No saved choice: start collapsed on phones, where the open sidebar covers the page.
  return window.matchMedia(PHONE).matches
}

interface NavItem {
  to: string
  icon: string
  labelKey: string
  /** Extra check for prefixes another item owns (Bills vs New bill). */
  isActive?: (pathname: string) => boolean
  adminOnly?: boolean
  /** Shows a count badge (accent pill) when above 0. */
  badge?: 'requests'
}

const NAV: NavItem[] = [
  { to: '/', icon: '💸', labelKey: 'nav.debts', isActive: (p) => p === '/' },
  { to: '/bills', icon: '🧾', labelKey: 'nav.bills', isActive: (p) => p.startsWith('/bills') && !p.startsWith('/bills/new') },
  { to: '/bills/new', icon: '➕', labelKey: 'nav.newBill' },
  { to: '/exchange', icon: '🔁', labelKey: 'nav.exchange' },
  { to: '/spendings', icon: '📊', labelKey: 'nav.spendings' },
  { to: '/contacts', icon: '👥', labelKey: 'nav.contacts' },
  { to: '/subscriptions', icon: '🔔', labelKey: 'nav.subscriptions', badge: 'requests' },
  { to: '/profile', icon: '👤', labelKey: 'nav.profile' },
  { to: '/admin', icon: '🛠', labelKey: 'nav.admin', adminOnly: true },
]

/** App shell as in nopds/abook: collapsible sidebar; on phones an icon rail whose open state overlays the page. */
export function Layout() {
  const { t } = useTranslation()
  const { user, isAdmin, signOut } = useAuth()
  const { theme, setTheme } = useTheme()
  const [collapsed, setCollapsed] = useState(initialCollapsed)
  const navigate = useNavigate()
  const { pathname } = useLocation()
  const subNav = subNavFor(pathname)
  // People waiting for you to confirm a subscription (the 🔔 Subscriptions badge).
  const requests = pendingRequestCount(useContacts().data)
  const [shownPath, setShownPath] = useState(pathname)

  useEffect(() => {
    try {
      localStorage.setItem(COLLAPSE_KEY, collapsed ? '1' : '0')
    } catch {
      /* storage unavailable */
    }
  }, [collapsed])

  // On phones the open sidebar overlays the page; close it after navigating (also on Back/Forward).
  if (shownPath !== pathname) {
    setShownPath(pathname)
    if (window.matchMedia(PHONE).matches) setCollapsed(true)
  }

  const nextTheme = THEMES[(THEMES.indexOf(theme) + 1) % THEMES.length]
  const themeLabel = `${t('theme.label')}: ${t(`theme.${theme}`)}`

  return (
    <div className="flex h-dvh overflow-hidden">
      <a href="#main" className="sr-only focus:not-sr-only focus:absolute focus:top-2 focus:left-2 focus:z-50 btn-primary">
        {t('nav.skip')}
      </a>
      <aside
        className={clsx(
          'z-30 flex shrink-0 flex-col overflow-hidden border-r border-line bg-surface transition-[width] duration-200',
          collapsed ? 'w-12' : 'w-[min(75vw,15rem)] max-md:absolute max-md:inset-y-0 max-md:left-0 max-md:shadow-2xl',
        )}
      >
        <div className="shrink-0 px-1 pt-1.5 pb-1">
          <button className="sidebar-btn" onClick={() => setCollapsed((v) => !v)} title={t('nav.menu')} aria-expanded={!collapsed}>
            <Icon>☰</Icon>
            <Label collapsed={collapsed}>
              <span className="font-semibold">DebtBot</span>
            </Label>
            {!collapsed && (
              <a
                href="https://github.com/jncchds/DebtBot"
                target="_blank"
                rel="noopener noreferrer"
                onClick={(e) => e.stopPropagation()}
                className="shrink-0 rounded-full border border-accent/25 bg-accent-faint px-1.5 text-[0.65rem] leading-relaxed text-muted no-underline hover:text-accent"
              >
                v{__APP_VERSION__}
              </a>
            )}
          </button>
        </div>

        <nav className="flex-1 overflow-x-hidden overflow-y-auto px-1 py-0.5" aria-label={t('nav.main')}>
          {NAV.filter((n) => !n.adminOnly || isAdmin).map((n) => {
            const label = t(n.labelKey)
            return (
              <NavLink
                key={n.to}
                to={n.to}
                end={n.to === '/bills/new'}
                title={label}
                className={({ isActive }) => clsx('sidebar-btn', (n.isActive ? n.isActive(pathname) : isActive) && 'bg-accent-faint text-accent')}
              >
                <Icon>{n.icon}</Icon>
                <Label collapsed={collapsed}>{label}</Label>
                {n.badge === 'requests' && requests > 0 && !collapsed && (
                  <span className="ml-auto shrink-0 rounded-full bg-accent px-1.5 text-[0.68rem] leading-normal font-bold text-accent-contrast" aria-label={t('nav.requestsBadge', { count: requests })}>
                    {requests}
                  </span>
                )}
              </NavLink>
            )
          })}
          {subNav && !collapsed && (
            <>
              <SidebarDivider />
              {subNav}
            </>
          )}
        </nav>

        <div className="shrink-0 border-t border-line px-1 pt-1 pb-2">
          <button className="sidebar-btn" onClick={() => signOut().then(() => navigate('/login'))} title={t('auth.signOut')}>
            <Icon>🚪</Icon>
            <Label collapsed={collapsed}>{t('auth.signOutAs', { name: user?.displayName ?? '' })}</Label>
          </button>
          <button className="sidebar-btn" onClick={() => setTheme(nextTheme)} title={themeLabel}>
            <Icon>{theme === 'dark' ? '🌙' : theme === 'light' ? '☀️' : '◐'}</Icon>
            <Label collapsed={collapsed}>{themeLabel}</Label>
          </button>
        </div>
      </aside>

      {!collapsed && <div className="fixed inset-0 z-20 bg-black/40 md:hidden" onClick={() => setCollapsed(true)} />}

      <main id="main" className={clsx('min-w-0 flex-1 overflow-y-auto bg-bg px-4 py-6 md:px-8', !collapsed && 'max-md:ml-12')}>
        <div className="mx-auto max-w-6xl">
          <Outlet />
        </div>
      </main>
    </div>
  )
}
