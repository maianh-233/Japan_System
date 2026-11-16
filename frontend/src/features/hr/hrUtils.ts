import dayjs from 'dayjs';
import type { Tone } from '@/components/common';
import type { SessionUser } from '@/types/auth';
import type { ShiftAssignment } from '@/features/work/types';
import { LATE_GRACE_MIN, OFFICE_HOURS, WORK_SHIFTS } from './constants';
import type { ApprovalStep, Employee, HrRequest, RequestType, TimeRecord } from './types';

export const toMin = (t: string) => {
  const [h, m] = t.split(':').map(Number);
  return h * 60 + m;
};

/** Nhân viên ứng với tài khoản đang đăng nhập (khớp theo họ tên — dữ liệu giả) */
export const meOf = (employees: Employee[], user: SessionUser | null) => employees.find((e) => e.fullName === user?.fullName);

export const nameOf = (employees: Employee[], id?: string) => employees.find((e) => e.id === id)?.fullName ?? id ?? '—';

/** Giờ làm dự kiến của nhân viên trong ngày (theo ca đã xếp hoặc giờ hành chính) */
export function expectedOf(e: Employee, date: string, shifts: ShiftAssignment[]): { from: string; to: string; label: string } | undefined {
  if (e.shiftBased) {
    const mine = shifts.filter((s) => s.employeeId === e.id && s.date === date).map((s) => WORK_SHIFTS[s.shift]);
    if (!mine.length) return undefined;
    return { from: mine.map((s) => s.from).sort()[0], to: mine.map((s) => s.to).sort().reverse()[0], label: mine.map((s) => s.short).join('+') };
  }
  const wd = dayjs(date).day();
  if (wd === 0) return undefined;
  return { from: OFFICE_HOURS.from, to: wd === 6 ? OFFICE_HOURS.satTo : OFFICE_HOURS.to, label: 'HC' };
}

export type DayState = 'ok' | 'late' | 'early' | 'late_early' | 'no_out' | 'absent' | 'leave' | 'off' | 'future' | 'working';

export const DAY_STATE: Record<DayState, { label: string; tone: Tone; short: string }> = {
  ok: { label: 'Đúng giờ', tone: 'success', short: '✓' },
  late: { label: 'Đi muộn', tone: 'warning', short: 'M' },
  early: { label: 'Về sớm', tone: 'warning', short: 'S' },
  late_early: { label: 'Muộn + về sớm', tone: 'danger', short: 'MS' },
  no_out: { label: 'Quên check-out', tone: 'danger', short: '?' },
  absent: { label: 'Vắng không phép', tone: 'danger', short: 'V' },
  leave: { label: 'Nghỉ phép', tone: 'info', short: 'P' },
  off: { label: 'Không có ca', tone: 'neutral', short: '' },
  future: { label: '', tone: 'neutral', short: '' },
  working: { label: 'Đang làm', tone: 'primary', short: '…' },
};

/** Đơn nghỉ phép / công tác đã duyệt phủ ngày này */
export const onLeave = (employeeId: string, date: string, requests: HrRequest[]) =>
  requests.find((r) => r.employeeId === employeeId && r.status === 'approved' && (r.type === 'leave' || r.type === 'business_trip') && r.from <= date && date <= r.to);

/** Trạng thái chấm công 1 ngày */
export function dayState(e: Employee, date: string, rec: TimeRecord | undefined, shifts: ShiftAssignment[], requests: HrRequest[]) {
  const exp = expectedOf(e, date, shifts);
  const today = dayjs().format('YYYY-MM-DD');
  const lateMin = exp && rec?.checkIn ? toMin(rec.checkIn) - toMin(exp.from) : 0;
  const earlyMin = exp && rec?.checkOut ? toMin(exp.to) - toMin(rec.checkOut) : 0;
  const excused = requests.some((r) => r.employeeId === e.id && r.status === 'approved' && r.type === 'late_early' && r.from === date);
  let state: DayState;
  if (onLeave(e.id, date, requests)) state = 'leave';
  else if (date > today) state = 'future';
  else if (!exp) state = rec ? 'ok' : 'off';
  else if (!rec?.checkIn) state = date === today && dayjs().format('HH:mm') < exp.to ? 'future' : 'absent';
  else if (!rec.checkOut) state = date === today ? 'working' : 'no_out';
  else {
    const late = lateMin > LATE_GRACE_MIN && !excused;
    const early = earlyMin > LATE_GRACE_MIN && !excused;
    state = late && early ? 'late_early' : late ? 'late' : early ? 'early' : 'ok';
  }
  const worked = rec?.checkIn && rec.checkOut ? Math.max(0, toMin(rec.checkOut) - toMin(rec.checkIn)) : 0;
  return { state, exp, lateMin: Math.max(0, lateMin), earlyMin: Math.max(0, earlyMin), worked };
}

/** Hợp đồng sắp hết hạn trong N ngày */
export const contractEndsIn = (e: Employee) => (e.contract.end ? dayjs(e.contract.end).diff(dayjs().startOf('day'), 'day') : undefined);

/** Ngày làm việc (bỏ Chủ nhật) trong khoảng */
export const workDays = (a: dayjs.Dayjs, b: dayjs.Dayjs) => {
  let n = 0;
  for (let d = a.startOf('day'); !d.isAfter(b, 'day'); d = d.add(1, 'day')) if (d.day() !== 0) n++;
  return n;
};

/** Các cấp duyệt theo loại đơn (dùng chung: Đơn từ & phê duyệt, Chấm công của tôi) */
export function buildSteps(e: Employee, employees: Employee[], type: RequestType, days = 0, amount = 0): ApprovalStep[] {
  const steps: ApprovalStep[] = [{ role: 'Quản lý trực tiếp', approver: nameOf(employees, e.managerId) }];
  if (type === 'leave' && days > 3) steps.push({ role: 'Phó giám đốc (nghỉ > 3 ngày)', approver: 'Phạm Thu Trang' });
  if (type === 'advance') steps.push({ role: 'Kế toán trưởng', approver: 'Hoàng Thị Lan' });
  if (type === 'proposal' && amount > 5_000_000) steps.push({ role: 'Kế toán trưởng (> 5 triệu)', approver: 'Hoàng Thị Lan' }, { role: 'Giám đốc', approver: 'Đỗ Quang Huy' });
  return steps;
}

/**
 * Nhân viên DƯỚI QUYỀN (theo cây quản lý managerId): trực tiếp + gián tiếp, bỏ người đã nghỉ việc.
 * `depth` = 1 là cấp dưới trực tiếp.
 */
export function reportsOf(managerId: string | undefined, employees: Employee[]): { e: Employee; depth: number }[] {
  if (!managerId) return [];
  const out: { e: Employee; depth: number }[] = [];
  const walk = (id: string, depth: number) => {
    employees
      .filter((x) => x.managerId === id && x.status !== 'quit' && x.id !== managerId)
      .forEach((x) => {
        if (out.some((o) => o.e.id === x.id)) return;
        out.push({ e: x, depth });
        walk(x.id, depth + 1);
      });
  };
  walk(managerId, 1);
  return out;
}
