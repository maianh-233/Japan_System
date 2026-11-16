import { useState } from 'react';
import dayjs from 'dayjs';
import { BookOpenCheck, CalendarCheck, CalendarDays, ChevronLeft, ChevronRight, MessageSquareText, NotebookPen, Printer, Trophy, Wallet } from 'lucide-react';
import { useSearchParams } from 'react-router-dom';
import { Button, PageHeader, StatusTag } from '@/components/common';
import { ChildSwitcher } from '@/features/portal/components/ChildSwitcher';
import { ATT_META } from '@/features/portal/components/LearningView';
import { shortCode, useChildren, useStudentId } from '@/features/portal/usePortal';
import { reportSummary, useWeeklyReport, weekLabel, weekStartOf } from '@/features/portal/weeklyReport';
import { cn } from '@/lib/cn';
import { formatCurrency } from '@/lib/format';

function Card({ icon: Icon, title, children, className }: { icon: typeof CalendarCheck; title: string; children: React.ReactNode; className?: string }) {
  return (
    <section className={cn('rounded-[var(--radius-card)] border border-line bg-surface p-4 break-inside-avoid', className)}>
      <h2 className="mb-3 flex items-center gap-2 text-[15px] font-semibold text-ink">
        <Icon size={17} className="text-primary-600" aria-hidden /> {title}
      </h2>
      {children}
    </section>
  );
}

