import { useEffect, useState } from 'react';
import { Form, Input, Modal } from 'antd';
import { CircleCheckBig, KeyRound, Lock, Mail, MailCheck, ShieldCheck } from 'lucide-react';
import { Button, TextInput } from '@/components/common';
import { cn } from '@/lib/cn';
import { requestPasswordReset, resetPassword, verifyOtp } from '@/features/auth/authApi';
import { DEMO_OTP } from '@/mocks/data/users';

type Step = 'email' | 'otp' | 'reset' | 'done';

interface ForgotPasswordDialogProps {
  open: boolean;
  /** Email đang nhập ở form đăng nhập — điền sẵn */
  initialEmail?: string;
  onClose: () => void;
  /** Đặt lại xong → quay về đăng nhập với email này */
  onDone: (email: string) => void;
}

const STEPS: { key: Exclude<Step, 'done'>; label: string }[] = [
  { key: 'email', label: 'Email' },
  { key: 'otp', label: 'Mã OTP' },
  { key: 'reset', label: 'Mật khẩu mới' },
];

/** Quên mật khẩu: nhập email → Kiểm tra → nhập OTP → đặt mật khẩu mới. (Sự kiện giả lập) */
export function ForgotPasswordDialog({ open, initialEmail, onClose, onDone }: ForgotPasswordDialogProps) {
  const [step, setStep] = useState<Step>('email');
  const [email, setEmail] = useState('');
  const [maskedEmail, setMaskedEmail] = useState('');
  const [otp, setOtp] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [countdown, setCountdown] = useState(0);
  const [form] = Form.useForm<{ password: string; confirm: string }>();

  // Mở lại → về bước 1
  useEffect(() => {
    if (open) {
      setStep('email');
      setEmail(initialEmail ?? '');
      setOtp('');
      setError(null);
      setCountdown(0);
    }
  }, [open, initialEmail]);

  // Đếm ngược gửi lại mã
  useEffect(() => {
    if (countdown <= 0) return;
    const t = setTimeout(() => setCountdown((c) => c - 1), 1000);
    return () => clearTimeout(t);
  }, [countdown]);

  const run = async (fn: () => Promise<void>) => {
    setLoading(true);
    setError(null);
    try {
      await fn();
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Có lỗi xảy ra');
    } finally {
      setLoading(false);
    }
  };

  const checkEmail = () => {
    if (!/^\S+@\S+\.\S+$/.test(email.trim())) {
      setError('Email không hợp lệ');
      return;
    }
    run(async () => {
      const res = await requestPasswordReset(email);
      setMaskedEmail(res.maskedEmail);
      setCountdown(res.resendAfter);
      setOtp('');
      setStep('otp');
    });
  };

  const submitOtp = (code = otp) => {
    if (code.length !== 6) {
      setError('Nhập đủ 6 số');
      return;
    }
    run(async () => {
      try {
        await verifyOtp(email, code);
      } catch (e) {
        setOtp(''); // sai → xóa trắng để nhập lại ngay
        throw e;
      }
      setStep('reset');
    });
  };

  const resend = () =>
    run(async () => {
      const res = await requestPasswordReset(email);
      setCountdown(res.resendAfter);
      setOtp('');
    });

  const submitReset = ({ password }: { password: string }) =>
    run(async () => {
      await resetPassword(email, password);
      setStep('done');
    });

  const stepIndex = STEPS.findIndex((s) => s.key === step);

  return (
    <Modal open={open} onCancel={loading ? undefined : onClose} footer={null} width={440} centered destroyOnHidden title={null}>
      {/* Thanh bước */}
      {step !== 'done' && (
        <ol className="mb-6 flex items-center gap-2 pr-6" aria-label="Các bước đặt lại mật khẩu">
          {STEPS.map((s, i) => (
            <li key={s.key} className="flex flex-1 items-center gap-2">
              <span
                className={cn(
                  'tabular flex h-7 w-7 shrink-0 items-center justify-center rounded-full text-[13px] font-semibold',
                  i < stepIndex && 'bg-success-50 text-success-700',
                  i === stepIndex && 'bg-primary-600 text-white',
                  i > stepIndex && 'bg-disabled text-ink-muted',
                )}
                aria-current={i === stepIndex ? 'step' : undefined}
              >
                {i < stepIndex ? '✓' : i + 1}
              </span>
              <span className={cn('truncate text-[13px] max-sm:hidden', i === stepIndex ? 'font-medium text-ink' : 'text-ink-muted')}>
                {s.label}
              </span>
              {i < STEPS.length - 1 && <span className="h-px flex-1 bg-line" aria-hidden />}
            </li>
          ))}
        </ol>
      )}

      {step === 'email' && (
        <form
          className="flex flex-col gap-4"
          onSubmit={(e) => {
            e.preventDefault();
            checkEmail();
          }}
        >
          <Header icon={KeyRound} title="Quên mật khẩu" desc="Nhập email đăng nhập, hệ thống sẽ gửi mã xác thực 6 số." />
          <TextInput
            label="Email"
            type="email"
            icon={Mail}
            value={email}
            onChange={(e) => {
              setEmail(e.target.value);
              setError(null);
            }}
            placeholder="email@jpcenter.vn"
            error={error ?? undefined}
            autoFocus
            autoComplete="email"
          />
          <Button icon={MailCheck} htmlType="submit" loading={loading} block size="lg">
            Kiểm tra
          </Button>
        </form>
      )}

      {step === 'otp' && (
        <div className="flex flex-col gap-4">
          <Header
            icon={ShieldCheck}
            title="Nhập mã xác thực"
            desc={
              <>
                Mã 6 số đã được gửi tới <strong className="text-ink">{maskedEmail}</strong>. Mã có hiệu lực 5 phút.
              </>
            }
          />
          <div className="flex flex-col items-center gap-2">
            <Input.OTP
              length={6}
              value={otp}
              onChange={(v) => {
                setOtp(v);
                setError(null);
                if (v.length === 6) submitOtp(v);
              }}
              onInput={(chars) => setOtp(chars.join(''))}
              status={error ? 'error' : undefined}
              disabled={loading}
              size="large"
              formatter={(s) => s.replace(/\D/g, '')}
              autoFocus
              aria-label="Mã OTP 6 số"
            />
            {error && (
              <p role="alert" className="text-center text-[13px] text-danger-600">
                {error}
              </p>
            )}
            <p className="rounded-md bg-warning-50 px-2 py-1 text-xs text-warning-700">Dữ liệu giả: mã OTP là {DEMO_OTP}</p>
          </div>
          <Button icon={ShieldCheck} loading={loading} block size="lg" onClick={() => submitOtp()}>
            Xác nhận
          </Button>
          <div className="flex items-center justify-between text-[13px]">
            <button type="button" className="text-ink-secondary hover:text-primary-700" onClick={() => setStep('email')} disabled={loading}>
              ← Đổi email
            </button>
            {countdown > 0 ? (
              <span className="tabular text-ink-muted">Gửi lại mã sau {countdown}s</span>
            ) : (
              <button type="button" className="font-medium text-primary-700 hover:underline" onClick={resend} disabled={loading}>
                Gửi lại mã
              </button>
            )}
          </div>
        </div>
      )}

      {step === 'reset' && (
        <div className="flex flex-col gap-4">
          <Header icon={Lock} title="Đặt mật khẩu mới" desc="Tối thiểu 8 ký tự, gồm cả chữ và số." />
          <Form form={form} layout="vertical" requiredMark={false} onFinish={submitReset}>
            <Form.Item
              name="password"
              label="Mật khẩu mới"
              rules={[
                { required: true, message: 'Nhập mật khẩu mới' },
                { min: 8, message: 'Tối thiểu 8 ký tự' },
                { pattern: /(?=.*[A-Za-z])(?=.*\d)/, message: 'Cần có cả chữ và số' },
              ]}
            >
              <TextInput type="password" icon={Lock} autoFocus autoComplete="new-password" />
            </Form.Item>
            <Form.Item
              name="confirm"
              label="Nhập lại mật khẩu"
              dependencies={['password']}
              rules={[
                { required: true, message: 'Nhập lại mật khẩu' },
                ({ getFieldValue }) => ({
                  validator: (_, v) =>
                    !v || v === getFieldValue('password') ? Promise.resolve() : Promise.reject(new Error('Mật khẩu nhập lại không khớp')),
                }),
              ]}
            >
              <TextInput type="password" icon={Lock} autoComplete="new-password" />
            </Form.Item>
            {error && (
              <p role="alert" className="mb-3 text-[13px] text-danger-600">
                {error}
              </p>
            )}
            <Button action="save" htmlType="submit" loading={loading} block size="lg">
              Đặt lại mật khẩu
            </Button>
          </Form>
        </div>
      )}

      {step === 'done' && (
        <div className="flex flex-col items-center gap-3 py-4 text-center">
          <span className="flex h-14 w-14 items-center justify-center rounded-full bg-success-50 text-success-600">
            <CircleCheckBig size={28} aria-hidden />
          </span>
          <h2 className="text-lg font-semibold">Đổi mật khẩu thành công</h2>
          <p className="text-ink-muted">Bạn có thể đăng nhập bằng mật khẩu mới.</p>
          <Button icon={KeyRound} block size="lg" className="mt-2" onClick={() => onDone(email)}>
            Đăng nhập ngay
          </Button>
        </div>
      )}
    </Modal>
  );
}

function Header({ icon: Icon, title, desc }: { icon: typeof KeyRound; title: string; desc: React.ReactNode }) {
  return (
    <div className="flex flex-col gap-1">
      <span className="mb-1 flex h-11 w-11 items-center justify-center rounded-full bg-primary-100 text-primary-600">
        <Icon size={22} aria-hidden />
      </span>
      <h2 className="text-lg leading-7 font-semibold text-ink">{title}</h2>
      <p className="text-ink-muted">{desc}</p>
    </div>
  );
}
