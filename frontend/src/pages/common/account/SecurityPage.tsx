import { useCallback, useEffect, useState } from 'react';
import { Form, Progress } from 'antd';
import { Check, KeyRound, Laptop, LogOut, Smartphone, X } from 'lucide-react';
import { Button, PageHeader, StatusTag, TextInput, useConfirm, useNotify } from '@/components/common';
import { changePassword } from '@/features/auth/authApi';
import * as realAuth from '@/features/auth/realAuth';
import { USE_API } from '@/lib/api/config';
import { useSystemStore } from '@/features/system/systemStore';
import { useAuth } from '@/hooks/useAuth';
import { cn } from '@/lib/cn';
import { fmtDateTime, fromNow } from '@/lib/date';

/** Tiêu chí mật khẩu mạnh */
const RULES = [
  { label: 'Ít nhất 8 ký tự', test: (p: string) => p.length >= 8 },
  { label: 'Có chữ hoa và chữ thường', test: (p: string) => /[a-z]/.test(p) && /[A-Z]/.test(p) },
  { label: 'Có chữ số', test: (p: string) => /\d/.test(p) },
  { label: 'Có ký tự đặc biệt (!@#...)', test: (p: string) => /[^A-Za-z0-9]/.test(p) },
];

interface Values {
  current: string;
  next: string;
  confirm: string;
}

