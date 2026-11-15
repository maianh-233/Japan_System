/**
 * API xác thực GIẢ LẬP — chạy trên trình duyệt, có độ trễ như gọi mạng thật.
 * Khi có backend: giữ nguyên chữ ký hàm, thay phần thân bằng gọi HTTP.
 */
import type { SessionUser } from '@/types/auth';
import { DEMO_OTP, MOCK_ACCOUNTS } from '@/mocks/data/users';

const delay = (ms = 700) => new Promise((r) => setTimeout(r, ms));
const normalize = (email: string) => email.trim().toLowerCase();

export class ApiError extends Error {
  constructor(
    message: string,
    public code: string,
  ) {
    super(message);
  }
}

/** Đăng nhập bằng email + mật khẩu */
export async function login(email: string, password: string): Promise<SessionUser> {
  await delay(800);
  const acc = MOCK_ACCOUNTS.find((a) => a.email === normalize(email));
  if (!acc || acc.password !== password) {
    throw new ApiError('Email hoặc mật khẩu không đúng.', 'INVALID_CREDENTIALS');
  }
  if (acc.locked) {
    throw new ApiError('Tài khoản đã bị khóa. Vui lòng liên hệ quản trị viên.', 'ACCOUNT_LOCKED');
  }
  const { password: _pw, locked: _locked, ...user } = acc;
  void _pw;
  void _locked;
  return user;
}

// ---------------- Quên mật khẩu ----------------
/** Lưu tạm trạng thái OTP theo email (giả lập phía server) */
const otpState = new Map<string, { attempts: number; verified: boolean; sentAt: number }>();
const MAX_OTP_ATTEMPTS = 5;

/** Bước 1: kiểm tra email có tồn tại → gửi OTP */
export async function requestPasswordReset(email: string): Promise<{ maskedEmail: string; resendAfter: number }> {
  await delay(900);
  const acc = MOCK_ACCOUNTS.find((a) => a.email === normalize(email));
  if (!acc) throw new ApiError('Không tìm thấy tài khoản với email này.', 'EMAIL_NOT_FOUND');
  if (acc.locked) throw new ApiError('Tài khoản đã bị khóa, không thể đặt lại mật khẩu.', 'ACCOUNT_LOCKED');
  otpState.set(acc.email, { attempts: 0, verified: false, sentAt: Date.now() });
  const [name, domain] = acc.email.split('@');
  return { maskedEmail: `${name.slice(0, 2)}${'*'.repeat(Math.max(1, name.length - 2))}@${domain}`, resendAfter: 60 };
}

/** Bước 2: xác thực mã OTP */
export async function verifyOtp(email: string, otp: string): Promise<void> {
  await delay(700);
  const st = otpState.get(normalize(email));
  if (!st) throw new ApiError('Phiên đặt lại mật khẩu đã hết hạn, vui lòng gửi lại mã.', 'OTP_EXPIRED');
  if (st.attempts >= MAX_OTP_ATTEMPTS) throw new ApiError('Nhập sai quá nhiều lần. Vui lòng gửi lại mã mới.', 'OTP_LOCKED');
  if (Date.now() - st.sentAt > 5 * 60_000) throw new ApiError('Mã OTP đã hết hạn (5 phút). Vui lòng gửi lại.', 'OTP_EXPIRED');
  if (otp !== DEMO_OTP) {
    st.attempts += 1;
    const left = MAX_OTP_ATTEMPTS - st.attempts;
    throw new ApiError(left > 0 ? `Mã OTP không đúng. Còn ${left} lần thử.` : 'Nhập sai quá nhiều lần. Vui lòng gửi lại mã mới.', 'OTP_INVALID');
  }
  st.verified = true;
}

/** Bước 3: đặt mật khẩu mới (chỉ sau khi OTP hợp lệ) */
export async function resetPassword(email: string, newPassword: string): Promise<void> {
  await delay(800);
  const key = normalize(email);
  const st = otpState.get(key);
  if (!st?.verified) throw new ApiError('Bạn cần xác thực OTP trước.', 'OTP_REQUIRED');
  const acc = MOCK_ACCOUNTS.find((a) => a.email === key);
  if (acc) acc.password = newPassword;
  otpState.delete(key);
}
