import { useMemo } from 'react';
import dayjs from 'dayjs';
import { useClassStore } from '@/features/academic/classes/classStore';
import type { ClassInfo, ClassSession, StaffRole } from '@/features/academic/classes/types';
import { staffOf } from '@/features/academic/sessions/sessionOps';
import { useCrmStore } from '@/features/crm/crmStore';
import type { Lead, LeadStudentLink, Student } from '@/features/crm/types';
import { useHrStore } from '@/features/hr/hrStore';
import { dayState, onLeave, toMin } from '@/features/hr/hrUtils';
import type { Employee, HrRequest, TimeRecord } from '@/features/hr/types';
import { summarizeInvoice, useStudentStore, type InvoiceSummary } from '@/features/student/studentStore';
import type { Enrollment, Invoice } from '@/features/student/types';
import type { ShiftAssignment } from '@/features/work/types';
import { unaccent } from '@/mocks/data/finance';
import { getBranches } from '@/features/system/useBranches';
import { BANK_ACCOUNTS, INSURANCE_RATE, LATE_FINE, NO_OUT_FINE, PERSONAL_DEDUCTION, STANDARD_DAYS, SUBSTITUTE_BONUS, TEACHER_LATE_FINE, type AgingKey } from './constants';
import { useFinanceStore } from './financeStore';
import type { BankTxn, CommissionTier, PayAdjust } from './types';

export { unaccent };

// ============ Hóa đơn ============

export interface InvoiceRow {
  id: string;
  invoice: Invoice;
  sum: InvoiceSummary;
  enrollment?: Enrollment;
  student?: Student;
  cls?: ClassInfo;
  branchId: string;
  saleId?: string;
}

/**
 * Sale được ghi nhận doanh số:
 *  1. Kế toán ghi đè tay (trang Hoa hồng)
 *  2. Sale PHỤ TRÁCH lead đã liên kết với học viên (lead đã chốt ưu tiên, rồi lead mới nhất) — người thực sự tư vấn
 *  3. Không có lead (khách vãng lai, hồ sơ cũ) → chia đều Sale của chi nhánh (ổn định theo mã học viên)
 */
export function saleOfInvoice(inv: Invoice, studentId: string | undefined, branchId: string, employees: Employee[], overrides: Record<string, string>, leadSale?: Map<string, string>) {
  if (overrides[inv.id]) return overrides[inv.id];
  // Chế độ API: server đã chọn Sale lúc lập hóa đơn / kế toán gán lại
  if (inv.saleId) return inv.saleId;
  const fromLead = studentId ? leadSale?.get(studentId) : undefined;
  if (fromLead && employees.some((e) => e.id === fromLead)) return fromLead;
  const sales = employees.filter((e) => e.department === 'sales' && e.branchId === branchId && e.status !== 'quit').sort((a, b) => a.id.localeCompare(b.id));
  if (!sales.length || !studentId) return undefined;
  const h = [...studentId].reduce((n, c) => n + c.charCodeAt(0), 0);
  return sales[h % sales.length].id;
}

/** Học viên → Sale phụ trách lead liên kết (lead đã chốt trước, rồi lead mới nhất) — dùng cả ở scripts/export-seed.ts */
export function leadSaleMap(leads: Lead[], links: LeadStudentLink[]) {
  const m = new Map<string, string>();
  const byStudent = new Map<string, Lead[]>();
  links.forEach((k) => {
    const l = leads.find((x) => x.id === k.leadId);
    if (l?.assigneeId) byStudent.set(k.studentId, [...(byStudent.get(k.studentId) ?? []), l]);
  });
  byStudent.forEach((ls, sid) => {
    const best = [...ls].sort((a, b) => Number(b.status === 'won') - Number(a.status === 'won') || b.createdAt.localeCompare(a.createdAt))[0];
    m.set(sid, best.assigneeId!);
  });
  return m;
}

/** Bảng hóa đơn đã ghép học viên / lớp / chi nhánh / Sale */
export function useInvoiceRows(): InvoiceRow[] {
  const invoices = useStudentStore((s) => s.invoices);
  const enrollments = useStudentStore((s) => s.enrollments);
  const students = useCrmStore((s) => s.students);
  const classes = useClassStore((s) => s.classes);
  const employees = useHrStore((s) => s.employees);
  const overrides = useFinanceStore((s) => s.saleOverrides);
  const leads = useCrmStore((s) => s.leads);
  const links = useCrmStore((s) => s.links);
  return useMemo(() => {
    const leadSale = leadSaleMap(leads, links);
    const enr = new Map(enrollments.map((e) => [e.id, e]));
    const st = new Map(students.map((s) => [s.id, s]));
    const cl = new Map(classes.map((c) => [c.id, c]));
    return invoices.map((invoice) => {
      const enrollment = enr.get(invoice.enrollmentId);
      const student = enrollment ? st.get(enrollment.studentId) : undefined;
      const cls = enrollment ? cl.get(enrollment.classId) : undefined;
      const branchId = cls?.branchId ?? student?.branchId ?? '';
      return { id: invoice.id, invoice, sum: summarizeInvoice(invoice), enrollment, student, cls, branchId, saleId: saleOfInvoice(invoice, student?.id, branchId, employees, overrides, leadSale) };
    });
  }, [invoices, enrollments, students, classes, employees, overrides, leads, links]);
}

