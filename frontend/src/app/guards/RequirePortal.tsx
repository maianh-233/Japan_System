import type { ReactNode } from 'react';
import { Navigate, useLocation } from 'react-router-dom';
import { PORTAL_HOME } from '@/config/navigation';
import { useAuthStore } from '@/stores/authStore';
import type { Portal } from '@/types/auth';

/**
 * Bảo vệ cổng:
 *  - Chưa đăng nhập → /login (nhớ trang định vào)
 *  - Sai cổng (vd học viên gõ /admin) → về trang chủ cổng của mình
 */
export function RequirePortal({ portal, children }: { portal: Portal; children: ReactNode }) {
  const user = useAuthStore((s) => s.user);
  const location = useLocation();
  if (!user) return <Navigate to="/login" replace state={{ from: location.pathname }} />;
  if (user.portal !== portal) return <Navigate to={PORTAL_HOME[user.portal]} replace />;
  return <>{children}</>;
}

/** "/" → trang chủ cổng hoặc đăng nhập */
export function RootRedirect() {
  const user = useAuthStore((s) => s.user);
  return <Navigate to={user ? PORTAL_HOME[user.portal] : '/login'} replace />;
}
