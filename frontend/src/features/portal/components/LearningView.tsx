import { useMemo } from 'react';
import { Progress, Tooltip } from 'antd';
import dayjs from 'dayjs';
import { Check, MessageSquareText } from 'lucide-react';
import { useClassStore } from '@/features/academic/classes/classStore';
import { totalSessionsOf } from '@/features/academic/classes/constants';
import type { StudentStats } from '@/features/academic/classes/studentInsights';
import { isTaught } from '@/features/academic/classes/sessionUtils';
import type { AttendanceStatus, ClassInfo } from '@/features/academic/classes/types';
import { useCurriculumStore } from '@/features/academic/curriculum/curriculumStore';
import { cn } from '@/lib/cn';
import { fmtDate } from '@/lib/date';
import { shortCode, type MyClass } from '../usePortal';

export const ATT_META: Record<AttendanceStatus | 'none', { label: string; dot: string; text: string }> = {
  present: { label: 'Có mặt', dot: 'bg-success-600', text: 'text-success-700' },
  late: { label: 'Đi muộn', dot: 'bg-warning-600', text: 'text-warning-700' },
  excused: { label: 'Vắng có phép', dot: 'bg-primary-400', text: 'text-primary-700' },
  absent: { label: 'Vắng không phép', dot: 'bg-danger-600', text: 'text-danger-700' },
  none: { label: 'Chưa điểm danh', dot: 'bg-line', text: 'text-ink-muted' },
};

/** Chọn lớp (khi học nhiều lớp) */
export function ClassPills({ list, value, onChange }: { list: MyClass[]; value?: string; onChange: (id: string) => void }) {
  if (list.length < 2) return null;
  return (
    <div className="mb-4 flex gap-2 overflow-x-auto pb-1" role="tablist" aria-label="Chọn lớp">
      {list.map((x) => {
        const active = x.cls.id === value;
        return (
          <button
            key={x.cls.id}
            type="button"
            role="tab"
            aria-selected={active}
            onClick={() => onChange(x.cls.id)}
            className={cn('h-9 shrink-0 rounded-full border px-3.5 text-sm font-medium', active ? 'border-primary-600 bg-primary-50 text-primary-700' : 'border-line bg-surface text-ink-secondary')}
          >
            {shortCode(x.cls)}
            <span className="ml-1.5 text-xs text-ink-muted">{x.enrollment.status === 'studying' ? 'đang học' : x.enrollment.status === 'completed' ? 'đã xong' : x.enrollment.status === 'reserved' ? 'bảo lưu' : 'đã nghỉ'}</span>
          </button>
        );
      })}
    </div>
  );
}

