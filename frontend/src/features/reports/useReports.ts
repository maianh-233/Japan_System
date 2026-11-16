import { useMemo } from 'react';
import dayjs from 'dayjs';
import { useClassStore } from '@/features/academic/classes/classStore';
import { sessionRoom } from '@/features/academic/classes/sessionUtils';
import { useRoomStore } from '@/features/academic/rooms/roomStore';
import { useAdmissionStore } from '@/features/crm/admission/admissionStore';
import { LEAD_SOURCES, salesOf } from '@/features/crm/constants';
import { useHrStore } from '@/features/hr/hrStore';
import { useCrmStore } from '@/features/crm/crmStore';
import type { LeadSource } from '@/features/crm/types';
import { AGING } from '@/features/finance/constants';
import { agingOf, useInvoiceRows } from '@/features/finance/financeUtils';
import { useStudentStore } from '@/features/student/studentStore';
import { getBranches } from '@/features/system/useBranches';
import { ALL_BRANCHES } from '@/types/auth';

/**
 * Số liệu BÁO CÁO tính trực tiếp từ các store (lead, ghi danh, hóa đơn, buổi học, phòng).
 * Khi có backend: thay bằng API tổng hợp phía server — giữ nguyên hình dạng dữ liệu trả về.
 */

export interface MonthPoint {
  month: string;
  label: string;
  value: number;
}

const pct = (a: number, b: number) => (b ? Math.round((a / b) * 100) : undefined);
/** Giờ mở cửa / tuần dùng tính công suất phòng: T2–T7, 8:00–21:30 */
const OPEN_HOURS_WEEK = 6 * 13.5;
const toMin = (t: string) => Number(t.slice(0, 2)) * 60 + Number(t.slice(3, 5));

