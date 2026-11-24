/**
 * Lương GV / NV, hoa hồng & KPI Sale (user story 08 TC-12 → 14, giai đoạn 22) — backend Finance: /api/v1/finance.
 * Server TÍNH lương (buổi đã xác nhận, đơn giá, bảng công, hoa hồng, BHXH, thuế) và giữ bản chốt; ở đây chỉ đổi sang kiểu frontend sẵn có
 * (`TeacherPay`, `StaffPay`, `SaleResult`) để các trang hiển thị như trước. Khóa lương của tháng đang xem ghi vào `useFinanceStore.locks`
 * (PayrollBar đọc). Mọi thao tác (điều chỉnh, chốt / mở khóa / đã chi, bậc, chỉ tiêu, gán lại Sale) gọi API rồi tăng `payrollVersion`
 * → các hook tải lại. Chế độ mock: các hook trả `undefined`, trang tự tính như cũ.
 */
import { useEffect, useMemo, useState } from 'react';
import { useClassStore } from '@/features/academic/classes/classStore';
import type { ClassInfo, ClassSession, StaffRole } from '@/features/academic/classes/types';
import { useHrStore } from '@/features/hr/hrStore';
import type { Employee } from '@/features/hr/types';
import { USE_API } from '@/lib/api/config';
import { api } from '@/lib/api/http';
import { fromGuid, toGuid, toGuids } from '@/lib/api/ids';
import { reportError } from '@/lib/api/report';
import { ALL_BRANCHES } from '@/types/auth';
import { useFinanceStore } from './financeStore';
import type { InvoiceRow, SaleResult, StaffPay, TeachItem, TeacherPay } from './financeUtils';
import type { CommissionTier, PayAdjust, PayrollLock } from './types';

// ---------- Kiểu dữ liệu server ----------

interface ServerAdjust {
  id: string;
  month: string;
  employeeId: string;
  amount: number;
  reason: string;
  by: string;
}

interface ServerTeachItem {
  sessionId: string;
  classId: string;
  classCode: string;
  className: string;
  branchId: string;
  date: string;
  startTime: string;
  endTime: string;
  role: StaffRole;
  online: boolean;
  checkIn?: string | null;
  checkOut?: string | null;
  minutes: number;
  rate: number;
  amount: number;
  confirmed: boolean;
  late: boolean;
  bonus: number;
}

interface ServerTeacherPay {
  employeeId: string;
  code: string;
  fullName: string;
  kind: Employee['kind'];
  branchId: string;
  items: ServerTeachItem[];
  cancelled: number;
  hours: number;
  base: number;
  teaching: number;
  substituteBonus: number;
  lateFine: number;
  adjust: number;
  adjusts: ServerAdjust[];
  total: number;
}

interface ServerStaffPay {
  employeeId: string;
  code: string;
  fullName: string;
  department: Employee['department'];
  branchId: string;
  worked: number;
  paidLeave: number;
  unpaidLeave: number;
  absent: number;
  late: number;
  noOut: number;
  hasTimesheet: boolean;
  payDays: number;
  base: number;
  basePay: number;
  allowance: number;
  commission: number;
  adjust: number;
  adjusts: ServerAdjust[];
  gross: number;
  fines: number;
  insurance: number;
  tax: number;
  net: number;
}

interface ServerLock {
  kind: PayrollLock['kind'];
  month: string;
  branchId: string;
  lockedBy: string;
  lockedAt: string;
  total: number;
  paidAt?: string | null;
}

interface ServerBoard<T> {
  month: string;
  branches: string[];
  rows: T[];
  locks: ServerLock[];
  locked: boolean;
  lockDeadline: string;
  lockOverdue: boolean;
  pendingSessions: number;
  total: number;
}

interface ServerSaleResult {
  employeeId: string;
  code: string;
  fullName: string;
  branchId: string;
  deals: string[];
  booked: number;
  collected: number;
  target: number;
  kpi: number;
  tier?: CommissionTier | null;
  commission: number;
  rank: number;
}

