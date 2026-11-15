import { useCallback, useMemo } from 'react';
import { useAuthStore } from '@/stores/authStore';
import { hasPermission } from '@/lib/permission';
import { MOCK_BRANCHES } from '@/mocks/data/branches';
import { ALL_BRANCHES, type Branch } from '@/types/auth';

/** Người dùng hiện tại + chi nhánh có thể chọn + hàm kiểm tra quyền */
export function useAuth() {
  const user = useAuthStore((s) => s.user);
  const branchId = useAuthStore((s) => s.branchId);
  const setBranch = useAuthStore((s) => s.setBranch);
  const signOut = useAuthStore((s) => s.signOut);

  /** Chi nhánh người dùng được phép xem */
  const branches: Branch[] = useMemo(() => {
    if (!user) return [];
    return user.scope === 'global' ? MOCK_BRANCHES : MOCK_BRANCHES.filter((b) => user.branchIds.includes(b.id));
  }, [user]);

  const currentBranch = branchId === ALL_BRANCHES ? null : (MOCK_BRANCHES.find((b) => b.id === branchId) ?? null);

  const can = useCallback((permission?: string) => hasPermission(user?.permissions ?? [], permission), [user]);

  return { user, branches, branchId, currentBranch, setBranch, signOut, can };
}
