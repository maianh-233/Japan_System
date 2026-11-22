import { useMemo, useState, type Key } from 'react';
import { Dropdown, Form, Radio } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { ArrowRightLeft, Award, BellRing, Ellipsis, Megaphone, FileUser, HandCoins, Mail, PauseCircle, PhoneCall, PlayCircle, Undo2, UserPlus, UserX } from 'lucide-react';
import { Button, DataTable, DateInput, ModalForm, NumberInput, Select, StatusTag, TextInput, UserCell, useConfirm, useNotify, type DataColumn } from '@/components/common';
import { useCrmStore } from '@/features/crm/crmStore';
import { RemindModal } from '@/features/finance/InvoiceDrawer';
import { pendingRefundOf, refundNeedsApproval, refundTitle, useApprovalStore } from '@/features/system/approvals';
import { ENROLLMENT_STATUS, PAYMENT_METHODS } from '@/features/student/constants';
import * as financeApi from '@/features/finance/financeApi';
import { summarizeInvoice, useStudentStore, type InvoiceSummary } from '@/features/student/studentStore';
import * as studentApi from '@/features/student/studentApi';
import { USE_API } from '@/lib/api/config';
import type { Enrollment, Invoice, PaymentMethod } from '@/features/student/types';
import { useAuth } from '@/hooks/useAuth';
import { cn } from '@/lib/cn';
import { fmtDate } from '@/lib/date';
import { formatCurrency } from '@/lib/format';
import type { ClassInfo } from '../types';
import { useClassStudentStats } from '../studentInsights';
import { ComposeAnnouncementModal } from './ClassAnnouncementsTab';
import { AttentionPanel, BulkTransferModal, CareLogModal, CertificateModal, certificateChecks, ReserveModal, TransferModal } from './StudentActions';
import { StudentLearningDrawer } from './StudentLearningDrawer';

interface Row {
  id: string;
  enrollment: Enrollment;
  invoice?: Invoice;
  sum?: InvoiceSummary;
  fullName: string;
  code: string;
  phone: string;
  email?: string;
}

type Dialog = { kind: 'pay' | 'refund' | 'reserve' | 'transfer'; row: Row } | { kind: 'add' } | null;

const METHOD_OPTIONS = (Object.keys(PAYMENT_METHODS) as PaymentMethod[]).map((k) => ({ value: k, label: PAYMENT_METHODS[k] }));

