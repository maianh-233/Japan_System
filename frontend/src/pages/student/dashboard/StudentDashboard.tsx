import { useState } from 'react';
import { ClassInbox } from '@/features/portal/components/ClassInbox';
import { TeacherReviewSurveys } from '@/features/portal/components/TeacherReviewSurveys';
import { USE_API } from '@/lib/api/config';
import dayjs from 'dayjs';
import { Brain, CalendarCheck, CheckCircle2, MapPin, NotebookPen, ScanLine, Trophy, Video, Wallet } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { Button, PageHeader, StatCard, StatusTag } from '@/components/common';
import { useClassStore } from '@/features/academic/classes/classStore';
import { teacherNames } from '@/features/academic/classes/constants';
import { notStarted, sessionRoom, sessionTeachers } from '@/features/academic/classes/sessionUtils';
import { QR_OPEN_BEFORE_MIN } from '@/features/academic/classes/attendanceQr';
import { PromotionOffers } from '@/features/portal/components/PromotionOffers';
import { ScanAttendanceModal } from '@/features/portal/components/ScanAttendanceModal';
import { usePortalStore } from '@/features/portal/portalStore';
import { activeOf, shortCode, useStudentId, useStudentLearning } from '@/features/portal/usePortal';
import { summarizeTuition, useStudentStore } from '@/features/student/studentStore';
import { useAuth } from '@/hooks/useAuth';
import { cn } from '@/lib/cn';
import { formatCurrency } from '@/lib/format';

