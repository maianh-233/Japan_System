import { useState } from 'react';
import { CalendarDays, ClipboardList, Target, Users, Wallet } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { Button, PageHeader, StatCard, StatusTag, UserCell, useConfirm, useNotify } from '@/components/common';
import { SessionCard } from '@/features/academic/SessionCard';
import { useAuth } from '@/hooks/useAuth';
import { formatCurrency, formatNumber } from '@/lib/format';
import { upcomingSessions } from '@/mocks/data/schedule';

/** Dữ liệu giả: hệ số theo chi nhánh để số liệu thay đổi khi đổi chi nhánh */
const BRANCH_FACTOR: Record<string, number> = { all: 1, cg: 0.45, q1: 0.35, hc: 0.2 };

const PENDING = [
  { id: 'r1', who: 'Nguyễn Thu Hà', type: 'Hủy buổi', detail: 'N3-K12 · Thứ 4 18:00 · GV ốm', tone: 'session' },
  { id: 'r2', who: 'Trần Văn Bình', type: 'Học viên xin nghỉ', detail: 'N4-K15 · Thứ 6 19:00', tone: 'student' },
  { id: 'r3', who: 'Lê Minh Chi', type: 'Nghỉ phép', detail: '2 ngày · 20–21/11', tone: 'staff' },
];

export default function StaffDashboard() {
  const { user, branchId, currentBranch } = useAuth();
  const notify = useNotify();
  const navigate = useNavigate();
  const { confirm, confirmDialog } = useConfirm();
  const [requests, setRequests] = useState(PENDING);
  const f = BRANCH_FACTOR[branchId] ?? 1;

  const decide = async (id: string, approve: boolean) => {
    const r = requests.find((x) => x.id === id)!;
    const ok = approve
      ? await confirm({ title: `Duyệt đơn “${r.type}” của ${r.who}?`, tone: 'success', confirmText: 'Duyệt' })
      : await confirm({ title: `Từ chối đơn của ${r.who}?`, tone: 'danger', confirmAction: 'reject', requireReason: { label: 'Lý do từ chối' } });
    if (!ok) return;
    setRequests((l) => l.filter((x) => x.id !== id));
    notify[approve ? 'success' : 'warning']({ title: approve ? 'Đã duyệt đơn' : 'Đã từ chối đơn', description: `${r.type} — ${r.who}` });
  };

  return (
    <>
      <PageHeader
        title={`Xin chào, ${user?.fullName.split(' ').pop()} 👋`}
        subtitle={`Tổng quan hoạt động · ${currentBranch ? `Chi nhánh ${currentBranch.name}` : 'Toàn hệ thống'}`}
        actions={<Button action="export" variant="outline" collapseOnMobile>Xuất báo cáo</Button>}
      />

      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-4">
        <StatCard label="Học viên đang học" value={formatNumber(Math.round(1240 * f))} icon={Users} trend={6.4} trendLabel="so với tháng trước" onClick={() => navigate('/admin/students/profiles')} />
        <StatCard label="Lead mới tháng này" value={formatNumber(Math.round(318 * f))} icon={Target} tone="info" trend={12} trendLabel="so với tháng trước" />
        <StatCard label="Doanh thu tháng" value={formatCurrency(Math.round(1_850_000_000 * f))} icon={Wallet} tone="success" trend={3.1} />
        <StatCard label="Công nợ quá hạn" value={formatCurrency(Math.round(96_500_000 * f))} icon={ClipboardList} tone="warning" trend={-8} trendInverse />
      </div>

      <div className="mt-6 grid grid-cols-1 gap-6 xl:grid-cols-[minmax(0,1fr)_400px]">
        <section className="flex flex-col gap-3">
          <div className="flex items-center justify-between">
            <h2 className="text-lg font-semibold">Buổi học sắp tới</h2>
            <Button icon={CalendarDays} variant="ghost" size="sm" onClick={() => navigate('/admin/academic/sessions')}>Xem lịch</Button>
          </div>
          {upcomingSessions(4).map((s) => (
            <SessionCard key={s.id} session={s} showTeacher />
          ))}
        </section>

        <section className="flex flex-col gap-3">
          <h2 className="text-lg font-semibold">Đơn chờ duyệt</h2>
          <div className="flex flex-col divide-y divide-line rounded-[var(--radius-card)] border border-line bg-surface">
            {requests.length === 0 && <p className="p-6 text-center text-ink-muted">Không còn đơn nào chờ duyệt 🎉</p>}
            {requests.map((r) => (
              <div key={r.id} className="flex flex-col gap-3 p-4">
                <div className="flex items-start justify-between gap-2">
                  <UserCell name={r.who} subtitle={r.detail} size="sm" />
                  <StatusTag group="request" status="pending" size="sm" />
                </div>
                <div className="flex items-center justify-between gap-2">
                  <span className="text-[13px] font-medium text-ink-secondary">{r.type}</span>
                  <div className="flex gap-2">
                    <Button action="reject" size="sm" onClick={() => decide(r.id, false)} />
                    <Button action="approve" size="sm" onClick={() => decide(r.id, true)} />
                  </div>
                </div>
              </div>
            ))}
          </div>
        </section>
      </div>
      {confirmDialog}
    </>
  );
}
