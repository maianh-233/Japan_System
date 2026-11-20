import { useEffect, useState, type ReactNode } from 'react';
import { Form, Switch } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { AlarmClock, Building, CalendarCheck, Receipt, RotateCcw, Send, ShieldCheck, TriangleAlert, Users } from 'lucide-react';
import { Button, NumberInput, PageHeader, Select, TextInput, TimeInput, useConfirm, useNotify } from '@/components/common';
import { DEFAULT_SETTINGS, diffSettings, formatInvoiceCode, SETTING_LABELS, useSettingsStore, type CenterSettings } from '@/features/system/settings';
import { saveSettings } from '@/features/system/settingsApi';
import { USE_API } from '@/lib/api/config';
import { ApiError } from '@/lib/api/http';
import { runWeeklyReports } from '@/features/portal/parentAlerts';
import { useAuth } from '@/hooks/useAuth';
import { fmtDateTime } from '@/lib/date';

/** Giá trị form: giờ dạng Dayjs, còn lại giữ nguyên cấu trúc cài đặt */
type FormValues = Omit<CenterSettings, 'office' | 'parent'> & {
  office: Omit<CenterSettings['office'], 'from' | 'to' | 'satTo'> & { from: Dayjs; to: Dayjs; satTo?: Dayjs | null };
  parent: Omit<CenterSettings['parent'], 'weeklyTime'> & { weeklyTime: Dayjs };
};

const t = (hm?: string) => (hm ? dayjs(`2000-01-01T${hm}`) : undefined);
const toForm = (s: CenterSettings): FormValues => ({ ...s, office: { ...s.office, from: t(s.office.from)!, to: t(s.office.to)!, satTo: t(s.office.satTo) ?? null }, parent: { ...s.parent, weeklyTime: t(s.parent.weeklyTime)! } });
const fromForm = (v: FormValues): CenterSettings => ({
  ...v,
  office: { ...v.office, from: v.office.from.format('HH:mm'), to: v.office.to.format('HH:mm'), satTo: v.office.satTo ? v.office.satTo.format('HH:mm') : undefined },
  parent: { ...v.parent, weeklyTime: v.parent.weeklyTime.format('HH:mm') },
});
const WEEKDAYS = ['Chủ nhật', 'Thứ 2', 'Thứ 3', 'Thứ 4', 'Thứ 5', 'Thứ 6', 'Thứ 7'];

function Section({ icon: Icon, title, hint, children }: { icon: typeof Building; title: string; hint: string; children: ReactNode }) {
  return (
    <section className="rounded-[var(--radius-card)] border border-line bg-surface p-4 sm:p-5">
      <h2 className="flex items-center gap-2 text-base font-semibold text-ink">
        <Icon size={18} className="text-primary-600" aria-hidden /> {title}
      </h2>
      <p className="mt-0.5 mb-4 text-[13px] text-ink-muted">{hint}</p>
      <div className="grid grid-cols-1 gap-x-4 sm:grid-cols-2 lg:grid-cols-3">{children}</div>
    </section>
  );
}

