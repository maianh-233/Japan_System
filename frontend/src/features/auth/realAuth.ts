/**
 * Xác thực qua backend thật (/api/v1/system/auth/*). authApi.ts gọi các hàm này khi USE_API.
 * Backend trả Id dạng GUID → đổi về Id mock để các module còn dùng dữ liệu giả (Nhân sự, CRM...) vẫn khớp.
 */
import type { Portal, SessionUser } from '@/types/auth';
import { ApiError, api, lastSignIn, refreshSession, setAccessToken } from '@/lib/api/http';
import { fromGuid, fromGuids } from '@/lib/api/ids';

/** SessionUserDto của backend */
interface ServerUser {
  id: string;
  accountId: string;
  email: string;
  fullName: string;
  avatarUrl?: string;
  portal: Portal;
  roleName: string;
  scope: 'global' | 'branch';
  branchIds: string[];
  portalAccountId?: string;
  portalOwnerId?: string;
  permissions: string[];
  grants: Record<string, 'own' | 'branch' | 'all'>;
  availablePortals: Portal[];
  mustChangePassword: boolean;
}

interface SignIn {
  user: ServerUser;
  accessToken: string;
  accessTokenExpiresAt: string;
}

/** Mã lỗi backend → mã frontend đang dùng (giao diện so sánh theo mã cũ ở vài chỗ) */
const CODE_MAP: Record<string, string> = {
  'auth.invalid_credentials': 'INVALID_CREDENTIALS',
  'auth.account_locked': 'ACCOUNT_LOCKED',
  'auth.account_invited': 'ACCOUNT_INVITED',
  'auth.account_inactive': 'ACCOUNT_INACTIVE',
  'auth.otp_invalid': 'OTP_INVALID',
  'auth.otp_expired': 'OTP_EXPIRED',
  'auth.otp_locked': 'OTP_LOCKED',
  'auth.wrong_password': 'WRONG_PASSWORD',
  'auth.same_password': 'SAME_PASSWORD',
};

async function call<T>(path: string, body?: unknown, noRefresh = true): Promise<T> {
  try {
    return await api<T>(path, { method: 'POST', body, noRefresh });
  } catch (e) {
    if (e instanceof ApiError && CODE_MAP[e.code]) throw new ApiError(e.message, CODE_MAP[e.code], e.status, e.fields);
    throw e;
  }
}

export function toSessionUser(u: ServerUser): SessionUser & { accountId: string; grants: ServerUser['grants']; mustChangePassword: boolean } {
  const portalUser = u.portal === 'student' || u.portal === 'parent';
  return {
    id: portalUser ? `portal-${fromGuid(u.id)}` : fromGuid(u.id),
    accountId: u.accountId,
    email: u.email,
    fullName: u.fullName,
    avatarUrl: u.avatarUrl ?? undefined,
    portal: u.portal,
    roleName: u.roleName,
    scope: u.scope,
    branchIds: fromGuids(u.branchIds),
    portalAccountId: u.portalAccountId ? fromGuid(u.portalAccountId) : undefined,
    portalOwnerId: u.portalOwnerId ? fromGuid(u.portalOwnerId) : undefined,
    permissions: u.permissions,
    grants: u.grants,
    mustChangePassword: u.mustChangePassword,
  };
}

function accept(r: SignIn) {
  setAccessToken(r.accessToken);
  return toSessionUser(r.user);
}

export async function login(identifier: string, password: string, remember: boolean) {
  return accept(await call<SignIn>('/system/auth/login', { identifier: identifier.trim(), password, remember }));
}

/** Tải lại trang: đổi cookie refresh lấy access token + thông tin người dùng mới nhất. null = hết phiên. */
export async function restore() {
  if (!(await refreshSession())) return null;
  return toSessionUser((lastSignIn as SignIn).user);
}

export async function logout() {
  try {
    await call('/system/auth/logout');
  } finally {
    setAccessToken(null);
  }
}

// ---------- Quên mật khẩu (AUTH-04): OTP → mã đặt lại → mật khẩu mới ----------
const resetTokens = new Map<string, string>();
const key = (email: string) => email.trim().toLowerCase();

export const requestPasswordReset = (email: string) =>
  call<{ maskedEmail: string; resendAfter: number }>('/system/auth/password/forgot', { email: email.trim() });

export async function verifyOtp(email: string, otp: string) {
  const r = await call<{ resetToken: string }>('/system/auth/password/verify-otp', { email: email.trim(), otp: otp.trim() });
  resetTokens.set(key(email), r.resetToken);
}

export async function resetPassword(email: string, newPassword: string) {
  const resetToken = resetTokens.get(key(email));
  if (!resetToken) throw new ApiError('Bạn cần xác thực OTP trước.', 'OTP_REQUIRED');
  await call('/system/auth/password/reset', { resetToken, newPassword });
  resetTokens.delete(key(email));
}

// ---------- Đổi mật khẩu (AUTH-06) ----------
export async function changePassword(current: string, next: string, logoutOtherDevices: boolean) {
  const r = await call<{ accessToken: string; loggedOutDevices: number }>(
    '/system/auth/password/change',
    { currentPassword: current, newPassword: next, logoutOtherDevices },
    false,
  );
  setAccessToken(r.accessToken);
  return r.loggedOutDevices;
}

// ---------- Kích hoạt tài khoản được mời (AUTH-05) ----------
export interface Invitation {
  fullName: string;
  email: string;
  roles: string[];
  expiresAt: string;
}

export const getInvitation = (token: string) => api<Invitation>(`/system/auth/invitations/${encodeURIComponent(token)}`, { noRefresh: true });

export async function acceptInvitation(token: string, password: string) {
  return accept(await call<SignIn>('/system/auth/invitations/accept', { token, password }));
}

// ---------- Thiết bị & lịch sử đăng nhập (AUTH-08, AUTH-09) ----------
export interface MySession {
  id: string;
  device: string;
  ip: string;
  createdAt: string;
  lastSeenAt: string;
  remember: boolean;
  current: boolean;
}

export const mySessions = () => api<MySession[]>('/system/me/sessions');
export const revokeSession = (id: string) => api(`/system/me/sessions/${id}`, { method: 'DELETE' });
export const revokeOtherSessions = () => api<number>('/system/me/sessions/revoke-others', { method: 'POST' });

export interface LoginHistory {
  items: { at: string; action: string; success: boolean; ip: string; device: string; detail?: string }[];
  recentFailures: number;
}

export const loginHistory = () => api<LoginHistory>('/system/me/login-history');
