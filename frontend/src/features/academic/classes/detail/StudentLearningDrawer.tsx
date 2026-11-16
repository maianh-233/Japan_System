import type { ReactNode } from 'react';
import { Drawer } from 'antd';
import dayjs from 'dayjs';
import { Award, BookOpenCheck, CalendarCheck, CircleAlert, NotebookPen, PhoneCall, TriangleAlert, UserX } from 'lucide-react';
import { EmptyState, StatusTag, UserCell } from '@/components/common';
import { useCrmStore } from '@/features/crm/crmStore';
import { ENROLLMENT_STATUS } from '@/features/student/constants';
import { useBreakpoint } from '@/hooks/useBreakpoint';
import { cn } from '@/lib/cn';
import { fmtDate } from '@/lib/date';
import { formatCurrency } from '@/lib/format';
import { useCurriculumStore } from '../../curriculum/curriculumStore';
import { ALERT_RULES, useClassStudentStats, type StudentStats } from '../studentInsights';
import type { AttendanceStatus, ClassInfo } from '../types';

export const ATT_CELL: Record<AttendanceStatus, { short: string; label: string; cls: string }> = {
  present: { short: '✓', label: 'Có mặt', cls: 'bg-success-50 text-success-700' },
  late: { short: 'M', label: 'Đi muộn', cls: 'bg-warning-50 text-warning-700' },
  excused: { short: 'P', label: 'Vắng có phép', cls: 'bg-primary-50 text-primary-700' },
  absent: { short: '✗', label: 'Vắng không phép', cls: 'bg-danger-50 text-danger-700 font-semibold' },
};

const CARE_CHANNEL = { call: 'Gọi điện', zalo: 'Nhắn Zalo', meet: 'Gặp trực tiếp' } as const;

function Stat({ label, value, sub, bad }: { label: string; value: string; sub?: string; bad?: boolean }) {
  return (
    <div className="rounded-lg bg-page px-3 py-2.5">
      <p className="text-xs text-ink-muted">{label}</p>
      <p className={cn('tabular text-lg font-semibold', bad ? 'text-danger-600' : 'text-ink')}>{value}</p>
      {sub && <p className="text-xs text-ink-muted">{sub}</p>}
    </div>
  );
}

function Section({ icon: Icon, title, children, extra }: { icon: typeof Award; title: string; children: ReactNode; extra?: ReactNode }) {
  return (
    <section className="flex flex-col gap-2">
      <h3 className="flex items-center gap-1.5 text-sm font-semibold text-ink">
        <Icon size={16} aria-hidden /> {title}
        {extra && <span className="ml-auto text-xs font-normal text-ink-muted">{extra}</span>}
      </h3>
      {children}
    </section>
  );
}

/** Hồ sơ học tập của 1 học viên trong lớp: chuyên cần · bài tập · bài thi · nhận xét · lịch sử chăm sóc */
export function StudentLearningDrawer({ cls, studentId, onClose }: { cls: ClassInfo; studentId: string | null; onClose: () => void }) {
  const { isMobile } = useBreakpoint();
  const { map } = useClassStudentStats(cls);
  const student = useCrmStore((s) => s.students.find((x) => x.id === studentId));
  const st = studentId ? map.get(studentId) : undefined;

  return (
    <Drawer
      open={Boolean(studentId)}
      onClose={onClose}
      width={isMobile ? '100%' : 720}
      destroyOnHidden
      title={
        student && st ? (
          <div className="flex items-center gap-3 pr-2">
            <UserCell name={student.fullName} subtitle={`${student.code}${student.phone ? ` · ${student.phone}` : ''}`} className="min-w-0" />
            <StatusTag tone={ENROLLMENT_STATUS[st.enrollment.status].tone} icon={ENROLLMENT_STATUS[st.enrollment.status].icon} size="sm">
              {ENROLLMENT_STATUS[st.enrollment.status].label}
            </StatusTag>
          </div>
        ) : (
          'Hồ sơ học tập'
        )
      }
    >
      {!st ? <EmptyState size="compact" title="Không có dữ liệu học tập" /> : <Body cls={cls} st={st} />}
    </Drawer>
  );
}

