import { useEffect, useState } from 'react';
import { Form } from 'antd';
import { CircleAlert, KeyRound, Lock } from 'lucide-react';
import { Link, Navigate, useNavigate, useSearchParams } from 'react-router-dom';
import { Button, TextInput } from '@/components/common';
import { Logo, SYSTEM_TAGLINE } from '@/components/layout/Logo';
import { PORTAL_HOME } from '@/config/navigation';
import { acceptInvitation, getInvitation, type Invitation } from '@/features/auth/realAuth';
import { onSignedIn } from '@/features/auth/session';
import { USE_API } from '@/lib/api/config';
import { fmtDateTime } from '@/lib/date';
import { useAuthStore } from '@/stores/authStore';

interface Values {
  password: string;
  confirm: string;
}

/**
 * AUTH-05 — kích hoạt tài khoản được mời: mở link trong email (/activate?token=...), đặt mật khẩu, tự đăng nhập.
 * Link dùng 1 lần, hạn 72 giờ; quản trị gửi lại lời mời thì link cũ hết hiệu lực.
 */
export default function ActivatePage() {
  const [params] = useSearchParams();
  const token = params.get('token') ?? '';
  const navigate = useNavigate();
  const signIn = useAuthStore((s) => s.signIn);
  const [invitation, setInvitation] = useState<Invitation | null>(null);
  const [error, setError] = useState<string | null>(token ? null : 'Link kích hoạt không hợp lệ.');
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (!token || !USE_API) return;
    getInvitation(token)
      .then(setInvitation)
      .catch((e: Error) => setError(e.message));
  }, [token]);

  if (!USE_API) return <Navigate to="/login" replace />;

  const submit = async (v: Values) => {
    setSaving(true);
    setError(null);
    try {
      const user = await acceptInvitation(token, v.password);
      await onSignedIn(user);
      signIn(user, false);
      navigate(PORTAL_HOME[user.portal], { replace: true });
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setSaving(false);
    }
  };

  return (
    <main className="flex min-h-dvh items-center justify-center bg-page px-4 py-10">
      <div className="w-full max-w-[420px] rounded-2xl border border-line bg-surface p-6 shadow-[var(--shadow-soft-md)] sm:p-8">
        <Logo subtitle={SYSTEM_TAGLINE} className="mb-6" />
        <h1 className="text-xl font-semibold text-ink">Kích hoạt tài khoản</h1>

        {error && (
          <div role="alert" className="mt-4 flex items-start gap-2 rounded-lg border border-danger-100 bg-danger-50 px-3 py-2.5 text-[13px] text-danger-700">
            <CircleAlert size={16} className="mt-0.5 shrink-0" aria-hidden />
            <span>
              {error}
              {!invitation && ' Liên hệ quản trị viên để được gửi lại lời mời.'}
            </span>
          </div>
        )}

        {!invitation && !error && <p className="mt-4 text-ink-muted" role="status">Đang kiểm tra link…</p>}

        {invitation && (
          <>
            <p className="mt-1 mb-5 text-[13px] text-ink-secondary">
              Xin chào <strong className="text-ink">{invitation.fullName}</strong> ({invitation.email}). Vai trò:{' '}
              {invitation.roles.length ? invitation.roles.join(', ') : 'chưa gán'}. Đặt mật khẩu để bắt đầu — link hết hạn lúc{' '}
              {fmtDateTime(invitation.expiresAt)}.
            </p>
            <Form<Values> layout="vertical" requiredMark={false} onFinish={submit}>
              <Form.Item
                name="password"
                label="Mật khẩu mới"
                extra="Tối thiểu 8 ký tự, có cả chữ và số."
                rules={[
                  { required: true, message: 'Nhập mật khẩu' },
                  { min: 8, message: 'Tối thiểu 8 ký tự' },
                  { pattern: /(?=.*[A-Za-zÀ-ỹ])(?=.*\d)/, message: 'Cần có cả chữ và số' },
                ]}
              >
                <TextInput type="password" icon={Lock} autoComplete="new-password" size="large" autoFocus />
              </Form.Item>
              <Form.Item
                name="confirm"
                label="Nhập lại mật khẩu"
                dependencies={['password']}
                rules={[
                  { required: true, message: 'Nhập lại mật khẩu' },
                  ({ getFieldValue }) => ({
                    validator: (_, v) => (!v || v === getFieldValue('password') ? Promise.resolve() : Promise.reject(new Error('Không khớp mật khẩu'))),
                  }),
                ]}
              >
                <TextInput type="password" icon={Lock} autoComplete="new-password" size="large" />
              </Form.Item>
              <Button icon={KeyRound} htmlType="submit" size="lg" block loading={saving}>
                Kích hoạt & đăng nhập
              </Button>
            </Form>
          </>
        )}

        <p className="mt-6 text-center text-[13px]">
          <Link to="/login" className="font-medium text-primary-700 hover:underline">
            Về trang đăng nhập
          </Link>
        </p>
      </div>
    </main>
  );
}
