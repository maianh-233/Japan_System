import { isTaught } from '@/features/academic/classes/sessionUtils';
import { useMemo, useState } from 'react';
import dayjs from 'dayjs';
import { AlertTriangle, BookOpen, CheckCircle2, ChevronRight, Circle, CircleDot, NotebookPen, TrendingDown, TrendingUp } from 'lucide-react';
import { EmptyState, StatusTag } from '@/components/common';
import { cn } from '@/lib/cn';
import { fmtDate } from '@/lib/date';
import { EXAM_KINDS } from '../../curriculum/constants';
import { useCurriculumStore } from '../../curriculum/curriculumStore';
import { useClassStore } from '../classStore';
import { hoursPerSession, SESSION_STATUS, totalSessionsOf } from '../constants';
import type { ClassInfo, ClassSession } from '../types';

/** "Hôm nay" / "Ngày mai" / "còn N ngày" */
const daysUntil = (date: string) => {
  const n = dayjs(date).startOf('day').diff(dayjs().startOf('day'), 'day');
  return n === 0 ? 'hôm nay' : n === 1 ? 'ngày mai' : `còn ${n} ngày`;
};

/** Tab Tiến độ: buổi đã dạy, đúng / chậm tiến độ, chuyên cần, tiến độ theo từng bài trong lộ trình */
export function ClassProgressTab({ cls }: { cls: ClassInfo }) {
  const [openLesson, setOpenLesson] = useState<string | null>(null);
  const all = useClassStore((s) => s.sessions);
  const curriculum = useCurriculumStore((s) => s.curricula.find((c) => c.id === cls.curriculumId));
  const sessions = useMemo(() => all.filter((s) => s.classId === cls.id).sort((a, b) => a.date.localeCompare(b.date)), [all, cls.id]);

  const today = dayjs().format('YYYY-MM-DD');
  const total = totalSessionsOf(cls);
  const done = sessions.filter(isTaught);
  const cancelled = sessions.filter((s) => s.status === 'cancelled');
  const makeups = sessions.filter((s) => s.status === 'makeup');
  const unconfirmed = sessions.filter((s) => s.status === 'scheduled' && s.date < today);
  // Kế hoạch: số buổi lẽ ra đã học tới hôm nay (theo lịch, không tính buổi bị hủy chưa bù)
  const planned = sessions.filter((s) => s.date < today && s.status !== 'makeup').length;
  const behind = Math.max(0, planned - done.length);
  const attended = done.filter((s) => s.attendance);
  const attendanceRate = attended.length
    ? Math.round((attended.reduce((n, s) => n + s.attendance!.present, 0) / attended.reduce((n, s) => n + s.attendance!.total, 0)) * 100)
    : undefined;
  const pct = total ? Math.round((done.length / total) * 100) : 0;
  const nextExam = sessions.find((s) => s.kind === 'exam' && s.status === 'scheduled');
  const nextExamMeta = nextExam && curriculum?.exams.find((e) => e.id === nextExam.examId);

  if (!sessions.length)
    return <EmptyState size="compact" title="Chưa có buổi học" description="Tạo lịch ở tab Buổi học để theo dõi tiến độ." />;

  const lessonRows = (curriculum?.lessons ?? []).map((l, i) => {
    const ss = sessions.filter((s) => s.lessonId === l.id && s.status !== 'cancelled');
    const d = ss.filter((s) => s.status === 'completed' || (s.status === 'makeup' && s.attendance)).length;
    const state = ss.length && d === ss.length ? 'done' : d > 0 ? 'doing' : 'todo';
    return { lesson: l, no: i + 1, sessions: ss, done: d, state, from: ss[0]?.date, to: ss[ss.length - 1]?.date };
  });
  const lessonsDone = lessonRows.filter((r) => r.state === 'done').length;
  const recentLogs = [...done].filter((x) => x.teacherLog).reverse().slice(0, 3);
  const missingLogs = done.filter((x) => !x.teacherLog);
  const lessonLabel = (x: ClassSession) => {
    if (x.kind === 'exam') return curriculum?.exams.find((e) => e.id === x.examId)?.title;
    const i = curriculum?.lessons.findIndex((l) => l.id === x.lessonId) ?? -1;
    return i >= 0 ? `Bài ${i + 1}: ${curriculum!.lessons[i].title}` : undefined;
  };

  const card = (label: string, value: string, sub?: string, tone?: 'good' | 'bad') => (
    <div className="rounded-lg bg-page px-3 py-2.5">
      <p className="text-xs text-ink-muted">{label}</p>
      <p className={cn('tabular text-lg font-semibold', tone === 'bad' ? 'text-danger-600' : tone === 'good' ? 'text-success-700' : 'text-ink')}>{value}</p>
      {sub && <p className="text-xs text-ink-muted">{sub}</p>}
    </div>
  );

  return (
    <div className="flex flex-col gap-5">
      <div>
        <div className="mb-1.5 flex items-center justify-between text-[13px]">
          <span className="font-medium text-ink">Hoàn thành {pct}%</span>
          <span className="tabular text-ink-muted">
            {done.length}/{total} buổi · {Math.round(done.length * hoursPerSession(cls))}/{cls.totalHours} giờ
          </span>
        </div>
        <div className="h-2.5 overflow-hidden rounded-full bg-disabled" role="progressbar" aria-valuenow={pct} aria-valuemin={0} aria-valuemax={100} aria-label="Tiến độ lớp">
          <div className="h-full rounded-full bg-success-600" style={{ width: `${pct}%` }} />
        </div>
      </div>

      <div className="grid grid-cols-2 gap-2 lg:grid-cols-4">
        {behind > 0
          ? card('So với kế hoạch', `Chậm ${behind} buổi`, `${cancelled.length} buổi hủy · ${makeups.length} buổi đã bù`, 'bad')
          : card('So với kế hoạch', 'Đúng tiến độ', `${cancelled.length} buổi hủy · ${makeups.length} buổi đã bù`, 'good')}
        {card('Chuyên cần TB', attendanceRate !== undefined ? `${attendanceRate}%` : '—', `${attended.length} buổi có điểm danh`, attendanceRate !== undefined && attendanceRate < 80 ? 'bad' : undefined)}
        {card('Bài học xong', curriculum ? `${lessonsDone}/${curriculum.lessons.length}` : '—', curriculum ? `${curriculum.name} ${curriculum.version}` : 'Chưa chọn lộ trình')}
        {card(
          'Mốc thi tiếp theo',
          nextExam ? fmtDate(nextExam.date) : '—',
          nextExamMeta ? `${nextExamMeta.title} · ${daysUntil(nextExam!.date)}` : 'Không có',
        )}
      </div>

      {unconfirmed.length > 0 && (
        <p className="flex items-center gap-2 rounded-lg bg-warning-50 px-3 py-2 text-[13px] text-warning-700">
          <TrendingDown size={16} aria-hidden /> {unconfirmed.length} buổi đã qua chưa được xác nhận dạy (buổi {unconfirmed.map((s) => s.no).join(', ')}) — giáo viên cần điểm danh.
        </p>
      )}
      {behind === 0 && !unconfirmed.length && (
        <p className="flex items-center gap-2 rounded-lg bg-success-50 px-3 py-2 text-[13px] text-success-700">
          <TrendingUp size={16} aria-hidden /> Lớp đang theo đúng lịch.
        </p>
      )}

      <section>
        <div className="mb-2 flex flex-wrap items-baseline justify-between gap-2">
          <h3 className="text-sm font-semibold text-ink">Nhật ký giáo viên gần đây</h3>
          <span className="text-xs text-ink-muted">Bấm vào từng bài bên dưới để xem nhật ký mọi buổi của bài</span>
        </div>
        {recentLogs.length ? (
          <ul className="flex flex-col gap-2">
            {recentLogs.map((x) => (
              <SessionLogItem key={x.id} session={x} lessonLabel={lessonLabel(x)} />
            ))}
          </ul>
        ) : (
          <p className="rounded-lg bg-page px-3 py-2 text-[13px] text-ink-muted">Chưa có nhật ký nào.</p>
        )}
        {missingLogs.length > 0 && (
          <p className="mt-2 flex items-center gap-1.5 text-xs text-warning-700">
            <AlertTriangle size={13} aria-hidden /> {missingLogs.length} buổi đã dạy chưa có nhật ký (buổi {missingLogs.map((x) => x.no).join(', ')}) — nhắc giáo viên ghi trên Cổng giáo viên.
          </p>
        )}
      </section>

      {curriculum && (
        <section>
          <h3 className="mb-2 text-sm font-semibold text-ink">Tiến độ theo lộ trình</h3>
          <ol className="divide-y divide-line rounded-[var(--radius-card)] border border-line">
            {lessonRows.map((r) => {
              const Icon = r.state === 'done' ? CheckCircle2 : r.state === 'doing' ? CircleDot : Circle;
              const examsAfter = curriculum.exams.filter((e) => e.afterLessonId === r.lesson.id);
              const isOpen = openLesson === r.lesson.id;
              const logged = r.sessions.filter((x) => x.teacherLog).length;
              return (
                <li key={r.lesson.id}>
                  <button
                    type="button"
                    onClick={() => setOpenLesson(isOpen ? null : r.lesson.id)}
                    aria-expanded={isOpen}
                    className="flex w-full flex-wrap items-center gap-x-3 gap-y-1 px-3 py-2 text-left text-[13px] hover:bg-page"
                  >
                    <Icon size={16} className={cn('shrink-0', r.state === 'done' ? 'text-success-600' : r.state === 'doing' ? 'text-primary-600' : 'text-ink-muted')} aria-hidden />
                    <span className={cn('min-w-0 flex-1', r.state === 'todo' ? 'text-ink-secondary' : 'font-medium text-ink')}>
                      Bài {r.no}: {r.lesson.title}
                    </span>
                    {examsAfter.map((e) => (
                      <StatusTag key={e.id} tone={EXAM_KINDS[e.kind].tone} icon={EXAM_KINDS[e.kind].icon} size="sm">
                        {e.title}
                      </StatusTag>
                    ))}
                    {logged > 0 && (
                      <span className="inline-flex items-center gap-1 text-xs text-primary-700" title="Số buổi có nhật ký giáo viên">
                        <NotebookPen size={12} aria-hidden /> {logged}
                      </span>
                    )}
                    <span className="tabular w-20 text-right text-xs text-ink-muted">
                      {r.done}/{r.sessions.length} buổi
                    </span>
                    <span className="tabular hidden w-48 text-right text-xs whitespace-nowrap text-ink-muted sm:inline">
                      {r.from ? `${fmtDate(r.from)}${r.to !== r.from ? ` – ${fmtDate(r.to)}` : ''}` : '—'}
                    </span>
                    <ChevronRight size={16} className={cn('shrink-0 text-ink-muted transition-transform', isOpen && 'rotate-90')} aria-hidden />
                  </button>
                  {isOpen && (
                    <ul className="flex flex-col gap-2 border-t border-line bg-page px-3 py-3">
                      {r.sessions.length ? (
                        r.sessions.map((x) => <SessionLogItem key={x.id} session={x} />)
                      ) : (
                        <li className="text-[13px] text-ink-muted">Chưa có buổi nào của bài này trên lịch.</li>
                      )}
                    </ul>
                  )}
                </li>
              );
            })}
          </ol>
        </section>
      )}
    </div>
  );
}

