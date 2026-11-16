import dayjs from 'dayjs';
import { CheckCircle2, CircleDashed, Clock, HeartPulse, House, PlugZap, Radio, TriangleAlert, UserRoundCog, XCircle, type LucideIcon } from 'lucide-react';
import type { Tone } from '@/components/common';
import { getTeachers, isAssistant } from '../classes/constants';
import { sessionTeachers } from '../classes/sessionUtils';
import type { ClassInfo, ClassSession, IncidentAction, IncidentReason, StaffRole } from '../classes/types';

/**
 * Nghiệp vụ trang Buổi học (cho nhân viên):
 *  - Trạng thái vận hành của buổi: sắp diễn ra → đang diễn ra → chờ xác nhận → đã xác nhận (hoặc đã hủy)
 *  - Ai đứng lớp: GV lớp / GV dạy thay / trợ giảng (+ người vào thay khi có sự cố)
 *  - Kiểm tra trước khi xác nhận: GV đã chấm công, đã điểm danh HV, đã ghi nhật ký
 */

export type RunState = 'upcoming' | 'live' | 'awaiting' | 'confirmed' | 'cancelled';

export const RUN_STATE: Record<RunState, { label: string; tone: Tone; icon: LucideIcon }> = {
  upcoming: { label: 'Sắp diễn ra', tone: 'info', icon: Clock },
  live: { label: 'Đang diễn ra', tone: 'primary', icon: Radio },
  awaiting: { label: 'Chờ xác nhận', tone: 'warning', icon: CircleDashed },
  confirmed: { label: 'Đã xác nhận', tone: 'success', icon: CheckCircle2 },
  cancelled: { label: 'Đã hủy', tone: 'neutral', icon: XCircle },
};

export function runState(s: ClassSession, now = dayjs()): RunState {
  if (s.status === 'cancelled') return 'cancelled';
  if (s.confirmation) return 'confirmed';
  const start = dayjs(`${s.date}T${s.startTime}`);
  const end = dayjs(`${s.date}T${s.endTime}`);
  if (now.isBefore(start)) return 'upcoming';
  if (now.isBefore(end)) return 'live';
  return 'awaiting';
}

export const ROLE_LABEL: Record<StaffRole, string> = { teacher: 'GV của lớp', substitute: 'GV dạy thay', assistant: 'Trợ giảng' };

/** Người đứng lớp của buổi: GV (theo buổi), trợ giảng, + người có trong chấm công (vào thay khi sự cố) */
export function staffOf(s: ClassSession, cls: ClassInfo): { id: string; role: StaffRole }[] {
  const out: { id: string; role: StaffRole }[] = [];
  const add = (id: string, role: StaffRole) => !out.some((x) => x.id === id) && out.push({ id, role });
  sessionTeachers(s, cls).forEach((id) => add(id, cls.teacherIds.includes(id) ? 'teacher' : 'substitute'));
  (s.assistantIds ?? cls.assistantIds ?? []).forEach((id) => add(id, 'assistant'));
  (s.teacherAttendance ?? []).forEach((t) => add(t.teacherId, t.role ?? (isAssistant(t.teacherId) ? 'assistant' : 'substitute')));
  return out;
}

/** Việc còn thiếu trước khi xác nhận buổi */
export function confirmChecklist(s: ClassSession, cls: ClassInfo): { ok: boolean; label: string }[] {
  const staff = staffOf(s, cls).filter((x) => x.role !== 'assistant');
  const att = new Map((s.teacherAttendance ?? []).map((t) => [t.teacherId, t]));
  const taught = staff.filter((x) => att.get(x.id)?.status !== 'absent');
  const noIn = taught.filter((x) => !att.get(x.id)?.checkIn);
  const noOut = taught.filter((x) => att.get(x.id)?.checkIn && !att.get(x.id)?.checkOut);
  return [
    { ok: noIn.length === 0, label: noIn.length ? `Giáo viên chưa check-in (${noIn.length})` : 'Giáo viên đã check-in' },
    { ok: noOut.length === 0, label: noOut.length ? `Giáo viên chưa check-out (${noOut.length})` : 'Giáo viên đã check-out' },
    { ok: Boolean(s.records?.length || s.attendance), label: s.attendance ? `Đã điểm danh học viên (${s.attendance.present}/${s.attendance.total})` : 'Chưa điểm danh học viên' },
    { ok: Boolean(s.teacherLog), label: s.teacherLog ? 'Đã có nhật ký buổi' : 'Chưa có nhật ký buổi (không bắt buộc)' },
  ];
}

