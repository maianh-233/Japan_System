import { AppShell } from '@/components/layout/AppShell';
import { PARENT_MENU, STUDENT_MENU, TEACHER_MENU } from '@/config/navigation';
import { useAuth } from '@/hooks/useAuth';

/** Cổng giáo viên: header = logo + tên chi nhánh, chuông, người dùng, đăng xuất. */
export function TeacherLayout() {
  const { currentBranch } = useAuth();
  return (
    <AppShell
      sidebarItems={TEACHER_MENU}
      sidebarTitle="Giảng dạy"
      logoSubtitle={currentBranch ? `Chi nhánh ${currentBranch.name}` : undefined}
    />
  );
}

/** Cổng học viên: giống cổng giáo viên, menu khác. */
export function StudentLayout() {
  const { currentBranch } = useAuth();
  return (
    <AppShell
      sidebarItems={STUDENT_MENU}
      sidebarTitle="Học tập"
      logoSubtitle={currentBranch ? `Chi nhánh ${currentBranch.name}` : undefined}
    />
  );
}

/** Cổng phụ huynh: xem lịch, chuyên cần, kết quả, học phí của con. */
export function ParentLayout() {
  const { currentBranch } = useAuth();
  return (
    <AppShell
      sidebarItems={PARENT_MENU}
      sidebarTitle="Theo dõi học tập"
      logoSubtitle={currentBranch ? `Chi nhánh ${currentBranch.name}` : undefined}
    />
  );
}