/** Tab Học viên & học phí: danh sách ghi danh + thu học phí / hoàn tiền / cho nghỉ học */
export function ClassStudentsTab({ cls }: { cls: ClassInfo }) {
  const { can, user } = useAuth();
  const notify = useNotify();
  const { confirm, confirmDialog } = useConfirm();
  const students = useCrmStore((s) => s.students);
  const enrollments = useStudentStore((s) => s.enrollments);
  const invoices = useStudentStore((s) => s.invoices);
  const enroll = useStudentStore((s) => s.enroll);
  const addPayment = useStudentStore((s) => s.addPayment);
  const addRefund = useStudentStore((s) => s.addRefund);
  const dropEnrollment = useStudentStore((s) => s.dropEnrollment);
  const resumeEnrollment = useStudentStore((s) => s.resumeEnrollment);
  const sendCertificate = useStudentStore((s) => s.sendCertificate);
  const { map: stats } = useClassStudentStats(cls);
  const [dialog, setDialog] = useState<Dialog>(null);
  const [profileId, setProfileId] = useState<string | null>(null);
  const [certId, setCertId] = useState<string | null>(null);
  const [careId, setCareId] = useState<string | null>(null);
  const [selected, setSelected] = useState<Key[]>([]);
  const [bulk, setBulk] = useState<'remind' | 'notify' | 'transfer' | null>(null);
  const approvals = useApprovalStore((s) => s.requests);
  const requestApproval = useApprovalStore((s) => s.request);
  /** Còn hoàn được = theo hóa đơn − khoản đang chờ duyệt */
  const refundableOf = (r: Row) => Math.max(0, (r.sum?.refundable ?? 0) - (r.invoice ? pendingRefundOf(approvals, r.invoice.id) : 0));
  const editable = can('academic.classes') || can('finance.invoices');
  const closed = cls.status === 'finished' || cls.status === 'cancelled';

  const rows: Row[] = useMemo(
    () =>
      enrollments
        .filter((e) => e.classId === cls.id)
        .map((e) => {
          const st = students.find((s) => s.id === e.studentId);
          const invoice = invoices.find((i) => i.enrollmentId === e.id);
          return { id: e.id, enrollment: e, invoice, sum: invoice && summarizeInvoice(invoice), fullName: st?.fullName ?? e.studentId, code: st?.code ?? '', phone: st?.phone ?? '', email: st?.email };
        })
        // Đang học / bảo lưu lên đầu
        .sort((a, b) => Number(['dropped', 'transferred'].includes(a.enrollment.status)) - Number(['dropped', 'transferred'].includes(b.enrollment.status))),
    [enrollments, invoices, students, cls.id],
  );

  const totals = rows.reduce(
    (t, r) => {
      if (!r.sum) return t;
      t.due += r.sum.status === 'cancelled' || r.sum.status === 'refunded' ? r.sum.paid : r.sum.total;
      t.paid += r.sum.paid;
      t.remaining += r.sum.remaining;
      t.refunded += r.sum.refunded;
      return t;
    },
    { due: 0, paid: 0, remaining: 0, refunded: 0 },
  );
  const activeCount = rows.filter((r) => r.enrollment.status === 'studying' || r.enrollment.status === 'reserved').length;

  // ===== Thao tác hàng loạt (chỉ áp dụng cho học viên còn trong lớp) =====
  const isActive = (r: Row) => r.enrollment.status === 'studying' || r.enrollment.status === 'reserved';
  const picked = rows.filter((r) => selected.includes(r.id));
  const pickedActive = picked.filter(isActive);
  const pickedDebt = picked.filter((r) => r.invoice && !r.invoice.cancelled && (r.sum?.remaining ?? 0) > 0);
  const clearSelection = () => {
    setSelected([]);
    setBulk(null);
  };

  const drop = async (r: Row) => {
    const res = await confirm({
      title: `Cho ${r.fullName} nghỉ học lớp ${cls.code}?`,
      consequences: [
        'Trạng thái học chuyển sang "Đã nghỉ"',
        r.sum?.remaining ? `Hủy công nợ còn lại ${formatCurrency(r.sum.remaining)}` : 'Không còn công nợ',
        r.sum && r.sum.paid > 0 ? 'Học viên đã đóng tiền — có thể hoàn tiền ngay sau bước này' : 'Học viên chưa đóng tiền',
      ],
      tone: 'danger',
      confirmAction: 'reject',
      confirmText: 'Cho nghỉ học',
      requireReason: { label: 'Lý do nghỉ', placeholder: 'VD: Chuyển công tác, không sắp xếp được thời gian...' },
      onConfirm: USE_API ? async ({ reason }) => void (await studentApi.dropEnrollment(r.enrollment.id, reason ?? '')) : undefined,
    });
    if (!res) return;
    if (!USE_API) dropEnrollment(r.enrollment.id, res.reason ?? '');
    notify.success({ title: 'Đã cho học viên nghỉ', description: `${r.fullName} — ${cls.code}` });
    if (r.sum && r.sum.paid > 0) setTimeout(() => setDialog({ kind: 'refund', row: r }), 300);
  };

  const resume = async (r: Row) => {
    const ok = await confirm({
      title: `${r.fullName} học lại lớp ${cls.code}?`,
      description: r.enrollment.reservedUntil ? `Đang bảo lưu đến ${fmtDate(r.enrollment.reservedUntil)}.` : undefined,
      tone: 'success',
      confirmText: 'Học lại',
      onConfirm: USE_API ? async () => void (await studentApi.resumeEnrollment(r.enrollment.id)) : undefined,
    });
    if (!ok) return;
    if (!USE_API) resumeEnrollment(r.enrollment.id);
    notify.toast.success(`${r.fullName} đã quay lại học`);
  };

  /** Học viên đủ điều kiện, chưa nhận chứng nhận, có email */
  const certReady = rows.filter((r) => {
    const st = stats.get(r.enrollment.studentId);
    return !r.enrollment.certificate && r.email && (r.enrollment.status === 'completed' || r.enrollment.status === 'studying') && certificateChecks(cls, st).every((c) => c.ok);
  });
  const sendAllCertificates = async () => {
    const ok = await confirm({
      title: `Gửi chứng nhận hoàn thành cho ${certReady.length} học viên?`,
      consequences: [
        'Mỗi học viên nhận 1 email kèm file PDF chứng nhận + mã tra cứu',
        'Học viên chưa đủ điều kiện hoặc chưa có email không được gửi — xử lý riêng từng người',
      ],
      tone: 'primary',
      confirmText: 'Gửi email',
    });
    if (!ok) return;
    certReady.forEach((r) => sendCertificate(r.enrollment.id, { email: r.email!, by: user?.fullName ?? 'Giáo vụ' }));
    notify.success({ title: `Đã gửi ${certReady.length} chứng nhận`, description: cls.code });
  };

  const columns: DataColumn<Row>[] = [
    {
      key: 'fullName',
      title: 'Học viên',
      width: 210,
      mobile: 'title',
      sorter: (a, b) => a.fullName.localeCompare(b.fullName, 'vi'),
      render: (_, r) => (
        <div className={cn(r.enrollment.status === 'dropped' && 'opacity-60')}>
          <UserCell name={r.fullName} subtitle={r.code} size="sm" onClick={() => setProfileId(r.enrollment.studentId)} />
        </div>
      ),
    },
    { key: 'phone', title: 'Điện thoại', dataIndex: 'phone', width: 130, mobile: 'subtitle' },
    { key: 'enrolledAt', title: 'Ghi danh', width: 120, render: (_, r) => <span className="tabular">{fmtDate(r.enrollment.enrolledAt)}</span> },
    {
      key: 'status',
      title: 'Trạng thái học',
      width: 150,
      render: (_, r) => {
        const s = ENROLLMENT_STATUS[r.enrollment.status];
        const waiting = cls.status === 'enrolling' && r.enrollment.status === 'studying';
        return (
          <span title={r.enrollment.note} className="flex flex-col items-start gap-0.5">
            <StatusTag tone={waiting ? 'info' : s.tone} icon={s.icon} size="sm">
              {waiting ? 'Chờ khai giảng' : s.label}
            </StatusTag>
            {r.enrollment.status === 'reserved' && r.enrollment.reservedUntil && (
              <span className={cn('text-xs', r.enrollment.reservedUntil < dayjs().format('YYYY-MM-DD') ? 'text-danger-600' : 'text-ink-muted')}>
                đến {fmtDate(r.enrollment.reservedUntil)}
              </span>
            )}
            {r.enrollment.certificate && (
              <span className="flex items-center gap-1 text-xs text-success-700" title={`${r.enrollment.certificate.code} → ${r.enrollment.certificate.sentTo}`}>
                <Award size={12} aria-hidden /> Đã gửi chứng nhận
              </span>
            )}
          </span>
        );
      },
    },
    { key: 'total', title: 'Học phí', width: 120, align: 'right', render: (_, r) => (r.sum ? <span className="tabular">{formatCurrency(r.sum.total)}</span> : '—') },
    {
      key: 'paid',
      title: 'Đã đóng',
      width: 120,
      align: 'right',
      render: (_, r) =>
        r.sum ? (
          <span className="tabular" title={r.sum.refunded ? `Đã thu ${formatCurrency(r.sum.collected)}, đã hoàn ${formatCurrency(r.sum.refunded)}` : undefined}>
            {formatCurrency(r.sum.paid) || '0 ₫'}
          </span>
        ) : (
          '—'
        ),
    },
    {
      key: 'remaining',
      title: 'Còn nợ',
      width: 120,
      align: 'right',
      sorter: (a, b) => (a.sum?.remaining ?? 0) - (b.sum?.remaining ?? 0),
      render: (_, r) => (r.sum?.remaining ? <span className="tabular font-medium text-danger-600">{formatCurrency(r.sum.remaining)}</span> : <span className="text-ink-muted">—</span>),
    },
    {
      key: 'tuition',
      title: 'Tình trạng học phí',
      width: 150,
      mobile: 'aside',
      render: (_, r) =>
        r.sum ? (
          <span className="flex flex-col items-start gap-0.5">
            <StatusTag group="invoice" status={r.sum.status} size="sm" />
            {r.sum.remaining > 0 && r.invoice && <span className="tabular text-xs text-ink-muted">Hạn {fmtDate(r.invoice.dueDate)}</span>}
          </span>
        ) : (
          '—'
        ),
    },
  ];

  return (
    <div className="flex flex-col gap-4">
      <AttentionPanel cls={cls} onProfile={setProfileId} onContact={setCareId} />
      <div className="grid grid-cols-2 gap-2 lg:grid-cols-4">
        {[
          ['Phải thu', formatCurrency(totals.due) || '0 ₫', 'text-ink'],
          ['Đã thu (thực)', formatCurrency(totals.paid) || '0 ₫', 'text-success-700'],
          ['Còn nợ', formatCurrency(totals.remaining) || '0 ₫', totals.remaining ? 'text-danger-600' : 'text-ink'],
          ['Đã hoàn', formatCurrency(totals.refunded) || '0 ₫', 'text-ink-secondary'],
        ].map(([k, v, c]) => (
          <div key={k} className="rounded-lg bg-page px-3 py-2.5">
            <p className="text-xs text-ink-muted">{k}</p>
            <p className={cn('tabular text-base font-semibold', c)}>{v}</p>
          </div>
        ))}
      </div>

      <DataTable<Row>
        storageKey="class-students"
        columns={columns}
        data={rows}
        rowKey="id"
        unitLabel="học viên"
        searchKeys={['fullName', 'code', 'phone']}
        search={{
          placeholder: 'Tìm học viên trong lớp...',
          actions: editable ? (
            <>
              {cls.status === 'finished' && (
                <Button icon={Mail} variant="outline" collapseOnMobile disabled={!certReady.length} tooltip={certReady.length ? undefined : 'Không còn học viên đủ điều kiện chưa nhận'} onClick={sendAllCertificates}>
                  Gửi chứng nhận ({certReady.length})
                </Button>
              )}
              {!closed && (
                <Button icon={UserPlus} collapseOnMobile disabled={activeCount >= cls.maxStudents} onClick={() => setDialog({ kind: 'add' })}>
                  Thêm học viên
                </Button>
              )}
            </>
          ) : undefined,
        }}
        selectedKeys={editable ? selected : undefined}
        onSelectionChange={editable ? (keys) => setSelected(keys) : undefined}
        bulkActions={() => (
          <>
            <Button icon={BellRing} size="sm" variant="soft" disabled={!pickedDebt.length} tooltip={pickedDebt.length ? undefined : 'Không ai trong số đã chọn còn nợ học phí'} onClick={() => setBulk('remind')}>
              Nhắc học phí ({pickedDebt.length})
            </Button>
            {can('academic.classes') && cls.status !== 'cancelled' && (
              <Button icon={Megaphone} size="sm" variant="soft" disabled={!pickedActive.length} onClick={() => setBulk('notify')}>
                Gửi thông báo ({pickedActive.length})
              </Button>
            )}
            {can('academic.classes') && (
              <Button icon={ArrowRightLeft} size="sm" variant="soft" disabled={!pickedActive.length} tooltip={pickedActive.length ? undefined : 'Chỉ chuyển được học viên đang học / bảo lưu'} onClick={() => setBulk('transfer')}>
                Chuyển lớp ({pickedActive.length})
              </Button>
            )}
          </>
        )}
        rowActions={(r) => {
          const active = r.enrollment.status === 'studying' || r.enrollment.status === 'reserved';
          const canCert = r.enrollment.status === 'completed' || (cls.status === 'finished' && r.enrollment.status === 'studying');
          const more = [
            { key: 'refund', icon: <Undo2 size={15} />, label: 'Hoàn tiền', disabled: !refundableOf(r), onClick: () => setDialog({ kind: 'refund', row: r }) },
            { key: 'care', icon: <PhoneCall size={15} />, label: 'Ghi nhận liên hệ', disabled: closed, onClick: () => setCareId(r.enrollment.studentId) },
            { type: 'divider' as const },
            r.enrollment.status === 'reserved'
              ? { key: 'resume', icon: <PlayCircle size={15} />, label: 'Học lại (hết bảo lưu)', disabled: closed, onClick: () => resume(r) }
              : { key: 'reserve', icon: <PauseCircle size={15} />, label: 'Bảo lưu', disabled: closed || r.enrollment.status !== 'studying', onClick: () => setDialog({ kind: 'reserve', row: r }) },
            { key: 'transfer', icon: <ArrowRightLeft size={15} />, label: 'Chuyển lớp', disabled: !active, onClick: () => setDialog({ kind: 'transfer', row: r }) },
            { key: 'drop', icon: <UserX size={15} />, label: 'Cho nghỉ học', danger: true, disabled: !active, onClick: () => drop(r) },
          ];
          return (
            <>
              <Button icon={FileUser} iconOnly size="sm" variant="ghost" tooltip="Hồ sơ học tập" aria-label="Hồ sơ học tập" onClick={() => setProfileId(r.enrollment.studentId)} />
              {editable && (
                <>
                  {canCert ? (
                    <Button
                      icon={Mail}
                      iconOnly
                      size="sm"
                      tone="primary"
                      variant="soft"
                      tooltip={r.enrollment.certificate ? 'Gửi lại chứng nhận qua email' : 'Gửi chứng nhận hoàn thành qua email'}
                      aria-label="Gửi chứng nhận"
                      onClick={() => setCertId(r.enrollment.studentId)}
                    />
                  ) : (
                    <Button icon={HandCoins} iconOnly size="sm" tone="success" variant="soft" tooltip="Thu học phí" aria-label="Thu học phí" disabled={!r.sum?.remaining} onClick={() => setDialog({ kind: 'pay', row: r })} />
                  )}
                  <Dropdown trigger={['click']} menu={{ items: more }}>
                    <Button icon={Ellipsis} iconOnly size="sm" variant="ghost" tooltip="Thao tác khác" aria-label="Thao tác khác" />
                  </Dropdown>
                </>
              )}
            </>
          );
        }}
        actionsWidth={editable ? 130 : 60}
        empty={{ title: 'Lớp chưa có học viên', description: 'Thêm học viên đã có hồ sơ, hoặc tạo hồ sơ từ Lead ở Tuyển sinh.' }}
      />

      {/* ===== Thu học phí ===== */}
      <ModalForm<{ amount: number; method: PaymentMethod; paidAt: Dayjs }>
        key={dialog?.kind === 'pay' ? `pay-${dialog.row.id}` : 'pay'}
        open={dialog?.kind === 'pay'}
        onClose={() => setDialog(null)}
        title="Thu học phí"
        description={dialog?.kind === 'pay' ? `${dialog.row.fullName} · ${dialog.row.invoice?.code} · còn nợ ${formatCurrency(dialog.row.sum?.remaining)}` : undefined}
        submitAction="save"
        submitText="Xác nhận đã thu"
        successMessage="Đã ghi nhận thu học phí"
        initialValues={dialog?.kind === 'pay' ? { amount: dialog.row.sum?.remaining, method: 'cash', paidAt: dayjs() } : undefined}
        onSubmit={async (v) => {
          if (dialog?.kind !== 'pay' || !dialog.row.invoice) return;
          if (USE_API) return void (await financeApi.recordPayment(dialog.row.invoice.id, { amount: v.amount, method: v.method, paidAt: v.paidAt.toISOString() }));
          await new Promise((r) => setTimeout(r, 400));
          addPayment(dialog.row.invoice.id, { amount: v.amount, method: v.method, paidAt: v.paidAt.toISOString(), collector: user?.fullName ?? 'Nhân viên' });
        }}
      >
        <Form.Item
          name="amount"
          label="Số tiền thu"
          rules={[
            { required: true, message: 'Nhập số tiền' },
            { validator: (_, v: number) => (dialog?.kind === 'pay' && v > (dialog.row.sum?.remaining ?? 0) ? Promise.reject(new Error('Vượt quá số còn nợ')) : v > 0 ? Promise.resolve() : Promise.reject(new Error('Phải lớn hơn 0'))) },
          ]}
          extra="Thu một phần được — phần còn lại giữ là công nợ"
        >
          <NumberInput currency min={0} step={100_000} />
        </Form.Item>
        <Form.Item name="method" label="Hình thức" rules={[{ required: true }]}>
          <Radio.Group optionType="button" buttonStyle="solid" options={METHOD_OPTIONS} />
        </Form.Item>
        <Form.Item name="paidAt" label="Ngày thu" rules={[{ required: true, message: 'Chọn ngày' }]}>
          <DateInput showTime disabledDate={(d) => d.isAfter(dayjs())} />
        </Form.Item>
      </ModalForm>

      {/* ===== Hoàn tiền ===== */}
      <ModalForm<{ amount: number; method: PaymentMethod; reason: string }>
        key={dialog?.kind === 'refund' ? `refund-${dialog.row.id}` : 'refund'}
        open={dialog?.kind === 'refund'}
        onClose={() => setDialog(null)}
        title="Hoàn tiền học phí"
        description={dialog?.kind === 'refund' ? `${dialog.row.fullName} · đã đóng ${formatCurrency(dialog.row.sum?.paid)} · hoàn tối đa ${formatCurrency(refundableOf(dialog.row))}` : undefined}
        submitAction="send"
        submitText="Gửi hoàn tiền"
        successMessage="Đã ghi nhận"
        initialValues={{ method: 'transfer' }}
        onSubmit={async (v) => {
          if (dialog?.kind !== 'refund' || !dialog.row.invoice) return;
          const reason = v.reason.trim();
          if (USE_API) {
            // Server quyết theo ngưỡng Cài đặt: ghi hoàn ngay hoặc mở phiếu duyệt
            const ticket = await financeApi.requestRefund(dialog.row.invoice.id, { amount: v.amount, method: v.method, reason });
            if (ticket) notify.info({ title: `Đã gửi yêu cầu hoàn tiền (${ticket.code})`, description: 'Tiền chỉ được ghi hoàn sau khi người thứ hai duyệt (Hệ thống › Duyệt thao tác).' });
            return;
          }
          await new Promise((r) => setTimeout(r, 400));
          if (refundNeedsApproval(v.amount)) {
            requestApproval(
              { kind: 'refund', title: refundTitle(v.amount, dialog.row.invoice.code, dialog.row.fullName), detail: reason, branchId: cls.branchId, payload: { kind: 'refund', invoiceId: dialog.row.invoice.id, invoiceCode: dialog.row.invoice.code, amount: v.amount, method: v.method, reason } },
              { id: user?.id ?? '', name: user?.fullName ?? 'Nhân viên' },
            );
            notify.info({ title: 'Đã gửi yêu cầu hoàn tiền', description: 'Tiền chỉ được ghi hoàn sau khi người thứ hai duyệt (Hệ thống › Duyệt thao tác).' });
          } else addRefund(dialog.row.invoice.id, { amount: v.amount, method: v.method, reason, refundedAt: new Date().toISOString(), by: user?.fullName ?? 'Nhân viên' });
        }}
      >
        <p className="mb-4 rounded-lg bg-page px-3 py-2 text-[13px] text-ink-secondary">
          Gợi ý: hoàn theo số buổi chưa học. Hoàn tiền cần một người khác duyệt (Hệ thống › Duyệt thao tác) rồi Kế toán mới chi.
        </p>
        <Form.Item
          name="amount"
          label="Số tiền hoàn"
          rules={[
            { required: true, message: 'Nhập số tiền' },
            { validator: (_, v: number) => (dialog?.kind === 'refund' && v > refundableOf(dialog.row) ? Promise.reject(new Error('Vượt quá số còn hoàn được')) : v > 0 ? Promise.resolve() : Promise.reject(new Error('Phải lớn hơn 0'))) },
          ]}
        >
          <NumberInput currency min={0} step={100_000} />
        </Form.Item>
        <Form.Item name="method" label="Hoàn qua" rules={[{ required: true }]}>
          <Radio.Group optionType="button" buttonStyle="solid" options={METHOD_OPTIONS.filter((o) => o.value !== 'vietqr')} />
        </Form.Item>
        <Form.Item name="reason" label="Lý do hoàn" rules={[{ required: true, whitespace: true, message: 'Nhập lý do' }]}>
          <TextInput placeholder="VD: Nghỉ sau buổi 10 — hoàn 26/36 buổi chưa học" />
        </Form.Item>
      </ModalForm>

      {/* ===== Thêm học viên ===== */}
      <AddStudentDialog
        cls={cls}
        open={dialog?.kind === 'add'}
        onClose={() => setDialog(null)}
        excludeIds={rows.filter((r) => r.enrollment.status !== 'dropped').map((r) => r.enrollment.studentId)}
        onAdd={async (studentId, discount, due) => {
          if (!USE_API) return void enroll(studentId, cls.id, { discount, dueDate: due });
          // Server kiểm lớp còn chỗ / chưa trùng; HV đã ẩn được bỏ ẩn
          const warnings = await studentApi.enroll(studentId, cls.id, { discount, dueDate: due });
          if (warnings.length) notify.info({ title: 'Đã thêm học viên', description: warnings.join(' · ') });
        }}
      />
      <ReserveModal enrollment={dialog?.kind === 'reserve' ? dialog.row.enrollment : null} name={dialog?.kind === 'reserve' ? dialog.row.fullName : undefined} onClose={() => setDialog(null)} />
      <TransferModal cls={cls} enrollment={dialog?.kind === 'transfer' ? dialog.row.enrollment : null} name={dialog?.kind === 'transfer' ? dialog.row.fullName : undefined} onClose={() => setDialog(null)} />
      <RemindModal
        open={bulk === 'remind'}
        invoiceIds={pickedDebt.map((r) => r.invoice!.id)}
        onClose={() => setBulk(null)}
        description={`${cls.code} · tổng còn nợ ${formatCurrency(pickedDebt.reduce((t, r) => t + (r.sum?.remaining ?? 0), 0))}`}
        onDone={clearSelection}
      />
      <ComposeAnnouncementModal cls={cls} open={bulk === 'notify'} onClose={() => setBulk(null)} studentIds={pickedActive.map((r) => r.enrollment.studentId)} />
      <BulkTransferModal
        cls={cls}
        items={bulk === 'transfer' ? pickedActive.map((r) => ({ enrollment: r.enrollment, name: r.fullName })) : null}
        onClose={() => setBulk(null)}
        onDone={clearSelection}
      />
      <CertificateModal cls={cls} studentId={certId} onClose={() => setCertId(null)} />
      <CareLogModal cls={cls} studentId={careId} name={rows.find((r) => r.enrollment.studentId === careId)?.fullName} onClose={() => setCareId(null)} />
      <StudentLearningDrawer cls={cls} studentId={profileId} onClose={() => setProfileId(null)} />
      {confirmDialog}
    </div>
  );
}

