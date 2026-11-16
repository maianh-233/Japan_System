import { useEffect, type ReactNode } from 'react';
import { Navigate, useLocation } from 'react-router-dom';
import { PORTAL_HOME } from '@/config/navigation';
import { useAuthStore } from '@/stores/authStore';
import type { Portal } from '@/types/auth';
import { useHrStore } from '@/features/hr/hrStore';
import { useStudentStore } from '@/features/student/studentStore';
import { useSystemStore } from '@/features/system/systemStore';

const LEGACY_PORTAL_OWNERS: Record<string, { ownerType: 'student' | 'guardian'; ownerId: string }> = {
  'u-student': { ownerType: 'student', ownerId: 'ST-1' },
  'u-parent': { ownerType: 'guardian', ownerId: 'L-0001' },
};

/**
 * Bảo vệ cổng:
 *  - Chưa đăng nhập → /login (nhớ trang định vào)
 *  - Sai cổng (vd học viên gõ /admin) → về trang chủ cổng của mình
 */
export function RequirePortal({ portal, children }: { portal: Portal; children: ReactNode }) {
  const user = useAuthStore((s) => s.user);
  const signOut = useAuthStore((s) => s.signOut);
  const location = useLocation();
  const employeeId = useHrStore((s) => s.employees.find((e) => e.fullName === user?.fullName)?.id);
  const access = useSystemStore((s) => s.access.find((a) => a.userId === user?.id || a.userId === employeeId));
  const portalAccount = useStudentStore((s) => {
    if (user?.portalAccountId) return s.accounts.find((account) => account.id === user.portalAccountId);
    const owner = user?.portalOwnerId
      ? { ownerId: user.portalOwnerId, ownerType: user.portal === 'student' ? 'student' : 'guardian' }
      : user ? LEGACY_PORTAL_OWNERS[user.id] : undefined;
    return owner ? s.accounts.find((account) => account.ownerId === owner.ownerId && account.ownerType === owner.ownerType) : undefined;
  });
  const staffAccountInvalid = Boolean(user && (user.portal === 'staff' || user.portal === 'teacher') && (!access || access.status !== 'active'));
  const legacyPortalAccount = user ? LEGACY_PORTAL_OWNERS[user.id] : undefined;
  const portalAccountInvalid = Boolean(
    user && (user.portal === 'student' || user.portal === 'parent') &&
    (((user.portalAccountId || legacyPortalAccount) && !portalAccount) || (portalAccount && portalAccount.status !== 'active')),
  );
  const sessionInvalid = staffAccountInvalid || portalAccountInvalid;

  useEffect(() => {
    if (sessionInvalid) signOut();
  }, [sessionInvalid, signOut]);

  if (!user) return <Navigate to="/login" replace state={{ from: location.pathname }} />;
  if (sessionInvalid) return <Navigate to="/login" replace />;
  if (user.portal !== portal) return <Navigate to={PORTAL_HOME[user.portal]} replace />;
  return <>{children}</>;
}

/** "/" → trang chủ cổng hoặc đăng nhập */
export function RootRedirect() {
  const user = useAuthStore((s) => s.user);
  return <Navigate to={user ? PORTAL_HOME[user.portal] : '/login'} replace />;
}