interface ServerCommissionBoard {
  month: string;
  tiers: CommissionTier[];
  results: ServerSaleResult[];
}

interface ServerMyPayroll {
  month: string;
  slip?: ServerTeacherPay | null;
  locked: boolean;
  paid: boolean;
}

// ---------- Đổi sang kiểu frontend ----------

const toAdjust = (a: ServerAdjust): PayAdjust => ({ id: a.id, month: a.month, employeeId: fromGuid(a.employeeId), amount: a.amount, reason: a.reason, by: a.by });

const toLock = (l: ServerLock): PayrollLock => ({
  kind: l.kind,
  month: l.month,
  branchId: fromGuid(l.branchId),
  lockedBy: l.lockedBy,
  lockedAt: l.lockedAt,
  total: l.total,
  paidAt: l.paidAt ?? undefined,
});

/** Nhân viên trong Nhân sự; người xem không tải được danh sách nhân sự (vd cổng GV) → bản rút gọn đủ để hiển thị */
function employeeOf(id: string, fallback: Pick<Employee, 'code' | 'fullName' | 'branchId'> & Partial<Employee>): Employee {
  const found = useHrStore.getState().employees.find((e) => e.id === id);
  return (
    found ??
    ({
      id,
      kind: 'staff',
      department: 'teaching',
      status: 'active',
      position: '',
      contract: {},
      ...fallback,
    } as unknown as Employee)
  );
}

function toTeachItem(i: ServerTeachItem, teacherId: string, classes: ClassInfo[], sessions: ClassSession[]): TeachItem {
  const sessionId = fromGuid(i.sessionId);
  const classId = fromGuid(i.classId);
  const session =
    sessions.find((s) => s.id === sessionId) ??
    ({
      id: sessionId,
      classId,
      no: 0,
      date: i.date,
      startTime: i.startTime,
      endTime: i.endTime,
      status: 'completed',
      teacherAttendance: [{ teacherId, checkIn: i.checkIn ?? undefined, checkOut: i.checkOut ?? undefined }],
    } as unknown as ClassSession);
  const cls = classes.find((c) => c.id === classId) ?? ({ id: classId, code: i.classCode, name: i.className, online: i.online, branchId: fromGuid(i.branchId) } as ClassInfo);
  return { session, cls, role: i.role, minutes: i.minutes, online: i.online, rate: i.rate, amount: i.amount, confirmed: i.confirmed, late: i.late, bonus: i.bonus };
}

function toTeacherPay(r: ServerTeacherPay, classes: ClassInfo[], sessions: ClassSession[]): TeacherPay {
  const id = fromGuid(r.employeeId);
  const items = r.items.map((i) => toTeachItem(i, id, classes, sessions));
  return {
    employee: employeeOf(id, { code: r.code, fullName: r.fullName, branchId: fromGuid(r.branchId), kind: r.kind }),
    items,
    confirmed: items.filter((i) => i.confirmed),
    pending: items.filter((i) => !i.confirmed),
    cancelled: r.cancelled,
    hours: r.hours,
    teaching: r.teaching,
    substituteBonus: r.substituteBonus,
    lateFine: r.lateFine,
    adjust: r.adjust,
    adjusts: r.adjusts.map(toAdjust),
    base: r.base,
    total: r.total,
  };
}

/** Xem mọi chi nhánh: server trả 1 dòng / (GV, chi nhánh) → gộp thành 1 phiếu / GV */
function mergeTeacherPays(list: TeacherPay[]): TeacherPay[] {
  const byId = new Map<string, TeacherPay>();
  for (const p of list) {
    const x = byId.get(p.employee.id);
    if (!x) {
      byId.set(p.employee.id, p);
      continue;
    }
    byId.set(p.employee.id, {
      ...x,
      items: [...x.items, ...p.items],
      confirmed: [...x.confirmed, ...p.confirmed],
      pending: [...x.pending, ...p.pending],
      cancelled: x.cancelled + p.cancelled,
      hours: x.hours + p.hours,
      teaching: x.teaching + p.teaching,
      substituteBonus: x.substituteBonus + p.substituteBonus,
      lateFine: x.lateFine + p.lateFine,
      adjust: x.adjust + p.adjust,
      adjusts: [...x.adjusts, ...p.adjusts.filter((a) => !x.adjusts.some((b) => b.id === a.id))],
      base: x.base + p.base,
      total: x.total + p.total,
    });
  }
  return [...byId.values()].map((p) => ({ ...p, items: [...p.items].sort((a, b) => (a.session.date + a.session.startTime).localeCompare(b.session.date + b.session.startTime)) }));
}

