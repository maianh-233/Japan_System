/**
 * Báo cáo + bảng điều khiển (user story 17, giai đoạn 24) — backend Reporting /api/v1/reporting, chỉ đọc.
 *  - GET /reports        → cùng hình dạng `ReportData` (useReports.ts); frontend gắn nhãn nguồn lead / tuổi nợ.
 *  - GET /dashboard      → 4 thẻ chỉ số + "Cần xử lý" (BC-01).
 *  - GET /today-work     → Việc hôm nay theo quyền (BC-02); đường dẫn trong việc chứa GUID → đổi về Id frontend.
 *  - POST /reports/export → số liệu để ghi file .xlsx + server ghi nhật ký "Xuất báo cáo" (BC-07).
 * Phạm vi chi nhánh do server quyết theo quyền (chi nhánh ngoài phạm vi → chi nhánh của mình).
 */
import { useEffect, useState } from 'react';
import { LEAD_SOURCES } from '@/features/crm/constants';
import type { LeadSource } from '@/features/crm/types';
import { AGING } from '@/features/finance/constants';
import { USE_API } from '@/lib/api/config';
import { api } from '@/lib/api/http';
import { fromGuid, toGuid } from '@/lib/api/ids';
import { ALL_BRANCHES } from '@/types/auth';
import { useReports, type MonthPoint, type ReportData } from './useReports';

interface ServerPoint {
  month: string;
  label: string;
  value?: number | null;
}

interface ServerReport {
  months: number;
  branchId?: string | null;
  monthKeys: string[];
  lead: {
    total: number;
    won: number;
    rate?: number | null;
    open: number;
    funnel: { key: string; value: number }[];
    bySource: { key: LeadSource; leads: number; won: number; rate?: number | null }[];
    bySale: { key: string; label: string; leads: number; won: number; open: number; rate?: number | null }[];
    byCampaign: { key: string; label: string; name: string; spent: number; leads: number; won: number; cpl?: number | null; cpa?: number | null }[];
    byMonth: ServerPoint[];
  };
  money: {
    revenue: ServerPoint[];
    thisMonth: number;
    prevMonth: number;
    growth?: number | null;
    booked: number;
    collected: number;
    debt: number;
    overdue: number;
    aging: { key: string; count: number; amount: number }[];
  };
  study: {
    studying: number;
    ongoing: number;
    enrolling: number;
    newEnroll: ServerPoint[];
    attendanceByMonth: ServerPoint[];
    fill: { key: string; label: string; course: string; enrolled: number; max: number; rate: number; status: 'ongoing' | 'enrolling' }[];
    roomUse: { key: string; label: string; branchId: string; hours: number; rate: number }[];
    roomUseAvg?: number | null;
  };
  branches: {
    key: string;
    label: string;
    leads: number;
    conv?: number | null;
    students: number;
    classes: number;
    fill?: number | null;
    attendance?: number | null;
    collected: number;
    thisMonth: number;
    debt: number;
    overdue: number;
    needsAttention: boolean;
  }[];
}

export interface DashboardAlert {
  kind: 'lead_sla_overdue' | 'unconfirmed_sessions' | 'missing_teacher_logs' | 'low_fill_classes' | 'integration_error';
  tone: 'danger' | 'warning';
  text: string;
  path: string;
}

export interface DashboardData {
  studying: number;
  ongoingClasses: number;
  leadsThisMonth: number;
  leadsPrevMonth: number;
  leadGrowth?: number;
  collectedThisMonth: number;
  collectedPrevMonth: number;
  revenueGrowth?: number;
  debt: number;
  overdue: number;
  alerts: DashboardAlert[];
}

export interface ServerWorkBoard {
  role: 'sale' | 'academic' | 'finance';
  label: string;
  count: number;
  groups: {
    key: string;
    label: string;
    allPath: string;
    empty: string;
    items: { key: string; title: string; subtitle?: string | null; meta: string; tone: 'danger' | 'warning' | 'info'; path: string }[];
  }[];
}

