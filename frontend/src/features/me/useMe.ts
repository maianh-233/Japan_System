import { useMemo } from 'react';
import { useHrStore } from '@/features/hr/hrStore';
import { meOf, reportsOf } from '@/features/hr/hrUtils';
import { useAuth } from '@/hooks/useAuth';

/** Hồ sơ nhân viên của người đang đăng nhập (khớp theo họ tên — dữ liệu giả; khi có backend: user.employeeId) */
export function useMe() {
  const { user } = useAuth();
  const employees = useHrStore((s) => s.employees);
  return useMemo(() => meOf(employees, user), [employees, user]);
}

/** Nhân viên dưới quyền (trực tiếp + gián tiếp) của người đang đăng nhập */
export function useMyReports() {
  const me = useMe();
  const employees = useHrStore((s) => s.employees);
  return useMemo(() => reportsOf(me?.id, employees), [me, employees]);
}
