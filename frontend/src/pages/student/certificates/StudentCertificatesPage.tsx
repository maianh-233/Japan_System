import { Award, BadgeCheck, Check, Download, X } from 'lucide-react';
import { Button, PageHeader, StatusTag, useNotify } from '@/components/common';
import { certificateChecks } from '@/features/academic/classes/detail/StudentActions';
import { shortCode, useStudentId, useStudentLearning } from '@/features/portal/usePortal';
import { cn } from '@/lib/cn';
import { fmtDate } from '@/lib/date';

/**
 * Cổng HV › Chứng nhận khóa học: chứng nhận HOÀN THÀNH KHÓA do TRUNG TÂM cấp cho học viên pass lớp
 * (khác chứng chỉ JLPT — xem mục Chứng chỉ JLPT).
 */
export default function StudentCertificatesPage() {
  const notify = useNotify();
  const studentId = useStudentId();
  const learning = useStudentLearning(studentId);

  return (
    <>
      <PageHeader title="Chứng nhận khóa học" subtitle="Chứng nhận hoàn thành khóa do trung tâm cấp khi bạn hoàn thành lớp và đủ điều kiện. Chứng chỉ JLPT (do hội đồng thi cấp) xem ở mục Chứng chỉ JLPT." />
      <div className="mb-6 grid gap-3 md:grid-cols-2">
        {learning
          .filter((x) => x.enrollment.status !== 'dropped' && x.enrollment.status !== 'transferred')
          .map(({ cls, enrollment, stats }) => {
            const cert = enrollment.certificate;
            const checks = certificateChecks(cls, stats);
            return (
              <article key={enrollment.id} className={cn('rounded-xl border p-4', cert ? 'border-success-100 bg-success-50/40' : 'border-line bg-surface')}>
                <div className="flex items-start justify-between gap-2">
                  <div>
                    <p className="font-semibold text-ink">{cls.courseLabel}</p>
                    <p className="text-xs text-ink-muted">
                      {shortCode(cls)} · {enrollment.status === 'completed' ? `hoàn thành ${fmtDate(enrollment.endedAt)}` : 'đang học'}
                    </p>
                  </div>
                  {cert ? (
                    <StatusTag tone="success" icon={BadgeCheck} size="sm">
                      Đã cấp
                    </StatusTag>
                  ) : (
                    <StatusTag tone={checks.every((c) => c.ok) ? 'info' : 'neutral'} icon={Award} size="sm">
                      {checks.every((c) => c.ok) ? 'Đủ điều kiện — chờ cấp' : 'Chưa đủ điều kiện'}
                    </StatusTag>
                  )}
                </div>
                {cert ? (
                  <div className="mt-3 text-[13px]">
                    <p>
                      Mã tra cứu: <code className="rounded bg-surface px-1.5 py-0.5">{cert.code}</code>
                    </p>
                    <p className="text-xs text-ink-muted">
                      Cấp {fmtDate(cert.issuedAt)} · đã gửi tới {cert.sentTo}
                    </p>
                    <Button icon={Download} size="sm" variant="soft" className="mt-2" onClick={() => notify.toast.info(`Tải ${cert.code}.pdf (giả lập)`)}>
                      Tải chứng nhận (PDF)
                    </Button>
                  </div>
                ) : (
                  <ul className="mt-3 flex flex-col gap-1 text-[13px]">
                    {checks.map((c) => (
                      <li key={c.label} className={cn('flex items-start gap-1.5', c.ok ? 'text-success-700' : 'text-ink-secondary')}>
                        {c.ok ? <Check size={14} className="mt-0.5 shrink-0" aria-hidden /> : <X size={14} className="mt-0.5 shrink-0 text-danger-600" aria-hidden />}
                        {c.label}
                      </li>
                    ))}
                  </ul>
                )}
              </article>
            );
          })}
      </div>

    </>
  );
}