/** Cổng HV › Tổng quan */
export default function StudentDashboard() {
  const { user } = useAuth();
  const navigate = useNavigate();
  const studentId = useStudentId();
  const learning = useStudentLearning(studentId);
  const active = activeOf(learning);
  const sessions = useClassStore((s) => s.sessions);
  const invoices = useStudentStore((s) => s.invoices);
  const enrollments = useStudentStore((s) => s.enrollments);
  const progress = usePortalStore((s) => s.cardProgress);
  const decks = usePortalStore((s) => s.decks);

  const main = active[0];
  const st = main?.stats;
  const todo = active.flatMap((x) => x.stats?.assignments.list.filter((a) => !a.submission && !dayjs(a.assignment.dueDate).endOf('day').isBefore(dayjs())) ?? []).sort((a, b) => a.assignment.dueDate.localeCompare(b.assignment.dueDate));
  const lastExam = learning.flatMap((x) => x.stats?.exams.list ?? []).filter((e) => e.result.status === 'done').slice(-1)[0];
  const tuition = summarizeTuition(invoices.filter((i) => enrollments.some((e) => e.id === i.enrollmentId && e.studentId === studentId)));
  const next = active
    .flatMap(({ cls }) => sessions.filter((s) => s.classId === cls.id && s.status !== 'cancelled' && notStarted(s)).map((s) => ({ s, cls })))
    .sort((a, b) => (a.s.date + a.s.startTime).localeCompare(b.s.date + b.s.startTime))[0];
  const [scanning, setScanning] = useState(false);
  // Buổi đang mở điểm danh (từ 30' trước giờ học tới hết buổi)
  const live = active
    .flatMap(({ cls }) => sessions.filter((s) => s.classId === cls.id && s.status !== 'cancelled' && s.date === dayjs().format('YYYY-MM-DD')).map((s) => ({ s, cls })))
    .find(({ s }) => !dayjs().isBefore(dayjs(`${s.date}T${s.startTime}`).subtract(QR_OPEN_BEFORE_MIN, 'minute')) && !dayjs().isAfter(dayjs(`${s.date}T${s.endTime}`)));
  const liveRec = live?.s.records?.find((r) => r.studentId === studentId);
  const checkedIn = liveRec && (liveRec.status === 'present' || liveRec.status === 'late') && liveRec.checkInAt;
  const dueCards = decks.flatMap((d) => d.cards).filter((c) => !progress[c.id] || !dayjs(progress[c.id].due).isAfter(dayjs())).length;

  return (
    <>
      <PageHeader
        title={`Chào ${user?.fullName.split(' ').pop()} 👋`}
        subtitle="がんばって！ Chúc bạn một ngày học tập hiệu quả."
        actions={
          <Button icon={ScanLine} onClick={() => setScanning(true)}>
            Quét QR điểm danh
          </Button>
        }
      />

      {live && (
        <div className={cn('mb-4 flex flex-col gap-3 rounded-[var(--radius-card)] border px-4 py-3 sm:flex-row sm:items-center', checkedIn ? 'border-success-100 bg-success-50' : 'border-primary-200 bg-primary-50')}>
          {checkedIn ? <CheckCircle2 size={24} className="shrink-0 text-success-600" aria-hidden /> : <ScanLine size={24} className="shrink-0 text-primary-600" aria-hidden />}
          <div className="min-w-0 flex-1 text-[13px]">
            <p className={cn('font-semibold', checkedIn ? 'text-success-700' : 'text-primary-900')}>
              {shortCode(live.cls)} · Buổi {live.s.no} · {live.s.startTime}–{live.s.endTime}
            </p>
            <p className={checkedIn ? 'text-success-700' : 'text-primary-800'}>
              {checkedIn ? `Bạn đã điểm danh lúc ${liveRec!.checkInAt}${liveRec!.status === 'late' ? ' (đi muộn)' : ''}.` : 'Đã mở điểm danh — quét mã QR giáo viên chiếu trên lớp.'}
            </p>
          </div>
          {!checkedIn && (
            <Button icon={ScanLine} onClick={() => setScanning(true)}>
              Quét QR ngay
            </Button>
          )}
        </div>
      )}
      <ScanAttendanceModal open={scanning} onClose={() => setScanning(false)} studentId={studentId} />
      <PromotionOffers studentIds={[studentId]} viewer="student" />

      {USE_API && (
        <>
          <TeacherReviewSurveys />
          <ClassInbox />
        </>
      )}
      <div className="grid grid-cols-2 gap-3 xl:grid-cols-4">
        <StatCard label="Chuyên cần" value={st?.attendance.rate !== undefined ? `${st.attendance.rate}%` : '—'} icon={CalendarCheck} tone={st?.attendance.rate !== undefined && st.attendance.rate < 80 ? 'danger' : 'success'} hint={st ? `${st.attendance.present + st.attendance.late}/${st.attendance.sessions} buổi` : undefined} onClick={() => navigate('/student/progress')} />
        <StatCard label="Bài tập cần làm" value={todo.length} icon={NotebookPen} tone="warning" onClick={() => navigate('/student/assignments')} />
        <StatCard label="Bài thi gần nhất" value={lastExam ? `${lastExam.total}/${lastExam.max}` : '—'} icon={Trophy} tone="info" hint={lastExam?.title} onClick={() => navigate('/student/exams')} />
        <StatCard label="Học phí còn lại" value={formatCurrency(tuition.remaining)} icon={Wallet} tone={tuition.status === 'overdue' ? 'danger' : 'primary'} onClick={() => navigate('/student/tuition')} />
      </div>

      <div className="mt-6 grid grid-cols-1 gap-6 lg:grid-cols-2">
        <section className="flex flex-col gap-3">
          <h2 className="text-lg font-semibold">Buổi học tiếp theo</h2>
          {next ? (
            <div className="rounded-[var(--radius-card)] border border-line bg-surface p-4">
              <p className="text-xs text-ink-muted">{dayjs(next.s.date).format('dddd, DD/MM')}</p>
              <p className="text-lg font-semibold text-ink">
                {next.s.startTime}–{next.s.endTime} · {shortCode(next.cls)}
                {next.s.kind === 'exam' && <span className="ml-2 text-sm text-danger-700">BUỔI THI</span>}
              </p>
              <p className="mt-1 flex flex-wrap gap-x-4 text-[13px] text-ink-secondary">
                <span>GV {teacherNames(sessionTeachers(next.s, next.cls))}</span>
                <span className="inline-flex items-center gap-1">
                  {next.cls.online ? <Video size={14} aria-hidden /> : <MapPin size={14} aria-hidden />} {next.cls.online ? 'Online' : `Phòng ${sessionRoom(next.s, next.cls)}`}
                </span>
              </p>
              <Button action="view" variant="outline" size="sm" className="mt-3" onClick={() => navigate('/student/timetable')}>
                Thời khóa biểu
              </Button>
            </div>
          ) : (
            <p className="rounded-xl border border-line p-6 text-center text-ink-muted">Không có buổi học sắp tới.</p>
          )}
          <button type="button" onClick={() => navigate('/student/flashcards')} className="flex items-center gap-3 rounded-[var(--radius-card)] border border-line bg-surface p-4 text-left hover:border-primary-300">
            <Brain size={22} className="text-primary-600" aria-hidden />
            <span className="flex-1">
              <span className="block font-medium text-ink">{dueCards} thẻ flashcard cần ôn hôm nay</span>
              <span className="text-xs text-ink-muted">5 phút mỗi ngày để giữ chuỗi học</span>
            </span>
          </button>
        </section>
        <section className="flex flex-col gap-3">
          <h2 className="text-lg font-semibold">Bài tập sắp đến hạn</h2>
          <ul className="flex flex-col divide-y divide-line rounded-[var(--radius-card)] border border-line bg-surface">
            {todo.slice(0, 5).map(({ assignment: a }) => {
              const days = dayjs(a.dueDate).endOf('day').diff(dayjs(), 'day');
              return (
                <li key={a.id} className="flex items-center justify-between gap-3 p-4">
                  <div className="min-w-0">
                    <p className="truncate font-medium">{a.title}</p>
                    <p className="text-[13px] text-ink-muted">
                      {a.type === 'quiz' ? 'Trắc nghiệm' : 'Nộp bài'} · hạn {dayjs(a.dueDate).format('DD/MM')}
                    </p>
                  </div>
                  <StatusTag tone={days < 2 ? 'warning' : 'info'} icon={NotebookPen} size="sm">
                    {days < 1 ? 'Hôm nay' : `Còn ${days} ngày`}
                  </StatusTag>
                </li>
              );
            })}
            {!todo.length && <li className="p-6 text-center text-ink-muted">Không còn bài nào phải làm 🎉</li>}
          </ul>
        </section>
      </div>
    </>
  );
}
