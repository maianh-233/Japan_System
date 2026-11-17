/**
 * API xác thực. Chế độ "api" (mặc định) → gọi backend thật qua realAuth.ts; chế độ "mock" → giả lập trên trình duyệt
 * (có độ trễ như gọi mạng thật). Chữ ký hàm giống nhau ở 2 chế độ.
 */
import type { SessionUser } from '@/types/auth';
import { DEMO_OTP, DEMO_PASSWORD, MOCK_ACCOUNTS } from '@/mocks/data/users';
import { menuPermissions } from '@/features/system/access';
import { useHrStore } from '@/features/hr/hrStore';
import { useStudentStore } from '@/features/student/studentStore';
import { useCrmStore } from '@/features/crm/crmStore';
import { useSystemStore } from '@/features/system/systemStore';
import { recordActivity } from '@/features/system/activity';
import type { PortalAccount } from '@/features/student/types';
import { USE_API } from '@/lib/api/config';
import { ApiError } from '@/lib/api/http';
import * as real from './realAuth';

export { ApiError };

const delay = (ms = 700) => new Promise((r) => setTimeout(r, ms));
const normalize = (email: string) => email.trim().toLowerCase();
const DEMO_PORTAL_OWNERS: Record<string, { ownerType: 'student' | 'guardian'; ownerId: string }> = {
  'u-student': { ownerType: 'student', ownerId: 'ST-1' },
  'u-parent': { ownerType: 'guardian', ownerId: 'L-0001' },
};

function portalUser(account: PortalAccount): SessionUser {
  const { ownerId, ownerType } = account;
  const { students, leads, links } = useCrmStore.getState();
  const student = ownerType === 'student' ? students.find((x) => x.id === ownerId) : undefined;
  const lead = ownerType === 'guardian' ? leads.find((x) => x.id === ownerId) : undefined;
  const childId = ownerType === 'guardian' ? links.find((x) => x.leadId === ownerId)?.studentId : undefined;
  const child = students.find((x) => x.id === childId);
  const portal = ownerType === 'student' ? 'student' : 'parent';
  const branchId = student?.branchId ?? child?.branchId ?? lead?.branchId;
  return {
    id: `portal-${account.id}`,
    portalAccountId: account.id,
    portalOwnerId: ownerId,
    email: account.email ?? account.username,
    fullName: student?.fullName ?? lead?.fullName ?? account.username,
    portal,
    roleName: ownerType === 'student' ? 'Học viên' : 'Phụ huynh',
    scope: 'branch',
    branchIds: branchId ? [branchId] : [],
    permissions: [`${portal}.*`],
  };
}

function assertAccountActive(status: 'active' | 'invited' | 'locked' | 'inactive') {
  if (status === 'locked') throw new ApiError('Tài khoản đã bị khóa. Vui lòng liên hệ quản trị viên.', 'ACCOUNT_LOCKED');
  if (status === 'invited') throw new ApiError('Tài khoản đang chờ kích hoạt.', 'ACCOUNT_INVITED');
  if (status === 'inactive') throw new ApiError('Tài khoản đã ngừng hoạt động.', 'ACCOUNT_INACTIVE');
}

function staffUser(account: (typeof MOCK_ACCOUNTS)[number] | undefined, employeeId: string): SessionUser {
  const accessState = useSystemStore.getState();
  const employee = useHrStore.getState().employees.find((x) => x.id === employeeId);
  const access = accessState.access.find((x) => x.userId === employeeId);
  if (!access) throw new ApiError('Tài khoản chưa được cấp quyền đăng nhập.', 'ACCESS_NOT_FOUND');
  assertAccountActive(access.status);
  const roleNames = access.roles.map((assignment) => accessState.roles.find((x) => x.id === assignment.roleId)?.name).filter(Boolean);
  const branchIds = [...new Set(access.roles.flatMap((assignment) => assignment.branchIds))];
  const portal = employee?.kind === 'teacher' ? 'teacher' : account?.portal ?? 'staff';
  return {
    id: employeeId,
    email: account?.email ?? access.email,
    fullName: employee?.fullName ?? account?.fullName ?? access.email,
    portal,
    roleName: roleNames.join(' · ') || account?.roleName || 'Nhân viên',
    scope: access.roles.some((assignment) => assignment.branchIds.length === 0) ? 'global' : 'branch',
    branchIds,
    permissions: portal === 'teacher' ? ['teacher.*'] : menuPermissions(access, accessState.roles),
  };
}

function completeLogin(user: SessionUser): SessionUser {
  recordActivity({
    category: 'auth',
    severity: 'info',
    action: 'Đăng nhập',
    target: user.fullName,
    branchId: user.branchIds[0],
    notifications: [{
      portal: user.portal,
      title: 'Đăng nhập thành công',
      description: `Xin chào ${user.fullName}.`,
      tone: 'success',
    }],
  });
  return user;
}

