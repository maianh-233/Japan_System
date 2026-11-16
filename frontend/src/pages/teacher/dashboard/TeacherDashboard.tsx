import { useMemo, useState } from 'react';
import dayjs from 'dayjs';
import { ArrowLeftRight, CalendarDays, MapPin, NotebookPen, School, Video } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { Button, PageHeader, StatCard, StatusTag } from '@/components/common';
import { useClassStore } from '@/features/academic/classes/classStore';
import { notStarted, sessionRoom, sessionTeachers } from '@/features/academic/classes/sessionUtils';
import { TeachSessionDrawer } from '@/features/portal/components/TeachSessionDrawer';
import { usePortalStore } from '@/features/portal/portalStore';
import { shortCode, useTeacherId, useTeachingClasses } from '@/features/portal/usePortal';
import { useStudentStore } from '@/features/student/studentStore';
import { useAuth } from '@/hooks/useAuth';

/** Cổng GV › Tổng quan: buổi dạy sắp tới, bài chờ chấm, lời mời dạy thay, nhật ký còn thiếu */
export default function TeacherDashboard() {
  const { user } = useAuth();
  const navigate = useNavigate();
  const me = useTeacherId();
  const myClasses = useTeachingClasses();
  const classes = useClassStore((s) => s.classes);
  const sessions = useClassStore((s) => s.sessions);
  const assignments = useClassStore((s) => s.assignments);
  const submissions = useClassStore((s) => s.submissions);
  const enrollments = useStudentStore((s) => s.enrollments);
  const invites = usePortalStore((s) => s.subInvites);
  const [open, setOpen] = useState<string | null>(null);

  const mine = useMemo(
    () =>
      sessions
        .map((s) => ({ s, cls: classes.find((c) => c.id === s.classId) }))
        .filter(({ s, cls }) => cls && s.status !== 'cancelled' && sessionTeachers(s, cls).includes(me)),
    [sessions, classes, me],
  );
  const today = dayjs().format('YYYY-MM-DD');
  const upcoming = mine.filter(({ s }) => notStarted(s) || s.date === today).sort((a, b) => (a.s.date + a.s.startTime).localeCompare(b.s.date + b.s.startTime));
  const weekEnd = dayjs().endOf('week').add(1, 'day').format('YYYY-MM-DD');
  const thisWeek = upcoming.filter(({ s }) => s.date <= weekEnd);
  const noLog = mine.filter(({ s }) => s.status === 'completed' && !s.teacherLog && s.date >= dayjs().subtract(14, 'day').format('YYYY-MM-DD'));
  const ongoing = myClasses.filter((c) => c.status === 'ongoing');
  const students = enrollments.filter((e) => ongoing.some((c) => c.id === e.classId) && e.status === 'studying').length;
  const waiting = submissions.filter((x) => x.score === undefined && assignments.some((a) => a.id === x.assignmentId && a.type === 'upload' && myClasses.some((c) => c.id === a.classId))).length;
  const pendingInv = invites.filter((i) => i.toTeacherId === me && i.status === 'pending' && dayjs(i.respondBy).isAfter(dayjs())).length;
  const openSession = mine.find(({ s }) => s.id === open);

  return (
    <>
      <PageHeader
        title={`Xin chào, ${user?.fullName} 👋`}
        subtitle={`${dayjs().format('dddd, DD/MM/YYYY')} · lịch dạy và việc cần làm`}
        actions={
          <Button icon={CalendarDays} variant="outline" collapseOnMobile onClick={() => navigate('/teacher/timetable')}>
            Thời khóa biểu
          </Button>
        }
      />
      <div className="grid grid-cols-2 gap-3 xl:grid-cols-4">
        <StatCard label="Lớp đang dạy" value={ongoing.length} icon={School} hint={`${students} học viên`} onClick={() => navigate('/teacher/classes')} />
        <StatCard label="Buổi dạy tuần này" value={thisWeek.length} icon={CalendarDays} tone="info" />
        <StatCard label="Bài chờ chấm" value={waiting} icon={NotebookPen} tone="warning" onClick={() => navigate('/teacher/assignments')} />
        <StatCard label="Lời mời dạy thay" value={pendingInv} icon={ArrowLeftRight} tone={pendingInv ? 'danger' : 'neutral'} onClick={() => navigate('/teacher/substitutes')} />
      </div>

      {noLog.length > 0 && (
        <p className="mt-4 rounded-xl border border-warning-100 bg-warning-50 px-4 py-3 text-[13px] text-warning-700">
          {noLog.length} buổi đã dạy chưa ghi nhật ký:{' '}
          {noLog.slice(0, 4).map(({ s, cls }) => (
            <button key={s.id} type="button" className="mr-2 font-medium underline" onClick={() => setOpen(s.id)}>
              {shortCode(cls!)} {dayjs(s.date).format('DD/MM')}
            </button>
          ))}
        </p>
      )}

      <section className="mt-6 flex flex-col gap-3">
        <h2 className="text-lg font-semibold">Buổi dạy sắp tới</h2>
        <ul className="flex flex-col divide-y divide-line rounded-[var(--radius-card)] border border-line bg-surface">
          {upcoming.slice(0, 5).map(({ s, cls }) => (
            <li key={s.id} className="flex flex-wrap items-center gap-3 px-4 py-3">
              <span className="w-24 shrink-0">
                <span className="block text-xs text-ink-muted">{s.date === today ? 'Hôm nay' : dayjs(s.date).format('dddd')}</span>
                <span className="tabular font-semibold text-ink">
                  {dayjs(s.date).format('DD/MM')} {s.startTime}
                </span>
              </span>
              <span className="min-w-0 flex-1">
                <span className="block font-medium text-ink">
                  {shortCode(cls!)} · buổi {s.no}
                  {s.kind === 'exam' && <span className="ml-1.5 text-xs text-danger-700">THI</span>}
                  {!cls!.teacherIds.includes(me) && (
                    <StatusTag tone="success" icon={ArrowLeftRight} size="sm" className="ml-1.5">
                      Dạy thay
                    </StatusTag>
                  )}
                </span>
                <span className="inline-flex items-center gap-1 text-xs text-ink-muted">
                  {cls!.online ? <Video size={12} aria-hidden /> : <MapPin size={12} aria-hidden />} {cls!.online ? 'Online' : sessionRoom(s, cls!)}
                </span>
              </span>
              <Button icon={s.date === today ? NotebookPen : CalendarDays} size="sm" variant={s.date === today ? 'solid' : 'outline'} onClick={() => setOpen(s.id)}>
                {s.date === today ? 'Vào buổi' : 'Xem bài dạy'}
              </Button>
            </li>
          ))}
          {!upcoming.length && <li className="p-6 text-center text-ink-muted">Không có buổi dạy sắp tới.</li>}
        </ul>
      </section>
      {openSession && <TeachSessionDrawer key={open} cls={openSession.cls!} sessionId={open} onClose={() => setOpen(null)} />}
    </>
  );
}
