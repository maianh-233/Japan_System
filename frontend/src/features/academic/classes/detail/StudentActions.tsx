import { useMemo, useState } from 'react';
import { Checkbox, Form, Radio } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { CheckCircle2, ChevronDown, CircleAlert, ChevronUp, CircleX, FileUser, PhoneCall, TriangleAlert } from 'lucide-react';
import { Button, DateInput, ModalForm, NumberInput, Select, StatusTag, TextArea, TextInput, useNotify } from '@/components/common';
import { useCrmStore } from '@/features/crm/crmStore';
import { summarizeInvoice, useStudentStore } from '@/features/student/studentStore';
import type { Enrollment } from '@/features/student/types';
import { useAuth } from '@/hooks/useAuth';
import { cn } from '@/lib/cn';
import { fmtDate } from '@/lib/date';
import { formatCurrency } from '@/lib/format';
import { useClassStore } from '../classStore';
import { effectiveStart, totalSessionsOf } from '../constants';
import { isTaught } from '../sessionUtils';
import { ALERT_RULES, useClassStudentStats, type StudentStats } from '../studentInsights';
import type { ClassInfo } from '../types';
import { CARE_CHANNEL } from './StudentLearningDrawer';

const round10k = (n: number) => Math.round(n / 10_000) * 10_000;

// ======================= Bảo lưu =======================

export function ReserveModal({ enrollment, name, onClose }: { enrollment: Enrollment | null; name?: string; onClose: () => void }) {
  const reserve = useStudentStore((s) => s.reserveEnrollment);
  return (
    <ModalForm<{ until: Dayjs; reason: string }>
      key={enrollment?.id ?? 'reserve'}
      open={Boolean(enrollment)}
      onClose={onClose}
      title={`Bảo lưu cho ${name ?? ''}`}
      description="Học viên giữ chỗ trong lớp, được tính 'Vắng có phép' khi điểm danh. Học phí đã đóng được giữ nguyên."
      submitAction="save"
      submitText="Bảo lưu"
      successMessage="Đã bảo lưu"
      initialValues={{ until: dayjs().add(1, 'month') }}
      onSubmit={(v) => void (enrollment && reserve(enrollment.id, v.until.format('YYYY-MM-DD'), v.reason.trim()))}
    >
      <Form.Item name="until" label="Bảo lưu đến ngày" rules={[{ required: true, message: 'Chọn ngày' }]} extra="Tối đa 6 tháng — quá hạn hệ thống sẽ nhắc Giáo vụ liên hệ">
        <DateInput disabledDate={(d) => d.isBefore(dayjs(), 'day') || d.isAfter(dayjs().add(6, 'month'))} />
      </Form.Item>
      <Form.Item name="reason" label="Lý do" rules={[{ required: true, whitespace: true, message: 'Nhập lý do' }]}>
        <TextArea rows={2} placeholder="VD: Đi công tác 1 tháng, ốm dài ngày..." />
      </Form.Item>
    </ModalForm>
  );
}

// ======================= Chuyển lớp =======================