/** Đăng nhập bằng email hoặc tên đăng nhập + mật khẩu */
export async function login(identifier: string, password: string, remember = false): Promise<SessionUser> {
  if (USE_API) return real.login(identifier, password, remember);
  await delay(800);
  const loginId = normalize(identifier);
  const demo = MOCK_ACCOUNTS.find((a) => normalize(a.email) === loginId);
  const portalAccounts = useStudentStore.getState().accounts;
  const issued = portalAccounts.find((a) => normalize(a.username) === loginId || (a.email && normalize(a.email) === loginId));

  if (demo) {
    if (demo.password !== password) throw new ApiError('Email hoặc mật khẩu không đúng.', 'INVALID_CREDENTIALS');
    if (demo.locked) throw new ApiError('Tài khoản đã bị khóa. Vui lòng liên hệ quản trị viên.', 'ACCOUNT_LOCKED');
    const owner = DEMO_PORTAL_OWNERS[demo.id];
    if (owner) {
      const linked = portalAccounts.find((a) => a.ownerType === owner.ownerType && a.ownerId === owner.ownerId);
      if (linked) {
        assertAccountActive(linked.status);
        return completeLogin(portalUser(linked));
      }
    }
    const employee = useHrStore.getState().employees.find((x) => x.id === demo.employeeId);
    if (employee && (demo.portal === 'staff' || demo.portal === 'teacher')) return completeLogin(staffUser(demo, employee.id));
    const { password: _pw, locked: _locked, employeeId: _eid, ...user } = demo;
    return completeLogin(user);
  }

  if (issued) {
    if (password !== DEMO_PASSWORD) throw new ApiError('Email hoặc mật khẩu không đúng.', 'INVALID_CREDENTIALS');
    assertAccountActive(issued.status);
    return completeLogin(portalUser(issued));
  }

  const access = useSystemStore.getState().access.find((a) => normalize(a.email) === loginId);
  if (access) {
    if (password !== DEMO_PASSWORD) throw new ApiError('Email hoặc mật khẩu không đúng.', 'INVALID_CREDENTIALS');
    const account = MOCK_ACCOUNTS.find((x) => x.employeeId === access.userId);
    return completeLogin(staffUser(account, access.userId));
  }

  throw new ApiError('Email hoặc mật khẩu không đúng.', 'INVALID_CREDENTIALS');
}

// ---------------- Quên mật khẩu ----------------
/** Lưu tạm trạng thái OTP theo email (giả lập phía server) */
/** `exists = false`: email không có / bị khóa — vẫn giữ trạng thái để phản hồi y hệt email thật (chống dò email) */
const otpState = new Map<string, { attempts: number; verified: boolean; sentAt: number; exists: boolean }>();
const MAX_OTP_ATTEMPTS = 5;

/**
 * Bước 1: gửi OTP. LUÔN trả cùng một kết quả dù email có tồn tại hay không, có bị khóa hay không
 * (user story AUTH-04.E1 / E2) — người ngoài không dò được email nào có trong hệ thống.
 */
export async function requestPasswordReset(email: string): Promise<{ maskedEmail: string; resendAfter: number }> {
  if (USE_API) return real.requestPasswordReset(email);
  await delay(900);
  const key = normalize(email);
  const acc = MOCK_ACCOUNTS.find((a) => a.email === key);
  // Chỉ thật sự gửi mã khi tài khoản tồn tại và không bị khóa
  otpState.set(key, { attempts: 0, verified: false, sentAt: Date.now(), exists: Boolean(acc && !acc.locked) });
  const [name, domain = ''] = key.split('@');
  return { maskedEmail: `${name.slice(0, 2)}${'*'.repeat(Math.max(1, name.length - 2))}@${domain}`, resendAfter: 60 };
}

/** Bước 2: xác thực mã OTP */
export async function verifyOtp(email: string, otp: string): Promise<void> {
  if (USE_API) return real.verifyOtp(email, otp);
  await delay(700);
  const st = otpState.get(normalize(email));
  if (!st) throw new ApiError('Phiên đặt lại mật khẩu đã hết hạn, vui lòng gửi lại mã.', 'OTP_EXPIRED');
  if (st.attempts >= MAX_OTP_ATTEMPTS) throw new ApiError('Nhập sai quá nhiều lần. Vui lòng gửi lại mã mới.', 'OTP_LOCKED');
  if (Date.now() - st.sentAt > 5 * 60_000) throw new ApiError('Mã OTP đã hết hạn (5 phút). Vui lòng gửi lại.', 'OTP_EXPIRED');
  // Email không tồn tại / bị khóa: mã nào cũng sai, đếm lượt như thường
  if (!st.exists || otp !== DEMO_OTP) {
    st.attempts += 1;
    const left = MAX_OTP_ATTEMPTS - st.attempts;
    throw new ApiError(left > 0 ? `Mã OTP không đúng. Còn ${left} lần thử.` : 'Nhập sai quá nhiều lần. Vui lòng gửi lại mã mới.', 'OTP_INVALID');
  }
  st.verified = true;
}

/** Bước 3: đặt mật khẩu mới (chỉ sau khi OTP hợp lệ) */
export async function resetPassword(email: string, newPassword: string): Promise<void> {
  if (USE_API) return real.resetPassword(email, newPassword);
  await delay(800);
  const key = normalize(email);
  const st = otpState.get(key);
  if (!st?.verified) throw new ApiError('Bạn cần xác thực OTP trước.', 'OTP_REQUIRED');
  const acc = MOCK_ACCOUNTS.find((a) => a.email === key);
  if (acc) acc.password = newPassword;
  otpState.delete(key);
}

// ---------------- Đổi mật khẩu (đang đăng nhập) ----------------
/** Đổi mật khẩu: kiểm tra mật khẩu hiện tại. Backend: đổi xong thu hồi phiên ở thiết bị khác nếu được yêu cầu. */
export async function changePassword(email: string, current: string, next: string): Promise<void> {
  if (USE_API) {
    await real.changePassword(current, next, false);
    return;
  }
  await delay(800);
  const acc = MOCK_ACCOUNTS.find((a) => a.email === normalize(email));
  if (!acc) throw new ApiError('Không tìm thấy tài khoản.', 'NOT_FOUND');
  if (acc.password !== current) throw new ApiError('Mật khẩu hiện tại không đúng.', 'WRONG_PASSWORD');
  if (current === next) throw new ApiError('Mật khẩu mới phải khác mật khẩu hiện tại.', 'SAME_PASSWORD');
  acc.password = next;
}
