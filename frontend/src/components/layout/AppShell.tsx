import { useEffect, useState, type ReactNode } from 'react';
import { Drawer } from 'antd';
import { Menu, PanelLeftClose, PanelLeftOpen } from 'lucide-react';
import { NavLink, Outlet, useLocation } from 'react-router-dom';
import { cn } from '@/lib/cn';
import { useAuth } from '@/hooks/useAuth';
import { Button } from '@/components/common';
import { findNavItem, type NavItem, type NavModule } from '@/config/navigation';
import { Logo, SYSTEM_NAME } from './Logo';
import { NotificationBell } from './NotificationBell';
import { useLogout, UserMenu } from './UserMenu';
import { ForbiddenView } from './ForbiddenView';
import { SidebarNav as SidebarNavList } from './SidebarNav';

export interface AppShellProps {
  /** Module trên header (chỉ cổng nhân viên) */
  modules?: NavModule[];
  /** Key module đang mở */
  activeModuleKey?: string;
  /** Mục ở sidebar */
  sidebarItems: NavItem[];
  /** Tiêu đề nhóm trên sidebar (tên module) */
  sidebarTitle?: string;
  /** Dòng phụ dưới logo (tên chi nhánh — cổng GV/HV) */
  logoSubtitle?: string;
  /** Vùng bên phải header trước chuông (vd: chọn chi nhánh) */
  headerExtra?: ReactNode;
  /** Thêm vào đầu drawer mobile (vd: chọn chi nhánh) */
  drawerExtra?: ReactNode;
}

const COLLAPSE_KEY = 'sidebar-collapsed';

function readCollapsed(): boolean {
  try {
    return localStorage.getItem(COLLAPSE_KEY) === '1';
  } catch {
    return false;
  }
}

/**
 * Khung chung cho cả 3 cổng:
 *   Header (logo · [module] · [extra] · chuông · người dùng · đăng xuất)
 *   Sidebar (mục của module / menu cổng) — thu gọn được
 *   Main (Outlet)
 * Màn < 1024px: sidebar + module chuyển vào drawer mở bằng nút ☰.
 */
