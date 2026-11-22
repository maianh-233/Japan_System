import { useMemo, useState } from 'react';
import { Modal } from 'antd';
import dayjs from 'dayjs';
import { CalendarClock, CheckCircle2, Copy, QrCode, Receipt, Wallet } from 'lucide-react';
import { Button, StatCard, StatusTag, useNotify } from '@/components/common';
import { useClassStore } from '@/features/academic/classes/classStore';
import { FakeQr } from '@/features/academic/classes/detail/SessionAttendanceTab';
import { useCrmStore } from '@/features/crm/crmStore';
import { summarizeInvoice, useStudentStore } from '@/features/student/studentStore';
import type { Invoice } from '@/features/student/types';
import { useSystemStore } from '@/features/system/systemStore';
import { useBreakpoint } from '@/hooks/useBreakpoint';
import { cn } from '@/lib/cn';
import { fmtDate } from '@/lib/date';
import { formatCurrency } from '@/lib/format';
import { normalizeText } from '@/lib/text';
import { USE_API } from '@/lib/api/config';
import { shortCode } from '../usePortal';

const METHOD: Record<string, string> = { cash: 'Tiền mặt', transfer: 'Chuyển khoản', vietqr: 'VietQR', card: 'Thẻ' };

interface Row {
  invoice: Invoice;
  sum: ReturnType<typeof summarizeInvoice>;
  studentName: string;
  classLabel: string;
  branchId: string;
}

