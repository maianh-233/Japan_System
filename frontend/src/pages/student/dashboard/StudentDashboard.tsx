import dayjs from 'dayjs';
import { CalendarCheck, NotebookPen, Trophy, Wallet } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { Button, PageHeader, StatCard, StatusTag } from '@/components/common';
import { SessionCard } from '@/features/academic/SessionCard';
import { useAuth } from '@/hooks/useAuth';
import { formatCurrency } from '@/lib/format';
import { upcomingSessions } from '@/mocks/data/schedule';

const ASSIGNMENTS = [
  { id: 1, title: 'Bài 12 — Ngữ pháp ～ば', className: 'N3-K12', due: dayjs().add(2, 'day').hour(23).minute(59) },
  { id: 2, title: 'Luyện nghe Choukai đề 3', className: 'N3-K12', due: dayjs().add(5, 'day').hour(23).minute(59) },
];

export default function StudentDashboard() {
  const { user } = useAuth();
  const navigate = useNavigate();
  const next = upcomingSessions(1)[0];

  return (
    <>
      <PageHeader title={`Chào ${user?.fullName.split(' ').pop()} 👋`} subtitle="がんばって！ Chúc bạn một ngày học tập hiệu quả." />

      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-4">
        <StatCard label="Chuyên cần" value="92%" icon={CalendarCheck} tone="success" hint="23/25 buổi" />
        <StatCard label="Bài tập chưa nộp" value={ASSIGNMENTS.length} icon={NotebookPen} tone="warning" onClick={() => navigate('/student/assignments')} />
        <StatCard label="Điểm thi thử gần nhất" value="128/180" icon={Trophy} tone="info" trend={9} trendLabel="so với lần trước" />
        <StatCard label="Học phí còn lại" value={formatCurrency(1_500_000)} icon={Wallet} tone="primary" hint="Hạn 30/11" onClick={() => navigate('/student/tuition')} />
      </div>

      <div className="mt-6 grid grid-cols-1 gap-6 lg:grid-cols-2">
        <section className="flex flex-col gap-3">
          <h2 className="text-lg font-semibold">Buổi học tiếp theo</h2>
          {next && <SessionCard session={next} showTeacher actions={<Button action="view" variant="outline" size="sm" onClick={() => navigate('/student/timetable')}>Lịch học</Button>} />}
        </section>
        <section className="flex flex-col gap-3">
          <h2 className="text-lg font-semibold">Bài tập sắp đến hạn</h2>
          <ul className="flex flex-col divide-y divide-line rounded-[var(--radius-card)] border border-line bg-surface">
            {ASSIGNMENTS.map((a) => (
              <li key={a.id} className="flex items-center justify-between gap-3 p-4">
                <div className="min-w-0">
                  <p className="truncate font-medium">{a.title}</p>
                  <p className="text-[13px] text-ink-muted">
                    {a.className} · Hạn {a.due.format('HH:mm DD/MM')}
                  </p>
                </div>
                <StatusTag tone={a.due.diff(dayjs(), 'day') < 3 ? 'warning' : 'info'} icon={NotebookPen} size="sm">
                  Còn {a.due.diff(dayjs(), 'day')} ngày
                </StatusTag>
              </li>
            ))}
          </ul>
        </section>
      </div>
    </>
  );
}
