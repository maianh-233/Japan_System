import { useState } from 'react';
import { Checkbox, Form } from 'antd';
import { CircleAlert, ChevronDown, Lock, LogIn, Mail } from 'lucide-react';
import { Navigate, useLocation, useNavigate } from 'react-router-dom';
import { Button, TextInput } from '@/components/common';
import { Logo, SYSTEM_TAGLINE } from '@/components/layout/Logo';
import { PORTAL_HOME } from '@/config/navigation';
import { login } from '@/features/auth/authApi';
import { useAuthStore } from '@/stores/authStore';
import { DEMO_PASSWORD, MOCK_ACCOUNTS } from '@/mocks/data/users';
import { cn } from '@/lib/cn';
import illustration from '@/assets/images/login-illustration.svg';
import { ForgotPasswordDialog } from './ForgotPasswordDialog';

interface LoginForm {
  email: string;
  password: string;
  remember: boolean;
}

/** Trang đăng nhập: khung ảnh (trái) + form (phải). Mobile: chỉ còn form. */
export default function LoginPage() {
  const user = useAuthStore((s) => s.user);
  const signIn = useAuthStore((s) => s.signIn);
  const navigate = useNavigate();
  const location = useLocation();
  const [form] = Form.useForm<LoginForm>();
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [forgotOpen, setForgotOpen] = useState(false);
  const [forgotEmail, setForgotEmail] = useState<string>();
  const [demoOpen, setDemoOpen] = useState(false);

  // Đã đăng nhập → về trang chủ của cổng
  if (user) return <Navigate to={PORTAL_HOME[user.portal]} replace />;

  const onFinish = async (v: LoginForm) => {
    setLoading(true);
    setError(null);
    try {
      const u = await login(v.email, v.password);
      signIn(u, v.remember);
      // quay lại trang định vào trước khi bị chuyển sang đăng nhập (nếu cùng cổng)
      const from = (location.state as { from?: string } | null)?.from;
      const prefix = PORTAL_HOME[u.portal].split('/')[1];
      navigate(from && from.startsWith(`/${prefix}`) ? from : PORTAL_HOME[u.portal], { replace: true });
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Đăng nhập thất bại');
    } finally {
      setLoading(false);
    }
  };

  return (
    <main className="flex min-h-dvh bg-page lg:p-4">
      <div className="flex w-full overflow-hidden bg-surface lg:rounded-2xl lg:border lg:border-line lg:shadow-[var(--shadow-soft-md)]">
        {/* ===== Khung ảnh ===== */}
        <section
          className="relative hidden w-1/2 flex-col overflow-hidden bg-primary-50 lg:flex xl:w-[55%]"
        >
          <div className="px-10 pt-10 pb-6">
          <Logo subtitle={SYSTEM_TAGLINE} />
          <div className="mt-10 max-w-md">
            <p className="font-jp text-sm font-medium tracking-[0.3em] text-primary-600">ようこそ</p>
            <p className="mt-2 text-3xl leading-10 font-semibold text-ink">
              Quản lý trung tâm
              <br />
              gọn gàng, mọi lúc mọi nơi
            </p>
            <p className="mt-3 text-ink-secondary">Lead, lớp học, học phí, giáo viên và học viên của mọi chi nhánh trên cùng một hệ thống.</p>
          </div>
          </div>
          {/* Ảnh chiếm phần còn lại, không đè lên chữ */}
          <div className="relative min-h-[220px] flex-1">
            <img src={illustration} alt="" className="absolute inset-0 h-full w-full object-cover object-center" />
            <div className="pointer-events-none absolute inset-x-0 top-0 h-24 bg-gradient-to-b from-primary-50 to-transparent" aria-hidden />
          </div>
        </section>

        {/* ===== Form đăng nhập ===== */}
        <section className="flex flex-1 flex-col items-center justify-center px-4 py-10 sm:px-8">
          <div className="w-full max-w-[400px]">
            <Logo subtitle={SYSTEM_TAGLINE} className="mb-8 lg:hidden" />
            <h1 className="text-2xl leading-8 font-semibold text-ink">Đăng nhập</h1>
            <p className="mt-1 mb-6 text-ink-muted">Dành cho nhân viên, giáo viên và học viên của trung tâm.</p>

            {error && (
              <div role="alert" className="mb-4 flex items-start gap-2 rounded-lg border border-danger-100 bg-danger-50 px-3 py-2.5 text-[13px] text-danger-700">
                <CircleAlert size={16} className="mt-0.5 shrink-0" aria-hidden />
                {error}
              </div>
            )}

            <Form<LoginForm>
              form={form}
              layout="vertical"
              requiredMark={false}
              initialValues={{ remember: true }}
              onFinish={onFinish}
              onValuesChange={() => error && setError(null)}
            >
              <Form.Item
                name="email"
                label="Email"
                rules={[
                  { required: true, message: 'Nhập email' },
                  { type: 'email', message: 'Email không hợp lệ' },
                ]}
              >
                <TextInput type="email" icon={Mail} placeholder="email@jpcenter.vn" autoComplete="username" size="large" autoFocus />
              </Form.Item>
              <Form.Item name="password" label="Mật khẩu" rules={[{ required: true, message: 'Nhập mật khẩu' }]} className="!mb-3">
                <TextInput type="password" icon={Lock} placeholder="Mật khẩu" autoComplete="current-password" size="large" />
              </Form.Item>

              <div className="mb-6 flex items-center justify-between gap-2">
                <Form.Item name="remember" valuePropName="checked" noStyle>
                  <Checkbox>Ghi nhớ đăng nhập</Checkbox>
                </Form.Item>
                <button
                  type="button"
                  onClick={() => {
                    setForgotEmail(form.getFieldValue('email'));
                    setForgotOpen(true);
                  }}
                  className="rounded text-[13px] font-medium text-primary-700 hover:underline focus-visible:ring-[3px] focus-visible:ring-primary-500/30 focus-visible:outline-none"
                >
                  Quên mật khẩu?
                </button>
              </div>

              <Button icon={LogIn} htmlType="submit" size="lg" block loading={loading}>
                Đăng nhập
              </Button>
            </Form>

            {/* ===== Tài khoản demo (dữ liệu giả) ===== */}
            <div className="mt-8 rounded-[var(--radius-card)] border border-dashed border-line-strong">
              <button
                type="button"
                onClick={() => setDemoOpen((o) => !o)}
                aria-expanded={demoOpen}
                className="flex w-full items-center justify-between px-4 py-3 text-left text-[13px] font-medium text-ink-secondary"
              >
                Tài khoản dùng thử (mật khẩu: {DEMO_PASSWORD})
                <ChevronDown size={16} className={cn('transition-transform', demoOpen && 'rotate-180')} aria-hidden />
              </button>
              {demoOpen && (
                <ul className="grid grid-cols-1 gap-1 border-t border-line p-2 sm:grid-cols-2">
                  {MOCK_ACCOUNTS.map((a) => (
                    <li key={a.id}>
                      <button
                        type="button"
                        onClick={() => {
                          form.setFieldsValue({ email: a.email, password: DEMO_PASSWORD });
                          setError(null);
                        }}
                        className="flex w-full flex-col rounded-md px-2 py-1.5 text-left hover:bg-primary-50"
                      >
                        <span className="text-[13px] font-medium text-ink">{a.roleName}{a.locked ? ' (bị khóa)' : ''}</span>
                        <span className="truncate text-xs text-ink-muted">{a.email}</span>
                      </button>
                    </li>
                  ))}
                </ul>
              )}
            </div>

            <p className="mt-8 text-center text-xs text-ink-muted">© {new Date().getFullYear()} JP Center · Hỗ trợ: 1900 0000</p>
          </div>
        </section>
      </div>

      <ForgotPasswordDialog
        open={forgotOpen}
        initialEmail={forgotEmail}
        onClose={() => setForgotOpen(false)}
        onDone={(email) => {
          setForgotOpen(false);
          form.setFieldsValue({ email, password: '' });
        }}
      />
    </main>
  );
}