export function TransferModal({ cls, enrollment, name, onClose }: { cls: ClassInfo; enrollment: Enrollment | null; name?: string; onClose: () => void }) {
  const notify = useNotify();
  const [form] = Form.useForm<{ targetId: string; amount: number; credit: number; dueDate: Dayjs; reason: string }>();
  const classes = useClassStore((s) => s.classes);
  const sessions = useClassStore((s) => s.sessions);
  const enrollments = useStudentStore((s) => s.enrollments);
  const invoices = useStudentStore((s) => s.invoices);
  const transfer = useStudentStore((s) => s.transferEnrollment);

  const inv = enrollment && invoices.find((i) => i.enrollmentId === enrollment.id);
  const paid = inv ? summarizeInvoice(inv).paid : 0;
  const total = totalSessionsOf(cls) || 1;
  const taught = sessions.filter((s) => s.classId === cls.id && isTaught(s)).length;
  const used = round10k((cls.fee / total) * taught);
  const credit = Math.max(0, paid - used);

  const targets = useMemo(
    () =>
      classes
        .filter((c) => c.id !== cls.id && (c.status === 'enrolling' || c.status === 'ongoing') && c.branchId === cls.branchId)
        .map((c) => {
          const seats = c.maxStudents - enrollments.filter((e) => e.classId === c.id && (e.status === 'studying' || e.status === 'reserved')).length;
          const doneT = sessions.filter((s) => s.classId === c.id && isTaught(s)).length;
          const totalT = totalSessionsOf(c) || 1;
          return { c, seats, doneT, totalT, fee: c.status === 'enrolling' ? c.fee : round10k((c.fee * (totalT - doneT)) / totalT) };
        })
        .sort((a, b) => Number(b.c.courseId === cls.courseId) - Number(a.c.courseId === cls.courseId)),
    [classes, cls, enrollments, sessions],
  );

  return (
    <ModalForm
      key={enrollment?.id ?? 'transfer'}
      form={form}
      open={Boolean(enrollment)}
      onClose={onClose}
      width={680}
      title={`Chuyển lớp cho ${name ?? ''}`}
      description={`Từ lớp ${cls.code} · đã học ${taught}/${total} buổi`}
      submitAction="save"
      submitText="Chuyển lớp"
      successMessage="Đã chuyển lớp"
      initialValues={{ credit, dueDate: dayjs().add(7, 'day') }}
      onSubmit={(v) => {
        if (!enrollment) return;
        transfer(enrollment.id, v.targetId, { amount: v.amount, credit: v.credit, dueDate: v.dueDate.format('YYYY-MM-DD'), reason: v.reason.trim() });
        const t = targets.find((x) => x.c.id === v.targetId);
        notify.info({ title: `Đã chuyển ${name} sang ${t?.c.code}`, description: `Hóa đơn mới: ${formatCurrency(Math.max(0, v.amount - v.credit)) || '0 ₫'} cần thu.` });
      }}
    >
      <Form.Item name="targetId" label="Lớp chuyển đến" rules={[{ required: true, message: 'Chọn lớp' }]}>
        <Select
          placeholder="Chọn lớp đang mở cùng chi nhánh"
          onChange={(id) => form.setFieldsValue({ amount: targets.find((x) => x.c.id === id)?.fee })}
          options={targets.map((x) => ({
            value: x.c.id,
            disabled: x.seats <= 0,
            label: `${x.c.code} — ${x.c.name}${x.c.courseId === cls.courseId ? ' · cùng khóa' : ''} · ${x.c.status === 'enrolling' ? `khai giảng ${fmtDate(effectiveStart(x.c))}` : `đã học ${x.doneT}/${x.totalT}`} · ${x.seats > 0 ? `còn ${x.seats} chỗ` : 'hết chỗ'}`,
          }))}
        />
      </Form.Item>
      <div className="mb-4 grid grid-cols-3 gap-2 rounded-lg bg-page p-3 text-[13px]">
        <div>
          <p className="text-xs text-ink-muted">Đã đóng lớp cũ</p>
          <p className="tabular font-semibold">{formatCurrency(paid) || '0 ₫'}</p>
        </div>
        <div>
          <p className="text-xs text-ink-muted">Giá trị đã học ({taught} buổi)</p>
          <p className="tabular font-semibold">− {formatCurrency(used) || '0 ₫'}</p>
        </div>
        <div>
          <p className="text-xs text-ink-muted">Được khấu trừ sang lớp mới</p>
          <p className="tabular font-semibold text-success-700">{formatCurrency(credit) || '0 ₫'}</p>
        </div>
      </div>
      <div className="grid grid-cols-1 gap-x-3 sm:grid-cols-2">
        <Form.Item name="amount" label="Học phí lớp mới" rules={[{ required: true, message: 'Nhập học phí' }]} extra="Lớp đang học → tính theo số buổi còn lại">
          <NumberInput currency min={0} step={100_000} />
        </Form.Item>
        <Form.Item name="credit" label="Khấu trừ" rules={[{ required: true }]} extra="Sửa được nếu trung tâm có chính sách khác">
          <NumberInput currency min={0} step={100_000} />
        </Form.Item>
      </div>
      <Form.Item noStyle shouldUpdate>
        {({ getFieldValue }) => {
          const amount = (getFieldValue('amount') as number) ?? 0;
          const c = (getFieldValue('credit') as number) ?? 0;
          const diff = amount - c;
          return amount ? (
            <p className={cn('mb-4 rounded-lg px-3 py-2 text-[13px] font-medium', diff > 0 ? 'bg-warning-50 text-warning-700' : 'bg-success-50 text-success-700')}>
              {diff > 0 ? `Học viên cần đóng thêm ${formatCurrency(diff)}` : diff < 0 ? `Dư ${formatCurrency(-diff)} — có thể hoàn lại cho học viên` : 'Không phải đóng thêm'}
            </p>
          ) : null;
        }}
      </Form.Item>
      <div className="grid grid-cols-1 gap-x-3 sm:grid-cols-2">
        <Form.Item name="dueDate" label="Hạn đóng (nếu phải đóng thêm)" rules={[{ required: true, message: 'Chọn ngày' }]}>
          <DateInput />
        </Form.Item>
        <Form.Item name="reason" label="Lý do chuyển" rules={[{ required: true, whitespace: true, message: 'Nhập lý do' }]}>
          <TextInput placeholder="VD: Đổi ca làm, học lại sau bảo lưu" />
        </Form.Item>
      </div>
    </ModalForm>
  );
}

