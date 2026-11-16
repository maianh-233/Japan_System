import { Form } from 'antd';
import dayjs from 'dayjs';
import { ExternalLink, Mail, Phone, User } from 'lucide-react';
import { DateInput, Select, TextArea, TextInput } from '@/components/common';
import { useAuth } from '@/hooks/useAuth';
import { COURSE_OPTIONS, LEAD_SOURCE_OPTIONS, LEAD_STATUS_OPTIONS, useSales } from '../constants';
import { useCrmStore, type LeadInput } from '../crmStore';
import { useAdmissionStore } from '../admission/admissionStore';
import type { Lead } from '../types';

interface Props {
  /** Bỏ qua chính lead đang sửa khi kiểm tra trùng */
  excludeId?: string;
  /** Mở lead bị trùng */
  onOpenLead?: (id: string) => void;
}

/**
 * Các ô nhập của lead — dùng chung cho Thêm mới và tab Thông tin.
 * Trùng SĐT: cảnh báo vàng (vẫn cho lưu) + nút mở lead cũ.
 */
export function LeadFormFields({ excludeId, onOpenLead }: Props) {
  const { branches } = useAuth();
  const findDuplicate = useCrmStore((s) => s.findDuplicate);
  const campaigns = useAdmissionStore((s) => s.campaigns);
  const sales = useSales();

  return (
    <>
      <Form.Item name="fullName" label="Tên lead" rules={[{ required: true, message: 'Nhập tên lead' }]}>
        <TextInput icon={User} placeholder="Nguyễn Văn A" />
      </Form.Item>

      <div>
        <Form.Item
          name="phone"
          label="Số điện thoại"
          validateFirst
          rules={[
            { required: true, message: 'Nhập số điện thoại' },
            { pattern: /^0\d{9}$/, message: 'Gồm 10 số, bắt đầu bằng 0' },
            {
              warningOnly: true,
              validator: (_, v: string) => {
                const dup = v ? findDuplicate(v, excludeId) : undefined;
                return dup ? Promise.reject(new Error(`Trùng với lead ${dup.code} — ${dup.fullName}`)) : Promise.resolve();
              },
            },
          ]}
          className="!mb-1"
        >
          <TextInput type="tel" icon={Phone} placeholder="09xxxxxxxx" />
        </Form.Item>
        <Form.Item noStyle shouldUpdate={(a, b) => a.phone !== b.phone}>
          {({ getFieldValue }) => {
            const phone = getFieldValue('phone') as string | undefined;
            const dup = phone && /^0\d{9}$/.test(phone) ? findDuplicate(phone, excludeId) : undefined;
            return dup && onOpenLead ? (
              <button
                type="button"
                onClick={() => onOpenLead(dup.id)}
                className="mb-4 inline-flex items-center gap-1 text-[13px] font-medium text-primary-700 hover:underline"
              >
                <ExternalLink size={14} aria-hidden /> Mở lead {dup.code}
              </button>
            ) : (
              <div className="mb-4" />
            );
          }}
        </Form.Item>
      </div>

      <Form.Item name="email" label="Email" rules={[{ type: 'email', message: 'Email không hợp lệ' }]}>
        <TextInput type="email" icon={Mail} placeholder="email@example.com" />
      </Form.Item>
      <Form.Item name="source" label="Nguồn lead" rules={[{ required: true, message: 'Chọn nguồn lead' }]}>
        <Select placeholder="Chọn nguồn" options={LEAD_SOURCE_OPTIONS} />
      </Form.Item>
      <Form.Item name="status" label="Trạng thái" rules={[{ required: true, message: 'Chọn trạng thái' }]}>
        <Select options={LEAD_STATUS_OPTIONS} allowClear={false} showSearch={false} />
      </Form.Item>
      <Form.Item name="branchId" label="Chi nhánh" rules={[{ required: true, message: 'Chọn chi nhánh' }]}>
        <Select placeholder="Chọn chi nhánh" options={branches.map((b) => ({ value: b.id, label: b.name }))} />
      </Form.Item>

      <Form.Item noStyle shouldUpdate={(a, b) => a.branchId !== b.branchId}>
        {({ getFieldValue }) => (
          <Form.Item name="assigneeId" label="Sale phụ trách" tooltip="Để trống → hệ thống tự chia theo quy tắc ở Tuyển sinh › Chia lead & SLA">
            <Select
              placeholder="Để trống = tự chia"
              options={sales
                .filter((s) => !getFieldValue('branchId') || s.branchId === getFieldValue('branchId'))
                .map((s) => ({ value: s.id, label: `${s.fullName} · ${s.position}` }))}
            />
          </Form.Item>
        )}
      </Form.Item>
      <Form.Item noStyle shouldUpdate={(a, b) => a.source !== b.source || a.branchId !== b.branchId}>
        {({ getFieldValue }) => {
          const src = getFieldValue('source');
          const br = getFieldValue('branchId');
          const opts = campaigns.filter((c) => c.channel === src && c.status !== 'planned' && (!br || c.branchIds.includes(br)));
          return (
            <Form.Item name="campaignId" label="Chiến dịch" tooltip="Lead từ quảng cáo / sự kiện — để tính chi phí / lead">
              <Select placeholder={opts.length ? 'Chọn chiến dịch' : 'Không có chiến dịch cho nguồn này'} disabled={!opts.length} options={opts.map((c) => ({ value: c.id, label: c.name }))} />
            </Form.Item>
          );
        }}
      </Form.Item>
      <Form.Item name="interest" label="Khóa học quan tâm">
        <Select placeholder="Chọn khóa" options={COURSE_OPTIONS} />
      </Form.Item>

      <Form.Item name="nextFollowUpAt" label="Hẹn gọi lại">
        <DateInput showTime disabledDate={(d) => d.isBefore(dayjs(), 'day')} />
      </Form.Item>

      <Form.Item noStyle shouldUpdate={(a, b) => a.status !== b.status}>
        {({ getFieldValue }) =>
          getFieldValue('status') === 'lost' ? (
            <Form.Item name="lostReason" label="Lý do mất" rules={[{ required: true, message: 'Nhập lý do mất lead' }]}>
              <TextInput placeholder="Vd: học phí cao, chọn trung tâm khác..." />
            </Form.Item>
          ) : (
            <div className="hidden md:block" />
          )
        }
      </Form.Item>

      <Form.Item name="note" label="Ghi chú" className="md:col-span-2">
        <TextArea maxLength={1000} placeholder="Nhu cầu, mục tiêu, thời gian rảnh, người đăng ký cho ai..." />
      </Form.Item>
    </>
  );
}

/** Giá trị trong form lead (ngày dạng dayjs) */
export type LeadFormValues = Omit<LeadInput, 'nextFollowUpAt'> & { nextFollowUpAt?: dayjs.Dayjs | null };

/** Lead → giá trị form (chuyển chuỗi ngày sang dayjs) */
export function leadToForm(lead: Lead): Partial<LeadFormValues> {
  return { ...lead, nextFollowUpAt: lead.nextFollowUpAt ? dayjs(lead.nextFollowUpAt) : undefined };
}

/** Giá trị form → dữ liệu lưu */
export function formToLead(v: LeadFormValues): LeadInput {
  return { ...v, nextFollowUpAt: v.nextFollowUpAt ? v.nextFollowUpAt.toISOString() : undefined };
}
