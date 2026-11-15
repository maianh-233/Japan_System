import { useNavigate } from 'react-router-dom';
import { Avatar, Button, useConfirm } from '@/components/common';
import { useAuth } from '@/hooks/useAuth';

/** Đăng xuất có hộp xác nhận */
export function useLogout() {
  const { signOut } = useAuth();
  const navigate = useNavigate();
  const { confirm, confirmDialog } = useConfirm();
  const logout = async () => {
    const ok = await confirm({
      title: 'Đăng xuất khỏi hệ thống?',
      description: 'Bạn sẽ cần đăng nhập lại để tiếp tục làm việc.',
      tone: 'primary',
      confirmAction: 'logout',
      confirmText: 'Đăng xuất',
    });
    if (ok) {
      signOut();
      navigate('/login', { replace: true });
    }
  };
  return { logout, confirmDialog };
}

/** Ảnh + tên + vai trò người dùng, kèm nút Đăng xuất. */
export function UserMenu({ showLogout = true }: { showLogout?: boolean }) {
  const { user } = useAuth();
  const { logout, confirmDialog } = useLogout();
  if (!user) return null;

  return (
    <div className="flex items-center gap-2">
      <div className="flex items-center gap-2.5">
        <Avatar name={user.fullName} src={user.avatarUrl} size="md" status="online" />
        <div className="hidden min-w-0 flex-col leading-tight md:flex">
          <span className="max-w-[160px] truncate text-sm font-semibold text-ink">{user.fullName}</span>
          <span className="max-w-[160px] truncate text-xs text-ink-muted">{user.roleName}</span>
        </div>
      </div>
      {showLogout && <Button action="logout" collapseOnMobile onClick={logout} />}
      {confirmDialog}
    </div>
  );
}
