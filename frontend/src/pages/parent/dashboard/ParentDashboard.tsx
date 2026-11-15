import { CalendarCheck, Trophy, Wallet } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { Button, PageHeader, StatCard, UserCell } from '@/components/common';
import { SessionCard } from '@/features/academic/SessionCard';
import { formatCurrency } from '@/lib/format';
import { upcomingSessions } from '@/mocks/data/schedule';

/** Dữ liệu giả: con đang học tại trung tâm */
const CHILD = { name: 'Nguyễn Văn An', code: 'HV1000', className: 'N3-K12 · Tối 2-4-6' };

export default function ParentDashboard() {
  const navigate = useNavigate();
  const next = upcomingSessions(1)[0];

  return (
    <>
      <PageHeader title="Theo dõi học tập" subtitle="Thông tin học tập của con tại trung tâm" />

      <div className="mb-6 rounded-[var(--radius-card)] border border-line bg-surface p-4">
        <UserCell name={CHILD.name} subtitle={`${CHILD.code} · ${CHILD.className}`} size="lg" />
      </div>

      <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
        <StatCard label="Chuyên cần" value="92%" icon={CalendarCheck} tone="success" hint="Vắng 2/25 buổi" onClick={() => navigate('/parent/attendance')} />
        <StatCard label="Điểm thi thử gần nhất" value="128/180" icon={Trophy} tone="info" trend={9} trendLabel="so với lần trước" onClick={() => navigate('/parent/results')} />
        <StatCard label="Học phí còn lại" value={formatCurrency(1_500_000)} icon={Wallet} tone="warning" hint="Hạn 30/11" onClick={() => navigate('/parent/tuition')} />
      </div>

      <section className="mt-6 flex flex-col gap-3">
        <h2 className="text-lg font-semibold">Buổi học tiếp theo</h2>
        {next && (
          <SessionCard
            session={next}
            showTeacher
            actions={<Button action="view" variant="outline" size="sm" onClick={() => navigate('/parent/timetable')}>Lịch học</Button>}
          />
        )}
      </section>
    </>
  );
}
