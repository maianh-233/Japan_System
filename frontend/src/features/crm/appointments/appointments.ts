import dayjs, { type Dayjs } from 'dayjs';
import { create } from 'zustand';
import { useClassStore } from '@/features/academic/classes/classStore';
import { sessionRoom, sessionTeachers } from '@/features/academic/classes/sessionUtils';
import type { Room } from '@/features/academic/rooms/types';
import type { Employee } from '@/features/hr/types';

/**
 * LỊCH HẸN TẠI TRUNG TÂM — lead đến tư vấn trực tiếp hoặc làm test xếp lớp trên giấy / máy của trung tâm.
 * (Test online gửi link vẫn ở trang Test xếp lớp.)
 *  - Kiểm tra trùng: PHÒNG (lịch hẹn khác + buổi học của lớp), NGƯỜI TIẾP (lịch hẹn khác + giờ dạy nếu là giáo viên), LEAD (đã có hẹn khác giờ đó)
 *  - Nhắc trước 1 giờ: tin cho lead (theo mẫu tin nhắn) + thông báo cho người tiếp — xem appointmentWatch.ts
 */

export type AppointmentKind = 'consult' | 'test';
export type AppointmentStatus = 'scheduled' | 'done' | 'no_show' | 'cancelled';

export interface Appointment {
  id: string;
  code: string;
  leadId: string;
  kind: AppointmentKind;
  branchId: string;
  roomId?: string;
  /** Nhân viên tiếp / coi test */
  staffId: string;
  start: string;
  end: string;
  status: AppointmentStatus;
  note?: string;
  /** Kết quả sau buổi hẹn */
  outcome?: string;
  createdBy: string;
  createdAt: string;
  /** Đã nhắc trước 1 giờ */
  remindedAt?: string;
  /** Hủy / đổi lịch: lý do */
  cancelReason?: string;
}

export const APPOINTMENT_KIND: Record<AppointmentKind, { label: string; short: string }> = {
  consult: { label: 'Tư vấn tại trung tâm', short: 'Tư vấn' },
  test: { label: 'Test xếp lớp tại trung tâm', short: 'Test' },
};

export const APPOINTMENT_STATUS: Record<AppointmentStatus, { label: string; tone: 'info' | 'success' | 'warning' | 'neutral' }> = {
  scheduled: { label: 'Đã hẹn', tone: 'info' },
  done: { label: 'Đã đến', tone: 'success' },
  no_show: { label: 'Không đến', tone: 'warning' },
  cancelled: { label: 'Đã hủy', tone: 'neutral' },
};

/** Thời lượng gợi ý (phút) */
export const DEFAULT_DURATION: Record<AppointmentKind, number> = { consult: 30, test: 60 };
/** Nhắc trước bao nhiêu phút */
export const REMIND_BEFORE_MIN = 60;

// ============================== Kiểm tra trùng ==============================

export interface Conflict {
  kind: 'room' | 'staff' | 'lead';
  message: string;
}

const overlap = (aS: Dayjs, aE: Dayjs, bS: Dayjs, bE: Dayjs) => aS.isBefore(bE) && bS.isBefore(aE);

export function findConflicts(
  draft: { id?: string; leadId?: string; branchId: string; roomId?: string; staffId?: string; start: Dayjs; end: Dayjs },
  ctx: { appointments: Appointment[]; rooms: Room[]; employees: Employee[]; leadName?: (id: string) => string | undefined },
): Conflict[] {
  const out: Conflict[] = [];
  const { start, end } = draft;
  if (!start.isBefore(end)) return out;
  const others = ctx.appointments.filter((a) => a.id !== draft.id && a.status === 'scheduled' && overlap(start, end, dayjs(a.start), dayjs(a.end)));
  const hm = (a: { start: string; end: string }) => `${dayjs(a.start).format('HH:mm')}–${dayjs(a.end).format('HH:mm')}`;
  const room = draft.roomId ? ctx.rooms.find((r) => r.id === draft.roomId) : undefined;
  const staff = draft.staffId ? ctx.employees.find((e) => e.id === draft.staffId) : undefined;

  // ---- Phòng ----
  if (room) {
    if (room.status !== 'active') out.push({ kind: 'room', message: `${room.name} đang ${room.status === 'maintenance' ? 'bảo trì' : 'ngừng dùng'}` });
    others
      .filter((a) => a.roomId === room.id)
      .forEach((a) => out.push({ kind: 'room', message: `${room.name} đã có hẹn ${APPOINTMENT_KIND[a.kind].short.toLowerCase()} ${a.code} (${hm(a)}) — ${ctx.leadName?.(a.leadId) ?? ''}` }));
    const { classes, sessions } = useClassStore.getState();
    sessions.forEach((s) => {
      if (s.status === 'cancelled') return;
      const c = classes.find((x) => x.id === s.classId);
      if (!c || c.branchId !== draft.branchId || sessionRoom(s, c) !== room.name) return;
      const sS = dayjs(`${s.date}T${s.startTime}`);
      const sE = dayjs(`${s.date}T${s.endTime}`);
      if (overlap(start, end, sS, sE)) out.push({ kind: 'room', message: `${room.name} đang có lớp ${c.code.replace(/^GV[VN]*_/, '')} học ${s.startTime}–${s.endTime}` });
    });
  }

  // ---- Người tiếp ----
  if (staff) {
    others
      .filter((a) => a.staffId === staff.id)
      .forEach((a) => out.push({ kind: 'staff', message: `${staff.fullName} đã có hẹn ${a.code} (${hm(a)}) — ${ctx.leadName?.(a.leadId) ?? ''}` }));
    if (staff.kind === 'teacher') {
      const { classes, sessions } = useClassStore.getState();
      sessions.forEach((s) => {
        if (s.status === 'cancelled') return;
        const c = classes.find((x) => x.id === s.classId);
        if (!c || !sessionTeachers(s, c).includes(staff.id)) return;
        const sS = dayjs(`${s.date}T${s.startTime}`);
        const sE = dayjs(`${s.date}T${s.endTime}`);
        if (overlap(start, end, sS, sE)) out.push({ kind: 'staff', message: `${staff.fullName} đang dạy lớp ${c.code.replace(/^GV[VN]*_/, '')} (${s.startTime}–${s.endTime})` });
      });
    }
    if (staff.status === 'leave') out.push({ kind: 'staff', message: `${staff.fullName} đang nghỉ phép dài ngày` });
  }

  // ---- Lead đã có hẹn khác cùng giờ ----
  if (draft.leadId)
    others.filter((a) => a.leadId === draft.leadId).forEach((a) => out.push({ kind: 'lead', message: `Lead này đã có hẹn ${a.code} lúc ${hm(a)} ${dayjs(a.start).format('DD/MM')}` }));

  return out;
}

