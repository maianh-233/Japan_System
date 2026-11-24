import dayjs from 'dayjs';
import { AlarmClock, BarChart3, CalendarDays, ClipboardCheck, Inbox, PlugZap, Target, TriangleAlert, Users, Video, Wallet } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { Button, PageHeader, StatCard, StatusTag, UserCell, useNotify } from '@/components/common';
import { useClassStore } from '@/features/academic/classes/classStore';
import { teacherNames } from '@/features/academic/classes/constants';
import { sessionRoom, sessionTeachers } from '@/features/academic/classes/sessionUtils';
import { useAdmissionStore } from '@/features/crm/admission/admissionStore';
import { slaOf } from '@/features/crm/admission/routing';
import { errorText } from '@/features/crm/crmApi';
import { isSlaOverdue, useCrmStore } from '@/features/crm/crmStore';
import { TodayWork } from '@/features/dashboard/TodayWork';
import { REQUEST_TYPES } from '@/features/hr/constants';
import { useInbox, sortPending } from '@/features/inbox/useInbox';
import { shortCode } from '@/features/portal/usePortal';
import { shortMoney } from '@/features/reports/charts';
import { loadDashboard, useServerData, type DashboardAlert } from '@/features/reports/reportsApi';
import { useReports } from '@/features/reports/useReports';
import { useSystemStore } from '@/features/system/systemStore';
import { useAuth } from '@/hooks/useAuth';
import { USE_API } from '@/lib/api/config';
import { cn } from '@/lib/cn';
import { fromNow } from '@/lib/date';
import { formatCurrency } from '@/lib/format';
import { ALL_BRANCHES } from '@/types/auth';

const ALERT_ICON: Record<DashboardAlert['kind'], typeof AlarmClock> = {
  lead_sla_overdue: AlarmClock,
  unconfirmed_sessions: ClipboardCheck,
  missing_teacher_logs: ClipboardCheck,
  low_fill_classes: Users,
  integration_error: PlugZap,
};

/**
 * Bảng điều khiển nhân viên — số liệu theo chi nhánh đang chọn (BC-01).
 * Chế độ API: 4 thẻ + "Cần xử lý" từ GET /api/v1/reporting/dashboard; buổi học hôm nay, đơn chờ duyệt dùng store đã nối API.
 */
