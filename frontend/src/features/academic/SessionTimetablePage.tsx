import { useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import dayjs from 'dayjs';
import { ClipboardList, ScanLine } from 'lucide-react';
import { Button, PageHeader, Timetable, useNotify } from '@/components/common';
import { useClassStore } from '@/features/academic/classes/classStore';
import { notStarted, sessionTeachers } from '@/features/academic/classes/sessionUtils';
import { CancelRequestModal } from '@/features/portal/components/CancelRequestModal';
import { ChildSwitcher } from '@/features/portal/components/ChildSwitcher';
import { LeaveRequestModal } from '@/features/portal/components/LeaveRequestModal';
import { ScanAttendanceModal } from '@/features/portal/components/ScanAttendanceModal';
import { usePortalStore } from '@/features/portal/portalStore';
import { activeOf, sessionEvent, useStudentId, useStudentLearning, useTeacherId } from '@/features/portal/usePortal';

/**
 * Thời khóa biểu cá nhân dùng chung (dữ liệu THẬT từ lịch lớp):
 *  - Giáo viên: buổi mình dạy (kể cả dạy thay). Buổi sắp tới → xin hủy buổi; buổi hôm nay / đã qua → mở buổi (điểm danh, nhật ký)
 *  - Học viên / Phụ huynh: buổi của các lớp đang học + buổi học bù đã đăng ký. Buổi sắp tới → xin nghỉ
 */
export function SessionTimetablePage({ role }: { role: 'teacher' | 'student' | 'parent' }) {
  const notify = useNotify();
  const navigate = useNavigate();
  const me = useTeacherId();
  const studentId = useStudentId();
  const sessions = useClassStore((s) => s.sessions);
  const classes = useClassStore((s) => s.classes);
  const cancelReqs = usePortalStore((s) => s.cancelRequests);
  const leaveReqs = usePortalStore((s) => s.leaveRequests);
  const learning = useStudentLearning(studentId);
  const [week, setWeek] = useState(() => dayjs());
  const [cancelFor, setCancelFor] = useState<string | null>(null);
  const [leaveFor, setLeaveFor] = useState<string | null>(null);
  const [scanning, setScanning] = useState(false);
  const isTeacher = role === 'teacher';

  const events = useMemo(() => {
    const fromDate = week.subtract((week.day() + 6) % 7, 'day').startOf('day');
    const from = fromDate.format('YYYY-MM-DD');
    const to = fromDate.add(6, 'day').format('YYYY-MM-DD');
    const inRange = sessions.filter((s) => s.date >= from && s.date <= to);
    if (isTeacher) {
      return inRange
        .map((s) => ({ s, cls: classes.find((c) => c.id === s.classId) }))
        .filter(({ s, cls }) => cls && sessionTeachers(s, cls).includes(me))
        .map(({ s, cls }) => {
          const req = cancelReqs.find((r) => r.sessionId === s.id && r.status === 'pending');
          const sub = !cls!.teacherIds.includes(me);
          const ev = sessionEvent(s, cls!, { subtitle: req ? 'Đã xin hủy — chờ duyệt' : sub ? `Dạy thay · buổi ${s.no}` : undefined });
          return req ? { ...ev, tone: 'warning' as const } : sub ? { ...ev, tone: 'success' as const } : ev;
        });
    }
    const classIds = activeOf(learning).map((x) => x.cls.id);
    const mine = leaveReqs.filter((r) => r.studentId === studentId);
    // Buổi học bù đã đăng ký: đơn đã duyệt (hoặc đang chờ duyệt) — đơn bị từ chối thì không
    const makeupReq = (id: string) => mine.find((r) => r.makeupSessionId === id && r.status !== 'rejected');
    const makeups = new Set(mine.filter((r) => r.status !== 'rejected').map((r) => r.makeupSessionId).filter(Boolean) as string[]);
    return inRange
      .filter((s) => classIds.includes(s.classId) || makeups.has(s.id))
      .map((s) => {
        const cls = classes.find((c) => c.id === s.classId)!;
        const leave = mine.find((r) => r.sessionId === s.id && r.status !== 'rejected');
        const ev = sessionEvent(s, cls, makeups.has(s.id) ? { subtitle: `Học bù${makeupReq(s.id)?.status === 'pending' ? ' (chờ duyệt)' : ''} · ${cls.courseLabel}` } : leave ? { subtitle: leave.status === 'approved' ? 'Đã được nghỉ' : 'Đã xin nghỉ — chờ duyệt' } : undefined);
        return makeups.has(s.id) ? { ...ev, tone: 'warning' as const } : leave ? { ...ev, tone: 'neutral' as const } : ev;
      });
  }, [week, sessions, classes, isTeacher, me, cancelReqs, leaveReqs, learning, studentId]);

  const onClick = (id: string) => {
    const s = sessions.find((x) => x.id === id);
    if (!s) return;
    if (s.status === 'cancelled') return notify.info({ title: 'Buổi đã hủy', description: s.note ?? 'Giáo vụ sẽ thông báo lịch học bù.' });
    // GV: mọi buổi (đã qua / hôm nay / sắp tới) đều mở trang Dạy học
    if (isTeacher) return navigate(`/teacher/teaching/${s.id}`);
    if (!notStarted(s)) return notify.info({ title: `Buổi ${s.no}`, description: 'Buổi đã diễn ra — xem điểm danh ở mục Chuyên cần / Tiến độ.' });
    if (leaveReqs.some((r) => r.sessionId === s.id && r.studentId === studentId && r.status !== 'rejected')) return notify.info({ title: 'Đã gửi đơn xin nghỉ buổi này' });
    setLeaveFor(s.id);
  };

  return (
    <>
      <PageHeader
        title={role === 'parent' ? 'Lịch học của con' : 'Thời khóa biểu'}
        subtitle={isTeacher ? 'Bấm vào buổi học để mở trang Dạy học: nội dung bài, tài liệu trình chiếu, chấm công, QR điểm danh, báo cáo tiến độ.' :'Bấm vào buổi sắp tới để xin nghỉ (và đăng ký học bù nếu muốn).'}
        actions={
          <>
            {role === 'student' && (
              <Button icon={ScanLine} onClick={() => setScanning(true)}>
                Quét QR điểm danh
              </Button>
            )}
            <Button icon={ClipboardList} variant="outline" collapseOnMobile onClick={() => (isTeacher ? setCancelFor('') : setLeaveFor(''))}>
              {isTeacher ? 'Xin hủy buổi' : 'Xin nghỉ'}
            </Button>
          </>
        }
      />
      {role === 'parent' && <ChildSwitcher />}
      {role === 'student' && <ScanAttendanceModal open={scanning} onClose={() => setScanning(false)} studentId={studentId} />}
      <div className="rounded-[var(--radius-card)] border border-line bg-surface p-3 sm:p-4">
        <Timetable events={events} week={week} onWeekChange={setWeek} onEventClick={(e) => onClick(String(e.id))} />
      </div>

      {isTeacher ? (
        <CancelRequestModal open={cancelFor !== null} sessionId={cancelFor || undefined} onClose={() => setCancelFor(null)} />
      ) : (
        <LeaveRequestModal open={leaveFor !== null} sessionId={leaveFor || undefined} studentId={studentId} by={role === 'parent' ? 'parent' : 'student'} onClose={() => setLeaveFor(null)} />
      )}
    </>
  );
}
