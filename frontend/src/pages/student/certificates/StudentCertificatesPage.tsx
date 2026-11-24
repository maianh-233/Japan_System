import { useEffect, useState } from 'react';
import { Spin } from 'antd';
import { Award, BadgeCheck, Check, Download, ExternalLink, X } from 'lucide-react';
import { Button, EmptyState, PageHeader, StatusTag, useNotify } from '@/components/common';
import { useClassStore } from '@/features/academic/classes/classStore';
import { certificateChecks } from '@/features/academic/classes/detail/StudentActions';
import type { ClassInfo } from '@/features/academic/classes/types';
import { shortCode, useStudentId, useStudentLearning } from '@/features/portal/usePortal';
import { loadMyCertificates, type MyCertificate } from '@/features/student/certificateApi';
import type { EnrollmentStatus } from '@/features/student/types';
import { USE_API } from '@/lib/api/config';
import { cn } from '@/lib/cn';
import { fmtDate } from '@/lib/date';

interface Item {
  id: string;
  cls: ClassInfo;
  status: EnrollmentStatus;
  endedAt?: string;
  checks: { ok: boolean; label: string }[];
  cert?: { code: string; issuedAt: string; sentTo: string };
}

/**
 * Cổng HV › Chứng nhận khóa học (HV-15.E5): chứng nhận HOÀN THÀNH KHÓA do TRUNG TÂM cấp cho học viên pass lớp
 * (khác chứng chỉ JLPT — xem mục Chứng chỉ JLPT). Chế độ API: checklist + chứng nhận do server trả.
 */
export default function StudentCertificatesPage() {
  const notify = useNotify();
  const studentId = useStudentId();
  const learning = useStudentLearning(studentId);
  const classes = useClassStore((s) => s.classes);
  const [server, setServer] = useState<MyCertificate[] | null>(null);

  useEffect(() => {
    if (!USE_API) return;
    let alive = true;
    setServer(null);
    loadMyCertificates(studentId)
      .then((list) => alive && setServer(list))
      .catch((e: Error) => {
        if (!alive) return;
        setServer([]);
        notify.error({ title: 'Không tải được chứng nhận', description: e.message });
      });
    return () => {
      alive = false;
    };
  }, [studentId, notify]);

  const items: Item[] = USE_API
    ? (server ?? []).flatMap((c) => {
        const cls = classes.find((x) => x.id === c.classId);
        return cls ? [{ id: c.enrollmentId, cls, status: c.status, endedAt: c.endedAt, checks: c.checks, cert: c.certificate }] : [];
      })
    : learning
        .filter((x) => x.enrollment.status !== 'dropped' && x.enrollment.status !== 'transferred')
        .map(({ cls, enrollment, stats }) => ({
          id: enrollment.id,
          cls,
          status: enrollment.status,
          endedAt: enrollment.endedAt,
          checks: certificateChecks(cls, stats),
          cert: enrollment.certificate,
        }));

  return (
    <>
      <PageHeader title="Chứng nhận khóa học" subtitle="Chứng nhận hoàn thành khóa do trung tâm cấp khi bạn hoàn thành lớp và đủ điều kiện. Chứng chỉ JLPT (do hội đồng thi cấp) xem ở mục Chứng chỉ JLPT." />
      {USE_API && server === null ? (
        <div className="grid place-items-center py-16">
          <Spin />
        </div>
      ) : !items.length ? (
        <EmptyState icon={Award} title="Bạn chưa có lớp nào để cấp chứng nhận" />
      ) : (
        <div className="mb-6 grid gap-3 md:grid-cols-2">
          {items.map(({ id, cls, status, endedAt, checks, cert }) => {
            const ready = checks.every((c) => c.ok);
            return (
              <article key={id} className={cn('rounded-xl border p-4', cert ? 'border-success-100 bg-success-50/40' : 'border-line bg-surface')}>
                <div className="flex items-start justify-between gap-2">
                  <div>
                    <p className="font-semibold text-ink">{cls.courseLabel}</p>
                    <p className="text-xs text-ink-muted">
                      {shortCode(cls)} · {status === 'completed' ? `hoàn thành ${fmtDate(endedAt)}` : status === 'reserved' ? 'đang bảo lưu' : 'đang học'}
                    </p>
                  </div>
                  {cert ? (
                    <StatusTag tone="success" icon={BadgeCheck} size="sm">
                      Đã cấp
                    </StatusTag>
                  ) : (
                    <StatusTag tone={ready ? 'info' : 'neutral'} icon={Award} size="sm">
                      {ready ? 'Đủ điều kiện — chờ cấp' : 'Chưa đủ điều kiện'}
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
                    {USE_API ? (
                      <Button icon={ExternalLink} size="sm" variant="soft" className="mt-2" onClick={() => window.open(`/verify/${encodeURIComponent(cert.code)}`, '_blank', 'noopener')}>
                        Xem trang xác thực
                      </Button>
                    ) : (
                      <Button icon={Download} size="sm" variant="soft" className="mt-2" onClick={() => notify.toast.info(`Tải ${cert.code}.pdf (giả lập)`)}>
                        Tải chứng nhận (PDF)
                      </Button>
                    )}
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
      )}
    </>
  );
}
