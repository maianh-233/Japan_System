import { Tooltip } from 'antd';
import { NavLink } from 'react-router-dom';
import { cn } from '@/lib/cn';
import type { NavItem } from '@/config/navigation';

interface SidebarNavProps {
  items: NavItem[];
  /** Thu gọn: chỉ icon + tooltip */
  collapsed?: boolean;
  onNavigate?: () => void;
}

/** Danh sách link sidebar: icon + tên, mục đang mở được tô nổi. */
export function SidebarNav({ items, collapsed = false, onNavigate }: SidebarNavProps) {
  return (
    <ul className="flex flex-col gap-0.5">
      {items.map((it) => {
        const Icon = it.icon;
        const link = (
          <NavLink
            to={it.path}
            onClick={onNavigate}
            aria-label={collapsed ? it.label : undefined}
            className={({ isActive }) =>
              cn(
                'group relative flex h-10 items-center gap-3 rounded-lg text-sm font-medium transition-colors',
                'focus-visible:ring-[3px] focus-visible:ring-primary-500/30 focus-visible:outline-none',
                collapsed ? 'justify-center px-0' : 'px-3',
                isActive ? 'bg-primary-100 text-primary-700' : 'text-ink-secondary hover:bg-primary-50 hover:text-ink',
              )
            }
          >
            {({ isActive }) => (
              <>
                {isActive && !collapsed && <span className="absolute top-2 bottom-2 left-0 w-[3px] rounded-r bg-primary-600" aria-hidden />}
                <Icon size={18} aria-hidden className={cn('shrink-0', isActive ? 'text-primary-600' : 'text-ink-muted group-hover:text-ink-secondary')} />
                {!collapsed && (
                  <span className="truncate" title={it.label}>
                    {it.label}
                  </span>
                )}
                {!collapsed && it.isNew && (
                  <span className="ml-auto shrink-0 rounded-md bg-success-50 px-1.5 py-0.5 text-[11px] leading-4 font-semibold text-success-700">
                    Mới
                  </span>
                )}
                {collapsed && it.isNew && <span className="absolute top-2 right-3 h-2 w-2 rounded-full bg-success-600" aria-hidden />}
              </>
            )}
          </NavLink>
        );
        return (
          <li key={it.key}>
            {collapsed ? (
              <Tooltip title={it.label} placement="right">
                {link}
              </Tooltip>
            ) : (
              link
            )}
          </li>
        );
      })}
    </ul>
  );
}
