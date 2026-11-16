import { useState } from 'react';
import { Drawer, Form, Radio } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { BellRing, Copy, HandCoins, Printer, QrCode, Undo2 } from 'lucide-react';
import { Button, DateInput, ModalForm, NumberInput, Select, StatusTag, TextInput, UserCell, useNotify } from '@/components/common';
import { FakeQr } from '@/features/academic/classes/detail/SessionAttendanceTab';
import { useHrStore } from '@/features/hr/hrStore';
import { nameOf } from '@/features/hr/hrUtils';
import { PAYMENT_METHODS } from '@/features/student/constants';
import { useStudentStore } from '@/features/student/studentStore';
import type { PaymentMethod } from '@/features/student/types';
import { useAuth } from '@/hooks/useAuth';
import { useBreakpoint } from '@/hooks/useBreakpoint';
import { cn } from '@/lib/cn';
import { fmtDate, fmtDateTime } from '@/lib/date';
import { formatCurrency } from '@/lib/format';
import { getBranches } from '@/features/system/useBranches';
import { BANK_ACCOUNTS, REMINDER_CHANNEL } from './constants';
import { useFinanceStore } from './financeStore';
import { daysLate, transferContent, type InvoiceRow } from './financeUtils';
import type { ReminderChannel } from './types';

const METHOD_OPTIONS = (Object.keys(PAYMENT_METHODS) as PaymentMethod[]).map((k) => ({ value: k, label: PAYMENT_METHODS[k] }));