/** 1 buổi học + nhật ký giáo viên */
function SessionLogItem({ session: x, lessonLabel }: { session: ClassSession; lessonLabel?: string }) {
  const st = SESSION_STATUS[x.status];
  const log = x.teacherLog;
  let body;
  if (log)
    body = (
      <div className="mt-2 flex flex-col gap-1.5 border-l-2 border-primary-200 pl-3">
        <p className="text-ink">{log.content}</p>
        {log.homework && (
          <p className="flex items-start gap-1.5 text-ink-secondary">
            <BookOpen size={13} className="mt-0.5 shrink-0" aria-hidden /> BTVN: {log.homework}
          </p>
        )}
        {log.issues && (
          <p className="flex items-start gap-1.5 text-warning-700">
            <AlertTriangle size={13} className="mt-0.5 shrink-0" aria-hidden /> {log.issues}
          </p>
        )}
        <p className="text-xs text-ink-muted">
          — {log.author}, {fmtDate(log.at)} {dayjs(log.at).format('HH:mm')}
        </p>
      </div>
    );
  else if (x.status === 'cancelled') body = x.note ? <p className="mt-1 text-ink-muted">Lý do hủy: {x.note}</p> : null;
  else if (x.status === 'scheduled') body = <p className="mt-1 text-ink-muted">Chưa diễn ra.</p>;
  else body = <p className="mt-1 text-warning-700">Giáo viên chưa ghi nhật ký buổi này.</p>;

  return (
    <li className="rounded-lg border border-line bg-surface px-3 py-2.5 text-[13px]">
      <div className="flex flex-wrap items-center gap-x-3 gap-y-1">
        <span className="font-medium text-ink">
          Buổi {x.no} · {fmtDate(x.date)}
        </span>
        <StatusTag group="session" status={x.status} size="sm" />
        {x.attendance && (
          <span className="tabular text-xs text-ink-muted">
            Có mặt {x.attendance.present}/{x.attendance.total}
          </span>
        )}
        {lessonLabel && <span className="text-xs text-ink-muted">{lessonLabel}</span>}
        <span className="sr-only">{st.label}</span>
      </div>
      {body}
    </li>
  );
}