// ======================= Chứng nhận hoàn thành =======================

/** Điều kiện cấp chứng nhận */
export function certificateChecks(cls: ClassInfo, st?: StudentStats) {
  if (!st) return [];
  const lastExam = st.exams.list[st.exams.list.length - 1];
  return [
    { ok: cls.status === 'finished' || st.enrollment.status === 'completed', label: 'Lớp đã kết thúc / học viên đã hoàn thành' },
    { ok: (st.attendance.rate ?? 0) >= ALERT_RULES.attendanceRate, label: `Chuyên cần ≥ ${ALERT_RULES.attendanceRate}% (hiện ${st.attendance.rate ?? 0}%)` },
    {
      ok: !lastExam || (lastExam.result.status === 'done' && lastExam.pct >= ALERT_RULES.scorePct),
      label: lastExam ? `${lastExam.title} ≥ ${ALERT_RULES.scorePct}% (${lastExam.result.status === 'absent' ? 'vắng thi' : `${lastExam.pct}%`})` : 'Không có bài thi bắt buộc',
    },
    { ok: !st.tuition?.remaining, label: st.tuition?.remaining ? `Hoàn tất học phí (còn nợ ${formatCurrency(st.tuition.remaining)})` : 'Hoàn tất học phí' },
  ];
}