/** Tuổi nợ theo hạn đóng */
export function agingOf(row: InvoiceRow): AgingKey | undefined {
  if (row.sum.remaining <= 0) return undefined;
  const late = dayjs().startOf('day').diff(dayjs(row.invoice.dueDate), 'day');
  return late <= 0 ? 'not_due' : late <= 30 ? 'd30' : late <= 60 ? 'd60' : 'd60p';
}
export const daysLate = (row: InvoiceRow) => Math.max(0, dayjs().startOf('day').diff(dayjs(row.invoice.dueDate), 'day'));

/** Nội dung chuyển khoản chuẩn của hóa đơn */
/** Nội dung chuyển khoản: server lập sẵn (chế độ API); dữ liệu giả ghép mã + tên không dấu */
export const transferContent = (row: InvoiceRow) => row.invoice.transferContent ?? `${row.invoice.code} ${unaccent(row.student?.fullName ?? '')}`.trim();

export interface BankAccountInfo {
  bank: string;
  no: string;
  name: string;
}

/** Tài khoản nhận VietQR của chi nhánh (Hệ thống › Chi nhánh); chưa khai báo → dữ liệu giả cũ / không có */
export function bankOfBranch(branchId: string): BankAccountInfo | undefined {
  const b = getBranches(false).find((x) => x.id === branchId)?.bank;
  return b?.account ? { bank: b.bank, no: b.account, name: b.holder } : BANK_ACCOUNTS[branchId];
}

/** Ngân hàng theo số tài khoản nhận của giao dịch (dữ liệu giả lưu mã chi nhánh) */
export function bankOfAccount(account: string): BankAccountInfo | undefined {
  const branch = getBranches(false).find((x) => x.bank?.account === account);
  return branch ? bankOfBranch(branch.id) : BANK_ACCOUNTS[account];
}

// ============ Đối soát ============

export interface MatchSuggestion {
  row: InvoiceRow;
  score: number;
  reasons: string[];
}

/** Gợi ý hóa đơn cho giao dịch: mã HĐ trong nội dung (chắc chắn) > tên + số tiền khớp */
export function suggestMatches(txn: BankTxn, rows: InvoiceRow[]): MatchSuggestion[] {
  const content = unaccent(txn.content);
  const codes = content.match(/HD\s?\d{3,}/g)?.map((c) => c.replace(/\s/g, '')) ?? [];
  return rows
    .filter((r) => r.sum.remaining > 0)
    .map((row) => {
      const reasons: string[] = [];
      let score = 0;
      if (codes.includes(row.invoice.code.toUpperCase())) (score += 100, reasons.push('Đúng mã hóa đơn'));
      const name = unaccent(row.student?.fullName ?? '');
      const last = name.split(' ').pop() ?? '';
      if (name && (content.includes(name) || unaccent(txn.payer) === name)) (score += 40, reasons.push('Trùng tên học viên'));
      else if (last.length > 2 && content.includes(last)) (score += 10, reasons.push(`Có tên "${last}"`));
      if (row.cls && content.includes(unaccent(row.cls.code))) (score += 20, reasons.push('Có mã lớp'));
      if (txn.amount === row.sum.remaining) (score += 30, reasons.push('Đúng số còn nợ'));
      else if (txn.amount < row.sum.remaining) score += 5;
      else score -= 50;
      if (row.branchId === txn.branchId) score += 5;
      return { row, score, reasons };
    })
    .filter((m) => m.score >= 30)
    .sort((a, b) => b.score - a.score)
    .slice(0, 5);
}

/** Tự khớp chỉ khi có mã hóa đơn hợp lệ và số tiền không vượt số còn nợ */
export const autoMatchable = (txn: BankTxn, rows: InvoiceRow[]) => {
  const best = suggestMatches(txn, rows)[0];
  return best && best.reasons.includes('Đúng mã hóa đơn') && txn.amount <= best.row.sum.remaining ? best.row : undefined;
};

// ============ Lương giáo viên ============

export interface TeachItem {
  session: ClassSession;
  cls: ClassInfo;
  role: StaffRole;
  minutes: number;
  online: boolean;
  rate: number;
  amount: number;
  confirmed: boolean;
  late: boolean;
  bonus: number;
}

