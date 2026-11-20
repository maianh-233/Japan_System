import { useState } from 'react';
import { Form } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { Mail, MapPin, Phone, User } from 'lucide-react';
import { Button, DateInput, Select, TextArea, TextInput, useNotify } from '@/components/common';
import { useCrmStore } from '@/features/crm/crmStore';
import * as studentApi from '@/features/student/studentApi';
import { USE_API } from '@/lib/api/config';
import type { Student } from '@/features/crm/types';
import { useAuth } from '@/hooks/useAuth';
import { fmtDate, fmtDateTime } from '@/lib/date';
import { getBranches } from '@/features/system/useBranches';
import { GENDER_OPTIONS } from '../constants';
import type { Gender } from '../types';

interface FormValues {
  fullName: string;
  gender?: Gender;
  dob?: Dayjs | null;
  phone?: string;
  email?: string;
  address?: string;
  note?: string;
}

const toForm = (s: Student): FormValues => ({
  fullName: s.fullName,
  gender: s.gender,
  dob: s.dob ? dayjs(s.dob) : undefined,
  phone: s.phone,
  email: s.email,
  address: s.address,
  note: s.note,
});

/** Tab Thông tin: phần cố định (mã, chi nhánh, ngày nhập học) + form sửa thông tin cơ bản */
export function StudentInfoTab({ student }: { student: Student }) {
  const [form] = Form.useForm<FormValues>();
  const notify = useNotify();
  const { can } = useAuth();
  const updateStudent = useCrmStore((s) => s.updateStudent);
  const [saving, setSaving] = useState(false);
  const [dirty, setDirty] = useState(false);
  const editable = can('students.profiles');

  const save = async (v: FormValues) => {
    setSaving(true);
    if (USE_API) {
      // Server chuẩn hóa SĐT / email, kiểm ngày sinh, chặn sửa đè
      try {
        await studentApi.updateStudent(student.id, {
          fullName: v.fullName.trim(),
          gender: v.gender,
          dob: v.dob ? v.dob.format('YYYY-MM-DD') : undefined,
          phone: v.phone || undefined,
          email: v.email || undefined,
          address: v.address || undefined,
          note: v.note || undefined,
          branchId: student.branchId,
        });
      } catch (e) {
        setSaving(false);
        notify.error({ title: 'Chưa lưu được hồ sơ', description: (e as Error).message });
        return;
      }
      setSaving(false);
      setDirty(false);
      notify.success({ title: 'Đã cập nhật hồ sơ học viên', description: `${student.code} — ${v.fullName}` });
      return;
    }
    await new Promise((r) => setTimeout(r, 600)); // giả lập gọi API
    updateStudent(student.id, {
      fullName: v.fullName.trim(),
      gender: v.gender,
      dob: v.dob ? v.dob.format('YYYY-MM-DD') : undefined,
      phone: v.phone || undefined,
      email: v.email || undefined,
      address: v.address || undefined,
      note: v.note || undefined,
    });
    setSaving(false);
    setDirty(false);
    notify.success({ title: 'Đã cập nhật hồ sơ học viên', description: `${student.code} — ${v.fullName}` });
  };

  const age = student.dob ? dayjs().diff(dayjs(student.dob), 'year') : undefined;
  const meta: [string, string][] = [
    ['Mã học viên', student.code],
    ['Chi nhánh', getBranches(false).find((b) => b.id === student.branchId)?.name ?? student.branchId],
    ['Ngày nhập học', fmtDate(student.joinedAt)],
    ['Tuổi', age !== undefined ? `${age} tuổi` : '—'],
  ];
  if (student.hidden && student.hiddenAt) meta.push(['Ẩn lúc', fmtDateTime(student.hiddenAt)]);

  return (
    <div className="flex flex-col gap-5">
      <dl className="grid grid-cols-1 gap-x-6 gap-y-2 rounded-[var(--radius-card)] bg-page p-4 text-[13px] sm:grid-cols-2">
        {meta.map(([k, v]) => (
          <div key={k} className="flex gap-2">
            <dt className="w-28 shrink-0 text-ink-muted">{k}</dt>
            <dd className="min-w-0 font-medium text-ink">{v}</dd>
          </div>
        ))}
      </dl>

      <Form
        form={form}
        layout="vertical"
        requiredMark
        disabled={!editable}
        initialValues={toForm(student)}
        onFinish={save}
        onValuesChange={() => setDirty(true)}
        className="grid grid-cols-1 gap-x-4 md:grid-cols-2"
      >
        <Form.Item name="fullName" label="Họ và tên" rules={[{ required: true, whitespace: true, message: 'Nhập họ tên học viên' }]}>
          <TextInput icon={User} placeholder="Nguyễn Văn A" />
        </Form.Item>
        <Form.Item name="gender" label="Giới tính">
          <Select placeholder="Chọn giới tính" options={GENDER_OPTIONS} showSearch={false} />
        </Form.Item>
        <Form.Item name="dob" label="Ngày sinh">
          <DateInput disabledDate={(d) => d.isAfter(dayjs())} />
        </Form.Item>
        <Form.Item name="phone" label="Số điện thoại" rules={[{ pattern: /^0\d{9}$/, message: 'Gồm 10 số, bắt đầu bằng 0' }]}>
          <TextInput type="tel" icon={Phone} placeholder="09xxxxxxxx" />
        </Form.Item>
        <Form.Item name="email" label="Email" rules={[{ type: 'email', message: 'Email không hợp lệ' }]}>
          <TextInput type="email" icon={Mail} placeholder="email@example.com" />
        </Form.Item>
        <Form.Item name="address" label="Địa chỉ">
          <TextInput icon={MapPin} placeholder="Số nhà, đường, quận/huyện, tỉnh/thành" />
        </Form.Item>
        <Form.Item name="note" label="Ghi chú" className="md:col-span-2">
          <TextArea maxLength={1000} placeholder="Mục tiêu học, lưu ý khi liên hệ, sức khỏe..." />
        </Form.Item>

        {editable && (
          <div className="sticky bottom-0 -mx-6 flex flex-col-reverse gap-2 border-t border-line bg-surface px-6 py-3 sm:flex-row sm:justify-end md:col-span-2">
            <Button
              action="reset"
              disabled={!dirty || saving}
              onClick={() => {
                form.resetFields();
                setDirty(false);
              }}
            >
              Hoàn tác thay đổi
            </Button>
            <Button action="save" htmlType="submit" loading={saving} disabled={!dirty}>
              Lưu thay đổi
            </Button>
          </div>
        )}
      </Form>
    </div>
  );
}