export function AppShell({
  modules,
  activeModuleKey,
  sidebarItems,
  sidebarTitle,
  logoSubtitle,
  headerExtra,
  drawerExtra,
}: AppShellProps) {
  const { can, user } = useAuth();
  const { pathname } = useLocation();
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [collapsed, setCollapsed] = useState(readCollapsed);
  const { logout, confirmDialog } = useLogout();
  const hasModuleBar = Boolean(modules?.length);

  // Tiêu đề tab trình duyệt theo trang đang mở
  const current = findNavItem(pathname);
  const pageLabel = current?.item.label;
  useEffect(() => {
    document.title = pageLabel ? `${pageLabel} · ${SYSTEM_NAME}` : SYSTEM_NAME;
  }, [pageLabel]);

  useEffect(() => setDrawerOpen(false), [pathname]);

  const toggleCollapsed = () =>
    setCollapsed((c) => {
      try {
        localStorage.setItem(COLLAPSE_KEY, c ? '0' : '1');
      } catch {
        /* bỏ qua */
      }
      return !c;
    });

  // Trang thuộc menu nhưng không có quyền → 403 (kiểm tra tập trung tại đây)
  const forbidden = current?.item.permission ? !can(current.item.permission) : false;

  const moduleLinks = (variant: 'bar' | 'grid') =>
    modules?.map((m) => {
      const Icon = m.icon;
      const active = m.key === activeModuleKey;
      return (
        <NavLink
          key={m.key}
          to={m.items[0].path}
          aria-current={active ? 'page' : undefined}
          className={cn(
            'flex shrink-0 items-center gap-2 font-medium transition-colors focus-visible:ring-[3px] focus-visible:ring-primary-500/30 focus-visible:outline-none',
            variant === 'bar'
              ? cn(
                  'relative h-12 px-3 text-sm',
                  active
                    ? 'text-primary-700 after:absolute after:inset-x-2 after:bottom-0 after:h-[3px] after:rounded-t after:bg-primary-600'
                    : 'text-ink-secondary hover:text-ink',
                )
              : cn(
                  'flex-col justify-center gap-1 rounded-lg border px-2 py-3 text-[13px]',
                  active ? 'border-primary-300 bg-primary-100 text-primary-700' : 'border-line text-ink-secondary hover:bg-primary-50',
                ),
          )}
        >
          <Icon size={variant === 'bar' ? 18 : 20} aria-hidden className={active ? 'text-primary-600' : undefined} />
          <span className="whitespace-nowrap">{m.label}</span>
        </NavLink>
      );
    });

  const headerHeight = hasModuleBar ? 'lg:top-[112px] lg:h-[calc(100dvh-112px)]' : 'lg:top-16 lg:h-[calc(100dvh-64px)]';

  return (
    <div className="flex min-h-dvh flex-col bg-page">
      {/* ================= HEADER ================= */}
      <header className="sticky top-0 z-30 border-b border-line bg-surface/95 backdrop-blur">
        <div className="flex h-16 items-center gap-2 px-3 sm:gap-3 sm:px-4 lg:px-6">
          <Button icon={Menu} iconOnly tone="neutral" variant="ghost" aria-label="Mở menu" className="lg:hidden" onClick={() => setDrawerOpen(true)} />
          <NavLink to={sidebarItems[0]?.path ?? '/'} className="min-w-0 rounded-lg focus-visible:ring-[3px] focus-visible:ring-primary-500/30 focus-visible:outline-none">
            <Logo subtitle={logoSubtitle} />
          </NavLink>
          <div className="flex-1" />
          {headerExtra && <div className="hidden md:block">{headerExtra}</div>}
          {user && <NotificationBell portal={user.portal} />}
          <div className="mx-1 hidden h-8 w-px bg-line sm:block" aria-hidden />
          <UserMenu />
        </div>

        {/* Thanh module (cổng nhân viên) */}
        {hasModuleBar && (
          <nav aria-label="Module" className="hidden h-12 items-stretch gap-1 overflow-x-auto overflow-y-hidden border-t border-line px-3 [scrollbar-width:thin] lg:flex lg:px-4">
            {moduleLinks('bar')}
          </nav>
        )}
      </header>

      <div className="flex flex-1">
        {/* ================= SIDEBAR ================= */}
        <aside
          aria-label="Menu"
          className={cn(
            'sticky hidden shrink-0 flex-col border-r border-line bg-surface transition-[width] duration-200 lg:flex',
            headerHeight,
            collapsed ? 'w-[72px]' : 'w-64',
          )}
        >
          <div className={cn('flex items-center px-3 pt-4 pb-2', collapsed ? 'justify-center' : 'justify-between')}>
            {!collapsed && sidebarTitle && (
              <span className="truncate px-2 text-xs font-semibold tracking-wide text-ink-muted uppercase">{sidebarTitle}</span>
            )}
            <Button
              icon={collapsed ? PanelLeftOpen : PanelLeftClose}
              iconOnly
              size="sm"
              tone="neutral"
              variant="ghost"
              aria-label={collapsed ? 'Mở rộng menu' : 'Thu gọn menu'}
              onClick={toggleCollapsed}
            />
          </div>
          <nav className="flex-1 overflow-y-auto px-3 pb-4">
            <SidebarNavList items={sidebarItems} collapsed={collapsed} />
          </nav>
        </aside>

        {/* ================= MAIN ================= */}
        <main id="main" className="min-w-0 flex-1 px-4 py-5 sm:px-6 lg:px-8 lg:py-6">
          <div className="mx-auto w-full max-w-[1440px]">{forbidden ? <ForbiddenView /> : <Outlet />}</div>
        </main>
      </div>

      {/* ================= DRAWER (mobile/tablet) ================= */}
      <Drawer
        open={drawerOpen}
        onClose={() => setDrawerOpen(false)}
        placement="left"
        width={300}
        title={<Logo subtitle={logoSubtitle} />}
        styles={{ body: { padding: 12 } }}
        footer={<Button action="logout" block onClick={logout} />}
      >
        <div className="flex flex-col gap-4">
          {drawerExtra && <div className="md:hidden">{drawerExtra}</div>}
          {hasModuleBar && (
            <div>
              <p className="mb-2 px-1 text-xs font-semibold tracking-wide text-ink-muted uppercase">Module</p>
              <nav aria-label="Module" className="grid grid-cols-3 gap-2">
                {moduleLinks('grid')}
              </nav>
            </div>
          )}
          <div>
            {sidebarTitle && <p className="mb-2 px-1 text-xs font-semibold tracking-wide text-ink-muted uppercase">{sidebarTitle}</p>}
            <SidebarNavList items={sidebarItems} onNavigate={() => setDrawerOpen(false)} />
          </div>
        </div>
      </Drawer>
      {confirmDialog}
    </div>
  );
}