/** Cài đặt chung của trung tâm — gom các con số trước đây viết cứng trong code */
export default function SettingsPage() {
  const { user, canDo } = useAuth();
  const notify = useNotify();
  const { confirm, confirmDialog } = useConfirm();
  const settings = useSettingsStore((s) => s.settings);
  const { save, updatedAt, updatedBy } = useSettingsStore();
  const [form] = Form.useForm<FormValues>();
  const editable = canDo('system.settings', 'update');
  const values = Form.useWatch([], form) as FormValues | undefined;
  const [dirty, setDirty] = useState(false);

  useEffect(() => {
    form.setFieldsValue(toForm(settings));
    setDirty(false);
  }, [settings, form]);

  const pattern = values?.invoice?.codePattern ?? settings.invoice.codePattern;
  const preview = /\{SO(:\d)?\}/.test(pattern) ? formatInvoiceCode(pattern, { prefix: 'CG', seq: 7 }) : undefined;

  const submit = async (v: FormValues) => {
    const next = fromForm(v);
    const changes = diffSettings(settings, next);
    if (!changes.length) return notify.toast.info('Không có thay đổi');
    const ok = await confirm({
      title: `Lưu ${changes.length} thay đổi?`,
      consequences: changes.map((c) => `${c.field}: ${c.before} → ${c.after}`),
      description: 'Áp dụng ngay cho mọi chi nhánh và ghi vào Nhật ký hệ thống.',
      tone: 'warning',
      confirmText: 'Lưu cài đặt',
    });
    if (!ok) return;
    if (!USE_API) {
      save(next, user?.fullName ?? 'Quản trị');
      return notify.toast.success('Đã lưu cài đặt chung');
    }
    try {
      const changed = await saveSettings(next);
      notify.toast.success(changed ? `Đã lưu ${changed} thay đổi` : 'Không có thay đổi');
    } catch (e) {
      if (e instanceof ApiError && e.fields) {
        // Lỗi theo mục "nhóm.mục" → báo đúng ô trên form
        form.setFields(Object.entries(e.fields).map(([key, errors]) => ({ name: key.split('.') as never, errors })));
        return notify.toast.error(`Có ${Object.keys(e.fields).length} mục chưa hợp lệ: ${Object.keys(e.fields).map((k) => SETTING_LABELS[k] ?? k).join(', ')}`);
      }
      notify.toast.error(e instanceof ApiError ? e.message : 'Không lưu được cài đặt');
    }
  };

  return (
    <>
      <PageHeader
        title="Cài đặt chung"
        subtitle={`Áp dụng cho toàn trung tâm${updatedAt ? ` · sửa lần cuối ${fmtDateTime(updatedAt)} bởi ${updatedBy}` : ' · đang dùng giá trị mặc định'}`}
        breadcrumb={[{ title: 'Hệ thống' }, { title: 'Cài đặt chung' }]}
        actions={
          editable && (
            <>
              <Button icon={RotateCcw} variant="ghost" tone="neutral" collapseOnMobile onClick={() => (form.setFieldsValue(toForm(DEFAULT_SETTINGS)), setDirty(true))}>
                Về mặc định
              </Button>
              <Button action="save" disabled={!dirty} onClick={() => form.submit()}>
                Lưu cài đặt
              </Button>
            </>
          )
        }
      />
      <Form<FormValues> form={form} layout="vertical" disabled={!editable} onFinish={submit} onValuesChange={() => setDirty(true)} className="flex flex-col gap-4">
        <Section icon={AlarmClock} title="Tuyển sinh" hint="SLA dùng khi lead không khớp quy tắc chia lead nào (quy tắc có SLA riêng thì theo quy tắc).">
          <Form.Item name={['crm', 'defaultSlaMinutes']} label="SLA liên hệ lead mới (phút)" rules={[{ required: true }]}>
            <NumberInput min={1} max={1440} />
          </Form.Item>
        </Section>

        <Section icon={Building} title="Giờ hành chính" hint="Nhân viên không làm theo ca chấm công theo giờ này; Sale ngoài giờ không được chia lead tự động.">
          <Form.Item name={['office', 'from']} label="Bắt đầu (T2–T6)" rules={[{ required: true }]}>
            <TimeInput minuteStep={15} />
          </Form.Item>
          <Form.Item
            name={['office', 'to']}
            label="Kết thúc (T2–T6)"
            dependencies={[['office', 'from']]}
            rules={[{ required: true }, ({ getFieldValue }) => ({ validator: (_, v?: Dayjs) => (!v || v.isAfter(getFieldValue(['office', 'from'])) ? Promise.resolve() : Promise.reject(new Error('Phải sau giờ bắt đầu'))) })]}
          >
            <TimeInput minuteStep={15} />
          </Form.Item>
          <Form.Item name={['office', 'satTo']} label="Thứ 7 làm đến" extra="Bỏ trống = nghỉ thứ 7">
            <TimeInput minuteStep={15} />
          </Form.Item>
          <Form.Item name={['office', 'lateGraceMin']} label="Châm chước đi muộn / về sớm (phút)" rules={[{ required: true }]}>
            <NumberInput min={0} max={60} />
          </Form.Item>
        </Section>

        <Section icon={Receipt} title="Mã hóa đơn" hint="Áp dụng cho hóa đơn tạo mới. {CN} = tiền tố hóa đơn của chi nhánh (Hệ thống › Chi nhánh), {YY}/{YYYY} năm, {MM} tháng, {SO:4} số thứ tự 4 chữ số.">
          <Form.Item
            name={['invoice', 'codePattern']}
            label="Mẫu mã"
            className="sm:col-span-2"
            rules={[{ required: true, whitespace: true }, { validator: (_, v?: string) => (!v || /\{SO(:\d)?\}/.test(v) ? Promise.resolve() : Promise.reject(new Error('Mẫu phải có {SO} hoặc {SO:n}'))) }]}
            extra={preview ? `Ví dụ (chi nhánh CG, số 7): ${preview}` : undefined}
          >
            <TextInput placeholder="HD-{CN}-{YY}{MM}-{SO:4}" />
          </Form.Item>
          <Form.Item name={['invoice', 'resetEvery']} label="Đánh số lại từ 1" extra="Nên để mẫu có năm / tháng tương ứng để mã không trùng">
            <Select allowClear={false} showSearch={false} options={[{ value: 'month', label: 'Mỗi tháng' }, { value: 'year', label: 'Mỗi năm' }, { value: 'never', label: 'Không bao giờ' }]} />
          </Form.Item>
        </Section>

        <Section icon={CalendarCheck} title="Bảng lương" hint="Bảng lương tháng trước phải chốt trước ngày này của tháng sau — quá hạn chưa chốt sẽ hiện cảnh báo ở trang lương.">
          <Form.Item name={['payroll', 'lockDay']} label="Ngày khóa bảng lương" rules={[{ required: true }]} extra="VD 5 → lương tháng 10 chốt trước 05/11">
            <NumberInput min={1} max={28} />
          </Form.Item>
        </Section>

        <Section icon={TriangleAlert} title="Cảnh báo bỏ học" hint="Học viên chạm 1 ngưỡng là có dấu hiệu; dấu hiệu nặng (vắng, vắng thi, điểm thi thấp) tính 3 điểm, nhẹ tính 1 điểm.">
          <Form.Item name={['atRisk', 'absent']} label="Vắng không phép từ (buổi)" rules={[{ required: true }]}>
            <NumberInput min={1} max={20} />
          </Form.Item>
          <Form.Item name={['atRisk', 'streak']} label="Vắng liên tiếp từ (buổi)" rules={[{ required: true }]}>
            <NumberInput min={1} max={10} />
          </Form.Item>
          <Form.Item name={['atRisk', 'attendanceRate']} label="Chuyên cần dưới (%)" rules={[{ required: true }]}>
            <NumberInput min={0} max={100} />
          </Form.Item>
          <Form.Item name={['atRisk', 'submitRate']} label="Nộp bài dưới (%)" rules={[{ required: true }]}>
            <NumberInput min={0} max={100} />
          </Form.Item>
          <Form.Item name={['atRisk', 'scorePct']} label="Điểm dưới (%)" rules={[{ required: true }]}>
            <NumberInput min={0} max={100} />
          </Form.Item>
          <div className="grid grid-cols-2 gap-x-3">
            <Form.Item name={['atRisk', 'mediumFrom']} label="Mức TB từ (điểm)" rules={[{ required: true }]}>
              <NumberInput min={1} max={30} />
            </Form.Item>
            <Form.Item
              name={['atRisk', 'highFrom']}
              label="Mức Cao từ (điểm)"
              dependencies={[['atRisk', 'mediumFrom']]}
              rules={[{ required: true }, ({ getFieldValue }) => ({ validator: (_, v?: number) => (v === undefined || v > getFieldValue(['atRisk', 'mediumFrom']) ? Promise.resolve() : Promise.reject(new Error('Phải lớn hơn mức TB'))) })]}
            >
              <NumberInput min={1} max={30} />
            </Form.Item>
          </div>
        </Section>

        <Section icon={Users} title="Thông báo phụ huynh" hint="Gửi cho người thân liên kết với học viên có bật “Nhận thông báo”, qua Zalo (hoặc SMS nếu phụ huynh không dùng Zalo). Nội dung sửa ở Tuyển sinh › Mẫu tin nhắn.">
          <Form.Item name={['parent', 'arrivalNotice']} label="Báo khi con đến lớp" valuePropName="checked" extra="Ngay khi điểm danh có mặt / đi muộn (GV, nhân viên hoặc quét QR)">
            <Switch />
          </Form.Item>
          <Form.Item name={['parent', 'absentAlertMin']} label="Báo chưa đến lớp sau (phút)" rules={[{ required: true }]} extra="Không có đơn nghỉ mà quá số phút này sau giờ vào lớp vẫn chưa có mặt. 0 = tắt">
            <NumberInput min={0} max={90} />
          </Form.Item>
          <Form.Item name={['parent', 'underAge']} label="Chỉ áp dụng cho học viên dưới (tuổi)" rules={[{ required: true }]} extra="0 = mọi học viên có phụ huynh liên kết">
            <NumberInput min={0} max={30} />
          </Form.Item>
          <Form.Item name={['parent', 'weeklyReport']} label="Tự gửi báo cáo tuần" valuePropName="checked">
            <Switch />
          </Form.Item>
          <div className="grid grid-cols-2 gap-x-3">
            <Form.Item name={['parent', 'weeklyDay']} label="Gửi vào" rules={[{ required: true }]}>
              <Select allowClear={false} showSearch={false} options={WEEKDAYS.map((d, i) => ({ value: i, label: d }))} />
            </Form.Item>
            <Form.Item name={['parent', 'weeklyTime']} label="Lúc" rules={[{ required: true }]}>
              <TimeInput minuteStep={15} />
            </Form.Item>
          </div>
          <div className="flex items-end pb-6">
            <Button
              icon={Send}
              variant="outline"
              disabled={!editable}
              onClick={() => {
                const n = runWeeklyReports(dayjs(), true);
                notify.toast[n ? 'success' : 'info'](n ? `Đã gửi báo cáo tuần cho phụ huynh của ${n} học viên` : 'Không còn học viên nào chưa nhận báo cáo tuần này');
              }}
            >
              Gửi báo cáo tuần ngay
            </Button>
          </div>
        </Section>

        <Section icon={ShieldCheck} title="An toàn dữ liệu" hint="Hủy hóa đơn và cấp quyền tài chính LUÔN cần người thứ hai duyệt; hoàn tiền cần duyệt từ mức tiền dưới đây.">
          <Form.Item name={['safety', 'refundApprovalMin']} label="Hoàn tiền cần duyệt từ" rules={[{ required: true }]} extra="0 = mọi khoản hoàn đều phải duyệt">
            <NumberInput currency min={0} step={100_000} />
          </Form.Item>
          <Form.Item name={['safety', 'trashDays']} label="Giữ trong Thùng rác (ngày)" rules={[{ required: true }]}>
            <NumberInput min={7} max={365} />
          </Form.Item>
        </Section>
      </Form>
      {confirmDialog}
    </>
  );
}