/** Chuyên cần: tổng hợp + dải màu từng buổi + danh sách buổi vắng */
export function AttendanceSection({ stats }: { stats: StudentStats }) {
  const a = stats.attendance;
  const absences = a.timeline.filter((t) => t.status === 'absent' || t.status === 'excused' || t.status === 'late').reverse();
  return (
    <section className="rounded-xl border border-line bg-surface p-4">
      <div className="mb-3 flex flex-wrap items-end justify-between gap-2">
        <p className="font-semibold text-ink">Chuyên cần</p>
        <p className="tabular text-2xl font-bold text-ink">{a.rate !== undefined ? `${a.rate}%` : '—'}</p>
      </div>
      <div className="mb-3 grid grid-cols-4 gap-2 text-center text-[13px]">
        {(['present', 'late', 'excused', 'absent'] as const).map((k) => (
          <div key={k} className="rounded-lg bg-canvas py-2">
            <p className={cn('tabular text-lg font-semibold', ATT_META[k].text)}>{a[k]}</p>
            <p className="text-[11px] text-ink-muted">{ATT_META[k].label}</p>
          </div>
        ))}
      </div>
      <div className="flex flex-wrap gap-1" aria-label="Điểm danh từng buổi">
        {a.timeline.map((t) => {
          const m = ATT_META[t.status ?? 'none'];
          return (
            <Tooltip key={t.sessionId} title={`Buổi ${t.no} · ${fmtDate(t.date)} · ${m.label}${t.madeUp ? ` · đã học bù ở ${t.madeUp}` : ''}${t.note ? ` — ${t.note}` : ''}`}>
              <span className={cn('size-4 rounded', t.madeUp ? 'bg-success-600 ring-2 ring-primary-400 ring-offset-1' : m.dot)} />
            </Tooltip>
          );
        })}
      </div>
      {a.madeUp > 0 && <p className="mt-2 text-[13px] text-success-700">{a.madeUp} buổi vắng có phép đã học bù — tính như có mặt.</p>}
      {a.streak >= 2 && <p className="mt-2 text-[13px] font-medium text-danger-700">Đang vắng {a.streak} buổi liên tiếp</p>}
      {absences.length > 0 && (
        <ul className="mt-3 flex flex-col gap-1 text-[13px]">
          {absences.slice(0, 8).map((t) => (
            <li key={t.sessionId} className="flex justify-between gap-2">
              <span>
                Buổi {t.no} · {dayjs(t.date).format('dd DD/MM')}
                {t.note && <span className="text-ink-muted"> — {t.note}</span>}
              </span>
              <span className={t.madeUp ? 'text-success-700' : ATT_META[t.status!].text}>{t.madeUp ? `Có phép · đã học bù (${t.madeUp})` : ATT_META[t.status!].label}</span>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}

/** Bài tập: điểm + nhận xét GV */
export function AssignmentsSection({ stats }: { stats: StudentStats }) {
  const s = stats.assignments;
  return (
    <section className="rounded-xl border border-line bg-surface p-4">
      <div className="mb-2 flex items-end justify-between gap-2">
        <p className="font-semibold text-ink">Bài tập</p>
        <p className="text-[13px] text-ink-secondary">
          Nộp {s.submitted}/{s.due}
          {s.avgPct !== undefined && (
            <>
              {' '}
              · TB <b className="tabular text-ink">{s.avgPct}%</b>
            </>
          )}
        </p>
      </div>
      <ul className="flex flex-col divide-y divide-line text-[13px]">
        {[...s.list].reverse().map(({ assignment: a, submission: sub, overdue }) => (
          <li key={a.id} className="py-2">
            <div className="flex items-start justify-between gap-2">
              <span className="min-w-0">
                <span className="block truncate text-ink">{a.title}</span>
                <span className="text-xs text-ink-muted">hạn {fmtDate(a.dueDate)}</span>
              </span>
              <span className={cn('tabular shrink-0 font-semibold', !sub ? (overdue ? 'text-danger-700' : 'text-ink-muted') : sub.score === undefined ? 'text-primary-700' : 'text-ink')}>
                {!sub ? (overdue ? 'Không nộp' : 'Chưa đến hạn') : sub.score === undefined ? 'Chờ chấm' : `${sub.score}/${a.maxScore}`}
              </span>
            </div>
            {sub?.comment && (
              <p className="mt-1 flex gap-1.5 text-xs text-primary-800">
                <MessageSquareText size={13} className="mt-0.5 shrink-0" aria-hidden /> {sub.comment}
              </p>
            )}
          </li>
        ))}
        {!s.list.length && <li className="py-3 text-ink-muted">Chưa có bài tập.</li>}
      </ul>
    </section>
  );
}

/** Kết quả thi: điểm từng phần + nhận xét */
export function ExamsSection({ stats }: { stats: StudentStats }) {
  return (
    <section className="rounded-xl border border-line bg-surface p-4">
      <p className="mb-2 font-semibold text-ink">Kết quả thi</p>
      <div className="flex flex-col gap-3 text-[13px]">
        {stats.exams.list.map((e) => (
          <div key={e.result.id}>
            <div className="flex justify-between gap-2">
              <span className="font-medium text-ink">{e.title}</span>
              <span className={cn('tabular font-semibold', e.result.status === 'absent' ? 'text-ink-muted' : e.pct >= 60 ? 'text-success-700' : 'text-danger-700')}>
                {e.result.status === 'absent' ? 'Vắng thi' : `${e.total}/${e.max} (${e.pct}%)`}
              </span>
            </div>
            {e.result.status === 'done' && (
              <div className="mt-1 grid grid-cols-2 gap-x-4 gap-y-0.5 text-xs text-ink-secondary sm:grid-cols-3">
                {e.result.sections.map((x) => (
                  <span key={x.name}>
                    {x.name}: <b className="tabular">{x.score}</b>/{x.max}
                  </span>
                ))}
              </div>
            )}
            {e.result.comment && (
              <p className="mt-1 flex gap-1.5 text-xs text-primary-800">
                <MessageSquareText size={13} className="mt-0.5 shrink-0" aria-hidden /> {e.result.comment}
              </p>
            )}
          </div>
        ))}
        {!stats.exams.list.length && <p className="text-ink-muted">Chưa có kỳ thi nào.</p>}
      </div>
    </section>
  );
}

/** Tiến độ theo lộ trình: bài đã học / sắp học */
export function CurriculumProgress({ cls }: { cls: ClassInfo }) {
  const sessions = useClassStore((s) => s.sessions);
  const curriculum = useCurriculumStore((s) => s.curricula.find((c) => c.id === cls.curriculumId));
  const own = useMemo(() => sessions.filter((s) => s.classId === cls.id), [sessions, cls.id]);
  const taught = own.filter(isTaught).length;
  const total = totalSessionsOf(cls);
  const doneLessons = new Set(own.filter(isTaught).map((s) => s.lessonId).filter(Boolean));
  const nextLesson = own.filter((s) => s.status === 'scheduled' && s.lessonId).sort((a, b) => a.date.localeCompare(b.date))[0]?.lessonId;

  return (
    <section className="rounded-xl border border-line bg-surface p-4">
      <div className="mb-2 flex items-end justify-between gap-2">
        <div>
          <p className="font-semibold text-ink">Tiến độ khóa học</p>
          <p className="text-xs text-ink-muted">{curriculum?.name ?? cls.courseLabel}</p>
        </div>
        <p className="tabular text-[13px] text-ink-secondary">
          {taught}/{total} buổi
        </p>
      </div>
      <Progress percent={Math.round((taught / Math.max(1, total)) * 100)} size="small" />
      {curriculum && (
        <ol className="mt-3 grid gap-1 text-[13px] sm:grid-cols-2">
          {curriculum.lessons.map((l, i) => {
            const done = doneLessons.has(l.id);
            const next = l.id === nextLesson;
            return (
              <li key={l.id} className={cn('flex items-center gap-2 rounded-md px-2 py-1', next && 'bg-primary-50 font-medium text-primary-700')}>
                <span className={cn('grid size-5 shrink-0 place-items-center rounded-full text-[11px]', done ? 'bg-success-600 text-white' : 'border border-line text-ink-muted')}>{done ? <Check size={12} aria-label="Đã học" /> : i + 1}</span>
                <span className={cn('truncate', !done && !next && 'text-ink-secondary')}>{l.title}</span>
                {next && <span className="ml-auto shrink-0 text-[11px]">học tiếp</span>}
              </li>
            );
          })}
        </ol>
      )}
    </section>
  );
}
