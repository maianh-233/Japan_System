import { useMemo, useState } from 'react';
import { Form, Radio } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { HandCoins, Undo2, UserPlus, UserX } from 'lucide-react';
import { Button, DataTable, DateInput, ModalForm, NumberInput, Select, StatusTag, TextInput, UserCell, useConfirm, useNotify, type DataColumn } from '@/components/common';
import { useCrmStore } from '@/features/crm/crmStore';
import { ENROLLMENT_STATUS, PAYMENT_METHODS } from '@/features/student/constants';
import { summarizeInvoice, useStudentStore, type InvoiceSummary } from '@/features/student/studentStore';
import type { Enrollment, Invoice, PaymentMethod } from '@/features/student/types';
import { useAuth } from '@/hooks/useAuth';
import { cn } from '@/lib/cn';
import { fmtDate } from '@/lib/date';
import { formatCurrency } from '@/lib/format';
import type { ClassInfo } from '../types';

interface Row {
  id: string;
  enrollment: Enrollment;
  invoice?: Invoice;
  sum?: InvoiceSummary;
  fullName: string;
  code: string;
  phone: string;
}

type Dialog = { kind: 'pay' | 'refund'; row: Row } | { kind: 'add' } | null;

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
  const [dialog, setDialog] = useState<Dialog>(null);
  const editable = can('academic.classes') || can('finance.invoices');
  const closed = cls.status === 'finished' || cls.status === 'cancelled';

  const rows: Row[] = useMemo(
    () =>
      enrollments
        .filter((e) => e.classId === cls.id)
        .map((e) => {
          const st = students.find((s) => s.id === e.studentId);
          const invoice = invoices.find((i) => i.enrollmentId === e.id);
          return { id: e.id, enrollment: e, invoice, sum: invoice && summarizeInvoice(invoice), fullName: st?.fullName ?? e.studentId, code: st?.code ?? '', phone: st?.phone ?? '' };
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
    });
    if (!res) return;
    dropEnrollment(r.enrollment.id, res.reason ?? '');
    notify.success({ title: 'Đã cho học viên nghỉ', description: `${r.fullName} — ${cls.code}` });
    if (r.sum && r.sum.paid > 0) setTimeout(() => setDialog({ kind: 'refund', row: r }), 300);
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
          <UserCell name={r.fullName} subtitle={r.code} size="sm" />
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
          <span title={r.enrollment.note}>
            <StatusTag tone={waiting ? 'info' : s.tone} icon={s.icon} size="sm">
              {waiting ? 'Chờ khai giảng' : s.label}
            </StatusTag>
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
          actions:
            editable && !closed ? (
              <Button icon={UserPlus} collapseOnMobile disabled={activeCount >= cls.maxStudents} onClick={() => setDialog({ kind: 'add' })}>
                Thêm học viên
              </Button>
            ) : undefined,
        }}
        rowActions={
          editable
            ? (r) => {
                const active = r.enrollment.status === 'studying' || r.enrollment.status === 'reserved';
                return (
                  <>
                    <Button icon={HandCoins} iconOnly size="sm" tone="success" variant="soft" tooltip="Thu học phí" aria-label="Thu học phí" disabled={!r.sum?.remaining} onClick={() => setDialog({ kind: 'pay', row: r })} />
                    <Button icon={Undo2} iconOnly size="sm" tone="warning" variant="soft" tooltip="Hoàn tiền" aria-label="Hoàn tiền" disabled={!r.sum?.refundable} onClick={() => setDialog({ kind: 'refund', row: r })} />
                    <Button icon={UserX} iconOnly size="sm" tone="danger" variant="soft" tooltip="Cho nghỉ học (hủy lớp cho học viên)" aria-label="Cho nghỉ học" disabled={!active} onClick={() => drop(r)} />
                  </>
                );
              }
            : undefined
        }
        actionsWidth={150}
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
        description={dialog?.kind === 'refund' ? `${dialog.row.fullName} · đã đóng ${formatCurrency(dialog.row.sum?.paid)} · hoàn tối đa ${formatCurrency(dialog.row.sum?.refundable)}` : undefined}
        submitAction="save"
        submitText="Xác nhận hoàn tiền"
        successMessage="Đã ghi nhận hoàn tiền"
        initialValues={{ method: 'transfer' }}
        onSubmit={async (v) => {
          if (dialog?.kind !== 'refund' || !dialog.row.invoice) return;
          await new Promise((r) => setTimeout(r, 400));
          addRefund(dialog.row.invoice.id, { amount: v.amount, method: v.method, reason: v.reason.trim(), refundedAt: new Date().toISOString(), by: user?.fullName ?? 'Nhân viên' });
        }}
      >
        <p className="mb-4 rounded-lg bg-page px-3 py-2 text-[13px] text-ink-secondary">
          Gợi ý: hoàn theo số buổi chưa học. Phiếu hoàn tiền được chuyển sang Tài chính để Kế toán chi.
        </p>
        <Form.Item
          name="amount"
          label="Số tiền hoàn"
          rules={[
            { required: true, message: 'Nhập số tiền' },
            { validator: (_, v: number) => (dialog?.kind === 'refund' && v > (dialog.row.sum?.refundable ?? 0) ? Promise.reject(new Error('Vượt quá số đã đóng')) : v > 0 ? Promise.resolve() : Promise.reject(new Error('Phải lớn hơn 0'))) },
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
        onAdd={(studentId, discount, due) => enroll(studentId, cls.id, { discount, dueDate: due })}
      />
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
  onAdd: (studentId: string, discount: number, due: string) => void;
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
        await new Promise((r) => setTimeout(r, 400));
        onAdd(v.studentId, v.discount ?? 0, v.dueDate.format('YYYY-MM-DD'));
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
