import dayjs from 'dayjs';
import { WORK_SHIFTS } from '@/features/hr/constants';
import type { ShiftAssignment } from '@/features/work/types';
import { useHrStore } from '@/features/hr/hrStore';
import type { Employee } from '@/features/hr/types';
import { saleName, salesOf, SLA_MINUTES } from '../constants';
import type { Lead, LeadRule } from '../types';

/**
 * Bộ máy CHIA LEAD (thuần, không phụ thuộc store) — backend sẽ chạy đúng logic này khi lead mới về.
 */

type LeadLike = Pick<Lead, 'source' | 'branchId' | 'interest'>;

const OPEN: Lead['status'][] = ['new', 'consulting', 'test_scheduled'];

export function ruleMatches(rule: LeadRule, lead: LeadLike): boolean {
  return (
    (!rule.sources.length || rule.sources.includes(lead.source)) &&
    (!rule.branchIds.length || rule.branchIds.includes(lead.branchId)) &&
    (!rule.interests.length || (!!lead.interest && rule.interests.includes(lead.interest)))
  );
}

/** Quy tắc đầu tiên (đang bật) khớp lead */
export const matchRule = (rules: LeadRule[], lead: LeadLike) => rules.find((r) => r.active && ruleMatches(r, lead));

/** SLA áp cho lead: theo quy tắc khớp, không có → mặc định */
export const slaOf = (rules: LeadRule[], lead: LeadLike) => matchRule(rules, lead)?.slaMinutes ?? SLA_MINUTES;

/** Số lead đang mở (mới / đang tư vấn / hẹn test) của từng Sale */
export function openLoad(leads: Lead[]): Map<string, number> {
  const m = new Map<string, number>();
  leads.forEach((l) => {
    if (l.assigneeId && !l.hidden && OPEN.includes(l.status)) m.set(l.assigneeId, (m.get(l.assigneeId) ?? 0) + 1);
  });
  return m;
}

/** Sale có đang trong ca tại thời điểm `at`? Làm theo ca → theo lịch Xếp ca; không → giờ hành chính T2–T7 08:00–17:30 */
export function isOnShift(saleId: string, shifts: ShiftAssignment[], at = dayjs(), employees: Employee[] = useHrStore.getState().employees): boolean {
  const e = employees.find((x) => x.id === saleId);
  const hm = at.format('HH:mm');
  if (!e?.shiftBased) return at.day() !== 0 && hm >= '08:00' && hm < '17:30';
  const date = at.format('YYYY-MM-DD');
  return shifts.some((s) => s.employeeId === saleId && s.date === date && hm >= WORK_SHIFTS[s.shift].from && hm < WORK_SHIFTS[s.shift].to);
}

export interface RouteResult {
  saleId?: string;
  rule?: LeadRule;
  /** Vị trí con trỏ xoay vòng mới (chỉ khi round_robin) */
  cursor?: number;
  reason: string;
}

export function routeLead(lead: LeadLike, rules: LeadRule[], leads: Lead[], shifts: ShiftAssignment[], at = dayjs(), employees: Employee[] = useHrStore.getState().employees): RouteResult {
  const rule = matchRule(rules, lead);
  if (!rule) return { reason: 'Không quy tắc nào khớp — chờ quản lý giao tay' };

  // Chỉ Sale còn làm việc (người đã nghỉ / chuyển phòng tự bị bỏ qua dù còn trong quy tắc)
  const active = salesOf(employees);
  const team = rule.saleIds.filter((id) => active.some((e) => e.id === id));
  if (!team.length) return { rule, reason: `Quy tắc "${rule.name}" không còn Sale nào đang làm việc` };

  let pool = team;
  let note = '';
  if (rule.onlyOnShift) {
    const on = team.filter((id) => isOnShift(id, shifts, at, employees));
    if (on.length) {
      pool = on;
      note = `${on.length}/${team.length} Sale đang trong ca`;
    } else note = 'không ai trong ca → chia cho cả nhóm';
  }

  let saleId = pool[0];
  let cursor: number | undefined;
  if (rule.method === 'round_robin') {
    // Đi tiếp từ vị trí sau con trỏ, bỏ qua người không nằm trong pool
    for (let k = 1; k <= team.length; k++) {
      const idx = (rule.cursor + k) % team.length;
      if (pool.includes(team[idx])) {
        cursor = idx;
        saleId = team[idx];
        break;
      }
    }
  } else if (rule.method === 'least_load') {
    const load = openLoad(leads);
    saleId = [...pool].sort((a, b) => (load.get(a) ?? 0) - (load.get(b) ?? 0))[0];
    note = [note, `đang giữ ${load.get(saleId) ?? 0} lead mở — ít nhất`].filter(Boolean).join(', ');
  }

  return { saleId, rule, cursor, reason: `Quy tắc "${rule.name}" → ${saleName(saleId)}${note ? ` (${note})` : ''}` };
}
