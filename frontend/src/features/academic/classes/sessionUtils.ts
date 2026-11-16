import dayjs from 'dayjs';
import type { Enrollment } from '@/features/student/types';
import { HOLIDAYS, teacherName } from './constants';
import type { AttendanceRecord, ClassInfo, ClassSession, SessionOrigin } from './types';

/** Nhãn nguồn gốc buổi */
export const SESSION_ORIGIN: Record<Exclude<SessionOrigin, 'regular'>, { label: string; hint: string }> = {
  makeup: { label: 'Học bù', hint: 'Bù cho 1 buổi bị hủy — tính vào số buổi của khóa' },
  extra: { label: 'Học thêm', hint: 'Tăng cường để kịp tiến độ — ngoài số buổi của khóa, không bị xóa khi tạo lại lịch' },
};

export const originOf = (s: ClassSession): SessionOrigin => s.origin ?? (s.status === 'makeup' ? 'makeup' : 'regular');

/** Buổi được tính vào tiến độ / số buổi của khóa (buổi học thêm thì không) */
export const isTaught = (s: ClassSession) => s.origin !== 'extra' && (s.status === 'completed' || s.status === 'makeup');

/** Buổi đã kết thúc việc học (đã điểm danh) */
export const isDone = (s: ClassSession) => s.status === 'completed' || Boolean(s.attendance);

/** GV / phòng hiệu lực của buổi (ghi đè của buổi, không có thì theo lớp) */
export const sessionTeachers = (s: ClassSession, cls: Pick<ClassInfo, 'teacherIds'>) => s.teacherIds ?? cls.teacherIds;
export const sessionRoom = (s: ClassSession, cls: Pick<ClassInfo, 'room' | 'online'>) => (cls.online ? undefined : (s.room ?? cls.room));

export const startOf = (s: Pick<ClassSession, 'date' | 'startTime'>) => dayjs(`${s.date}T${s.startTime}`);

/** Buổi chưa bắt đầu → còn hủy / dời được */
export const notStarted = (s: Pick<ClassSession, 'date' | 'startTime'>) => startOf(s).isAfter(dayjs());

/** Đã tới giờ học → điểm danh được */
export const canTakeAttendance = (s: ClassSession) => s.status !== 'cancelled' && !notStarted(s);

/** Học viên thuộc buổi: đang học / bảo lưu / đã hoàn thành, hoặc đã nghỉ / chuyển lớp SAU ngày học */
export function rosterFor(classId: string, date: string, enrollments: Enrollment[]) {
  return enrollments.filter(
    (e) =>
      e.classId === classId &&
      (e.status === 'studying' || e.status === 'reserved' || e.status === 'completed' || (e.endedAt !== undefined && e.endedAt > date)),
  );
}

/** Điểm danh mặc định: tất cả có mặt, học viên bảo lưu → vắng có phép */
export const defaultRecords = (roster: Enrollment[]): AttendanceRecord[] =>
  roster.map((e) => (e.status === 'reserved' ? { studentId: e.studentId, status: 'excused', note: 'Đang bảo lưu' } : { studentId: e.studentId, status: 'present' }));

export function summarize(records: AttendanceRecord[]) {
  const c = { present: 0, late: 0, excused: 0, absent: 0 };
  records.forEach((r) => c[r.status]++);
  return { ...c, attended: c.present + c.late, total: records.length };
}

// ---------------- Kiểm tra trùng lịch ----------------

const toMin = (t: string) => {
  const [h, m] = t.split(':').map(Number);
  return h * 60 + m;
};
const overlap = (a: { startTime: string; endTime: string }, b: { startTime: string; endTime: string }) =>
  toMin(a.startTime) < toMin(b.endTime) && toMin(b.startTime) < toMin(a.endTime);

export interface SlotDraft {
  id?: string;
  classId: string;
  date: string;
  startTime: string;
  endTime: string;
  teacherIds: string[];
  room?: string;
}

/**
 * Cảnh báo khi xếp 1 buổi: trùng giờ với buổi khác của lớp, GV đang dạy lớp khác, phòng đã có lớp, ngày lễ, giờ sai.
 * (Giả lập ở frontend — khi có module Xếp lịch, backend sẽ kiểm tra.)
 */
export function findConflicts(draft: SlotDraft, sessions: ClassSession[], classes: ClassInfo[]): string[] {
  const out: string[] = [];
  if (toMin(draft.endTime) <= toMin(draft.startTime)) out.push('Giờ kết thúc phải sau giờ bắt đầu.');
  const holiday = HOLIDAYS.find((h) => h.date === draft.date);
  if (holiday) out.push(`Ngày ${dayjs(draft.date).format('DD/MM')} là ngày nghỉ lễ (${holiday.name}).`);
  sessions.forEach((s) => {
    if (s.id === draft.id || s.date !== draft.date || s.status === 'cancelled' || !overlap(s, draft)) return;
    const cls = classes.find((c) => c.id === s.classId);
    if (!cls || cls.status === 'cancelled') return;
    const when = `${s.startTime}–${s.endTime}`;
    if (s.classId === draft.classId) {
      out.push(`Lớp đã có buổi ${s.no || '(học thêm)'} lúc ${when}.`);
      return;
    }
    sessionTeachers(s, cls)
      .filter((t) => draft.teacherIds.includes(t))
      .forEach((t) => out.push(`GV ${teacherName(t)} đang dạy lớp ${cls.code} lúc ${when}.`));
    const room = sessionRoom(s, cls);
    if (draft.room && room === draft.room && cls.branchId === classes.find((c) => c.id === draft.classId)?.branchId)
      out.push(`Phòng ${room} đã có lớp ${cls.code} lúc ${when}.`);
  });
  return out;
}