const u = <T,>(v: T | null | undefined) => v ?? undefined;
const branchParam = (branchId: string) => (branchId === ALL_BRANCHES ? '' : `branchId=${toGuid(branchId)}`);
/** Mã lớp ngắn như frontend (bỏ tiền tố GV / GVV / GVN_) */
const shortClass = (code: string) => code.replace(/^GV[VN]*_/, '');
/** Đường dẫn server chứa GUID (…?open=<guid>, /classes/<guid>) → Id frontend */
const GUID = /[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/gi;
export const localPath = (path: string) => path.replace(GUID, (g) => fromGuid(g));

/** Điểm theo tháng; không có số (chuyên cần tháng chưa điểm danh) → 0 trên biểu đồ + cờ `empty` */
const point = (p: ServerPoint): MonthPoint => ({ month: p.month, label: p.label, value: p.value ?? 0, empty: p.value == null || undefined });

export function toReportData(r: ServerReport): ReportData {
  return {
    monthKeys: r.monthKeys,
    lead: {
      total: r.lead.total,
      won: r.lead.won,
      rate: u(r.lead.rate),
      funnel: r.lead.funnel as ReportData['lead']['funnel'],
      bySource: r.lead.bySource.map((s) => ({ key: s.key, label: LEAD_SOURCES[s.key]?.label ?? s.key, leads: s.leads, won: s.won, rate: u(s.rate) })),
      bySale: r.lead.bySale.map((s) => ({ key: fromGuid(s.key), label: s.label, leads: s.leads, won: s.won, open: s.open, rate: u(s.rate) })),
      byCampaign: r.lead.byCampaign.map((c) => ({ key: fromGuid(c.key), label: c.label, name: c.name, spent: c.spent, leads: c.leads, won: c.won, cpl: u(c.cpl), cpa: u(c.cpa) })),
      byMonth: r.lead.byMonth.map(point),
    },
    money: {
      revenue: r.money.revenue.map(point),
      thisMonth: r.money.thisMonth,
      prevMonth: r.money.prevMonth,
      booked: r.money.booked,
      collected: r.money.collected,
      debt: r.money.debt,
      overdue: r.money.overdue,
      aging: AGING.map((a) => {
        const x = r.money.aging.find((g) => g.key === a.key);
        return { key: a.key, label: a.label, count: x?.count ?? 0, amount: x?.amount ?? 0 };
      }),
    },
    study: {
      studying: r.study.studying,
      newEnroll: r.study.newEnroll.map(point),
      attendanceByMonth: r.study.attendanceByMonth.map(point),
      fill: r.study.fill.map((c) => ({ key: fromGuid(c.key), label: shortClass(c.label), course: c.course, enrolled: c.enrolled, max: c.max, rate: c.rate, status: c.status })),
      roomUse: r.study.roomUse.map((x) => ({ key: fromGuid(x.key), label: x.label, branchId: fromGuid(x.branchId), hours: x.hours, rate: x.rate })),
      ongoing: r.study.ongoing,
    },
    branches: r.branches.map((b) => ({
      key: fromGuid(b.key),
      label: b.label,
      leads: b.leads,
      conv: u(b.conv),
      students: b.students,
      classes: b.classes,
      fill: u(b.fill),
      attendance: u(b.attendance),
      collected: b.collected,
      thisMonth: b.thisMonth,
      debt: b.debt,
      overdue: b.overdue,
    })),
  };
}

export async function loadReport(branchId: string, months: number): Promise<ReportData> {
  const q = [branchParam(branchId), `months=${months}`].filter(Boolean).join('&');
  return toReportData(await api<ServerReport>(`/reporting/reports?${q}`));
}

/** BC-07 — server ghi nhật ký, trả số liệu để ghi file */
export async function exportReport(branchId: string, months: number): Promise<ReportData> {
  const body = { branchId: branchId === ALL_BRANCHES ? null : toGuid(branchId), months };
  return toReportData(await api<ServerReport>('/reporting/reports/export', { method: 'POST', body }));
}

export async function loadDashboard(branchId: string): Promise<DashboardData> {
  const d = await api<DashboardData & { leadGrowth?: number | null; revenueGrowth?: number | null }>(`/reporting/dashboard?${branchParam(branchId)}`);
  return { ...d, leadGrowth: u(d.leadGrowth), revenueGrowth: u(d.revenueGrowth), alerts: d.alerts.map((a) => ({ ...a, path: localPath(a.path) })) };
}

export async function loadTodayWork(branchId: string): Promise<ServerWorkBoard[]> {
  const boards = await api<ServerWorkBoard[]>(`/reporting/today-work?${branchParam(branchId)}`);
  return boards.map((b) => ({
    ...b,
    groups: b.groups.map((g) => ({ ...g, allPath: localPath(g.allPath), items: g.items.map((i) => ({ ...i, path: localPath(i.path) })) })),
  }));
}

/**
 * Hook tải số liệu từ server (chế độ API) — nạp lại khi `deps` đổi; `undefined` = đang tải lần đầu.
 */
export function useServerData<T>(load: () => Promise<T>, deps: unknown[], onError: (e: unknown) => void): T | undefined {
  const [data, setData] = useState<{ key: string; value: T }>();
  const key = JSON.stringify(deps);
  useEffect(() => {
    if (!USE_API) return;
    let live = true;
    load().then(
      (value) => live && setData({ key, value }),
      (e) => live && onError(e),
    );
    return () => {
      live = false;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [key]);
  // Đổi kỳ / chi nhánh: giữ số cũ tới khi có số mới (không nháy trắng)
  return data?.value;
}

/** Báo cáo: chế độ API lấy từ server; chế độ mock tính từ store như trước. `undefined` = đang tải. */
export function useReportData(branchId: string, months: number, onError: (e: unknown) => void): ReportData | undefined {
  const local = useReports(branchId, months);
  const server = useServerData(() => loadReport(branchId, months), [branchId, months], onError);
  return USE_API ? server : local;
}
