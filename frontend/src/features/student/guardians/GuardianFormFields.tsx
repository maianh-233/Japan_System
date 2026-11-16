import { Checkbox, Form } from 'antd';
import { Info, Mail, Phone } from 'lucide-react';
import { Select, TextArea, TextInput } from '@/components/common';
import { useCrmStore } from '@/features/crm/crmStore';
import type { NotifyChannel } from '@/features/crm/types';
import { useAuth } from '@/hooks/useAuth';
import { ageOf, GUARDIAN_ROLE_OPTIONS, NOTIFY_CHANNEL_OPTIONS, type GuardianRelationship } from './guardianUtils';

export interface GuardianFormValues {
  fullName: string;
  phone: string;
  email?: string;
  notifyChannels?: NotifyChannel[];
  note?: string;
  // ---- chỉ khi THÊM
  branchId?: string;
  studentIds?: string[];
  relationship?: GuardianRelationship;
  /** Đặt làm người liên hệ chính + nhận hóa đơn cho các con được chọn */
  primary?: boolean;
  issueAccount?: boolean;
}

/**
 * Ô nhập phụ huynh. mode = "create": kèm chọn con + vai trò; "edit": chỉ thông tin liên hệ.
 * SĐT trùng người liên hệ khác → khi THÊM sẽ liên kết vào người có sẵn (không tạo trùng).
 */
export function GuardianFormFields({ mode, editingId }: { mode: 'create' | 'edit'; editingId?: string }) {
  const { branches } = useAuth();
  const form = Form.useFormInstance<GuardianFormValues>();
  const findDuplicate = useCrmStore((s) => s.findDuplicate);
  const students = useCrmStore((s) => s.students);
  const phone = Form.useWatch('phone', form);
  const branchId = Form.useWatch('branchId', form);
  const dup = phone && phone.replace(/\D/g, '').length >= 9 ? findDuplicate(phone, editingId) : undefined;

  return (
    <>
      <Form.Item name="fullName" label="Họ tên" rules={[{ required: true, whitespace: true, message: 'Nhập họ tên' }]}>
        <TextInput placeholder="VD: Nguyễn Văn Bình" maxLength={80} />
      </Form.Item>
      <Form.Item
        name="phone"
        label="Số điện thoại"
        rules={[
          { required: true, message: 'Nhập số điện thoại' },
          { pattern: /^0\d{9}$/, message: 'SĐT gồm 10 số, bắt đầu bằng 0' },
          ...(mode === 'edit'
            ? [{ validator: (_: unknown, v: string) => (v && findDuplicate(v, editingId) ? Promise.reject(new Error('SĐT đã thuộc người liên hệ khác')) : Promise.resolve()) }]
            : []),
        ]}
        extra={
          mode === 'create' && dup ? (
            <span className="inline-flex items-start gap-1.5 text-primary-700">
              <Info size={14} className="mt-0.5 shrink-0" aria-hidden />
              Trùng {dup.fullName} ({dup.code}) — sẽ liên kết con vào người này, không tạo mới.
            </span>
          ) : undefined
        }
      >
        <TextInput icon={Phone} placeholder="09xxxxxxxx" maxLength={10} inputMode="tel" />
      </Form.Item>
      <Form.Item name="email" label="Email" rules={[{ type: 'email', message: 'Email không hợp lệ' }]}>
        <TextInput icon={Mail} placeholder="Nhận hóa đơn, thông báo" maxLength={120} />
      </Form.Item>
      <Form.Item name="notifyChannels" label="Kênh nhận thông báo">
        <Checkbox.Group options={NOTIFY_CHANNEL_OPTIONS} />
      </Form.Item>

      {mode === 'create' && (
        <>
          <Form.Item name="branchId" label="Chi nhánh" rules={[{ required: true, message: 'Chọn chi nhánh' }]}>
            <Select options={branches.map((b) => ({ value: b.id, label: b.name }))} showSearch={false} allowClear={false} onChange={() => form.setFieldValue('studentIds', [])} />
          </Form.Item>
          <Form.Item name="relationship" label="Là ... của học viên" rules={[{ required: true, message: 'Chọn vai trò' }]}>
            <Select options={GUARDIAN_ROLE_OPTIONS} showSearch={false} allowClear={false} />
          </Form.Item>
          <Form.Item
            name="studentIds"
            label="Học viên (con / người được giám hộ)"
            className="md:col-span-2"
            rules={[{ required: true, type: 'array', min: 1, message: 'Chọn ít nhất 1 học viên' }]}
            extra="Một phụ huynh liên kết được nhiều học viên (anh chị em)."
          >
            <Select
              mode="multiple"
              placeholder="Tìm theo tên, mã HV"
              optionFilterProp="label"
              options={students
                .filter((s) => !s.hidden && (!branchId || s.branchId === branchId))
                .map((s) => {
                  const age = ageOf(s.dob);
                  return { value: s.id, label: `${s.fullName} · ${s.code}${age !== undefined ? ` · ${age} tuổi` : ''}` };
                })}
            />
          </Form.Item>
          <Form.Item name="primary" valuePropName="checked" className="md:col-span-2">
            <Checkbox>Đặt làm người liên hệ chính & nhận hóa đơn học phí của các học viên trên</Checkbox>
          </Form.Item>
          <Form.Item name="issueAccount" valuePropName="checked" className="md:col-span-2 -mt-3">
            <Checkbox>Cấp luôn tài khoản Cổng phụ huynh (gửi mật khẩu tạm qua SMS)</Checkbox>
          </Form.Item>
        </>
      )}

      <Form.Item name="note" label="Ghi chú" className="md:col-span-2">
        <TextArea rows={2} maxLength={500} placeholder="VD: chỉ gọi sau 17h, ưu tiên nhắn Zalo" />
      </Form.Item>
    </>
  );
}
