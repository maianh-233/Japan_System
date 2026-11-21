/**
 * Tài khoản Cổng học viên / phụ huynh (HV-13, AUTH-10) — backend System: /api/v1/system/portal-accounts.
 * Server kiểm chủ tài khoản có thật + trong chi nhánh được phân công, lấy tên từ hồ sơ; mật khẩu tạm gửi email,
 * không có email → trả mật khẩu tạm 1 lần để giáo vụ đưa trực tiếp (hiện thông báo không tự tắt).
 * Đổ vào useStudentStore.accounts như dữ liệu giả cũ — các trang đọc store như trước.
 */
import { useNotify } from '@/components/common';
import { USE_API } from '@/lib/api/config';
import { api, ApiError } from '@/lib/api/http';
import { fromGuid, toGuid } from '@/lib/api/ids';
import { useStudentStore } from './studentStore';
import type { AccountStatus, PortalAccount } from './types';

interface ServerAccount {
  id: string;
  ownerType: 'student' | 'guardian';
  ownerId: string;
  displayName: string;
  username: string;
  email?: string | null;
  status: AccountStatus;
  mustChangePassword: boolean;
  lastLoginAt?: string | null;
}

interface ServerCredential {
  account: ServerAccount;
  temporaryPassword?: string | null;
  emailSent: boolean;
}

const toAccount = (a: ServerAccount): PortalAccount => ({
  id: a.id,
  ownerType: a.ownerType,
  ownerId: fromGuid(a.ownerId),
  username: a.username,
  email: a.email ?? undefined,
  status: a.status,
  // Server chưa trả ngày cấp — để trống, thẻ tài khoản hiện "—"
  createdAt: '',
  lastLoginAt: a.lastLoginAt ?? undefined,
});

function upsert(a: ServerAccount) {
  const acc = toAccount(a);
  useStudentStore.setState((s) => ({ accounts: [...s.accounts.filter((x) => x.id !== acc.id), acc] }));
  return acc;
}

/** Tài khoản cổng của HV / PH trong phạm vi (thay dữ liệu giả). Không có quyền → rỗng */
export async function loadPortalAccounts() {
  try {
    useStudentStore.setState({ accounts: (await api<ServerAccount[]>('/system/portal-accounts')).map(toAccount) });
  } catch (e) {
    if (e instanceof ApiError && e.status === 403) useStudentStore.setState({ accounts: [] });
    else throw e;
  }
}

export interface IssueInput {
  ownerType: 'student' | 'guardian';
  ownerId: string;
  /** Gợi ý: HV = mã HV; PH = ph.{SĐT} */
  username: string;
  email?: string;
  /** Tên hiển thị trong thông báo (server tự lấy tên chủ tài khoản) */
  ownerName: string;
}

/**
 * Thao tác tài khoản cổng dùng chung (hồ sơ HV, trang Phụ huynh, tab Người thân):
 * chế độ API gọi server + báo kết quả; chế độ mock ghi store như trước. Lỗi → báo, trả undefined / false.
 */
export function usePortalAccountActions() {
  const notify = useNotify();
  const issueLocal = useStudentStore((s) => s.issueAccount);
  const setStatusLocal = useStudentStore((s) => s.setAccountStatus);

  const fail = (e: unknown, fallback: string) => notify.error({ title: fallback, description: e instanceof ApiError ? e.message : undefined });

  /** Mật khẩu tạm: gửi email được → báo đã gửi; không → hiện 1 lần, không tự tắt */
  const announce = (r: ServerCredential, title: string) => {
    if (r.emailSent || !r.temporaryPassword) {
      notify.success({ title, description: `Tên đăng nhập ${r.account.username} — mật khẩu tạm đã gửi qua email ${r.account.email ?? ''}`.trim() });
      return;
    }
    notify.info({
      title,
      duration: 0,
      description: (
        <span className="flex flex-col gap-1">
          <span>Chưa có email nhận — đưa trực tiếp cho người dùng (chỉ hiện 1 lần, đổi ở lần đăng nhập đầu):</span>
          <span>
            Tên đăng nhập <b className="font-mono">{r.account.username}</b> · Mật khẩu tạm <b className="font-mono">{r.temporaryPassword}</b>
          </span>
        </span>
      ),
    });
  };

  const issue = async (v: IssueInput): Promise<PortalAccount | undefined> => {
    const title = v.ownerType === 'student' ? 'Đã cấp tài khoản Cổng học viên' : 'Đã cấp tài khoản Cổng phụ huynh';
    if (!USE_API) {
      const acc = issueLocal({ ownerType: v.ownerType, ownerId: v.ownerId, username: v.username, email: v.email });
      notify.success({ title, description: `Tên đăng nhập ${acc.username} — mật khẩu tạm đã gửi qua SMS / Email` });
      return acc;
    }
    try {
      const r = await api<ServerCredential>('/system/portal-accounts', {
        body: { ownerType: v.ownerType, ownerId: toGuid(v.ownerId), username: v.username, email: v.email || null },
      });
      announce(r, title);
      return upsert(r.account);
    } catch (e) {
      fail(e, `Không cấp được tài khoản cho ${v.ownerName}`);
      return undefined;
    }
  };

  const resetPassword = async (account: PortalAccount): Promise<boolean> => {
    if (!USE_API) {
      notify.success({ title: 'Đã gửi mật khẩu tạm', description: `Tài khoản ${account.username}` });
      return true;
    }
    try {
      announce(await api<ServerCredential>(`/system/portal-accounts/${account.id}/reset-password`, { method: 'POST' }), 'Đã đặt lại mật khẩu');
      return true;
    } catch (e) {
      fail(e, 'Không đặt lại được mật khẩu');
      return false;
    }
  };

  const setLocked = async (account: PortalAccount, locked: boolean): Promise<boolean> => {
    if (!USE_API) {
      setStatusLocal(account.id, locked ? 'locked' : 'active');
      return true;
    }
    try {
      upsert(await api<ServerAccount>(`/system/portal-accounts/${account.id}/${locked ? 'lock' : 'unlock'}`, { method: 'POST' }));
      return true;
    } catch (e) {
      fail(e, locked ? 'Không khóa được tài khoản' : 'Không mở khóa được tài khoản');
      return false;
    }
  };

  return { issue, resetPassword, setLocked };
}
