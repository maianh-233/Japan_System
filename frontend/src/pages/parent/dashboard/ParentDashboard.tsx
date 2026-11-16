import dayjs from 'dayjs';
import { CalendarCheck, MapPin, MessageSquareText, Trophy, Video, Wallet } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { Button, PageHeader, StatCard, UserCell } from '@/components/common';
import { useClassStore } from '@/features/academic/classes/classStore';
import { teacherNames } from '@/features/academic/classes/constants';
import { notStarted, sessionRoom, sessionTeachers } from '@/features/academic/classes/sessionUtils';
import { ChildSwitcher } from '@/features/portal/components/ChildSwitcher';
import { ATT_META } from '@/features/portal/components/LearningView';
import { activeOf, shortCode, useChildren, useStudentId, useStudentLearning } from '@/features/portal/usePortal';
import { summarizeTuition, useStudentStore } from '@/features/student/studentStore';
import { cn } from '@/lib/cn';
import { formatCurrency } from '@/lib/format';

/** Cổng PH › Tổng quan: con đang học gì, chuyên cần, điểm gần nhất, học phí (gộp các con), buổi tới */
export default function ParentDashboard() {
  const navigate = useNavigate();
  const children = useChildren();
  const studentId = useStudentId();
  const child = children.find((c) => c.id === studentId);
  const learning = useStudentLearning(studentId);
  const active = activeOf(learning);
  const sessions = useClassStore((s) => s.sessions);
  const invoices = useStudentStore((s) => s.invoices);
  const enrollments = useStudentStore((s) => s.enrollments);

  const main = active[0];
  const st = main?.stats;
  const lastExam = learning.flatMap((x) => x.stats?.exams.list ?? []).filter((e) => e.result.status === 'done').slice(-1)[0];
  const ids = children.map((c) => c.id);
  const tuition = summarizeTuition(invoices.filter((i) => enrollments.some((e) => e.id === i.enrollmentId && ids.includes(e.studentId))));
  const next = active
    .flatMap(({ cls }) => sessions.filter((s) => s.classId === cls.id && s.status !== 'cancelled' && notStarted(s)).map((s) => ({ s, cls })))
    .sort((a, b) => (a.s.date + a.s.startTime).localeCompare(b.s.date + b.s.startTime))[0];
  const lastLog = main ? sessions.filter((s) => s.classId === main.cls.id && s.teacherLog).sort((a, b) => b.date.localeCompare(a.date))[0] : undefined;
  const recent = st?.attendance.timeline.slice(-10) ?? [];

  return (
    <>
      <PageHeader title="Theo dõi học tập" subtitle="Thông tin học tập của con tại trung tâm" />
      <ChildSwitcher />

      <div className="mb-6 rounded-[var(--radius-card)] border border-line bg-surface p-4">
        <UserCell name={child?.fullName ?? ''} subtitle={`${child?.code ?? ''} · ${active.map((x) => `${shortCode(x.cls)} (${x.cls.courseLabel})`).join(', ') || 'Chưa học lớp nào'}`} size="lg" />
        {recent.length > 0 && (
          <div className="mt-3 flex items-center gap-2 text-xs text-ink-muted">
            10 buổi gần nhất:
            <span className="flex gap-1">
              {recent.map((t) => (
                <span key={t.sessionId} title={`${dayjs(t.date).format('DD/MM')} · ${ATT_META[t.status ?? 'none'].label}`} className={cn('size-3.5 rounded', ATT_META[t.status ?? 'none'].dot)} />
              ))}
            </span>
          </div>
        )}
      </div>

      <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
        <StatCard label="Chuyên cần" value={st?.attendance.rate !== undefined ? `${st.attendance.rate}%` : '—'} icon={CalendarCheck} tone={st?.attendance.rate !== undefined && st.attendance.rate < 80 ? 'danger' : 'success'} hint={st ? `Vắng ${st.attendance.absent + st.attendance.excused}/${st.attendance.sessions} buổi` : undefined} onClick={() => navigate('/parent/attendance')} />
        <StatCard label="Bài thi gần nhất" value={lastExam ? `${lastExam.total}/${lastExam.max}` : '—'} icon={Trophy} tone="info" hint={lastExam?.title} onClick={() => navigate('/parent/results')} />
        <StatCard label="Học phí còn lại" value={formatCurrency(tuition.remaining)} icon={Wallet} tone={tuition.status === 'overdue' ? 'danger' : tuition.remaining ? 'warning' : 'success'} hint={children.length > 1 ? `Gộp ${children.length} con` : undefined} onClick={() => navigate('/parent/tuition')} />
      </div>

      <div className="mt-6 grid gap-6 lg:grid-cols-2">
        <section className="flex flex-col gap-3">
          <h2 className="text-lg font-semibold">Buổi học tiếp theo</h2>
          {next ? (
            <div className="rounded-[var(--radius-card)] border border-line bg-surface p-4">
              <p className="text-xs text-ink-muted">{dayjs(next.s.date).format('dddd, DD/MM')}</p>
              <p className="text-lg font-semibold text-ink">
                {next.s.startTime}–{next.s.endTime} · {shortCode(next.cls)}
              </p>
              <p className="mt-1 flex flex-wrap gap-x-4 text-[13px] text-ink-secondary">
                <span>GV {teacherNames(sessionTeachers(next.s, next.cls))}</span>
                <span className="inline-flex items-center gap-1">
                  {next.cls.online ? <Video size={14} aria-hidden /> : <MapPin size={14} aria-hidden />} {next.cls.online ? 'Online' : `Phòng ${sessionRoom(next.s, next.cls)}`}
                </span>
              </p>
              <Button action="view" variant="outline" size="sm" className="mt-3" onClick={() => navigate('/parent/timetable')}>
                Lịch học
              </Button>
            </div>
          ) : (
            <p className="rounded-xl border border-line p-6 text-center text-ink-muted">Không có buổi học sắp tới.</p>
          )}
        </section>
        <section className="flex flex-col gap-3">
          <h2 className="text-lg font-semibold">Giáo viên ghi buổi gần nhất</h2>
          {lastLog?.teacherLog ? (
            <div className="rounded-[var(--radius-card)] border border-line bg-surface p-4 text-[13px]">
              <p className="text-xs text-ink-muted">
                Buổi {lastLog.no} · {dayjs(lastLog.date).format('dd DD/MM')} · {lastLog.teacherLog.author}
              </p>
              <p className="mt-1 flex gap-1.5 text-ink">
                <MessageSquareText size={14} className="mt-0.5 shrink-0 text-primary-600" aria-hidden /> {lastLog.teacherLog.content}
              </p>
              {lastLog.teacherLog.homework && <p className="mt-1 text-ink-secondary">Bài về nhà: {lastLog.teacherLog.homework}</p>}
              <Button action="view" variant="ghost" size="sm" className="mt-2" onClick={() => navigate('/parent/results')}>
                Xem kết quả học tập
              </Button>
            </div>
          ) : (
            <p className="rounded-xl border border-line p-6 text-center text-ink-muted">Chưa có nhật ký buổi học.</p>
          )}
        </section>
      </div>
    </>
  );
}