/** Học phí của 1 hoặc nhiều học viên (cổng HV / PH): hóa đơn, lịch sử đóng, thanh toán VietQR */
export function TuitionView({ studentIds }: { studentIds: string[] }) {
  const notify = useNotify();
  const { isMobile } = useBreakpoint();
  const enrollments = useStudentStore((s) => s.enrollments);
  const invoices = useStudentStore((s) => s.invoices);
  const classes = useClassStore((s) => s.classes);
  const students = useCrmStore((s) => s.students);
  const branches = useSystemStore((s) => s.branches);
  const [paying, setPaying] = useState<Row | null>(null);

  const rows: Row[] = useMemo(
    () =>
      invoices
        .map((inv) => {
          const e = enrollments.find((x) => x.id === inv.enrollmentId);
          if (!e || !studentIds.includes(e.studentId)) return null;
          const cls = classes.find((c) => c.id === e.classId);
          return { invoice: inv, sum: summarizeInvoice(inv), studentName: students.find((s) => s.id === e.studentId)?.fullName ?? '', classLabel: cls ? `${shortCode(cls)} · ${cls.courseLabel}` : '', branchId: cls?.branchId ?? 'cg' };
        })
        .filter((x): x is Row => !!x)
        .sort((a, b) => b.invoice.issuedAt.localeCompare(a.invoice.issuedAt)),
    [invoices, enrollments, classes, students, studentIds],
  );

  const owing = rows.filter((r) => r.sum.remaining > 0);
  const remaining = owing.reduce((s, r) => s + r.sum.remaining, 0);
  const nextDue = owing.map((r) => r.invoice.dueDate).sort()[0];
  const paidTotal = rows.reduce((s, r) => s + r.sum.paid, 0);
  const history = rows.flatMap((r) => r.invoice.payments.map((p) => ({ ...p, row: r }))).sort((a, b) => b.paidAt.localeCompare(a.paidAt));
  const multi = studentIds.length > 1;

  // Chế độ API: tài khoản nhận + nội dung CK do server trả (chi nhánh chưa khai báo → không có QR — TC-09.E2)
  const bank = paying ? (USE_API ? paying.invoice.qr : branches.find((b) => b.id === paying.branchId)?.bank) : undefined;
  const content = paying ? (paying.invoice.transferContent ?? `${paying.invoice.code} ${normalizeText(paying.studentName).toUpperCase()}`) : '';

  return (
    <>
      <div className="mb-4 grid grid-cols-1 gap-3 sm:grid-cols-3">
        <StatCard label="Còn phải đóng" value={formatCurrency(remaining)} icon={Wallet} tone={owing.some((r) => r.sum.status === 'overdue') ? 'danger' : remaining ? 'warning' : 'success'} hint={remaining ? `${owing.length} hóa đơn` : 'Đã đóng đủ'} />
        <StatCard label="Hạn gần nhất" value={nextDue ? fmtDate(nextDue) : '—'} icon={CalendarClock} tone="info" hint={nextDue ? (dayjs(nextDue).isBefore(dayjs(), 'day') ? `Quá hạn ${dayjs().diff(dayjs(nextDue), 'day')} ngày` : `Còn ${dayjs(nextDue).diff(dayjs().startOf('day'), 'day')} ngày`) : undefined} />
        <StatCard label="Đã đóng" value={formatCurrency(paidTotal)} icon={CheckCircle2} tone="success" hint={`${history.length} lần thanh toán`} />
      </div>

      <section className="mb-6 flex flex-col gap-3">
        <h2 className="text-base font-semibold text-ink">Hóa đơn</h2>
        {rows.map((r) => (
          <article key={r.invoice.id} className={cn('rounded-xl border bg-surface p-4', r.sum.status === 'overdue' ? 'border-danger-100' : 'border-line')}>
            <div className="flex flex-wrap items-start justify-between gap-2">
              <div className="min-w-0">
                <p className="font-semibold text-ink">{r.classLabel}</p>
                <p className="text-xs text-ink-muted">
                  <span className="font-mono">{r.invoice.code}</span> · lập {fmtDate(r.invoice.issuedAt)} · hạn {fmtDate(r.invoice.dueDate)}
                  {multi && ` · ${r.studentName}`}
                </p>
              </div>
              <StatusTag group="invoice" status={r.sum.status} size="sm" />
            </div>
            <div className="mt-3 grid grid-cols-3 gap-2 text-[13px]">
              <div>
                <p className="text-xs text-ink-muted">Học phí</p>
                <p className="tabular font-medium">{formatCurrency(r.sum.total)}</p>
                {r.invoice.discount > 0 && <p className="text-[11px] text-success-700">đã giảm {formatCurrency(r.invoice.discount)}</p>}
              </div>
              <div>
                <p className="text-xs text-ink-muted">Đã đóng</p>
                <p className="tabular font-medium text-success-700">{formatCurrency(r.sum.paid)}</p>
              </div>
              <div>
                <p className="text-xs text-ink-muted">Còn lại</p>
                <p className={cn('tabular font-semibold', r.sum.remaining ? 'text-danger-700' : 'text-ink')}>{formatCurrency(r.sum.remaining)}</p>
              </div>
            </div>
            {r.sum.total > 0 && (
              <div className="mt-2 h-1.5 overflow-hidden rounded-full bg-canvas">
                <div className="h-full bg-success-600" style={{ width: `${Math.min(100, (r.sum.collected / r.sum.total) * 100)}%` }} />
              </div>
            )}
            {r.sum.remaining > 0 &&
              (USE_API && !r.invoice.qr ? (
                <p className="mt-3 text-[13px] text-ink-secondary">Vui lòng đóng tại quầy hoặc liên hệ trung tâm để được hướng dẫn chuyển khoản.</p>
              ) : (
                <Button icon={QrCode} size="sm" className="mt-3" onClick={() => setPaying(r)}>
                  Thanh toán VietQR
                </Button>
              ))}
          </article>
        ))}
        {!rows.length && <p className="rounded-xl border border-dashed border-line p-6 text-center text-ink-muted">Chưa có hóa đơn học phí.</p>}
      </section>

      <section>
        <h2 className="mb-2 text-base font-semibold text-ink">Lịch sử thanh toán</h2>
        <ul className="flex flex-col divide-y divide-line rounded-xl border border-line bg-surface">
          {history.map((p) => (
            <li key={p.id} className="flex items-center justify-between gap-3 px-4 py-3 text-[13px]">
              <span className="flex min-w-0 items-center gap-2">
                <Receipt size={16} className="shrink-0 text-ink-muted" aria-hidden />
                <span className="min-w-0">
                  <span className="block truncate font-medium text-ink">{p.row.classLabel}</span>
                  <span className="block text-xs text-ink-muted">
                    {fmtDate(p.paidAt)} · {METHOD[p.method] ?? p.method} · {p.row.invoice.code}
                  </span>
                </span>
              </span>
              <span className="tabular shrink-0 font-semibold text-success-700">+{formatCurrency(p.amount)}</span>
            </li>
          ))}
          {!history.length && <li className="px-4 py-6 text-center text-ink-muted">Chưa có lần thanh toán nào.</li>}
        </ul>
      </section>

      <Modal open={Boolean(paying)} onCancel={() => setPaying(null)} footer={null} title="Thanh toán bằng VietQR" width={isMobile ? '100%' : 440} centered destroyOnHidden>
        {paying && (
          <div className="flex flex-col items-center gap-3 text-[13px]">
            <p className="text-center text-ink-secondary">
              {USE_API
                ? 'Chuyển khoản đúng số tiền và nội dung bên dưới — hệ thống tự ghi nhận khi tiền về tài khoản trung tâm.'
                : 'Đây là giao dịch mô phỏng, không thực hiện chuyển khoản thật. Xác nhận bên dưới sẽ cập nhật số dư và lịch sử học phí dùng chung.'}
            </p>
            <div className="rounded-xl border border-line bg-white p-3">
              <FakeQr seed={paying.invoice.code} className="size-48" />
            </div>
            <dl className="grid w-full grid-cols-[110px_1fr] gap-x-3 gap-y-1.5 rounded-xl bg-canvas p-3">
              <dt className="text-ink-muted">Ngân hàng</dt>
              <dd className="font-medium">{bank?.bank ?? '—'}</dd>
              <dt className="text-ink-muted">Số tài khoản</dt>
              <dd className="tabular font-medium">{bank?.account ?? '—'}</dd>
              <dt className="text-ink-muted">Chủ tài khoản</dt>
              <dd>{bank?.holder ?? '—'}</dd>
              <dt className="text-ink-muted">Số tiền</dt>
              <dd className="tabular text-base font-bold text-primary-700">{formatCurrency(paying.sum.remaining)}</dd>
              <dt className="text-ink-muted">Nội dung</dt>
              <dd className="flex items-center gap-1.5">
                <code className="rounded bg-surface px-1.5 py-0.5">{content}</code>
                <Button
                  icon={Copy}
                  iconOnly
                  size="sm"
                  variant="ghost"
                  tone="neutral"
                  aria-label="Sao chép nội dung"
                  onClick={() => {
                    void navigator.clipboard?.writeText(content);
                    notify.toast.success('Đã sao chép nội dung chuyển khoản');
                  }}
                />
              </dd>
            </dl>
            {!USE_API && (
              <Button
                icon={CheckCircle2}
                className="w-full"
                onClick={() => {
                  const invoice = useStudentStore.getState().invoices.find((item) => item.id === paying.invoice.id);
                  const amount = invoice ? summarizeInvoice(invoice).remaining : 0;
                  if (!invoice || amount <= 0) {
                    setPaying(null);
                    notify.info({ title: 'Hóa đơn đã được cập nhật', description: 'Không còn khoản phải thanh toán.' });
                    return;
                  }
                  useStudentStore.getState().addPayment(paying.invoice.id, {
                    amount,
                    method: 'vietqr',
                    paidAt: new Date().toISOString(),
                    collector: 'VietQR (mô phỏng)',
                  });
                  setPaying(null);
                  notify.success({ title: 'Đã ghi nhận thanh toán mô phỏng', description: 'Số dư và lịch sử học phí đã được cập nhật.' });
                }}
              >
                Xác nhận thanh toán mô phỏng
              </Button>
            )}
          </div>
        )}
      </Modal>
    </>
  );
}