export const INCIDENT_REASONS: Record<IncidentReason, { label: string; icon: LucideIcon }> = {
  faint: { label: 'Ngất / té xỉu', icon: TriangleAlert },
  sick: { label: 'Ốm / mệt đột ngột', icon: HeartPulse },
  family: { label: 'Việc gia đình khẩn cấp', icon: House },
  accident: { label: 'Tai nạn / chấn thương', icon: TriangleAlert },
  tech: { label: 'Sự cố kỹ thuật (mất mạng, mất điện...)', icon: PlugZap },
  other: { label: 'Lý do khác', icon: UserRoundCog },
};

export const INCIDENT_ACTIONS: Record<IncidentAction, { label: string; hint: string }> = {
  pause: { label: 'Tạm dừng lớp', hint: 'Lo cho GV trước (sơ cứu, gọi người nhà / cấp cứu), quyết định học tiếp hay cho nghỉ sau' },
  substitute: { label: 'GV khác vào dạy tiếp', hint: 'Chấm công tách đôi: GV gốc tới giờ sự cố, GV thay từ lúc vào lớp' },
  assistant: { label: 'Trợ giảng của lớp dạy tiếp', hint: 'Trợ giảng đứng lớp phần còn lại' },
  end_early: { label: 'Cho lớp nghỉ sớm', hint: 'Phần còn lại cần xếp học bù — không tính giờ cho thời gian nghỉ' },
};

const toMin = (t: string) => {
  const [h, m] = t.split(':').map(Number);
  return h * 60 + m;
};

/** Sự cố đang tạm dừng lớp, chưa có kết quả */
export const isPaused = (s: ClassSession) => s.incident?.action === 'pause' && !s.incident.outcome;

/** GV / trợ giảng đang bận (dạy lớp khác) trong khoảng giờ của ngày */
export function busyStaff(date: string, from: string, to: string, sessions: ClassSession[], classes: ClassInfo[], excludeSessionId?: string): Map<string, string> {
  const byId = new Map(classes.map((c) => [c.id, c]));
  const busy = new Map<string, string>();
  for (const x of sessions) {
    if (x.id === excludeSessionId || x.date !== date || x.status === 'cancelled') continue;
    if (Math.min(toMin(x.endTime), toMin(to)) <= Math.max(toMin(x.startTime), toMin(from))) continue;
    const cls = byId.get(x.classId);
    if (!cls) continue;
    [...sessionTeachers(x, cls), ...(x.assistantIds ?? cls.assistantIds ?? [])].forEach((id) => busy.set(id, `${cls.code} ${x.startTime}–${x.endTime}`));
  }
  return busy;
}

/** Danh sách người có thể vào thay (GV của chi nhánh), kèm đánh dấu đang bận */
export function takeoverOptions(cls: ClassInfo, busy: Map<string, string>, exclude: string[]) {
  return getTeachers().filter((t) => t.branchIds.includes(cls.branchId) && !exclude.includes(t.id)).map((t) => ({
    value: t.id,
    label: `${t.name}${t.type === 'jp' ? ' (GV Nhật)' : ''}${busy.has(t.id) ? ` — đang dạy ${busy.get(t.id)}` : ' — đang rảnh'}`,
    disabled: busy.has(t.id),
  }));
}