function AddStudentDialog({
  cls,
  open,
  onClose,
  excludeIds,
  onAdd,
}: {
  cls: ClassInfo;
  open: boolean;
  onClose: () => void;
  excludeIds: string[];
  onAdd: (studentId: string, discount: number, due: string) => void | Promise<void>;
}) {
  const students = useCrmStore((s) => s.students);
  const options = students
    .filter((s) => !s.hidden && !excludeIds.includes(s.id))
    .sort((a, b) => Number(b.branchId === cls.branchId) - Number(a.branchId === cls.branchId))
    .map((s) => ({ value: s.id, label: `${s.code} — ${s.fullName}${s.phone ? ` · ${s.phone}` : ''}` }));

  return (
    <ModalForm<{ studentId: string; discount?: number; dueDate: Dayjs }>
      open={open}
      onClose={onClose}
      title={`Thêm học viên vào lớp ${cls.code}`}
      description={`Tự sinh hóa đơn học phí ${formatCurrency(cls.fee)}`}
      submitAction="create"
      submitText="Ghi danh"
      successMessage="Đã ghi danh học viên"
      initialValues={{ discount: 0, dueDate: dayjs().add(7, 'day') }}
      onSubmit={async (v) => {
        await onAdd(v.studentId, v.discount ?? 0, v.dueDate.format('YYYY-MM-DD'));
      }}
    >
      <Form.Item name="studentId" label="Học viên" rules={[{ required: true, message: 'Chọn học viên' }]} extra="Học viên mới chưa có hồ sơ → tạo từ Lead ở Tuyển sinh">
        <Select placeholder="Tìm theo tên, mã, SĐT..." options={options} />
      </Form.Item>
      <Form.Item
        name="discount"
        label="Giảm giá"
        rules={[{ validator: (_, v?: number) => ((v ?? 0) > cls.fee ? Promise.reject(new Error('Vượt quá học phí')) : Promise.resolve()) }]}
        extra="Ưu đãi anh chị em, học viên cũ..."
      >
        <NumberInput currency min={0} step={100_000} />
      </Form.Item>
      <Form.Item name="dueDate" label="Hạn đóng học phí" rules={[{ required: true, message: 'Chọn hạn' }]}>
        <DateInput />
      </Form.Item>
    </ModalForm>
  );
}
