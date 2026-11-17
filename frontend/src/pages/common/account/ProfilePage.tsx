import { useEffect, useState } from 'react';
import { Form, Switch } from 'antd';
import dayjs from 'dayjs';
import { Bell, Briefcase, KeyRound, Mail, Phone, UserRound } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { Avatar, Button, DateInput, ImagePicker, PageHeader, TextInput, useNotify } from '@/components/common';
import { useCrmStore } from '@/features/crm/crmStore';
import { CONTRACT_TYPES, DEPARTMENTS } from '@/features/hr/constants';
import * as hrApi from '@/features/hr/hrApi';
import { useHrStore } from '@/features/hr/hrStore';
import { nameOf } from '@/features/hr/hrUtils';
import { useMe } from '@/features/me/useMe';
import { useStudentId } from '@/features/portal/usePortal';
import { useSystemStore } from '@/features/system/systemStore';
import { useAuth } from '@/hooks/useAuth';
import { USE_API } from '@/lib/api/config';
import { fmtDate, fromNow } from '@/lib/date';
import { getBranches } from '@/features/system/useBranches';
import { useAuthStore } from '@/stores/authStore';

const branchName = (id?: string) => getBranches(false).find((b) => b.id === id)?.name ?? id ?? '—';
const PORTAL_BASE: Record<string, string> = { staff: '/admin/me', teacher: '/teacher', student: '/student', parent: '/parent' };

/** Một dòng thông tin chỉ đọc */
function Info({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="contents">
      <dt className="text-ink-muted">{label}</dt>
      <dd className="text-ink">{children}</dd>
    </div>
  );
}

/**
 * Thông tin cá nhân — dùng chung 4 cổng.
 * Tự sửa: ảnh đại diện, SĐT, ngày sinh, kênh nhận thông báo. Chức vụ / hợp đồng / lương do phòng nhân sự cập nhật.
 * Chế độ API (NS-11): nhân viên / GV đọc & tự sửa SĐT, ngày sinh qua /hr/me. Ảnh đại diện, kênh thông báo, phần học viên vẫn dữ liệu giả ⏳.
 */
