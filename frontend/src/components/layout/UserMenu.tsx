import { useMemo } from 'react';
import { Dropdown } from 'antd';
import { ChevronDown, Clock, KeyRound, ListChecks, LogOut, UserRound, type LucideIcon } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { Avatar, useConfirm } from '@/components/common';
import { useMe } from '@/features/me/useMe';
import { useWorkStore } from '@/features/work/workStore';
import { useAuth } from '@/hooks/useAuth';
import { cn } from '@/lib/cn';

/** Đăng xuất có hộp xác nhận */
export function useLogout() {
  const { signOut } = useAuth();
  const navigate = useNavigate();
  const { confirm, confirmDialog } = useConfirm();
  const logout = async () => {
    const ok = await confirm({
      title: 'Đăng xuất khỏi hệ thống?',
      description: 'Bạn sẽ cần đăng nhập lại để tiếp tục làm việc.',
      tone: 'primary',
      confirmAction: 'logout',
      confirmText: 'Đăng xuất',
    });
    if (ok) {
      signOut();
      navigate('/login', { replace: true });
    }
  };
  return { logout, confirmDialog };
}

/** Đường dẫn trang cá nhân theo cổng */
const LINKS: Record<string, { key: string; label: string; icon: LucideIcon; path: string }[]> = {
  staff: [
    { key: 'profile', label: 'Thông tin cá nhân', icon: UserRound, path: '/admin/me/profile' },
    { key: 'attendance', label: 'Chấm công của tôi', icon: Clock, path: '/admin/me/attendance' },
    { key: 'tasks', label: 'Việc của tôi', icon: ListChecks, path: '/admin/me/tasks' },
    { key: 'security', label: 'Mật khẩu & bảo mật', icon: KeyRound, path: '/admin/me/security' },
  ],
  teacher: [
    { key: 'profile', label: 'Thông tin cá nhân', icon: UserRound, path: '/teacher/profile' },
    { key: 'timesheet', label: 'Chấm công', icon: Clock, path: '/teacher/timesheet' },
    { key: 'security', label: 'Mật khẩu & bảo mật', icon: KeyRound, path: '/teacher/security' },
  ],
  student: [
    { key: 'profile', label: 'Thông tin cá nhân', icon: UserRound, path: '/student/profile' },
    { key: 'security', label: 'Mật khẩu & bảo mật', icon: KeyRound, path: '/student/security' },
  ],
  parent: [
    { key: 'profile', label: 'Thông tin cá nhân', icon: UserRound, path: '/parent/profile' },
    { key: 'security', label: 'Mật khẩu & bảo mật', icon: KeyRound, path: '/parent/security' },
  ],
};

/** Ảnh + tên + vai trò người dùng. Bấm → menu: thông tin cá nhân, chấm công, việc của tôi, đổi mật khẩu, đăng xuất. */
export function UserMenu() {
  const { user } = useAuth();
  const navigate = useNavigate();
  const me = useMe();
  const tasks = useWorkStore((s) => s.tasks);
  const { logout, confirmDialog } = useLogout();
  const openTasks = useMemo(() => (me ? tasks.filter((t) => t.assigneeId === me.id && t.status !== 'done').length : 0), [tasks, me]);
  if (!user) return null;

  const links = LINKS[user.portal] ?? [];
  const items = [
    ...links.map((l) => ({
      key: l.key,
      icon: <l.icon size={16} aria-hidden />,
      label: (
        <span className="flex items-center justify-between gap-6">
          {l.label}
          {l.key === 'tasks' && openTasks > 0 && <span className="tabular rounded-full bg-primary-600 px-1.5 text-[11px] font-semibold text-white">{openTasks}</span>}
        </span>
      ),
      onClick: () => navigate(l.path),
    })),
    { type: 'divider' as const },
    { key: 'logout', icon: <LogOut size={16} aria-hidden />, label: 'Đăng xuất', danger: true, onClick: logout },
  ];

  return (
    <>
      <Dropdown
        trigger={['click']}
        placement="bottomRight"
        menu={{ items }}
        popupRender={(menu) => (
          <div className="min-w-64 overflow-hidden rounded-xl border border-line bg-surface shadow-lg">
            <div className="flex items-center gap-3 border-b border-line px-4 py-3">
              <Avatar name={user.fullName} src={user.avatarUrl} size="md" />
              <div className="min-w-0">
                <p className="truncate font-semibold text-ink">{user.fullName}</p>
                <p className="truncate text-xs text-ink-muted">{user.email}</p>
                <p className="truncate text-xs text-primary-700">{user.roleName}</p>
              </div>
            </div>
            <div className="[&_.ant-dropdown-menu]:!shadow-none">{menu}</div>
          </div>
        )}
      >
        <button type="button" className={cn('flex items-center gap-2.5 rounded-lg px-1.5 py-1 transition-colors hover:bg-canvas focus-visible:ring-[3px] focus-visible:ring-primary-500/30 focus-visible:outline-none')} aria-label="Menu tài khoản">
          <Avatar name={user.fullName} src={user.avatarUrl} size="md" status="online" />
          <span className="hidden min-w-0 flex-col text-left leading-tight md:flex">
            <span className="max-w-[160px] truncate text-sm font-semibold text-ink">{user.fullName}</span>
            <span className="max-w-[160px] truncate text-xs text-ink-muted">{user.roleName}</span>
          </span>
          <ChevronDown size={14} className="hidden text-ink-muted md:block" aria-hidden />
        </button>
      </Dropdown>
      {confirmDialog}
    </>
  );
}
