import { useMemo } from 'react';
import { useAuth } from '@/hooks/useAuth';
import { useMe } from '@/features/me/useMe';
import { effectivePerms, type EffectivePerm } from './access';
import { useSystemStore, type Actor } from './systemStore';

/**
 * Người đang thao tác: để ghi nhật ký + giới hạn "chỉ cấp được quyền mình có".
 * `granter = 'super'` khi có role Super Admin.
 * Hồ sơ nhân viên lấy qua useMe() (khớp theo user.id = mã nhân viên khi đăng nhập).
 */
export function useActor(): { actor: Actor; employeeId?: string; granter: Map<string, EffectivePerm> | 'super' } {
  const { user } = useAuth();
  const me = useMe();
  const roles = useSystemStore((s) => s.roles);
  const access = useSystemStore((s) => s.access);
  const employeeId = me?.id;

  const granter = useMemo(() => {
    if (user?.permissions.includes('*')) return 'super' as const;
    const mine = access.find((a) => a.userId === employeeId);
    if (!mine) return new Map<string, EffectivePerm>();
    const isSuper = mine.roles.some((assignment) => roles.find((r) => r.id === assignment.roleId)?.superAdmin);
    return isSuper ? ('super' as const) : effectivePerms(mine, roles);
  }, [user, access, roles, employeeId]);

  return { actor: { name: user?.fullName ?? 'Không rõ', role: user?.roleName ?? '—' }, employeeId, granter };
}
