import { useMemo } from 'react';
import { useLocation } from 'react-router-dom';
import { AppShell } from '@/components/layout/AppShell';
import { BranchSwitcher } from '@/components/layout/BranchSwitcher';
import { GlobalSearch } from '@/components/layout/GlobalSearch';
import { STAFF_MODULES } from '@/config/navigation';
import { useSlaWatcher } from '@/features/crm/admission/slaWatch';
import { useAppointmentReminders } from '@/features/crm/appointments/appointmentWatch';
import { useMyReports } from '@/features/me/useMe';
import { useAuth } from '@/hooks/useAuth';

/** Cổng nhân viên: module trên header, mục con của module ở sidebar, chọn chi nhánh. */
export default function StaffLayout() {
  const { can } = useAuth();
  const { pathname } = useLocation();
  const hasReports = useMyReports().length > 0;
  // Chế độ mock: giả lập job phía server — cảnh báo / tự chuyển lead quá SLA (chế độ API: server làm)
  useSlaWatcher();
  // Giả lập job: nhắc lịch hẹn tư vấn / test trước 1 giờ
  useAppointmentReminders();

  // Chỉ giữ module/mục người dùng có quyền
  const modules = useMemo(
    () =>
      STAFF_MODULES.map((m) => ({
        ...m,
        // "Giao việc cấp dưới" chỉ hiện với người có nhân viên dưới quyền
        items: m.items.filter((i) => can(i.permission) && (i.key !== 'team-tasks' || hasReports)),
      })).filter((m) => m.items.length > 0),
    [can, hasReports],
  );

  const moduleKey = pathname.split('/')[2];
  const active = modules.find((m) => m.key === moduleKey) ?? modules[0];

  return (
    <AppShell
      modules={modules}
      activeModuleKey={active?.key}
      sidebarItems={active?.items ?? []}
      sidebarTitle={active?.label}
      headerSearch={<GlobalSearch />}
      headerExtra={<BranchSwitcher />}
      drawerExtra={<BranchSwitcher className="w-full max-w-none" />}
    />
  );
}
