import { useEffect, useState } from 'react';
import { USE_API } from '@/lib/api/config';
import { onExpired } from '@/lib/api/http';
import { useAuthStore } from '@/stores/authStore';
import type { SessionUser } from '@/types/auth';
import { loadCourses } from '@/features/academic/courses/courseApi';
import { loadCurricula } from '@/features/academic/curriculum/curriculumApi';
import { loadClasses } from '@/features/academic/classes/classApi';
import { loadRooms } from '@/features/academic/rooms/roomApi';
import { loadStudents } from '@/features/student/studentApi';
import { loadCrm } from '@/features/crm/loadCrm';
import { useCrmStore } from '@/features/crm/crmStore';
import { hasPermission } from '@/lib/permission';
import { loadGuardians } from '@/features/student/guardians/guardianApi';
import { loadEmployees } from '@/features/hr/hrApi';
import { loadBranches } from '@/features/system/branchApi';
import { loadSystem, rememberAccount } from '@/features/system/systemApi';
import { loadLearningPortal, loadStaffRequests, loadTeacherPortal } from '@/features/portal/portalApi';
import { startRealtime } from '@/features/portal/realtime';
import { loadNotifications } from '@/features/notifications/notificationApi';
import { loadSettings } from '@/features/system/settingsApi';
import { restore } from './realAuth';

/** Việc làm ngay sau khi có phiên (đăng nhập / kích hoạt / tải lại trang) — chế độ API */
export async function onSignedIn(user: SessionUser & { accountId?: string }) {
  if (!USE_API) return;
  if (user.accountId) rememberAccount(user.accountId, user.id);
  // Lỗi tải dữ liệu không chặn việc vào hệ thống — trang tương ứng sẽ báo / dùng dữ liệu cũ
  const quiet = (p: Promise<unknown>) => p.catch(() => undefined);
  void startRealtime(user.portal, user.portalOwnerId);
  void quiet(loadNotifications(user.portal));
  // Cài đặt chung (SLA, giờ hành chính, mã hóa đơn, ngưỡng bỏ học…) — nhân viên & GV
  if (user.portal === 'staff' || user.portal === 'teacher') void quiet(loadSettings());
  // Cổng GV / HV / PH: chỉ dữ liệu của chủ tài khoản (lớp mình dạy / lớp của mình / của con)
  if (user.portal === 'teacher') return void (await quiet(loadTeacherPortal()));
  if (user.portal === 'student' || user.portal === 'parent') return void (await quiet(loadLearningPortal(user.portalOwnerId, user.portal === 'parent')));
  await Promise.all([quiet(loadSystem()), quiet(loadBranches()), quiet(loadEmployees()), quiet(loadCourses()), quiet(loadCurricula()), quiet(loadRooms())]);
  // Lớp cần phòng (đổi tên phòng ↔ Id) → tải sau phòng
  await Promise.all([quiet(loadClasses()), quiet(loadStudents()), quiet(loadStaffRequests())]);
  // Tuyển sinh (lead, chia lead, test, lịch hẹn, chiến dịch, mẫu tin) — cần nhân viên / khóa học đã nạp để đổi tên ↔ Id
  // Quyền xem trang theo grants server trả lúc đăng nhập — trang không có quyền thì không gọi API
  const grants = (user as { grants?: Record<string, string> }).grants;
  const canView = (page: string) =>
    user.permissions?.includes('*') || (grants ? Boolean(grants[`${page}.view`]) : hasPermission(user.permissions ?? [], page));
  await quiet(loadCrm(canView));
  // Phụ huynh / người giám hộ (HV-12) — lead có liên kết HV; nạp sau Tuyển sinh để gộp vào cùng danh sách lead
  if (canView('students.contacts')) await quiet(loadGuardians());
  else useCrmStore.setState({ links: [] });
}

/**
 * Chế độ API: access token chỉ nằm trong bộ nhớ → tải lại trang thì xin lại bằng cookie refresh (HttpOnly).
 * Trả `ready = false` trong lúc đang khôi phục để chưa vẽ trang cần đăng nhập.
 */
export function useSessionRestore(): boolean {
  const [ready, setReady] = useState(() => !USE_API || !useAuthStore.getState().user);

  useEffect(() => {
    if (!USE_API) return;
    // Phiên hết hẳn (refresh cũng hỏng) → về trang đăng nhập
    onExpired(() => useAuthStore.setState({ user: null }));
    if (ready) return;
    let cancelled = false;
    restore()
      .then(async (user) => {
        if (cancelled) return;
        if (!user) {
          useAuthStore.setState({ user: null });
          return;
        }
        // Cập nhật quyền mới nhất từ server, giữ chi nhánh đang chọn
        useAuthStore.setState({ user });
        await onSignedIn(user);
      })
      .catch(() => useAuthStore.setState({ user: null }))
      .finally(() => !cancelled && setReady(true));
    return () => {
      cancelled = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return ready;
}