export function CertificateModal({ cls, studentId, onClose }: { cls: ClassInfo; studentId: string | null; onClose: () => void }) {
  const notify = useNotify();
  const { user } = useAuth();
  const { map } = useClassStudentStats(cls);
  const student = useCrmStore((s) => s.students.find((x) => x.id === studentId));
  const send = useStudentStore((s) => s.sendCertificate);
  const st = studentId ? map.get(studentId) : undefined;
  const checks = certificateChecks(cls, st);
  const eligible = checks.every((c) => c.ok);
  const cert = st?.enrollment.certificate;

  return (
    <ModalForm<{ email: string; exception: boolean; exceptionReason?: string }>
      key={studentId ?? 'cert'}
      open={Boolean(studentId)}
      onClose={onClose}
      width={680}
      title={cert ? 'Gửi lại chứng nhận hoàn thành' : 'Cấp & gửi chứng nhận hoàn thành'}
      description={cert ? `Mã ${cert.code} · đã gửi ${cert.sendCount} lần, gần nhất tới ${cert.sentTo}` : `${student?.fullName} · lớp ${cls.code}`}
      submitAction="save"
      submitText="Gửi email"
      successMessage="Đã gửi chứng nhận"
      initialValues={{ email: cert?.sentTo ?? student?.email, exception: false }}
      onSubmit={(v) => {
        if (!st) return;
        if (!eligible && !cert && !v.exception) throw new Error('Học viên chưa đủ điều kiện');
        send(st.enrollment.id, { email: v.email.trim(), by: user?.fullName ?? 'Giáo vụ', exception: !eligible ? v.exceptionReason : undefined });
        notify.toast.success(`Đã gửi chứng nhận PDF tới ${v.email}`);
      }}
    >
      {/* Xem trước chứng nhận */}
      <div className="mb-4 rounded-lg border-4 border-double border-primary-300 bg-primary-50 px-6 py-5 text-center">
        <p className="text-xs font-semibold tracking-widest text-primary-700">JP CENTER · TRUNG TÂM NHẬT NGỮ</p>
        <p className="mt-2 text-lg font-bold text-primary-900">CHỨNG NHẬN HOÀN THÀNH KHÓA HỌC</p>
        <p className="mt-3 text-xs text-ink-muted">Chứng nhận học viên</p>
        <p className="text-xl font-semibold text-ink">{student?.fullName ?? '—'}</p>
        <p className="mt-2 text-[13px] text-ink-secondary">
          đã hoàn thành khóa <strong>{cls.courseLabel}</strong> · lớp {cls.code}
          <br />
          {fmtDate(effectiveStart(cls))} – {fmtDate(cls.actualEndDate ?? cls.plannedEndDate)} · {cls.totalHours} giờ · chuyên cần {st?.attendance.rate ?? 0}%
        </p>
        <p className="mt-3 font-mono text-xs text-ink-muted">Mã tra cứu: {cert?.code ?? 'CN-…(cấp khi gửi)'}</p>
      </div>

      <ul className="mb-4 flex flex-col gap-1 text-[13px]">
        {checks.map((c) => (
          <li key={c.label} className={cn('flex items-center gap-1.5', c.ok ? 'text-success-700' : 'text-danger-600')}>
            {c.ok ? <CheckCircle2 size={15} aria-hidden /> : <CircleX size={15} aria-hidden />} {c.label}
          </li>
        ))}
      </ul>

      <Form.Item
        name="email"
        label="Gửi tới email"
        rules={[
          { required: true, message: 'Nhập email' },
          { type: 'email', message: 'Email không hợp lệ' },
        ]}
        extra={student?.email ? 'Email trong hồ sơ học viên — sửa nếu học viên muốn nhận ở email khác' : 'Hồ sơ chưa có email'}
      >
        <TextInput type="email" placeholder="hocvien@gmail.com" />
      </Form.Item>
      {!eligible && !cert && (
        <>
          <Form.Item name="exception" valuePropName="checked">
            <Checkbox>Cấp ngoại lệ (được quản lý duyệt) dù chưa đủ điều kiện</Checkbox>
          </Form.Item>
          <Form.Item noStyle shouldUpdate={(a, b) => a.exception !== b.exception}>
            {({ getFieldValue }) =>
              getFieldValue('exception') ? (
                <Form.Item name="exceptionReason" label="Lý do cấp ngoại lệ" rules={[{ required: true, whitespace: true, message: 'Nhập lý do' }]}>
                  <TextInput placeholder="VD: Vắng thi do ốm, đã thi bù đạt" />
                </Form.Item>
              ) : null
            }
          </Form.Item>
        </>
      )}
      <p className="text-xs text-ink-muted">Email kèm file PDF chứng nhận + link tra cứu mã. Chứng nhận cũng hiện trên Cổng học viên.</p>
    </ModalForm>
  );
}

// ======================= Ghi nhận liên hệ =======================

export function CareLogModal({ cls, studentId, name, onClose }: { cls: ClassInfo; studentId: string | null; name?: string; onClose: () => void }) {
  const { user } = useAuth();
  const addCareLog = useClassStore((s) => s.addCareLog);
  return (
    <ModalForm<{ channel: 'call' | 'zalo' | 'meet'; note: string }>
      key={studentId ?? 'care'}
      open={Boolean(studentId)}
      onClose={onClose}
      title={`Ghi nhận liên hệ — ${name ?? ''}`}
      description="Lưu vào lịch sử chăm sóc của học viên trong lớp này"
      submitAction="save"
      submitText="Lưu"
      successMessage="Đã ghi nhận liên hệ"
      initialValues={{ channel: 'call' }}
      onSubmit={(v) => void (studentId && addCareLog({ classId: cls.id, studentId, channel: v.channel, note: v.note.trim(), by: user?.fullName ?? 'Giáo vụ' }))}
    >
      <Form.Item name="channel" label="Hình thức">
        <Radio.Group optionType="button" buttonStyle="solid" options={(Object.keys(CARE_CHANNEL) as (keyof typeof CARE_CHANNEL)[]).map((k) => ({ value: k, label: CARE_CHANNEL[k] }))} />
      </Form.Item>
      <Form.Item name="note" label="Nội dung / kết quả" rules={[{ required: true, whitespace: true, message: 'Nhập nội dung' }]}>
        <TextArea rows={3} placeholder="VD: Đã gọi phụ huynh, học viên ốm — tuần sau đi học lại, sẽ học bù bài 5." />
      </Form.Item>
    </ModalForm>
  );
}

