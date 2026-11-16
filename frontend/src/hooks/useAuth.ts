import { useCallback, useMemo } from 'react';
import { useAuthStore } from '@/stores/authStore';
import { hasPermission } from '@/lib/permission';
import { ALL_BRANCHES, type Branch } from '@/types/auth';
import { menuPermissions } from '@/features/system/access';
import { useHrStore } from '@/features/hr/hrStore';
import { useSystemStore } from '@/features/system/systemStore';
import { useBranches } from '@/features/system/useBranches';

/** Người dùng hiện tại + chi nhánh có thể chọn + hàm kiểm tra quyền */
export function useAuth() {
  const storedUser = useAuthStore((s) => s.user);
  const branchId = useAuthStore((s) => s.branchId);
  const setBranch = useAuthStore((s) => s.setBranch);
  const signOut = useAuthStore((s) => s.signOut);
  const employeeId = useHrStore((s) => s.employees.find((e) => e.fullName === storedUser?.fullName)?.id);
  const access = useSystemStore((s) => s.access.find((a) => a.userId === storedUser?.id || a.userId === employeeId));
  const roles = useSystemStore((s) => s.roles);
  const allBranches = useBranches();
  const activeBranches = useBranches(false);

  const user = useMemo(() => {
    if (!storedUser || !access || (storedUser.portal !== 'staff' && storedUser.portal !== 'teacher')) return storedUser;
    const roleNames = access.roles.map((assignment) => roles.find((role) => role.id === assignment.roleId)?.name).filter(Boolean);
    return {
      ...storedUser,
      roleName: roleNames.join(' · ') || storedUser.roleName,
      scope: access.roles.some((assignment) => assignment.branchIds.length === 0) ? 'global' as const : 'branch' as const,
      branchIds: [...new Set(access.roles.flatMap((assignment) => assignment.branchIds))],
      permissions: storedUser.portal === 'teacher' ? storedUser.permissions : menuPermissions(access, roles),
    };
  }, [storedUser, access, roles]);

  /** Chi nhánh người dùng được phép xem */
  const branches: Branch[] = useMemo(() => {
    if (!user) return [];
    return user.scope === 'global' ? activeBranches : activeBranches.filter((b) => user.branchIds.includes(b.id));
  }, [user, activeBranches]);

  const currentBranch = branchId === ALL_BRANCHES ? null : (allBranches.find((b) => b.id === branchId) ?? null);

  const can = useCallback((permission?: string) => hasPermission(user?.permissions ?? [], permission), [user]);

  return { user, branches, branchId, currentBranch, setBranch, signOut, can };
}