export function useReports(branchId: string, months: number) {
  const leads = useCrmStore((s) => s.leads);
  const campaigns = useAdmissionStore((s) => s.campaigns);
  const enrollments = useStudentStore((s) => s.enrollments);
  const classes = useClassStore((s) => s.classes);
  const sessions = useClassStore((s) => s.sessions);
  const rooms = useRoomStore((s) => s.rooms);
  const invoiceRows = useInvoiceRows();
  const employees = useHrStore((s) => s.employees);

  return useMemo(() => {
    const inB = (b?: string) => branchId === ALL_BRANCHES || b === branchId;
    const from = dayjs().subtract(months - 1, 'month').startOf('month');
    const monthKeys = Array.from({ length: months }, (_, i) => from.add(i, 'month').format('YYYY-MM'));
    const label = (m: string) => `T${Number(m.slice(5))}${m.slice(0, 4) !== dayjs().format('YYYY') ? `/${m.slice(2, 4)}` : ''}`;
    const classBranch = new Map(classes.map((c) => [c.id, c.branchId]));

    // ---------- Tuyển sinh ----------
    const ls = leads.filter((l) => !l.hidden && inB(l.branchId) && !dayjs(l.createdAt).isBefore(from));
    const won = ls.filter((l) => l.status === 'won');
    const funnel = (['new', 'consulting', 'test_scheduled', 'won', 'lost'] as const).map((st) => ({ key: st, value: ls.filter((l) => l.status === st).length }));
    const bySource = (Object.keys(LEAD_SOURCES) as LeadSource[])
      .map((src) => {
        const x = ls.filter((l) => l.source === src);
        const w = x.filter((l) => l.status === 'won').length;
        return { key: src, label: LEAD_SOURCES[src].label, leads: x.length, won: w, rate: pct(w, x.length) };
      })
      .filter((x) => x.leads)
      .sort((a, b) => b.leads - a.leads);
    const bySale = salesOf(employees, branchId)
      .map((s) => {
        const x = ls.filter((l) => l.assigneeId === s.id);
        const w = x.filter((l) => l.status === 'won').length;
        return { key: s.id, label: s.fullName, leads: x.length, won: w, open: x.filter((l) => ['new', 'consulting', 'test_scheduled'].includes(l.status)).length, rate: pct(w, x.length) };
      })
      .sort((a, b) => b.won - a.won);
    const byCampaign = campaigns
      .filter((c) => branchId === ALL_BRANCHES || c.branchIds.includes(branchId))
      .map((c) => {
        const x = leads.filter((l) => l.campaignId === c.id && !l.hidden);
        const w = x.filter((l) => l.status === 'won').length;
        return { key: c.id, label: c.code, name: c.name, spent: c.spent, leads: x.length, won: w, cpl: x.length ? c.spent / x.length : undefined, cpa: w ? c.spent / w : undefined };
      })
      .filter((c) => c.leads || c.spent)
      .sort((a, b) => (a.cpa ?? Infinity) - (b.cpa ?? Infinity));
    const leadsByMonth: MonthPoint[] = monthKeys.map((m) => ({ month: m, label: label(m), value: ls.filter((l) => l.createdAt.startsWith(m)).length }));

    // ---------- Doanh thu & công nợ ----------
    const rows = invoiceRows.filter((r) => inB(r.branchId));
    const collectedIn = (m: string, b?: string) =>
      rows
        .filter((r) => !b || r.branchId === b)
        .reduce((n, r) => n + r.invoice.payments.filter((p) => p.paidAt.startsWith(m)).reduce((x, p) => x + p.amount, 0) - r.invoice.refunds.filter((f) => f.refundedAt.startsWith(m)).reduce((x, f) => x + f.amount, 0), 0);
    const revenue: MonthPoint[] = monthKeys.map((m) => ({ month: m, label: label(m), value: collectedIn(m) }));
    const thisMonth = dayjs().format('YYYY-MM');
    const prevMonth = dayjs().subtract(1, 'month').format('YYYY-MM');
    const debtRows = rows.filter((r) => r.sum.remaining > 0);
    const aging = AGING.map((a) => {
      const x = debtRows.filter((r) => agingOf(r) === a.key);
      return { key: a.key, label: a.label, count: x.length, amount: x.reduce((n, r) => n + r.sum.remaining, 0) };
    });
    const booked = rows.filter((r) => !dayjs(r.invoice.issuedAt).isBefore(from)).reduce((n, r) => n + r.sum.total, 0);

    // ---------- Đào tạo ----------
    const studying = enrollments.filter((e) => e.status === 'studying' && inB(classBranch.get(e.classId)));
    const newEnroll: MonthPoint[] = monthKeys.map((m) => ({ month: m, label: label(m), value: enrollments.filter((e) => e.enrolledAt.startsWith(m) && inB(classBranch.get(e.classId))).length }));
    const attendanceByMonth: MonthPoint[] = monthKeys.map((m) => {
      let tot = 0;
      let att = 0;
      sessions.forEach((s) => {
        if (!s.date.startsWith(m) || !s.records || !inB(classBranch.get(s.classId))) return;
        s.records.forEach((r) => {
          tot++;
          if (r.status === 'present' || r.status === 'late') att++;
        });
      });
      return { month: m, label: label(m), value: pct(att, tot) ?? 0 };
    });
    const ongoing = classes.filter((c) => (c.status === 'ongoing' || c.status === 'enrolling') && inB(c.branchId));
    const fill = ongoing
      .map((c) => {
        const n = enrollments.filter((e) => e.classId === c.id && (e.status === 'studying' || e.status === 'reserved')).length;
        return { key: c.id, label: c.code.replace(/^GV[VN]*_/, ''), course: c.courseLabel, enrolled: n, max: c.maxStudents, rate: pct(n, c.maxStudents) ?? 0, status: c.status };
      })
      .sort((a, b) => a.rate - b.rate);
    // Công suất phòng tuần này
    const wkFrom = dayjs().startOf('week').add(1, 'day').format('YYYY-MM-DD');
    const wkTo = dayjs().startOf('week').add(7, 'day').format('YYYY-MM-DD');
    const roomUse = rooms
      .filter((r) => inB(r.branchId))
      .map((r) => {
        const mins = sessions
          .filter((s) => s.date >= wkFrom && s.date <= wkTo && s.status !== 'cancelled')
          .filter((s) => {
            const c = classes.find((k) => k.id === s.classId);
            return c && c.branchId === r.branchId && sessionRoom(s, c) === r.name;
          })
          .reduce((n, s) => n + toMin(s.endTime) - toMin(s.startTime), 0);
        return { key: r.id, label: r.name, branchId: r.branchId, hours: mins / 60, rate: Math.round((mins / 60 / OPEN_HOURS_WEEK) * 100) };
      })
      .sort((a, b) => b.rate - a.rate);

    // ---------- So sánh chi nhánh ----------
    const branches = getBranches(false).filter((b) => inB(b.id)).map((b) => {
      const bl = leads.filter((l) => !l.hidden && l.branchId === b.id && !dayjs(l.createdAt).isBefore(from));
      const bw = bl.filter((l) => l.status === 'won').length;
      const br = invoiceRows.filter((r) => r.branchId === b.id);
      let tot = 0;
      let att = 0;
      sessions.forEach((s) => {
        if (classBranch.get(s.classId) !== b.id || !s.records || dayjs(s.date).isBefore(from)) return;
        s.records.forEach((r) => {
          tot++;
          if (r.status === 'present' || r.status === 'late') att++;
        });
      });
      const bc = classes.filter((c) => c.branchId === b.id && (c.status === 'ongoing' || c.status === 'enrolling'));
      const seats = bc.reduce((n, c) => n + c.maxStudents, 0);
      const filled = enrollments.filter((e) => bc.some((c) => c.id === e.classId) && (e.status === 'studying' || e.status === 'reserved')).length;
      return {
        key: b.id,
        label: b.name,
        leads: bl.length,
        conv: pct(bw, bl.length),
        students: new Set(enrollments.filter((e) => e.status === 'studying' && classBranch.get(e.classId) === b.id).map((e) => e.studentId)).size,
        classes: bc.length,
        fill: pct(filled, seats),
        attendance: pct(att, tot),
        collected: monthKeys.reduce((n, m) => n + collectedIn(m, b.id), 0),
        thisMonth: collectedIn(thisMonth, b.id),
        debt: br.reduce((n, r) => n + r.sum.remaining, 0),
        overdue: br.filter((r) => r.sum.status === 'overdue').reduce((n, r) => n + r.sum.remaining, 0),
      };
    });

    return {
      monthKeys,
      lead: { total: ls.length, won: won.length, rate: pct(won.length, ls.length), funnel, bySource, bySale, byCampaign, byMonth: leadsByMonth },
      money: {
        revenue,
        thisMonth: collectedIn(thisMonth),
        prevMonth: collectedIn(prevMonth),
        booked,
        collected: revenue.reduce((n, p) => n + p.value, 0),
        debt: debtRows.reduce((n, r) => n + r.sum.remaining, 0),
        overdue: debtRows.filter((r) => r.sum.status === 'overdue').reduce((n, r) => n + r.sum.remaining, 0),
        aging,
      },
      study: {
        studying: new Set(studying.map((e) => e.studentId)).size,
        newEnroll,
        attendanceByMonth,
        fill,
        roomUse,
        ongoing: ongoing.filter((c) => c.status === 'ongoing').length,
      },
      branches,
    };
  }, [leads, campaigns, enrollments, classes, sessions, rooms, invoiceRows, employees, branchId, months]);
}

export type ReportData = ReturnType<typeof useReports>;