// ======================= Học viên cần chú ý =======================

export function AttentionPanel({ cls, onProfile, onContact }: { cls: ClassInfo; onProfile: (studentId: string) => void; onContact: (studentId: string) => void }) {
  const { map } = useClassStudentStats(cls);
  const students = useCrmStore((s) => s.students);
  const [expanded, setExpanded] = useState(false);
  const list = [...map.values()].filter((s) => s.flags.length).sort((a, b) => b.risk - a.risk);
  if (!list.length || cls.status === 'cancelled') return null;
  const shown = expanded ? list : list.slice(0, 4);

  return (
    <section className="rounded-[var(--radius-card)] border border-warning-100 bg-warning-50/50">
      <header className="flex flex-wrap items-center gap-2 px-4 py-3">
        <TriangleAlert size={18} className="text-warning-700" aria-hidden />
        <h3 className="text-sm font-semibold text-ink">Học viên cần chú ý ({list.length})</h3>
        <span className="text-xs text-ink-muted">
          Vắng ≥ {ALERT_RULES.absent} buổi / {ALERT_RULES.streak} buổi liền · chuyên cần &lt; {ALERT_RULES.attendanceRate}% · nộp bài &lt; {ALERT_RULES.submitRate}% · điểm &lt; {ALERT_RULES.scorePct}% · nợ quá hạn
        </span>
      </header>
      <ul className="divide-y divide-warning-100 border-t border-warning-100">
        {shown.map((s) => {
          const st = students.find((x) => x.id === s.enrollment.studentId);
          const last = s.careLogs[0];
          const recent = last && dayjs().diff(dayjs(last.at), 'day') <= 7;
          return (
            <li key={s.enrollment.id} className="flex flex-col gap-2 px-4 py-2.5 sm:flex-row sm:items-center">
              <button type="button" onClick={() => onProfile(s.enrollment.studentId)} className="w-48 shrink-0 text-left">
                <span className="block truncate text-[13px] font-medium text-ink hover:text-primary-700">{st?.fullName ?? s.enrollment.studentId}</span>
                <span className="block text-xs text-ink-muted">{st?.phone ?? st?.code}</span>
              </button>
              <div className="flex min-w-0 flex-1 flex-wrap gap-1">
                {s.flags.map((f) => (
                  <StatusTag key={f.label} tone={f.tone} icon={f.tone === 'danger' ? TriangleAlert : CircleAlert} size="sm">
                    {f.label}
                  </StatusTag>
                ))}
              </div>
              <span className={cn('text-xs', recent ? 'text-success-700' : 'text-ink-muted')}>{last ? `Đã liên hệ ${fmtDate(last.at)}` : 'Chưa liên hệ'}</span>
              <div className="flex gap-1">
                <Button icon={FileUser} iconOnly size="sm" variant="ghost" tooltip="Hồ sơ học tập" aria-label="Hồ sơ học tập" onClick={() => onProfile(s.enrollment.studentId)} />
                <Button icon={PhoneCall} size="sm" variant="soft" onClick={() => onContact(s.enrollment.studentId)}>
                  Ghi nhận liên hệ
                </Button>
              </div>
            </li>
          );
        })}
      </ul>
      {list.length > 4 && (
        <button type="button" onClick={() => setExpanded((x) => !x)} className="flex w-full items-center justify-center gap-1 border-t border-warning-100 py-2 text-[13px] text-primary-700 hover:bg-warning-50">
          {expanded ? <ChevronUp size={15} /> : <ChevronDown size={15} />} {expanded ? 'Thu gọn' : `Xem tất cả ${list.length} học viên`}
        </button>
      )}
    </section>
  );
}