const toStaffPay = (r: ServerStaffPay): StaffPay => ({
  employee: employeeOf(fromGuid(r.employeeId), { code: r.code, fullName: r.fullName, branchId: fromGuid(r.branchId), department: r.department }),
  worked: r.worked,
  paidLeave: r.paidLeave,
  unpaidLeave: r.unpaidLeave,
  absent: r.absent,
  late: r.late,
  noOut: r.noOut,
  payDays: r.payDays,
  base: r.base,
  basePay: r.basePay,
  allowance: r.allowance,
  commission: r.commission,
  adjust: r.adjust,
  adjusts: r.adjusts.map(toAdjust),
  gross: r.gross,
  fines: r.fines,
  insurance: r.insurance,
  tax: r.tax,
  net: r.net,
  hasTimesheet: r.hasTimesheet,
});

// ---------- Tải bảng ----------

/** Mọi thao tác đổi lương / hoa hồng → hook tải lại */
const bump = () => useFinanceStore.setState((s) => ({ payrollVersion: s.payrollVersion + 1 }));

const branchQuery = (branchId: string) => (branchId === ALL_BRANCHES ? '' : `&branchId=${toGuid(branchId)}`);

/** Khóa lương của tháng: thay các khóa cùng loại + tháng bằng bản server (chi nhánh ngoài phạm vi xem giữ nguyên) */
function applyLocks(kind: PayrollLock['kind'], month: string, board: ServerBoard<unknown>) {
  const branches = new Set(board.branches.map(fromGuid));
  useFinanceStore.setState((s) => ({
    locks: [...s.locks.filter((l) => !(l.kind === kind && l.month === month && branches.has(l.branchId))), ...board.locks.map(toLock)],
  }));
}

function useBoard<T>(kind: PayrollLock['kind'], month: string, branchId: string): ServerBoard<T> | undefined {
  const version = useFinanceStore((s) => s.payrollVersion);
  const [board, setBoard] = useState<{ key: string; data: ServerBoard<T> }>();
  const key = `${kind}|${month}|${branchId}`;
  useEffect(() => {
    if (!USE_API) return;
    let off = false;
    api<ServerBoard<T>>(`/finance/payroll/${kind}?month=${month}${branchQuery(branchId)}`)
      .then((data) => {
        if (off) return;
        applyLocks(kind, month, data as ServerBoard<unknown>);
        setBoard({ key, data });
      })
      .catch((e) => !off && reportError(e));
    return () => {
      off = true;
    };
  }, [kind, month, branchId, key, version]);
  return board?.key === key ? board.data : undefined;
}

/** Tổng thực nhận theo chi nhánh (PayrollBar hiện khi chốt) */
const totalsBy = <T extends { branchId: string }>(rows: T[], value: (r: T) => number) =>
  rows.reduce<Record<string, number>>((m, r) => ({ ...m, [fromGuid(r.branchId)]: (m[fromGuid(r.branchId)] ?? 0) + value(r) }), {});

export interface PayrollView<T> {
  rows: T[];
  totals: Record<string, number>;
  /** Số buổi chờ xác nhận (lương GV) */
  pending: number;
}