// ============================== Store ==============================

const now = () => new Date().toISOString();
let seq = 100;
/** Giờ hôm nay ± ngày, vd at(0, 18, 30) */
const at = (dayOffset: number, h: number, m = 0) => dayjs().add(dayOffset, 'day').hour(h).minute(m).second(0).millisecond(0);

function mock(): Appointment[] {
  const mk = (n: number, leadId: string, kind: AppointmentKind, branchId: string, roomId: string | undefined, staffId: string, s: Dayjs, status: AppointmentStatus = 'scheduled', extra: Partial<Appointment> = {}): Appointment => ({
    id: `AP-${n}`,
    code: `LH${String(n).padStart(4, '0')}`,
    leadId,
    kind,
    branchId,
    roomId,
    staffId,
    start: s.toISOString(),
    end: s.add(DEFAULT_DURATION[kind], 'minute').toISOString(),
    status,
    createdBy: 'Hồ Quỳnh Như',
    createdAt: s.subtract(2, 'day').toISOString(),
    ...extra,
  });
  // Giờ "sắp tới" tính từ lúc mở trang để luôn có ví dụ nhắc trước 1 giờ
  const soon = dayjs().add(40, 'minute').minute(Math.ceil(dayjs().add(40, 'minute').minute() / 15) * 15 % 60).second(0).millisecond(0);
  return [
    mk(1, 'L-0003', 'test', 'cg', 'R-CG-201', 'e-nhu', soon),
    mk(2, 'L-0004', 'consult', 'cg', 'R-CG-102', 'e-tung', at(0, 17, 30)),
    mk(3, 'L-0005', 'test', 'cg', 'R-CG-201', 'e-bao', at(1, 9, 0)),
    mk(4, 'L-0006', 'consult', 'q1', 'R-Q1-101', 'e-khoa', at(1, 18, 0)),
    mk(5, 'L-0007', 'test', 'cg', 'R-CG-201', 'e-nhu', at(2, 14, 0)),
    mk(6, 'L-0008', 'consult', 'cg', undefined, 'e-tung', at(-1, 10, 0), 'done', { outcome: 'Khách ưng lớp N4 tối 2-4-6, hẹn test thứ 7.' }),
    mk(7, 'L-0009', 'test', 'cg', 'R-CG-201', 'e-bao', at(-2, 9, 0), 'no_show'),
    mk(8, 'L-0010', 'consult', 'hc', undefined, 'e-vy', at(3, 15, 0)),
  ];
}

export type AppointmentInput = Omit<Appointment, 'id' | 'code' | 'createdAt' | 'status' | 'remindedAt'>;

interface AppointmentState {
  appointments: Appointment[];
  add: (input: AppointmentInput) => Appointment;
  update: (id: string, patch: Partial<Appointment>) => void;
}

export const useAppointmentStore = create<AppointmentState>((set) => ({
  appointments: mock(),
  add: (input) => {
    const n = ++seq;
    const a: Appointment = { ...input, id: `AP-${Date.now()}${n}`, code: `LH${String(n).padStart(4, '0')}`, status: 'scheduled', createdAt: now() };
    set((s) => ({ appointments: [...s.appointments, a] }));
    return a;
  },
  update: (id, patch) => set((s) => ({ appointments: s.appointments.map((a) => (a.id === id ? { ...a, ...patch } : a)) })),
}));
