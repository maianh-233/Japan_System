import { useMemo, useState } from 'react';
import { useClassStore } from '@/features/academic/classes/classStore';
import { scheduleLabel, teacherNames } from '@/features/academic/classes/constants';
import { ChevronDown, Clock, MonitorPlay, School, UserRound } from 'lucide-react';
import { EmptyState, StatusTag } from '@/components/common';
import { cn } from '@/lib/cn';
import { fmtDate, fmtDateTime } from '@/lib/date';
import { formatCurrency } from '@/lib/format';
import { getBranches } from '@/features/system/useBranches';
import { ACTIVE_ENROLLMENT, ENROLLMENT_STATUS, PAYMENT_METHODS } from '../constants';
import { summarizeInvoice, summarizeTuition, useStudentStore } from '../studentStore';
import type { Invoice } from '../types';

/** Tab Lớp học & học phí: mọi lớp học viên đã / đang học, kèm tình trạng học phí từng lớp */
export function StudentClassesTab({ studentId }: { studentId: string }) {
  const classes = useClassStore((s) => s.classes);
  const allEnrollments = useStudentStore((s) => s.enrollments);
  const allInvoices = useStudentStore((s) => s.invoices);

  const rows = useMemo(() => {
    const list = allEnrollments
      .filter((e) => e.studentId === studentId)
      .map((e) => ({ enrollment: e, cls: classes.find((c) => c.id === e.classId)!, invoice: allInvoices.find((i) => i.enrollmentId === e.id) }))
      .filter((x) => x.cls);
    // Lớp đang học / bảo lưu lên đầu, sau đó mới nhất trước
    return list.sort(
      (a, b) =>
        Number(ACTIVE_ENROLLMENT.includes(b.enrollment.status)) - Number(ACTIVE_ENROLLMENT.includes(a.enrollment.status)) ||
        b.enrollment.enrolledAt.localeCompare(a.enrollment.enrolledAt),
    );
  }, [allEnrollments, allInvoices, classes, studentId]);

  const tuition = summarizeTuition(rows.map((r) => r.invoice).filter((i): i is Invoice => Boolean(i)));

  if (!rows.length) {
    return (
      <div className="rounded-[var(--radius-card)] border border-line">
        <EmptyState
          size="compact"
          icon={School}
          title="Học viên chưa học lớp nào"
          description="Ghi danh vào lớp ở mục Đào tạo → Lớp học. Hóa đơn học phí sẽ tự sinh khi ghi danh."
        />
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-4">
      <div className="grid grid-cols-1 gap-2 sm:grid-cols-3">
        <Stat label="Tổng học phí" value={formatCurrency(tuition.total)} />
        <Stat label="Đã đóng" value={formatCurrency(tuition.paid) || '0 ₫'} tone="success" />
        <Stat label="Còn nợ" value={tuition.remaining ? formatCurrency(tuition.remaining) : 'Không nợ'} tone={tuition.remaining ? 'danger' : 'success'} />
      </div>

      <ul className="flex flex-col gap-3">
        {rows.map(({ enrollment: e, cls, invoice }) => {
          const st = ENROLLMENT_STATUS[e.status];
          return (
            <li key={e.id} className="flex flex-col gap-3 rounded-[var(--radius-card)] border border-line bg-surface p-4">
              <div className="flex flex-wrap items-start justify-between gap-2">
                <div className="min-w-0">
                  <p className="flex items-center gap-2 font-semibold text-ink">
                    {cls.code}
                    {cls.online && (
                      <span className="inline-flex items-center gap-1 rounded bg-primary-50 px-1.5 text-xs font-medium text-primary-700">
                        <MonitorPlay size={12} aria-hidden /> Online
                      </span>
                    )}
                  </p>
                  <p className="text-[13px] text-ink-muted">
                    {cls.courseLabel} · {getBranches(false).find((b) => b.id === cls.branchId)?.name}
                  </p>
                </div>
                <StatusTag tone={st.tone} icon={st.icon} size="sm">
                  {st.label}
                </StatusTag>
              </div>

              <dl className="grid grid-cols-1 gap-x-4 gap-y-1.5 text-[13px] sm:grid-cols-2">
                <div className="flex items-center gap-1.5 text-ink-secondary">
                  <UserRound size={14} className="text-ink-muted" aria-hidden />
                  <dt className="sr-only">Giáo viên</dt>
                  <dd>GV {teacherNames(cls.teacherIds)}</dd>
                </div>
                <div className="flex items-center gap-1.5 text-ink-secondary">
                  <Clock size={14} className="text-ink-muted" aria-hidden />
                  <dt className="sr-only">Lịch học</dt>
                  <dd>{scheduleLabel(cls)}</dd>
                </div>
                <div className="flex gap-2">
                  <dt className="text-ink-muted">Khóa học:</dt>
                  <dd className="tabular text-ink">
                    {fmtDate(cls.actualStartDate ?? cls.plannedStartDate)} – {fmtDate(cls.actualEndDate ?? cls.plannedEndDate)}
                  </dd>
                </div>
                <div className="flex gap-2">
                  <dt className="text-ink-muted">Ghi danh:</dt>
                  <dd className="tabular text-ink">
                    {fmtDate(e.enrolledAt)}
                    {e.endedAt && ` → ${fmtDate(e.endedAt)}`}
                  </dd>
                </div>
              </dl>
              {e.note && <p className="rounded-md bg-page px-2.5 py-1.5 text-[13px] text-ink-secondary">{e.note}</p>}

              {invoice ? <InvoiceBlock invoice={invoice} /> : <p className="text-[13px] text-ink-muted">Chưa có hóa đơn học phí</p>}
            </li>
          );
        })}
      </ul>
    </div>
  );
}

function Stat({ label, value, tone }: { label: string; value: string; tone?: 'success' | 'danger' }) {
  return (
    <div className="rounded-lg bg-page px-3 py-2.5">
      <p className="text-xs text-ink-muted">{label}</p>
      <p className={cn('tabular text-base font-semibold', tone === 'danger' ? 'text-danger-600' : tone === 'success' ? 'text-success-700' : 'text-ink')}>{value}</p>
    </div>
  );
}

function InvoiceBlock({ invoice }: { invoice: Invoice }) {
  const [open, setOpen] = useState(false);
  const sum = summarizeInvoice(invoice);
  const pct = sum.total ? Math.round((sum.paid / sum.total) * 100) : 0;

  return (
    <div className="rounded-lg border border-line">
      <button
        type="button"
        onClick={() => setOpen((v) => !v)}
        aria-expanded={open}
        className="flex w-full flex-col gap-2 px-3 py-2.5 text-left hover:bg-page sm:flex-row sm:items-center sm:justify-between"
      >
        <div className="flex min-w-0 flex-wrap items-center gap-2 text-[13px]">
          <span className="font-medium text-ink">Học phí</span>
          <span className="text-ink-muted">{invoice.code}</span>
          <StatusTag group="invoice" status={sum.status} size="sm" />
        </div>
        <div className="flex items-center gap-3 text-[13px]">
          <span className="tabular text-ink-secondary">
            {formatCurrency(sum.paid) || '0 ₫'} / {formatCurrency(sum.total)}
          </span>
          {sum.remaining > 0 && <span className="tabular font-semibold text-danger-600">Nợ {formatCurrency(sum.remaining)}</span>}
          <ChevronDown size={16} className={cn('shrink-0 text-ink-muted transition-transform', open && 'rotate-180')} aria-hidden />
        </div>
      </button>
      <div className="mx-3 mb-2.5 h-1.5 overflow-hidden rounded-full bg-disabled" role="progressbar" aria-valuenow={pct} aria-valuemin={0} aria-valuemax={100} aria-label="Tỷ lệ đã đóng">
        <div className={cn('h-full rounded-full', sum.status === 'overdue' ? 'bg-danger-600' : 'bg-success-600')} style={{ width: `${pct}%` }} />
      </div>

      {open && (
        <div className="flex flex-col gap-3 border-t border-line px-3 py-3 text-[13px]">
          <dl className="grid grid-cols-2 gap-x-4 gap-y-1 sm:grid-cols-4">
            <div>
              <dt className="text-ink-muted">Học phí gốc</dt>
              <dd className="tabular text-ink">{formatCurrency(invoice.amount)}</dd>
            </div>
            <div>
              <dt className="text-ink-muted">Giảm giá</dt>
              <dd className="tabular text-ink">{invoice.discount ? `−${formatCurrency(invoice.discount)}` : '—'}</dd>
            </div>
            <div>
              <dt className="text-ink-muted">Ngày lập</dt>
              <dd className="tabular text-ink">{fmtDate(invoice.issuedAt)}</dd>
            </div>
            <div>
              <dt className="text-ink-muted">Hạn đóng</dt>
              <dd className={cn('tabular', sum.status === 'overdue' ? 'font-medium text-danger-600' : 'text-ink')}>{fmtDate(invoice.dueDate)}</dd>
            </div>
          </dl>
          {invoice.cancelled && <p className="text-ink-muted">Đã hủy phần học phí còn lại khi học viên nghỉ.</p>}
          {invoice.refunds.map((r) => (
            <p key={r.id} className="text-ink-secondary">
              Hoàn {formatCurrency(r.amount)} ngày {fmtDate(r.refundedAt)} — {r.reason}
            </p>
          ))}
          {invoice.payments.length ? (
            <table className="w-full text-left">
              <caption className="sr-only">Lịch sử đóng tiền</caption>
              <thead className="text-xs text-ink-muted">
                <tr>
                  <th className="py-1 font-medium">Ngày đóng</th>
                  <th className="py-1 font-medium">Hình thức</th>
                  <th className="hidden py-1 font-medium sm:table-cell">Người thu</th>
                  <th className="py-1 text-right font-medium">Số tiền</th>
                </tr>
              </thead>
              <tbody>
                {invoice.payments.map((p) => (
                  <tr key={p.id} className="border-t border-line">
                    <td className="tabular py-1.5">{fmtDateTime(p.paidAt)}</td>
                    <td className="py-1.5">{PAYMENT_METHODS[p.method]}</td>
                    <td className="hidden py-1.5 sm:table-cell">{p.collector}</td>
                    <td className="tabular py-1.5 text-right font-medium">{formatCurrency(p.amount)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          ) : (
            <p className="text-ink-muted">Chưa đóng lần nào.</p>
          )}
        </div>
      )}
    </div>
  );
}
