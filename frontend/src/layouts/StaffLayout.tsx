import { useMemo } from 'react';
import { useLocation } from 'react-router-dom';
import { AppShell } from '@/components/layout/AppShell';
import { BranchSwitcher } from '@/components/layout/BranchSwitcher';
import { STAFF_MODULES } from '@/config/navigation';
import { useAuth } from '@/hooks/useAuth';
import { MOCK_NOTIFICATIONS } from '@/mocks/data/notifications';

/** Cổng nhân viên: module trên header, mục con của module ở sidebar, chọn chi nhánh. */
export default function StaffLayout() {
  const { can } = useAuth();
  const { pathname } = useLocation();

  // Chỉ giữ module/mục người dùng có quyền
  const modules = useMemo(
    () =>
      STAFF_MODULES.map((m) => ({ ...m, items: m.items.filter((i) => can(i.permission)) })).filter((m) => m.items.length > 0),
    [can],
  );

  const moduleKey = pathname.split('/')[2];
  const active = modules.find((m) => m.key === moduleKey) ?? modules[0];

  return (
    <AppShell
      modules={modules}
      activeModuleKey={active?.key}
      sidebarItems={active?.items ?? []}
      sidebarTitle={active?.label}
      headerExtra={<BranchSwitcher />}
      drawerExtra={<BranchSwitcher className="w-full max-w-none" />}
      notifications={MOCK_NOTIFICATIONS.staff}
    />
  );
}