/** Mật khẩu & bảo mật — dùng chung 4 cổng: đổi mật khẩu, thiết bị đang đăng nhập, lịch sử đăng nhập */
export default function SecurityPage() {
  const { user, signOut } = useAuth();
  const notify = useNotify();
  const { confirm, confirmDialog } = useConfirm();
  const audits = useSystemStore((s) => s.audits);
  const { audit } = useSystemStore();
  const [form] = Form.useForm<Values>();
  const next = Form.useWatch('next', form) ?? '';
  const [saving, setSaving] = useState(false);
  const [devices, setDevices] = useState(USE_API ? [] : [
    { id: 'd1', name: 'Chrome · Windows', place: 'Hà Nội', at: new Date().toISOString(), current: true, icon: Laptop },
    { id: 'd2', name: 'Safari · iPhone', place: 'Hà Nội', at: new Date(Date.now() - 3 * 3600_000).toISOString(), current: false, icon: Smartphone },
    { id: 'd3', name: 'Edge · Windows', place: 'TP.HCM', at: new Date(Date.now() - 4 * 86400_000).toISOString(), current: false, icon: Laptop },
  ]);

  // API thật: thiết bị đang đăng nhập + lịch sử đăng nhập lấy từ server (AUTH-08, AUTH-09)
  const [apiLogins, setApiLogins] = useState<{ id: string; action: string; severity: string; at: string; device: string }[]>([]);
  const reload = useCallback(async () => {
    if (!USE_API) return;
    try {
      const [sessions, history] = await Promise.all([realAuth.mySessions(), realAuth.loginHistory()]);
      setDevices(sessions.map((s) => ({ id: s.id, name: s.device, place: s.ip, at: s.lastSeenAt, current: s.current, icon: /iphone|android|mobile/i.test(s.device) ? Smartphone : Laptop })));
      setApiLogins(history.items.slice(0, 8).map((h, i) => ({ id: String(i), action: h.action, severity: h.success ? 'info' : 'critical', at: h.at, device: `${h.device} · ${h.ip}` })));
    } catch {
      /* không tải được thì để trống */
    }
  }, []);
  useEffect(() => {
    void reload();
  }, [reload]);

  const revokeOthers = async () => {
    if (USE_API) {
      await realAuth.revokeOtherSessions();
      await reload();
    } else setDevices((d) => d.filter((x) => x.current));
  };

  if (!user) return null;
  const actor = { name: user.fullName, role: user.roleName };
  const passed = RULES.filter((r) => r.test(next)).length;
  const logins = USE_API ? apiLogins : audits.filter((a) => a.category === 'auth' && (a.actor === user.fullName || a.target === user.email)).slice(0, 8);

  const submit = async (v: Values) => {
    setSaving(true);
    try {
      await changePassword(user.email, v.current, v.next);
      audit(actor, { category: 'auth', severity: 'warning', action: 'Đổi mật khẩu', target: user.email });
      form.resetFields();
      notify.success({ title: 'Đã đổi mật khẩu', description: 'Lần đăng nhập sau dùng mật khẩu mới.' });
      const out = await confirm({ title: 'Đăng xuất các thiết bị khác?', description: 'Nên làm nếu bạn nghi mật khẩu cũ bị lộ.', tone: 'primary', confirmText: 'Đăng xuất thiết bị khác', cancelText: 'Để sau' });
      if (out) await revokeOthers();
    } catch (e) {
      form.setFields([{ name: (e as Error).message.includes('hiện tại') ? 'current' : 'next', errors: [(e as Error).message] }]);
    } finally {
      setSaving(false);
    }
  };

  return (
    <>
      <PageHeader title="Mật khẩu & bảo mật" breadcrumb={[{ title: 'Cá nhân' }, { title: 'Mật khẩu & bảo mật' }]} />
      <div className="grid gap-4 lg:grid-cols-2">
        <section className="rounded-xl border border-line bg-surface p-4">
          <h2 className="mb-3 flex items-center gap-1.5 font-semibold text-ink">
            <KeyRound size={16} aria-hidden /> Đổi mật khẩu
          </h2>
          <Form form={form} layout="vertical" onFinish={submit} requiredMark={false}>
            <Form.Item name="current" label="Mật khẩu hiện tại" rules={[{ required: true, message: 'Nhập mật khẩu hiện tại' }]}>
              <TextInput type="password" autoComplete="current-password" />
            </Form.Item>
            <Form.Item
              name="next"
              label="Mật khẩu mới"
              rules={[{ required: true, message: 'Nhập mật khẩu mới' }, { validator: (_, v: string) => (!v || RULES.slice(0, 3).every((r) => r.test(v)) ? Promise.resolve() : Promise.reject(new Error('Chưa đủ mạnh — xem tiêu chí bên dưới'))) }]}
              className="!mb-2"
            >
              <TextInput type="password" autoComplete="new-password" />
            </Form.Item>
            <Progress percent={(passed / RULES.length) * 100} showInfo={false} size="small" status={passed < 3 ? 'exception' : 'success'} />
            <ul className="mb-4 mt-1 grid grid-cols-1 gap-0.5 text-xs sm:grid-cols-2">
              {RULES.map((r) => {
                const ok = r.test(next);
                return (
                  <li key={r.label} className={cn('inline-flex items-center gap-1', ok ? 'text-success-700' : 'text-ink-muted')}>
                    {ok ? <Check size={12} aria-hidden /> : <X size={12} aria-hidden />} {r.label}
                  </li>
                );
              })}
            </ul>
            <Form.Item
              name="confirm"
              label="Nhập lại mật khẩu mới"
              dependencies={['next']}
              rules={[{ required: true, message: 'Nhập lại mật khẩu' }, ({ getFieldValue }) => ({ validator: (_, v) => (!v || v === getFieldValue('next') ? Promise.resolve() : Promise.reject(new Error('Không khớp mật khẩu mới'))) })]}
            >
              <TextInput type="password" autoComplete="new-password" />
            </Form.Item>
            <Button action="save" htmlType="submit" loading={saving}>
              Đổi mật khẩu
            </Button>
          </Form>
        </section>

        <div className="flex flex-col gap-4">
          <section className="rounded-xl border border-line bg-surface p-4">
            <div className="mb-3 flex items-center justify-between gap-2">
              <h2 className="font-semibold text-ink">Thiết bị đang đăng nhập</h2>
              {devices.length > 1 && (
                <Button
                  icon={LogOut}
                  size="sm"
                  variant="ghost"
                  tone="danger"
                  onClick={async () => {
                    if (await confirm({ title: 'Đăng xuất mọi thiết bị khác?', tone: 'warning' })) {
                      await revokeOthers();
                      audit(actor, { category: 'auth', severity: 'info', action: 'Đăng xuất thiết bị khác', target: user.email });
                      notify.toast.success('Đã đăng xuất các thiết bị khác');
                    }
                  }}
                >
                  Đăng xuất thiết bị khác
                </Button>
              )}
            </div>
            <ul className="flex flex-col divide-y divide-line text-[13px]">
              {devices.map((d) => (
                <li key={d.id} className="flex items-center gap-3 py-2">
                  <d.icon size={18} className="text-ink-muted" aria-hidden />
                  <span className="flex-1">
                    {d.name}
                    <span className="block text-xs text-ink-muted">
                      {d.place} · {d.current ? 'đang dùng' : fromNow(d.at)}
                    </span>
                  </span>
                  {d.current ? (
                    <StatusTag tone="success" icon={Check} size="sm">
                      Thiết bị này
                    </StatusTag>
                  ) : (
                    <Button icon={LogOut} iconOnly size="sm" variant="ghost" tone="neutral" aria-label="Đăng xuất thiết bị" onClick={async () => {
                        if (USE_API) {
                          await realAuth.revokeSession(d.id);
                          await reload();
                        } else setDevices((x) => x.filter((y) => y.id !== d.id));
                      }} />
                  )}
                </li>
              ))}
            </ul>
            <Button
              icon={LogOut}
              size="sm"
              variant="outline"
              tone="neutral"
              className="mt-2"
              onClick={async () => {
                if (await confirm({ title: 'Đăng xuất khỏi thiết bị này?', tone: 'primary', confirmAction: 'logout' })) signOut();
              }}
            >
              Đăng xuất
            </Button>
          </section>

          {logins.length > 0 && (
            <section className="rounded-xl border border-line bg-surface p-4">
              <h2 className="mb-2 font-semibold text-ink">Hoạt động đăng nhập gần đây</h2>
              <ul className="flex flex-col gap-1.5 text-[13px]">
                {logins.map((a) => (
                  <li key={a.id} className="flex justify-between gap-2">
                    <span className={a.severity === 'critical' ? 'text-danger-700' : 'text-ink'}>{a.action}</span>
                    <span className="text-xs text-ink-muted">
                      {fmtDateTime(a.at)} · {a.device}
                    </span>
                  </li>
                ))}
              </ul>
            </section>
          )}
        </div>
      </div>
      {confirmDialog}
    </>
  );
}