export default function ProfilePage() {
  const { user } = useAuth();
  const navigate = useNavigate();
  const notify = useNotify();
  const me = useMe();
  const employees = useHrStore((s) => s.employees);
  const updateEmployee = useHrStore((s) => s.updateEmployee);
  const studentId = useStudentId();
  const student = useCrmStore((s) => s.students.find((x) => x.id === studentId));
  const updateStudent = useCrmStore((s) => s.updateStudent);
  const access = useSystemStore((s) => s.access.find((a) => a.userId === me?.id));
  const roles = useSystemStore((s) => s.roles);
  const [notify3, setNotify3] = useState({ portal: true, zalo: true, email: user?.portal === 'staff' });
  const [form] = Form.useForm<{ phone?: string; dob?: dayjs.Dayjs }>();
  const hasProfile = user?.portal === 'staff' || user?.portal === 'teacher';

  // Lấy hồ sơ mới nhất của chính mình (không cần quyền trang Nhân sự)
  useEffect(() => {
    if (!USE_API || !hasProfile) return;
    hrApi
      .loadMyProfile()
      .then((me) => form.setFieldsValue({ phone: me.phone, dob: me.dob ? dayjs(me.dob) : undefined }))
      .catch((e: Error) => notify.error({ title: 'Chưa tải được hồ sơ', description: e.message }));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [hasProfile]);

  if (!user) return null;
  const isStudent = user.portal === 'student';
  const editable = me ?? (isStudent ? student : undefined);

  const setAvatar = (url: string | null) => {
    useAuthStore.setState({ user: { ...user, avatarUrl: url ?? undefined } });
    notify.toast.success(url ? 'Đã đổi ảnh đại diện' : 'Đã xóa ảnh đại diện');
  };

  const save = async (v: { phone?: string; dob?: dayjs.Dayjs }) => {
    if (USE_API && me) {
      try {
        await hrApi.updateMyContact(v.phone ?? me.phone, v.dob?.format('YYYY-MM-DD'));
        notify.toast.success('Đã lưu thông tin');
      } catch (e) {
        notify.error({ title: 'Chưa lưu được', description: (e as Error).message });
      }
      return;
    }
    const patch = { phone: v.phone, dob: v.dob?.format('YYYY-MM-DD') };
    if (me) updateEmployee(me.id, patch);
    else if (isStudent && student) updateStudent(student.id, patch);
    notify.toast.success('Đã lưu thông tin');
  };

  return (
    <>
      <PageHeader title="Thông tin cá nhân" breadcrumb={[{ title: 'Cá nhân' }, { title: 'Thông tin cá nhân' }]} />
      <div className="grid gap-4 lg:grid-cols-[320px_1fr]">
        {/* Thẻ tài khoản */}
        <section className="flex flex-col items-center gap-3 rounded-xl border border-line bg-surface p-5 text-center">
          <div className="w-32">
            <ImagePicker shape="circle" value={user.avatarUrl ?? null} onChange={(url) => setAvatar(url)} maxSizeMB={2} placeholder="Ảnh đại diện" />
          </div>
          <div>
            <p className="text-lg font-semibold text-ink">{user.fullName}</p>
            <p className="text-[13px] text-ink-secondary">{user.roleName}</p>
            <p className="mt-1 inline-flex items-center gap-1 text-xs text-ink-muted">
              <Mail size={12} aria-hidden /> {user.email}
            </p>
          </div>
          {access && (
            <div className="flex w-full flex-col gap-1.5 rounded-lg bg-canvas p-3 text-left text-[13px]">
              <p className="text-xs font-semibold text-ink-muted uppercase">Vai trò trong hệ thống</p>
              {access.roles.map((r, i) => (
                <p key={i}>
                  {roles.find((x) => x.id === r.roleId)?.name} <span className="text-ink-muted">@ {r.branchIds.length ? r.branchIds.map(branchName).join(', ') : 'mọi chi nhánh'}</span>
                </p>
              ))}
              <p className="text-xs text-ink-muted">Đăng nhập gần nhất: {access.lastLoginAt ? fromNow(access.lastLoginAt) : '—'}</p>
            </div>
          )}
          <Button icon={KeyRound} variant="outline" block onClick={() => navigate(`${PORTAL_BASE[user.portal]}/security`)}>
            Đổi mật khẩu
          </Button>
        </section>

        <div className="flex flex-col gap-4">
          {/* Công việc (chỉ đọc) */}
          {me && (
            <section className="rounded-xl border border-line bg-surface p-4">
              <h2 className="mb-3 flex items-center gap-1.5 font-semibold text-ink">
                <Briefcase size={16} aria-hidden /> Công việc
              </h2>
              <dl className="grid grid-cols-[130px_1fr] gap-x-4 gap-y-2 text-[13px] sm:grid-cols-[130px_1fr_130px_1fr]">
                <Info label="Mã nhân viên">{me.code}</Info>
                <Info label="Chức vụ">{me.position}</Info>
                <Info label="Phòng ban">{DEPARTMENTS[me.department]}</Info>
                <Info label="Chi nhánh">{branchName(me.branchId)}</Info>
                <Info label="Quản lý trực tiếp">{me.managerId ? nameOf(employees, me.managerId) : '—'}</Info>
                <Info label="Ngày vào làm">{fmtDate(me.joinedAt)}</Info>
                <Info label="Hợp đồng">
                  {CONTRACT_TYPES[me.contract.type]}
                  {me.contract.end && <span className="text-ink-muted"> · hết hạn {fmtDate(me.contract.end)}</span>}
                </Info>
                <Info label="Phép năm còn">{me.leaveBalance} ngày</Info>
                <Info label="Làm việc">{me.shiftBased ? 'Theo ca (lịch Xếp ca)' : 'Giờ hành chính'}</Info>
                {me.teacher && <Info label="Chứng chỉ">{me.teacher.certificates.join(', ')}</Info>}
              </dl>
              <p className="mt-3 text-xs text-ink-muted">Sai thông tin công việc / hợp đồng? Liên hệ phòng nhân sự để cập nhật.</p>
            </section>
          )}
          {!me && student && (
            <section className="rounded-xl border border-line bg-surface p-4">
              <h2 className="mb-3 flex items-center gap-1.5 font-semibold text-ink">
                <UserRound size={16} aria-hidden /> {isStudent ? 'Hồ sơ học viên' : 'Con đang học'}
              </h2>
              <div className="flex items-center gap-3">
                <Avatar name={student.fullName} size="md" />
                <dl className="grid grid-cols-[110px_1fr] gap-x-4 gap-y-1 text-[13px]">
                  <Info label="Họ tên">{student.fullName}</Info>
                  <Info label="Mã học viên">{student.code}</Info>
                  <Info label="Chi nhánh">{branchName(student.branchId)}</Info>
                  <Info label="Ngày nhập học">{fmtDate(student.joinedAt)}</Info>
                </dl>
              </div>
            </section>
          )}

          {/* Liên hệ (tự sửa) */}
          {editable && (
            <section className="rounded-xl border border-line bg-surface p-4">
              <h2 className="mb-3 flex items-center gap-1.5 font-semibold text-ink">
                <Phone size={16} aria-hidden /> Liên hệ
              </h2>
              <Form form={form} layout="vertical" initialValues={{ phone: editable.phone, dob: editable.dob ? dayjs(editable.dob) : undefined }} onFinish={save}>
                <div className="grid gap-x-4 sm:grid-cols-2">
                  <Form.Item name="phone" label="Số điện thoại" rules={[{ pattern: /^0\d{9}$/, message: 'Gồm 10 số, bắt đầu bằng 0' }]}>
                    <TextInput type="tel" icon={Phone} />
                  </Form.Item>
                  <Form.Item name="dob" label="Ngày sinh">
                    <DateInput disabledDate={(d) => d.isAfter(dayjs().subtract(5, 'year'))} />
                  </Form.Item>
                </div>
                <Button action="save" htmlType="submit" size="sm" />
              </Form>
            </section>
          )}

          {/* Nhận thông báo */}
          <section className="rounded-xl border border-line bg-surface p-4">
            <h2 className="mb-3 flex items-center gap-1.5 font-semibold text-ink">
              <Bell size={16} aria-hidden /> Nhận thông báo qua
            </h2>
            <ul className="flex flex-col gap-2 text-[13px]">
              {(
                [
                  ['portal', 'Chuông thông báo trên hệ thống', true],
                  ['zalo', 'Zalo (OA của trung tâm)', false],
                  ['email', `Email (${user.email})`, false],
                ] as const
              ).map(([k, label, locked]) => (
                <li key={k} className="flex items-center justify-between gap-3">
                  <span>
                    {label}
                    {locked && <span className="ml-1 text-xs text-ink-muted">(luôn bật)</span>}
                  </span>
                  <Switch
                    size="small"
                    checked={notify3[k]}
                    disabled={locked}
                    onChange={(v) => {
                      setNotify3((s) => ({ ...s, [k]: v }));
                      notify.toast.success('Đã lưu cài đặt thông báo');
                    }}
                    aria-label={label}
                  />
                </li>
              ))}
            </ul>
          </section>
        </div>
      </div>
    </>
  );
}
