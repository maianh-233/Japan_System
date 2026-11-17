import { useEffect, useState } from 'react';
import { USE_API } from '@/lib/api/config';
import { onExpired } from '@/lib/api/http';
import { useAuthStore } from '@/stores/authStore';
import type { SessionUser } from '@/types/auth';
import { loadCourses } from '@/features/academic/courses/courseApi';
import { loadCurricula } from '@/features/academic/curriculum/curriculumApi';
import { loadEmployees } from '@/features/hr/hrApi';
import { loadBranches } from '@/features/system/branchApi';
import { loadSystem, rememberAccount } from '@/features/system/systemApi';
import { restore } from './realAuth';

/** Việc làm ngay sau khi có phiên (đăng nhập / kích hoạt / tải lại trang) — chế độ API */
export async function onSignedIn(user: SessionUser & { accountId?: string }) {
  if (!USE_API) return;
  if (user.accountId) rememberAccount(user.accountId, user.id);
  if (user.portal !== 'staff') return;
  // Lỗi tải dữ liệu không chặn việc vào hệ thống — trang tương ứng sẽ báo / dùng dữ liệu cũ
  const quiet = (p: Promise<unknown>) => p.catch(() => undefined);
  await Promise.all([quiet(loadSystem()), quiet(loadBranches()), quiet(loadEmployees()), quiet(loadCourses()), quiet(loadCurricula())]);
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