/** Chi tiết hóa đơn: VietQR, lịch sử thu / hoàn / nhắc + thao tác thu tiền, hoàn tiền, nhắc học phí */
export function InvoiceDrawer({ row, onClose }: { row?: InvoiceRow; onClose: () => void }) {
  const { user, can } = useAuth();
  const { isMobile } = useBreakpoint();
  const notify = useNotify();
  const employees = useHrStore((s) => s.employees);
  const addPayment = useStudentStore((s) => s.addPayment);
  const addRefund = useStudentStore((s) => s.addRefund);
  const reminders = useFinanceStore((s) => s.reminders);
  const [dialog, setDialog] = useState<'pay' | 'refund' | 'remind' | null>(null);
  const editable = can('finance.invoices');

  const inv = row?.invoice;
  const sum = row?.sum;
  const bank = row ? BANK_ACCOUNTS[row.branchId] : undefined;
  const history = inv
    ? [
        ...inv.payments.map((p) => ({ at: p.paidAt, kind: 'pay' as const, text: `Thu ${formatCurrency(p.amount)} · ${PAYMENT_METHODS[p.method]}`, by: p.collector })),
        ...inv.refunds.map((r) => ({ at: r.refundedAt, kind: 'refund' as const, text: `Hoàn ${formatCurrency(r.amount)} · ${PAYMENT_METHODS[r.method]} — ${r.reason}`, by: r.by })),
        ...reminders.filter((r) => r.invoiceId === inv.id).map((r) => ({ at: r.at, kind: 'remind' as const, text: `Nhắc qua ${REMINDER_CHANNEL[r.channel].label}${r.note ? ` — ${r.note}` : ''}`, by: r.by })),
        { at: inv.issuedAt, kind: 'issue' as const, text: `Phát hành hóa đơn ${formatCurrency(inv.amount)}${inv.discount ? ` (giảm ${formatCurrency(inv.discount)})` : ''}`, by: 'Hệ thống (ghi danh)' },
      ].sort((a, b) => b.at.localeCompare(a.at))
    : [];

  const copy = (text: string) => {
    void navigator.clipboard?.writeText(text);
    notify.toast.success('Đã sao chép');
  };

  return (
    <>
      <Drawer open={Boolean(row)} onClose={onClose} width={isMobile ? '100%' : 600} destroyOnHidden title={inv && `Hóa đơn ${inv.code}`}>
        {row && inv && sum && (
          <div className="flex flex-col gap-4 text-[13px]">
            <div className="flex flex-wrap items-start justify-between gap-2">
              <UserCell name={row.student?.fullName ?? '—'} subtitle={`${row.student?.code ?? ''} · ${row.student?.phone ?? ''}`} />
              <StatusTag group="invoice" status={sum.status} />
            </div>
            <dl className="grid grid-cols-[120px_1fr] gap-y-1.5">
              <dt className="text-ink-muted">Lớp</dt>
              <dd>
                {row.cls?.code} · {row.cls?.name}
              </dd>
              <dt className="text-ink-muted">Chi nhánh</dt>
              <dd>{getBranches(false).find((b) => b.id === row.branchId)?.name}</dd>
              <dt className="text-ink-muted">Sale ghi nhận</dt>
              <dd>{row.saleId ? nameOf(employees, row.saleId) : '—'}</dd>
              <dt className="text-ink-muted">Ngày phát hành</dt>
              <dd>{fmtDate(inv.issuedAt)}</dd>
              <dt className="text-ink-muted">Hạn đóng</dt>
              <dd className={cn(sum.status === 'overdue' && 'font-semibold text-danger-600')}>
                {fmtDate(inv.dueDate)}
                {sum.status === 'overdue' && ` · quá hạn ${daysLate(row)} ngày`}
              </dd>
            </dl>

            <div className="grid grid-cols-2 gap-2 rounded-xl border border-line p-3 sm:grid-cols-4">
              {[
                ['Học phí', inv.amount],
                ['Giảm', inv.discount ? -inv.discount : 0],
                ['Đã thu (thực)', sum.paid],
                ['Còn nợ', sum.remaining],
              ].map(([l, v], i) => (
                <div key={i}>
                  <p className="text-xs text-ink-muted">{l}</p>
                  <p className={cn('tabular font-semibold', i === 3 && (v as number) > 0 && 'text-danger-600', i === 2 && 'text-success-700')}>{formatCurrency(v as number)}</p>
                </div>
              ))}
            </div>

            {sum.remaining > 0 && bank && (
              <div className="flex gap-4 rounded-xl border border-primary-100 bg-primary-50/50 p-3">
                <FakeQr seed={`${inv.code}-${sum.remaining}`} className="size-28 shrink-0 rounded bg-white p-1 text-ink" />
                <div className="flex min-w-0 flex-col gap-1">
                  <p className="flex items-center gap-1.5 font-semibold text-ink">
                    <QrCode size={15} aria-hidden /> VietQR riêng của hóa đơn
                  </p>
                  <p className="text-ink-secondary">
                    {bank.bank} · <span className="tabular">{bank.no}</span>
                  </p>
                  <p className="truncate text-xs text-ink-muted">{bank.name}</p>
                  <p>
                    Số tiền: <span className="tabular font-semibold">{formatCurrency(sum.remaining)}</span>
                  </p>
                  <button type="button" onClick={() => copy(transferContent(row))} className="inline-flex items-center gap-1 self-start rounded bg-white px-2 py-0.5 font-mono text-xs text-primary-700 ring-1 ring-primary-200 hover:bg-primary-50">
                    {transferContent(row)} <Copy size={11} aria-hidden />
                  </button>
                  <p className="text-[11px] text-ink-muted">Học viên quét mã → tiền về tài khoản → hệ thống tự khớp ở Đối soát VietQR.</p>
                </div>
              </div>
            )}

            {editable && (
              <div className="flex flex-wrap gap-2">
                {sum.remaining > 0 && (
                  <Button icon={HandCoins} onClick={() => setDialog('pay')}>
                    Thu tiền
                  </Button>
                )}
                {sum.remaining > 0 && (
                  <Button icon={BellRing} variant="soft" onClick={() => setDialog('remind')}>
                    Nhắc học phí
                  </Button>
                )}
                {sum.refundable > 0 && (
                  <Button icon={Undo2} variant="soft" tone="neutral" onClick={() => setDialog('refund')}>
                    Hoàn tiền
                  </Button>
                )}
                <Button icon={Printer} variant="ghost" tone="neutral" onClick={() => notify.toast.info('Đã gửi phiếu thu tới máy in (mô phỏng)')}>
                  In phiếu thu
                </Button>
              </div>
            )}

            <div>
              <p className="mb-2 font-semibold text-ink">Lịch sử</p>
              <ol className="relative flex flex-col gap-3 border-l border-line pl-4">
                {history.map((h, i) => (
                  <li key={i} className="relative">
                    <span className={cn('absolute -left-[21px] top-1 size-2.5 rounded-full ring-2 ring-surface', h.kind === 'pay' ? 'bg-success-600' : h.kind === 'refund' ? 'bg-danger-600' : h.kind === 'remind' ? 'bg-warning-600' : 'bg-ink-muted')} />
                    <p className="text-ink">{h.text}</p>
                    <p className="text-xs text-ink-muted">
                      {fmtDateTime(h.at)} · {h.by}
                    </p>
                  </li>
                ))}
              </ol>
            </div>
          </div>
        )}
      </Drawer>

      {/* Thu tiền */}
      <ModalForm<{ amount: number; method: PaymentMethod; paidAt: Dayjs }>
        key={`pay-${inv?.id}`}
        open={dialog === 'pay'}
        onClose={() => setDialog(null)}
        title="Thu học phí"
        description={row && `${row.student?.fullName} · ${inv?.code} · còn nợ ${formatCurrency(sum?.remaining)}`}
        submitAction="save"
        submitText="Xác nhận đã thu"
        successMessage="Đã ghi nhận thu học phí — gửi biên nhận cho học viên"
        initialValues={{ amount: sum?.remaining, method: 'cash', paidAt: dayjs() }}
        onSubmit={(v) => addPayment(inv!.id, { amount: v.amount, method: v.method, paidAt: v.paidAt.toISOString(), collector: user?.fullName ?? 'Nhân viên' })}
      >
        <Form.Item
          name="amount"
          label="Số tiền thu"
          rules={[{ required: true, message: 'Nhập số tiền' }, { validator: (_, v: number) => (v > (sum?.remaining ?? 0) ? Promise.reject(new Error('Vượt quá số còn nợ')) : v > 0 ? Promise.resolve() : Promise.reject(new Error('Phải lớn hơn 0'))) }]}
          extra="Thu một phần được — phần còn lại giữ là công nợ"
        >
          <NumberInput currency min={0} step={100_000} />
        </Form.Item>
        <Form.Item name="method" label="Hình thức" rules={[{ required: true }]}>
          <Radio.Group optionType="button" buttonStyle="solid" options={METHOD_OPTIONS} />
        </Form.Item>
        <Form.Item name="paidAt" label="Thời điểm thu" rules={[{ required: true }]}>
          <DateInput showTime disabledDate={(d) => d.isAfter(dayjs())} />
        </Form.Item>
      </ModalForm>

      {/* Hoàn tiền */}
      <ModalForm<{ amount: number; method: PaymentMethod; reason: string }>
        key={`refund-${inv?.id}`}
        open={dialog === 'refund'}
        onClose={() => setDialog(null)}
        title="Hoàn tiền học phí"
        description={row && `${row.student?.fullName} · đã đóng ${formatCurrency(sum?.paid)} · hoàn tối đa ${formatCurrency(sum?.refundable)}`}
        submitAction="save"
        submitText="Xác nhận hoàn tiền"
        successMessage="Đã ghi nhận hoàn tiền"
        initialValues={{ method: 'transfer' }}
        onSubmit={(v) => addRefund(inv!.id, { amount: v.amount, method: v.method, reason: v.reason.trim(), refundedAt: new Date().toISOString(), by: user?.fullName ?? 'Nhân viên' })}
      >
        <Form.Item
          name="amount"
          label="Số tiền hoàn"
          rules={[{ required: true, message: 'Nhập số tiền' }, { validator: (_, v: number) => (v > (sum?.refundable ?? 0) ? Promise.reject(new Error('Vượt quá số đã đóng')) : v > 0 ? Promise.resolve() : Promise.reject(new Error('Phải lớn hơn 0'))) }]}
        >
          <NumberInput currency min={0} step={100_000} />
        </Form.Item>
        <Form.Item name="method" label="Hoàn qua" rules={[{ required: true }]}>
          <Radio.Group optionType="button" buttonStyle="solid" options={METHOD_OPTIONS.filter((o) => o.value !== 'vietqr')} />
        </Form.Item>
        <Form.Item name="reason" label="Lý do" rules={[{ required: true, whitespace: true, message: 'Nhập lý do' }]}>
          <TextInput placeholder="VD: Nghỉ sau buổi 10 — hoàn 26/36 buổi chưa học" />
        </Form.Item>
      </ModalForm>

      <RemindModal open={dialog === 'remind'} invoiceIds={inv ? [inv.id] : []} onClose={() => setDialog(null)} description={row && `${row.student?.fullName} · còn nợ ${formatCurrency(sum?.remaining)} · hạn ${fmtDate(inv?.dueDate)}`} onDone={() => undefined} />
    </>
  );
}