/** Cổng PH › Báo cáo tuần: chuyên cần từng buổi, nhận xét GV, bài tập, điểm thi, tuần tới, học phí */
export default function ParentWeeklyReportPage() {
  const [params] = useSearchParams();
  const children = useChildren();
  const studentId = useStudentId();
  const child = children.find((c) => c.id === studentId);
  const thisWeek = weekStartOf(dayjs());
  const fromLink = params.get('week');
  const [week, setWeek] = useState(() => (fromLink ? weekStartOf(dayjs(fromLink)) : dayjs().day() === 1 ? thisWeek.subtract(7, 'day') : thisWeek));
  const r = useWeeklyReport(studentId, week);
  const a = r.attendance;
  const done = r.assignments.filter((x) => x.sub).length;

  return (
    <>
      <PageHeader
        title="Báo cáo tuần"
        subtitle={`${child?.fullName ?? ''} · tuần ${weekLabel(week)}/${week.format('YYYY')}`}
        actions={
          <Button icon={Printer} variant="ghost" tone="neutral" collapseOnMobile onClick={() => window.print()}>
            In / lưu PDF
          </Button>
        }
      />
      <div className="mb-4 flex flex-wrap items-center gap-2 print:hidden">
        <ChildSwitcher />
        <div className="ml-auto flex items-center gap-1">
          <Button icon={ChevronLeft} iconOnly size="sm" variant="outline" aria-label="Tuần trước" onClick={() => setWeek(week.subtract(7, 'day'))} />
          <span className="tabular min-w-28 text-center text-[13px] font-medium text-ink">{weekLabel(week)}</span>
          <Button icon={ChevronRight} iconOnly size="sm" variant="outline" aria-label="Tuần sau" disabled={!week.isBefore(thisWeek)} onClick={() => setWeek(week.add(7, 'day'))} />
        </div>
      </div>

      {!r.classes.length ? (
        <p className="rounded-xl border border-line p-8 text-center text-ink-muted">Con chưa học lớp nào trong tuần này.</p>
      ) : (
        <div className="flex flex-col gap-4">
          {/* Tóm tắt */}
          <section className="rounded-[var(--radius-card)] border border-primary-100 bg-primary-50/60 p-4">
            <p className="text-xs font-medium text-primary-700">Tóm tắt tuần {week.isSame(thisWeek) ? '(đang diễn ra)' : ''}</p>
            <p className="mt-1 text-[15px] text-ink">{reportSummary(r)}</p>
            <div className="mt-3 grid grid-cols-2 gap-2 sm:grid-cols-4">
              {[
                ['Đi học', a.held ? `${a.present + a.late}/${a.held} buổi` : '—', a.absent ? 'text-danger-600' : 'text-ink'],
                ['Đi muộn', a.late ? `${a.late} buổi` : 'Không', a.late ? 'text-warning-700' : 'text-ink'],
                ['Bài tập', r.assignments.length ? `${done}/${r.assignments.length} đã nộp` : 'Không có', done < r.assignments.length ? 'text-warning-700' : 'text-ink'],
                ['Học phí còn', r.tuition.remaining ? formatCurrency(r.tuition.remaining) : 'Đã đủ', r.tuition.overdue ? 'text-danger-600' : 'text-ink'],
              ].map(([k, v, c]) => (
                <div key={k} className="rounded-lg bg-surface px-3 py-2">
                  <p className="text-xs text-ink-muted">{k}</p>
                  <p className={cn('tabular font-semibold', c)}>{v}</p>
                </div>
              ))}
            </div>
          </section>

          <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
            {/* Buổi học + nhận xét GV */}
            <Card icon={CalendarCheck} title={`Các buổi học (${r.sessions.length})`} className="lg:row-span-2">
              {r.sessions.length === 0 ? (
                <p className="text-[13px] text-ink-muted">Tuần này không có buổi học.</p>
              ) : (
                <ol className="flex flex-col divide-y divide-line">
                  {r.sessions.map(({ session: s, cls, status, checkInAt, log }) => {
                    const meta = status === 'upcoming' ? { label: 'Sắp học', dot: 'bg-primary-200', text: 'text-ink-muted' } : status === 'cancelled' ? { label: 'Buổi bị hủy', dot: 'bg-line', text: 'text-ink-muted' } : ATT_META[status];
                    return (
                      <li key={s.id} className="py-2.5 text-[13px]">
                        <div className="flex flex-wrap items-center gap-x-2 gap-y-0.5">
                          <span className={cn('size-2.5 rounded-full', meta.dot)} aria-hidden />
                          <span className="font-medium text-ink">{dayjs(s.date).format('dd DD/MM')}</span>
                          <span className="tabular text-ink-secondary">
                            {s.startTime}–{s.endTime} · {shortCode(cls)}
                            {s.kind === 'exam' ? ' · Thi' : ''}
                          </span>
                          <span className={cn('ml-auto font-medium', meta.text)}>
                            {meta.label}
                            {checkInAt && (status === 'present' || status === 'late') ? ` · vào ${checkInAt}` : ''}
                          </span>
                        </div>
                        {log && (
                          <div className="mt-1.5 ml-4.5 rounded-lg bg-page px-3 py-2">
                            <p className="flex gap-1.5 text-ink">
                              <MessageSquareText size={13} className="mt-0.5 shrink-0 text-primary-600" aria-hidden /> {log.content}
                            </p>
                            {log.homework && <p className="mt-1 text-ink-secondary">Bài về nhà: {log.homework}</p>}
                            <p className="mt-1 text-xs text-ink-muted">GV {log.author}</p>
                          </div>
                        )}
                      </li>
                    );
                  })}
                </ol>
              )}
            </Card>

            {/* Bài tập */}
            <Card icon={NotebookPen} title={`Bài tập tới hạn trong tuần (${r.assignments.length})`}>
              {r.assignments.length === 0 ? (
                <p className="text-[13px] text-ink-muted">Không có bài tập tới hạn.</p>
              ) : (
                <ul className="flex flex-col gap-2 text-[13px]">
                  {r.assignments.map(({ a: as, sub, score, overdue }) => (
                    <li key={as.id} className="flex flex-col gap-0.5">
                      <div className="flex items-center gap-2">
                        <span className="min-w-0 flex-1 truncate font-medium text-ink">{as.title}</span>
                        {sub ? (
                          score !== undefined ? (
                            <StatusTag tone={score / as.maxScore >= 0.6 ? 'success' : 'danger'} icon={BookOpenCheck} size="sm">
                              {score}/{as.maxScore}
                            </StatusTag>
                          ) : (
                            <StatusTag tone="info" icon={BookOpenCheck} size="sm">Đã nộp, chờ chấm</StatusTag>
                          )
                        ) : (
                          <StatusTag tone={overdue ? 'danger' : 'warning'} icon={NotebookPen} size="sm">{overdue ? 'Chưa nộp (quá hạn)' : 'Chưa nộp'}</StatusTag>
                        )}
                      </div>
                      <span className="text-xs text-ink-muted">
                        Hạn {dayjs(as.dueDate).format('dd DD/MM')}
                        {sub?.comment ? ` · GV nhận xét: ${sub.comment}` : ''}
                      </span>
                    </li>
                  ))}
                </ul>
              )}
            </Card>

            {r.exams.length > 0 && (
              <Card icon={Trophy} title="Bài thi">
                <ul className="flex flex-col gap-2 text-[13px]">
                  {r.exams.map((e, i) => (
                    <li key={i}>
                      <div className="flex items-center justify-between gap-2">
                        <span className="font-medium text-ink">{e.title}</span>
                        <span className={cn('tabular font-semibold', e.absent || e.pct < 60 ? 'text-danger-600' : 'text-ink')}>{e.absent ? 'Vắng thi' : `${e.total}/${e.max} (${e.pct}%)`}</span>
                      </div>
                      {e.comment && <p className="text-xs text-ink-muted">{e.comment}</p>}
                    </li>
                  ))}
                </ul>
              </Card>
            )}

            {/* Tuần tới */}
            <Card icon={CalendarDays} title="Tuần tới">
              <ul className="flex flex-col gap-1.5 text-[13px] text-ink-secondary">
                <li>
                  {r.next.sessions ? `${r.next.sessions} buổi học` : 'Không có buổi học'}
                  {r.next.first && ` · buổi đầu ${dayjs(r.next.first.session.date).format('dd DD/MM')} ${r.next.first.session.startTime}`}
                </li>
                {r.next.exams.map((e, i) => (
                  <li key={i} className="font-medium text-danger-700">
                    {e.title} — {dayjs(e.date).format('dd DD/MM')}
                  </li>
                ))}
                {r.next.due.map((d, i) => (
                  <li key={i}>
                    Nộp “{d.title}” trước {dayjs(d.dueDate).format('dd DD/MM')}
                  </li>
                ))}
              </ul>
            </Card>

            {r.tuition.remaining > 0 && (
              <Card icon={Wallet} title="Học phí">
                <p className={cn('text-[13px]', r.tuition.overdue ? 'text-danger-700' : 'text-ink-secondary')}>
                  Còn {formatCurrency(r.tuition.remaining)}
                  {r.tuition.dueDate && ` · hạn ${dayjs(r.tuition.dueDate).format('DD/MM/YYYY')}`}
                  {r.tuition.overdue && ' · đã quá hạn'}
                </p>
              </Card>
            )}
          </div>
        </div>
      )}
    </>
  );
}