export default function StaffDashboard() {
  const { user, branchId, currentBranch } = useAuth();
  const navigate = useNavigate();
  const report = useReports(branchId, 2);
  const classes = useClassStore((s) => s.classes);
  const sessions = useClassStore((s) => s.sessions);
  const leads = useCrmStore((s) => s.leads);
  const rules = useAdmissionStore((s) => s.rules);
  const integrations = useSystemStore((s) => s.integrations);
  const inbox = useInbox();
  const notify = useNotify();
  const dash = useServerData(() => loadDashboard(branchId), [branchId], (e) => notify.toast.error(errorText(e, 'Không tải được bảng điều khiển')));

  const inB = (b?: string) => branchId === ALL_BRANCHES || b === branchId;
  const clsOf = (id: string) => classes.find((c) => c.id === id);
  const today = dayjs().format('YYYY-MM-DD');

  const todaySessions = sessions.filter((s) => s.date === today && inB(clsOf(s.classId)?.branchId)).sort((a, b) => a.startTime.localeCompare(b.startTime));
  const pending = sortPending(inbox).slice(0, 5);

  const slaLeads = leads.filter((l) => !l.hidden && inB(l.branchId) && isSlaOverdue(l, slaOf(rules, l))).length;
  const unconfirmed = sessions.filter((s) => s.status === 'completed' && !s.confirmation && dayjs(s.date).isAfter(dayjs().subtract(14, 'day')) && inB(clsOf(s.classId)?.branchId)).length;
  const noLog = sessions.filter((s) => s.status === 'completed' && !s.teacherLog && dayjs(s.date).isAfter(dayjs().subtract(7, 'day')) && inB(clsOf(s.classId)?.branchId)).length;
  const badIntegrations = integrations.filter((i) => i.status === 'error');
  const lowFill = report.study.fill.filter((c) => c.rate < 50);
  const growth = report.money.prevMonth ? Math.round(((report.money.thisMonth - report.money.prevMonth) / report.money.prevMonth) * 1000) / 10 : undefined;
  const leadThis = report.lead.byMonth[report.lead.byMonth.length - 1]?.value ?? 0;
  const leadPrev = report.lead.byMonth[report.lead.byMonth.length - 2]?.value ?? 0;

  const localTodos = [
    { show: slaLeads > 0, icon: AlarmClock, tone: 'danger', text: `${slaLeads} lead mới quá hạn liên hệ (SLA)`, to: '/admin/crm/lead-rules' },
    { show: unconfirmed > 0, icon: ClipboardCheck, tone: 'warning', text: `${unconfirmed} buổi đã dạy chưa xác nhận (ảnh hưởng lương GV)`, to: '/admin/academic/sessions' },
    { show: noLog > 0, icon: ClipboardCheck, tone: 'warning', text: `${noLog} buổi tuần qua GV chưa ghi nhật ký`, to: '/admin/academic/sessions' },
    { show: lowFill.length > 0, icon: Users, tone: 'warning', text: `${lowFill.length} lớp lấp đầy dưới 50%: ${lowFill.slice(0, 3).map((c) => c.label).join(', ')}`, to: '/admin/academic/classes' },
    ...badIntegrations.map((i) => ({ show: true, icon: PlugZap, tone: 'danger', text: `Tích hợp ${i.name} lỗi: ${i.error?.split('—')[0] ?? ''}`, to: '/admin/system/integrations' })),
  ].filter((t) => t.show);
  const todos = USE_API
    ? (dash?.alerts ?? []).map((a) => ({ show: true, icon: ALERT_ICON[a.kind] ?? TriangleAlert, tone: a.tone, text: a.text, to: a.path }))
    : localTodos;

  // 4 thẻ chỉ số: API lấy từ server, mock tính từ store
  const kpi = USE_API
    ? {
        studying: dash?.studying ?? 0,
        ongoing: dash?.ongoingClasses ?? 0,
        leadThis: dash?.leadsThisMonth ?? 0,
        leadTrend: dash?.leadGrowth,
        thisMonth: dash?.collectedThisMonth ?? 0,
        growth: dash?.revenueGrowth,
        overdue: dash?.overdue ?? 0,
        debt: dash?.debt ?? 0,
      }
    : {
        studying: report.study.studying,
        ongoing: report.study.ongoing,
        leadThis,
        leadTrend: leadPrev ? Math.round(((leadThis - leadPrev) / leadPrev) * 100) : undefined,
        thisMonth: report.money.thisMonth,
        growth,
        overdue: report.money.overdue,
        debt: report.money.debt,
      };

  return (
    <>
      <PageHeader
        title={`Xin chào, ${user?.fullName.split(' ').pop()} 👋`}
        subtitle={`${dayjs().format('dddd, DD/MM/YYYY')} · ${currentBranch ? `Chi nhánh ${currentBranch.name}` : 'Toàn hệ thống'}`}
        actions={
          <Button icon={BarChart3} variant="outline" collapseOnMobile onClick={() => navigate('/admin/dashboard/reports')}>
            Báo cáo
          </Button>
        }
      />

      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-4">
        <StatCard label="Học viên đang học" value={kpi.studying} icon={Users} hint={`${kpi.ongoing} lớp đang học`} onClick={() => navigate('/admin/students/profiles')} />
        <StatCard label="Lead mới tháng này" value={kpi.leadThis} icon={Target} tone="info" trend={kpi.leadTrend} trendLabel="so với tháng trước" onClick={() => navigate('/admin/crm/leads')} />
        <StatCard label="Thực thu tháng này" value={formatCurrency(kpi.thisMonth)} icon={Wallet} tone="success" trend={kpi.growth} trendLabel="so với tháng trước" onClick={() => navigate('/admin/finance/invoices')} />
        <StatCard label="Công nợ quá hạn" value={shortMoney(kpi.overdue)} icon={TriangleAlert} tone="warning" hint={`Tổng còn phải thu ${shortMoney(kpi.debt)}`} onClick={() => navigate('/admin/finance/debts')} />
      </div>

      <div className="mt-6">
        <TodayWork />
      </div>

      <div className="mt-6 grid grid-cols-1 gap-6 xl:grid-cols-[minmax(0,1fr)_400px]">
        <div className="flex flex-col gap-6">
          <section className="flex flex-col gap-3">
            <div className="flex items-center justify-between">
              <h2 className="text-lg font-semibold">Buổi học hôm nay ({todaySessions.length})</h2>
              <Button icon={CalendarDays} variant="ghost" size="sm" onClick={() => navigate('/admin/academic/sessions')}>
                Xem lịch
              </Button>
            </div>
            <ul className="flex flex-col divide-y divide-line rounded-[var(--radius-card)] border border-line bg-surface">
              {todaySessions.map((s) => {
                const c = clsOf(s.classId)!;
                const started = dayjs(`${s.date}T${s.startTime}`).isBefore(dayjs());
                const ended = dayjs(`${s.date}T${s.endTime}`).isBefore(dayjs());
                const t = s.teacherAttendance?.some((x) => x.checkIn);
                return (
                  <li key={s.id} className="flex items-center gap-3 px-4 py-3">
                    <span className="tabular w-24 shrink-0 text-[13px] font-semibold text-ink">
                      {s.startTime}–{s.endTime}
                    </span>
                    <span className="min-w-0 flex-1">
                      <span className="block truncate font-medium text-ink">
                        {shortCode(c)}
                        {s.kind === 'exam' && <span className="ml-1.5 text-xs text-danger-700">THI</span>}
                      </span>
                      <span className="flex items-center gap-1 truncate text-xs text-ink-muted">
                        {c.online && <Video size={12} aria-hidden />}
                        {teacherNames(sessionTeachers(s, c))} · {c.online ? 'Online' : sessionRoom(s, c)}
                      </span>
                    </span>
                    {s.status === 'cancelled' ? (
                      <StatusTag tone="danger" icon={TriangleAlert} size="sm">
                        Đã hủy
                      </StatusTag>
                    ) : started && !t ? (
                      <StatusTag tone="danger" icon={AlarmClock} size="sm">
                        GV chưa check-in
                      </StatusTag>
                    ) : ended ? (
                      <StatusTag tone={s.attendance ? 'success' : 'warning'} icon={ClipboardCheck} size="sm">
                        {s.attendance ? `Có mặt ${s.attendance.present}/${s.attendance.total}` : 'Chưa điểm danh'}
                      </StatusTag>
                    ) : (
                      <StatusTag tone={started ? 'primary' : 'info'} icon={CalendarDays} size="sm">
                        {started ? 'Đang học' : 'Sắp học'}
                      </StatusTag>
                    )}
                  </li>
                );
              })}
              {!todaySessions.length && <li className="p-6 text-center text-ink-muted">Hôm nay không có buổi học.</li>}
            </ul>
          </section>

          <section className="flex flex-col gap-3">
            <h2 className="text-lg font-semibold">Cần xử lý</h2>
            <ul className="flex flex-col gap-2">
              {todos.map((t, i) => (
                <li key={i}>
                  <button type="button" onClick={() => navigate(t.to)} className={cn('flex w-full items-center gap-3 rounded-xl border px-4 py-3 text-left text-[13px] hover:bg-canvas', t.tone === 'danger' ? 'border-danger-100' : 'border-warning-100')}>
                    <t.icon size={18} className={t.tone === 'danger' ? 'text-danger-600' : 'text-warning-600'} aria-hidden />
                    <span className="flex-1 text-ink">{t.text}</span>
                  </button>
                </li>
              ))}
              {!todos.length && <li className="rounded-xl border border-line p-4 text-center text-ink-muted">Mọi thứ đều ổn 🎉</li>}
            </ul>
          </section>
        </div>

        <section className="flex flex-col gap-3">
          <div className="flex items-center justify-between gap-2">
            <h2 className="text-lg font-semibold">Đơn chờ duyệt ({inbox.filter((item) => item.status === 'pending').length})</h2>
            <Button icon={Inbox} size="sm" variant="ghost" onClick={() => navigate('/admin/academic/requests')}>Mở hộp đơn</Button>
          </div>
          <div className="flex flex-col divide-y divide-line rounded-[var(--radius-card)] border border-line bg-surface">
            {pending.length === 0 && <p className="p-6 text-center text-ink-muted">Không còn đơn nào chờ duyệt 🎉</p>}
            {pending.map((p) => (
              <div key={`${p.kind}-${p.id}`} className="flex flex-col gap-3 p-4">
                <div className="flex items-start justify-between gap-2">
                  <UserCell
                    name={p.who}
                    size="sm"
                    subtitle={p.kind === 'hr'
                      ? `${p.req.code} · ${REQUEST_TYPES[p.req.type].label}`
                      : `${p.cls?.code.replace(/^GV[VN]*_/, '') ?? ''} · ${p.session ? `${dayjs(p.session.date).format('dd DD/MM')} ${p.session.startTime}` : 'Chưa có thông tin buổi học'}`}
                  />
                  <span className="shrink-0 text-xs text-ink-muted">{fromNow(p.at)}</span>
                </div>
                <div className="flex items-center justify-between gap-2">
                  <span className="text-[13px] font-medium text-ink-secondary">{p.kind === 'hr' ? REQUEST_TYPES[p.req.type].label : p.kind === 'cancel' ? 'Giáo viên xin hủy buổi' : p.req.by === 'parent' ? 'Phụ huynh xin nghỉ cho con' : 'Học viên xin nghỉ'}</span>
                  <Button action="view" size="sm" onClick={() => navigate('/admin/academic/requests')}>Xử lý</Button>
                </div>
              </div>
            ))}
          </div>
        </section>
      </div>
    </>
  );
}