/** Gửi nhắc học phí (1 hoặc nhiều hóa đơn) */
export function RemindModal({ open, invoiceIds, onClose, description, onDone }: { open: boolean; invoiceIds: string[]; onClose: () => void; description?: string; onDone: () => void }) {
  const { user } = useAuth();
  const remind = useFinanceStore((s) => s.remind);
  return (
    <ModalForm<{ channel: ReminderChannel; note?: string }>
      open={open}
      onClose={onClose}
      title={invoiceIds.length > 1 ? `Nhắc học phí ${invoiceIds.length} hóa đơn` : 'Nhắc học phí'}
      description={description}
      submitAction="send"
      submitText="Gửi nhắc"
      successMessage={invoiceIds.length > 1 ? `Đã gửi nhắc ${invoiceIds.length} học viên / phụ huynh` : 'Đã gửi nhắc'}
      initialValues={{ channel: 'zalo' }}
      onSubmit={(v) => {
        remind(invoiceIds, v.channel, user?.fullName ?? 'Nhân viên', v.note?.trim() || undefined);
        onDone();
      }}
    >
      <Form.Item name="channel" label="Kênh" extra="Gửi tới người liên hệ nhận hóa đơn (phụ huynh) nếu có, nếu không gửi học viên. Tin nhắn kèm mã VietQR của hóa đơn.">
        <Select options={(Object.keys(REMINDER_CHANNEL) as ReminderChannel[]).map((k) => ({ value: k, label: REMINDER_CHANNEL[k].label }))} showSearch={false} allowClear={false} />
      </Form.Item>
      <Form.Item name="note" label="Ghi chú / kết quả (gọi điện)">
        <TextInput placeholder="VD: Phụ huynh hẹn đóng thứ 7" />
      </Form.Item>
    </ModalForm>
  );
}
