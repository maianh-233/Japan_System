import { useMemo } from 'react';
import { useAuth } from '@/hooks/useAuth';
import { effectivePerms, type EffectivePerm } from './access';
import { useSystemStore, type Actor } from './systemStore';

/** Tài khoản demo → hồ sơ nhân viên (khi có backend: user.employeeId trả về từ API) */
const USER_EMPLOYEE: Record<string, string> = {
  'u-sa': 'e-huy',
  'u-ad': 'e-trang',
  'u-bm': 'e-nam',
  'u-gv': 'e-mai',
  'u-sale': 'e-khoa',
  'u-mkt': 'e-baoanh',
  'u-kt': 'e-lan',
};

/**
 * Người đang thao tác: để ghi nhật ký + giới hạn "chỉ cấp được quyền mình có".
 * `granter = 'super'` khi là Super Admin.
 */
export function useActor(): { actor: Actor; employeeId?: string; granter: Map<string, EffectivePerm> | 'super' } {
  const { user } = useAuth();
  const roles = useSystemStore((s) => s.roles);
  const access = useSystemStore((s) => s.access);
  const employeeId = user ? USER_EMPLOYEE[user.id] : undefined;

  const granter = useMemo(() => {
    if (user?.permissions.includes('*')) return 'super' as const;
    const mine = access.find((a) => a.userId === employeeId);
    return mine ? effectivePerms(mine, roles) : new Map<string, EffectivePerm>();
  }, [user, access, roles, employeeId]);

  return { actor: { name: user?.fullName ?? 'Không rõ', role: user?.roleName ?? '—' }, employeeId, granter };
}
