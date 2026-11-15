import { CalendarDays, ClipboardCheck, NotebookPen, School, Video } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { Button, PageHeader, StatCard, useNotify } from '@/components/common';
import { SessionCard } from '@/features/academic/SessionCard';
import { useAuth } from '@/hooks/useAuth';
import { upcomingSessions } from '@/mocks/data/schedule';

export default function TeacherDashboard() {
  const { user } = useAuth();
  const navigate = useNavigate();
  const notify = useNotify();
  const sessions = upcomingSessions(3);

  return (
    <>
      <PageHeader
        title={`Xin chào, ${user?.fullName} 👋`}
        subtitle="Lịch dạy và việc cần làm của bạn"
        actions={<Button icon={CalendarDays} variant="outline" collapseOnMobile onClick={() => navigate('/teacher/timetable')}>Thời khóa biểu</Button>}
      />
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
        <StatCard label="Lớp đang dạy" value={3} icon={School} hint="40 học viên" />
        <StatCard label="Buổi dạy tuần này" value={6} icon={CalendarDays} tone="info" hint="9 giờ" />
        <StatCard label="Bài tập chờ chấm" value={4} icon={NotebookPen} tone="warning" onClick={() => navigate('/teacher/assignments')} />
      </div>

      <section className="mt-6 flex flex-col gap-3">
        <h2 className="text-lg font-semibold">Buổi dạy sắp tới</h2>
        {sessions.map((s) => (
          <SessionCard
            key={s.id}
            session={s}
            actions={
              <>
                <Button icon={ClipboardCheck} variant="outline" size="sm" onClick={() => notify.info({ title: 'Điểm danh', description: `${s.className} — chức năng sẽ có ở bước sau` })}>
                  Điểm danh
                </Button>
                {s.online && (
                  <Button icon={Video} tone="success" size="sm" onClick={() => notify.success({ title: 'Đang mở phòng học online', description: s.meetingUrl })}>
                    Vào lớp
                  </Button>
                )}
              </>
            }
          />
        ))}
      </section>
    </>
  );
}