export interface TeacherPay {
  employee: Employee;
  items: TeachItem[];
  /** Buổi đã xác nhận (tính lương) */
  confirmed: TeachItem[];
  /** Buổi chưa được giáo vụ xác nhận — chưa tính */
  pending: TeachItem[];
  /** Buổi bị hủy của lớp mình trong tháng (không tính) */
  cancelled: number;
  hours: number;
  teaching: number;
  substituteBonus: number;
  lateFine: number;
  adjust: number;
  adjusts: PayAdjust[];
  base: number;
  total: number;
}

export function teacherPayroll(month: string, employees: Employee[], classes: ClassInfo[], sessions: ClassSession[], adjusts: PayAdjust[]): TeacherPay[] {
  const from = dayjs(month + '-01');
  const inMonth = sessions.filter((s) => dayjs(s.date).isSame(from, 'month'));
  const cl = new Map(classes.map((c) => [c.id, c]));
  return employees
    .filter((e) => e.kind !== 'staff' && e.status !== 'quit')
    .map((e) => {
      const items: TeachItem[] = [];
      let cancelled = 0;
      inMonth.forEach((s) => {
        const cls = cl.get(s.classId);
        if (!cls) return;
        const role = staffOf(s, cls).find((x) => x.id === e.id)?.role;
        if (!role) return;
        if (s.status === 'cancelled') return void cancelled++;
        const t = s.teacherAttendance?.find((x) => x.teacherId === e.id);
        if (!t || t.status === 'absent' || !t.checkIn || !t.checkOut) return;
        const minutes = Math.max(0, toMin(t.checkOut) - toMin(t.checkIn));
        const online = Boolean(cls.online);
        const rate = online ? (e.teacher?.rate.online ?? 0) : (e.teacher?.rate.offline ?? 0);
        const late = t.status === 'late';
        const bonus = role === 'substitute' ? SUBSTITUTE_BONUS : 0;
        items.push({ session: s, cls, role, minutes, online, rate, amount: Math.round((minutes / 60) * rate), confirmed: Boolean(s.confirmation), late, bonus });
      });
      items.sort((a, b) => (a.session.date + a.session.startTime).localeCompare(b.session.date + b.session.startTime));
      const confirmed = items.filter((i) => i.confirmed);
      const mine = adjusts.filter((a) => a.month === month && a.employeeId === e.id);
      const teaching = confirmed.reduce((n, i) => n + i.amount, 0);
      const substituteBonus = confirmed.reduce((n, i) => n + i.bonus, 0);
      const lateFine = confirmed.filter((i) => i.late).length * TEACHER_LATE_FINE;
      const adjust = mine.reduce((n, a) => n + a.amount, 0);
      const base = e.contract.baseSalary ?? 0;
      return {
        employee: e,
        items,
        confirmed,
        pending: items.filter((i) => !i.confirmed),
        cancelled,
        hours: confirmed.reduce((n, i) => n + i.minutes, 0) / 60,
        teaching,
        substituteBonus,
        lateFine,
        adjust,
        adjusts: mine,
        base,
        total: base + teaching + substituteBonus - lateFine + adjust,
      };
    });
}

// ============ Hoa hồng ============

export interface SaleResult {
  employee: Employee;
  /** Hóa đơn mới phát sinh trong tháng (ghi danh mới) */
  deals: InvoiceRow[];
  /** Doanh số ký (sau giảm giá) */
  booked: number;
  /** Thực thu trong tháng (các lần đóng trong tháng − hoàn trong tháng) */
  collected: number;
  target: number;
  kpi: number;
  tier?: CommissionTier;
  commission: number;
}

export const tierOf = (tiers: CommissionTier[], revenue: number) => [...tiers].sort((a, b) => b.from - a.from).find((t) => revenue >= t.from);

export function saleResults(month: string, employees: Employee[], rows: InvoiceRow[], tiers: CommissionTier[], targets: { employeeId: string; month: string; target: number }[]): SaleResult[] {
  const m = dayjs(month + '-01');
  return employees
    .filter((e) => e.department === 'sales' && e.status !== 'quit')
    .map((e) => {
      const mine = rows.filter((r) => r.saleId === e.id);
      const deals = mine.filter((r) => dayjs(r.invoice.issuedAt).isSame(m, 'month'));
      const collected =
        mine.reduce((n, r) => n + r.invoice.payments.filter((p) => dayjs(p.paidAt).isSame(m, 'month')).reduce((x, p) => x + p.amount, 0), 0) -
        mine.reduce((n, r) => n + r.invoice.refunds.filter((p) => dayjs(p.refundedAt).isSame(m, 'month')).reduce((x, p) => x + p.amount, 0), 0);
      const target = targets.find((t) => t.employeeId === e.id && t.month === month)?.target ?? 0;
      const tier = tierOf(tiers, collected);
      return {
        employee: e,
        deals,
        booked: deals.reduce((n, r) => n + r.sum.total, 0),
        collected,
        target,
        kpi: target ? collected / target : 0,
        tier,
        commission: Math.max(0, Math.round((collected * (tier?.percent ?? 0)) / 100)),
      };
    })
    .sort((a, b) => b.collected - a.collected);
}