/** TC-13 — bảng lương GV từ server (undefined = chế độ mock hoặc đang tải) */
export function useServerTeacherPayroll(month: string, branchId: string): PayrollView<TeacherPay> | undefined {
  const board = useBoard<ServerTeacherPay>('teacher', month, branchId);
  const classes = useClassStore((s) => s.classes);
  const sessions = useClassStore((s) => s.sessions);
  const employees = useHrStore((s) => s.employees);
  return useMemo(() => {
    if (!board) return undefined;
    void employees; // đổi tên khi danh sách nhân sự nạp xong
    return {
      rows: mergeTeacherPays(board.rows.map((r) => toTeacherPay(r, classes, sessions))),
      totals: totalsBy(board.rows, (r) => r.total),
      pending: board.pendingSessions,
    };
  }, [board, classes, sessions, employees]);
}

/** TC-14 — bảng lương NV từ server */
export function useServerStaffPayroll(month: string, branchId: string): PayrollView<StaffPay> | undefined {
  const board = useBoard<ServerStaffPay>('staff', month, branchId);
  const employees = useHrStore((s) => s.employees);
  return useMemo(() => {
    if (!board) return undefined;
    void employees;
    return { rows: board.rows.map(toStaffPay), totals: totalsBy(board.rows, (r) => r.net), pending: 0 };
  }, [board, employees]);
}

/** TC-12 — kết quả hoa hồng tháng từ server; hóa đơn của mỗi Sale ghép từ danh sách hóa đơn đã nạp. Bậc + chỉ tiêu ghi vào store. */
export function useServerCommissions(month: string, branchId: string, invoiceRows: InvoiceRow[]): SaleResult[] | undefined {
  const version = useFinanceStore((s) => s.payrollVersion);
  const employees = useHrStore((s) => s.employees);
  const [board, setBoard] = useState<ServerCommissionBoard>();
  useEffect(() => {
    if (!USE_API) return;
    let off = false;
    api<ServerCommissionBoard>(`/finance/commissions?month=${month}${branchQuery(branchId)}`)
      .then((b) => {
        if (off) return;
        useFinanceStore.setState((s) => ({
          tiers: b.tiers,
          targets: [
            ...s.targets.filter((t) => t.month !== month),
            ...b.results.filter((r) => r.target > 0).map((r) => ({ employeeId: fromGuid(r.employeeId), month, target: r.target })),
          ],
        }));
        setBoard(b);
      })
      .catch((e) => !off && reportError(e));
    return () => {
      off = true;
    };
  }, [month, branchId, version]);
  return useMemo(() => {
    if (!board || board.month !== month) return undefined;
    const rowsById = new Map(invoiceRows.map((r) => [r.id, r]));
    void employees;
    return board.results.map(
      (r): SaleResult => ({
        employee: employeeOf(fromGuid(r.employeeId), { code: r.code, fullName: r.fullName, branchId: fromGuid(r.branchId), department: 'sales' }),
        deals: r.deals.map((d) => rowsById.get(fromGuid(d))).filter((x): x is InvoiceRow => Boolean(x)),
        booked: r.booked,
        collected: r.collected,
        target: r.target,
        kpi: r.kpi,
        tier: r.tier ?? undefined,
        commission: r.commission,
      }),
    );
  }, [board, month, invoiceRows, employees]);
}

/** TC-13 bước 6 — cổng GV: lương tạm tính của chính mình */
export function useMyPayroll(month: string): { pay?: TeacherPay; locked: boolean; paid: boolean } | undefined {
  const classes = useClassStore((s) => s.classes);
  const sessions = useClassStore((s) => s.sessions);
  const [data, setData] = useState<ServerMyPayroll>();
  useEffect(() => {
    if (!USE_API) return;
    let off = false;
    api<ServerMyPayroll>(`/finance/portal/payroll?month=${month}`)
      .then((d) => !off && setData(d))
      .catch((e) => !off && reportError(e));
    return () => {
      off = true;
    };
  }, [month]);
  return useMemo(() => {
    if (!data || data.month !== month) return undefined;
    return { pay: data.slip ? toTeacherPay(data.slip, classes, sessions) : undefined, locked: data.locked, paid: data.paid };
  }, [data, month, classes, sessions]);
}

// ---------- Thao tác ----------