function Body({ cls, st }: { cls: ClassInfo; st: StudentStats }) {
  const curriculum = useCurriculumStore((s) => s.curricula.find((c) => c.id === cls.curriculumId));
  const { attendance: a, assignments: as, exams: ex } = st;
  const lessonTitle = (id?: string) => {
    const i = curriculum?.lessons.findIndex((l) => l.id === id) ?? -1;
    return i >= 0 ? `Bài ${i + 1}` : '';
  };
  // Nhận xét của GV: từ bài tập đã chấm + bài thi
  const comments = [
    ...as.list.filter((x) => x.submission?.comment).map((x) => ({ at: x.submission!.gradedAt ?? x.submission!.submittedAt, src: x.assignment.title, text: x.submission!.comment!, by: x.submission!.gradedBy })),
    ...ex.list.filter((x) => x.result.comment).map((x) => ({ at: x.result.startedAt ?? '', src: x.title, text: x.result.comment!, by: undefined })),
  ].sort((p, q) => q.at.localeCompare(p.at));

  return (
    <div className="flex flex-col gap-6">
      {st.enrollment.note && <p className="rounded-lg bg-page px-3 py-2 text-[13px] text-ink-secondary">{st.enrollment.note}</p>}
      {st.flags.length > 0 && (
        <div className="flex flex-wrap items-center gap-1.5 rounded-lg border border-warning-100 bg-warning-50 px-3 py-2">
          <TriangleAlert size={15} className="text-warning-700" aria-hidden />
          {st.flags.map((f) => (
            <StatusTag key={f.label} tone={f.tone} icon={f.tone === 'danger' ? TriangleAlert : CircleAlert} size="sm">
              {f.label}
            </StatusTag>
          ))}
        </div>
      )}

      <div className="grid grid-cols-2 gap-2 sm:grid-cols-4">
        <Stat label="Chuyên cần" value={a.rate !== undefined ? `${a.rate}%` : '—'} sub={`${a.present + a.late}/${a.sessions} buổi`} bad={a.rate !== undefined && a.rate < ALERT_RULES.attendanceRate} />
        <Stat label="Nộp bài tập" value={`${as.submitted}/${as.due}`} sub={as.late ? `${as.late} bài nộp muộn` : 'bài đã tới hạn'} bad={as.due >= 2 && as.submitted / as.due < ALERT_RULES.submitRate / 100} />
        <Stat label="Điểm bài tập TB" value={as.avgPct !== undefined ? `${as.avgPct}%` : '—'} bad={as.avgPct !== undefined && as.avgPct < ALERT_RULES.scorePct} />
        <Stat label="Điểm thi TB" value={ex.avgPct !== undefined ? `${ex.avgPct}%` : '—'} sub={`${ex.list.length} bài thi`} bad={ex.avgPct !== undefined && ex.avgPct < ALERT_RULES.scorePct} />
      </div>

      <Section icon={CalendarCheck} title="Chuyên cần từng buổi" extra={`Vắng KP ${a.absent} · Có phép ${a.excused} · Muộn ${a.late}`}>
        {a.timeline.length ? (
          <div className="flex flex-wrap gap-1">
            {a.timeline.map((t) => {
              const m = t.status ? ATT_CELL[t.status] : undefined;
              return (
                <span
                  key={t.sessionId}
                  title={`Buổi ${t.no || '(thêm)'} · ${fmtDate(t.date)} · ${m?.label ?? 'Không thuộc buổi này'}${t.note ? ` — ${t.note}` : ''}`}
                  className={cn('tabular flex h-8 w-8 flex-col items-center justify-center rounded text-[11px] leading-none', m?.cls ?? 'bg-page text-ink-muted')}
                >
                  <span>{m?.short ?? '·'}</span>
                  <span className="mt-0.5 text-[9px] opacity-70">{t.no || '+'}</span>
                </span>
              );
            })}
          </div>
        ) : (
          <p className="text-[13px] text-ink-muted">Chưa có buổi nào được điểm danh.</p>
        )}
      </Section>

      <Section icon={NotebookPen} title="Bài tập" extra={`${as.list.length} bài`}>
        {as.list.length ? (
          <ul className="divide-y divide-line rounded-[var(--radius-card)] border border-line text-[13px]">
            {as.list.map(({ assignment: x, submission: sub, overdue }) => {
              const late = sub && dayjs(sub.submittedAt).isAfter(dayjs(x.dueDate).endOf('day'));
              return (
                <li key={x.id} className="flex flex-wrap items-center gap-x-3 gap-y-1 px-3 py-2">
                  <span className="min-w-0 flex-1">
                    <span className="text-ink">{x.title}</span>
                    <span className="ml-1.5 text-xs text-ink-muted">
                      {lessonTitle(x.lessonId)} · hạn {fmtDate(x.dueDate)}
                    </span>
                  </span>
                  {sub ? (
                    <>
                      {late && <span className="text-xs text-warning-700">Nộp muộn</span>}
                      {sub.score !== undefined ? (
                        <span className={cn('tabular font-medium', sub.score / x.maxScore < ALERT_RULES.scorePct / 100 ? 'text-danger-600' : 'text-ink')}>
                          {sub.score}/{x.maxScore}
                        </span>
                      ) : (
                        <span className="text-xs text-ink-muted">Chờ chấm</span>
                      )}
                    </>
                  ) : overdue ? (
                    <span className="text-xs font-medium text-danger-600">Không nộp</span>
                  ) : (
                    <span className="text-xs text-ink-muted">Chưa tới hạn</span>
                  )}
                </li>
              );
            })}
          </ul>
        ) : (
          <p className="text-[13px] text-ink-muted">Lớp chưa giao bài tập.</p>
        )}
      </Section>

      <Section icon={BookOpenCheck} title="Bài thi">
        {ex.list.length ? (
          <ul className="divide-y divide-line rounded-[var(--radius-card)] border border-line text-[13px]">
            {ex.list.map((x) => (
              <li key={x.examId} className="flex flex-col gap-1 px-3 py-2">
                <div className="flex items-center gap-2">
                  <span className="flex-1 font-medium text-ink">{x.title}</span>
                  {x.result.status === 'absent' ? (
                    <StatusTag tone="danger" icon={UserX} size="sm">
                      Vắng thi
                    </StatusTag>
                  ) : (
                    <span className={cn('tabular font-semibold', x.pct < ALERT_RULES.scorePct ? 'text-danger-600' : 'text-ink')}>
                      {x.total}/{x.max} ({x.pct}%)
                    </span>
                  )}
                </div>
                {x.result.status === 'done' && (
                  <span className="text-xs text-ink-muted">{x.result.sections.map((s) => `${s.name} ${s.score}/${s.max}`).join(' · ')}</span>
                )}
              </li>
            ))}
          </ul>
        ) : (
          <p className="text-[13px] text-ink-muted">Chưa có kết quả thi.</p>
        )}
      </Section>

      <Section icon={Award} title="Nhận xét của giáo viên">
        {comments.length ? (
          <ul className="flex flex-col gap-2">
            {comments.slice(0, 8).map((c, i) => (
              <li key={i} className="rounded-lg bg-page px-3 py-2 text-[13px]">
                <p className="text-ink">“{c.text}”</p>
                <p className="mt-0.5 text-xs text-ink-muted">
                  {c.src}
                  {c.by ? ` · ${c.by}` : ''}
                  {c.at ? ` · ${fmtDate(c.at)}` : ''}
                </p>
              </li>
            ))}
          </ul>
        ) : (
          <p className="text-[13px] text-ink-muted">Chưa có nhận xét.</p>
        )}
      </Section>

      <Section icon={PhoneCall} title="Lịch sử chăm sóc">
        {st.careLogs.length ? (
          <ul className="flex flex-col gap-2">
            {st.careLogs.map((c) => (
              <li key={c.id} className="rounded-lg border border-line px-3 py-2 text-[13px]">
                <p className="text-ink">{c.note}</p>
                <p className="mt-0.5 text-xs text-ink-muted">
                  {CARE_CHANNEL[c.channel]} · {c.by} · {dayjs(c.at).format('HH:mm DD/MM/YYYY')}
                </p>
              </li>
            ))}
          </ul>
        ) : (
          <p className="text-[13px] text-ink-muted">Chưa có lần liên hệ nào.</p>
        )}
      </Section>

      {st.tuition && (
        <p className="text-xs text-ink-muted">
          Học phí: đã đóng {formatCurrency(st.tuition.paid) || '0 ₫'}
          {st.tuition.remaining ? ` · còn nợ ${formatCurrency(st.tuition.remaining)}` : ' · đã hoàn tất'}
          {st.enrollment.certificate && ` · Chứng nhận ${st.enrollment.certificate.code} (gửi ${fmtDate(st.enrollment.certificate.issuedAt)})`}
        </p>
      )}
    </div>
  );
}

export { CARE_CHANNEL };