// ============ Lương nhân viên ============

export interface StaffPay {
  employee: Employee;
  /** Ngày công thực tế (có chấm công) */
  worked: number;
  paidLeave: number;
  unpaidLeave: number;
  absent: number;
  late: number;
  noOut: number;
  /** Công tính lương (≤ công chuẩn) */
  payDays: number;
  base: number;
  basePay: number;
  allowance: number;
  commission: number;
  adjust: number;
  adjusts: PayAdjust[];
  gross: number;
  fines: number;
  insurance: number;
  tax: number;
  net: number;
  /** false = tháng chưa có bản ghi chấm công — server tạm lấy công chuẩn (TC-14.E2). Chế độ mock: không có */
  hasTimesheet?: boolean;
}

/** Thuế TNCN lũy tiến tháng */
export function pitOf(taxable: number) {
  if (taxable <= 0) return 0;
  const brackets: [number, number][] = [
    [5_000_000, 0.05],
    [10_000_000, 0.1],
    [18_000_000, 0.15],
    [32_000_000, 0.2],
    [52_000_000, 0.25],
    [80_000_000, 0.3],
    [Infinity, 0.35],
  ];
  let tax = 0;
  let prev = 0;
  for (const [cap, rate] of brackets) {
    if (taxable <= prev) break;
    tax += (Math.min(taxable, cap) - prev) * rate;
    prev = cap;
  }
  return Math.round(tax);
}

export function staffPayroll(
  month: string,
  employees: Employee[],
  records: TimeRecord[],
  shifts: ShiftAssignment[],
  requests: HrRequest[],
  adjusts: PayAdjust[],
  commissions: Record<string, number>,
): StaffPay[] {
  const start = dayjs(month + '-01');
  const days = Array.from({ length: start.daysInMonth() }, (_, i) => start.add(i, 'day').format('YYYY-MM-DD'));
  const recMap = new Map(records.map((r) => [`${r.employeeId}|${r.date}`, r]));
  return employees
    .filter((e) => e.kind === 'staff' && e.status !== 'quit')
    .map((e) => {
      let worked = 0;
      let paidLeave = 0;
      let unpaidLeave = 0;
      let absent = 0;
      let late = 0;
      let noOut = 0;
      days.forEach((date) => {
        if (date < e.joinedAt) return;
        const st = dayState(e, date, recMap.get(`${e.id}|${date}`), shifts, requests).state;
        if (['ok', 'late', 'early', 'late_early', 'no_out', 'working'].includes(st)) worked++;
        if (st === 'late' || st === 'late_early') late++;
        if (st === 'no_out') noOut++;
        if (st === 'absent') absent++;
        if (st === 'leave') {
          const r = onLeave(e.id, date, requests);
          if (dayjs(date).day() === 0) return;
          if (r?.type === 'business_trip' || r?.paidLeave) paidLeave++;
          else unpaidLeave++;
        }
      });
      const base = e.contract.baseSalary ?? 0;
      const allowance = e.contract.allowance ?? 0;
      const payDays = Math.min(STANDARD_DAYS, worked + paidLeave);
      const basePay = Math.round((base * payDays) / STANDARD_DAYS);
      const commission = commissions[e.id] ?? 0;
      const mine = adjusts.filter((a) => a.month === month && a.employeeId === e.id);
      const adjust = mine.reduce((n, a) => n + a.amount, 0);
      const gross = basePay + allowance + commission + adjust;
      const fines = late * LATE_FINE + noOut * NO_OUT_FINE;
      const insurance = e.status === 'probation' ? 0 : Math.round(base * INSURANCE_RATE);
      const tax = pitOf(gross - fines - insurance - PERSONAL_DEDUCTION);
      return { employee: e, worked, paidLeave, unpaidLeave, absent, late, noOut, payDays, base, basePay, allowance, commission, adjust, adjusts: mine, gross, fines, insurance, tax, net: gross - fines - insurance - tax };
    });
}

/** Danh sách 12 tháng gần nhất cho ô chọn tháng */
export const monthOptions = (n = 12) =>
  Array.from({ length: n }, (_, i) => {
    const m = dayjs().subtract(i, 'month');
    return { value: m.format('YYYY-MM'), label: `Tháng ${m.format('MM/YYYY')}` };
  });