/** Thưởng (+) / phạt (−) tháng — tháng đã chốt server chặn (`finance.payroll.locked`) */
export async function addPayAdjust(a: Omit<PayAdjust, 'id' | 'by'>) {
  await api('/finance/pay-adjustments', { body: { month: a.month, employeeId: toGuid(a.employeeId), amount: a.amount, reason: a.reason } });
  bump();
}

export async function removePayAdjust(id: string) {
  await api(`/finance/pay-adjustments/${id}`, { method: 'DELETE' });
  bump();
}

/** Chốt / mở khóa / đã chi lương cho các chi nhánh — 1 request cho cả lô. Trả cảnh báo (còn buổi chờ xác nhận, NV chưa có chấm công) */
export async function payrollAction(kind: PayrollLock['kind'], month: string, action: 'lock' | 'unlock' | 'paid', branchIds: string[]) {
  const r = await api<{ locks: ServerLock[]; warnings: string[] }>(`/finance/payroll/${kind}/${month}/${action}`, { body: { branchIds: toGuids(branchIds) } });
  if (action === 'unlock') {
    const ids = new Set(branchIds);
    useFinanceStore.setState((s) => ({ locks: s.locks.filter((l) => !(l.kind === kind && l.month === month && ids.has(l.branchId))) }));
  } else {
    const fresh = r.locks.map(toLock);
    useFinanceStore.setState((s) => ({
      locks: [...s.locks.filter((l) => !fresh.some((f) => f.kind === l.kind && f.month === l.month && f.branchId === l.branchId)), ...fresh],
    }));
  }
  bump();
  return r.warnings;
}

/** Thay toàn bộ bảng bậc hoa hồng (TC-12 bước 1) */
export async function saveCommissionTiers(tiers: CommissionTier[]) {
  const saved = await api<CommissionTier[]>('/finance/commission-tiers', { method: 'PUT', body: { tiers } });
  useFinanceStore.setState({ tiers: saved });
  bump();
}

export async function saveSaleTarget(employeeId: string, month: string, target: number) {
  await api('/finance/sale-targets', { method: 'PUT', body: { employeeId: toGuid(employeeId), month, target } });
  useFinanceStore.setState((s) => ({ targets: [...s.targets.filter((t) => !(t.employeeId === employeeId && t.month === month)), { employeeId, month, target }] }));
  bump();
}

/** Kế toán gán lại Sale ghi nhận doanh số cho 1 hóa đơn (TC-12 bước 4) */
export async function reassignInvoiceSale(invoiceId: string, employeeId: string) {
  await api(`/finance/invoices/${toGuid(invoiceId)}/sale`, { method: 'PUT', body: { employeeId: toGuid(employeeId) } });
  const { useStudentStore } = await import('@/features/student/studentStore');
  useStudentStore.setState((s) => ({ invoices: s.invoices.map((i) => (i.id === invoiceId ? { ...i, saleId: employeeId } : i)) }));
  bump();
}

// ---------- Thay hàm thao tác của store (chế độ API) ----------

if (USE_API) {
  const fail = (e: unknown) => reportError(e);
  useFinanceStore.setState({
    adjusts: [],
    locks: [],
    tiers: [],
    targets: [],
    saleOverrides: {},
    addAdjust: (a) => void addPayAdjust(a).catch(fail),
    removeAdjust: (id) => void removePayAdjust(id).catch(fail),
    lockPayroll: (l) => void payrollAction(l.kind, l.month, 'lock', [l.branchId]).catch(fail),
    unlockPayroll: (kind, month, branchId) => void payrollAction(kind, month, 'unlock', [branchId]).catch(fail),
    markPaid: (kind, month, branchId) => void payrollAction(kind, month, 'paid', [branchId]).catch(fail),
    setTiers: (t) => void saveCommissionTiers(t).catch(fail),
    setTarget: (employeeId, month, target) => void saveSaleTarget(employeeId, month, target).catch(fail),
    assignSale: (invoiceId, employeeId) => void reassignInvoiceSale(invoiceId, employeeId).catch(fail),
  });
}
