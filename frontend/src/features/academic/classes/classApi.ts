/**
 * Nối Lớp học & Buổi học với backend thật (/api/v1/academic/classes, /class-sessions — user story 07 đợt 1).
 * Đọc rồi ĐỔ VÀO useClassStore (Id mock C-N3K12… nhờ seedIds) — các phần còn chạy dữ liệu giả (học viên, điểm danh, bài tập,
 * cổng GV / HV) vẫn tra được lớp / buổi như cũ.
 * Buổi học không tải hết 1 lần: lúc đăng nhập tải ±45 ngày quanh hôm nay; trang chi tiết lớp tải đủ buổi của lớp; trang Buổi học tải theo khoảng đang xem.
 * Mỗi thao tác ghi: chế độ API gọi server rồi cập nhật store (kèm cảnh báo không chặn); chế độ mock để trang tự xử lý như trước.
 */
import dayjs from 'dayjs';
import { api, ApiError } from '@/lib/api/http';
import { fromGuid, fromGuids, toGuid, toGuids } from '@/lib/api/ids';
import { roomIdOf } from '../rooms/roomApi';
import { useClassStore } from './classStore';
import type {
  AttendanceMethod,
  AttendanceStatus,
  ClassInfo,
  ClassSession,
  ClassStatus,
  IncidentAction,
  IncidentReason,
  LessonCoverage,
  SessionOrigin,
  StaffRole,
  SupportKind,
  Weekday,
} from './types';

// ---------------- Kiểu dữ liệu server ----------------

export interface ServerClass {
  id: string;
  courseId: string;
  curriculumId?: string;
  code: string;
  name: string;
  courseLabel: string;
  branchId: string;
  teacherIds: string[];
  assistantIds: string[];
  roomId?: string;
  room?: string;
  online: boolean;
  meetingUrl?: string;
  plannedStartDate: string;
  plannedEndDate: string;
  actualStartDate?: string;
  actualEndDate?: string;
  weekdays: number[];
  startTime: string;
  endTime: string;
  totalHours: number;
  minStudents: number;
  maxStudents: number;
  fee?: number;
  status: ClassStatus;
  previousClassId?: string;
  pauseReason?: string;
  cancelReason?: string;
  note?: string;
  createdAt: string;
  rowVersion: number;
}

interface PersonAt {
  id?: string;
  name: string;
  at: string;
}

export interface ServerSession {
  id: string;
  classId: string;
  no: number;
  date: string;
  startTime: string;
  endTime: string;
  kind: 'lesson' | 'exam';
  lessonId?: string;
  examId?: string;
  status: 'scheduled' | 'completed' | 'cancelled';
  origin: SessionOrigin;
  makeupForId?: string;
  teacherIds?: string[];
  assistantIds?: string[];
  room?: string;
  note?: string;
  attendance?: { present: number; total: number };
  records: {
    studentId: string;
    status: AttendanceStatus;
    note?: string;
    checkInAt?: string;
    method?: AttendanceMethod;
    leftAt?: string;
    leaveReason?: string;
    leaveRequestId?: string;
    madeUp?: { sessionId: string; classCode?: string; status?: AttendanceStatus };
    makeupFor?: { sessionId: string; classId?: string; leaveRequestId?: string };
  }[];
  attendanceBy?: PersonAt;
  teacherAttendance: {
    teacherId: string;
    role: StaffRole;
    source: 'self' | 'staff';
    checkedAt: string;
    status: 'present' | 'late' | 'absent';
    checkIn?: string;
    checkOut?: string;
    substituteId?: string;
    note?: string;
  }[];
  confirmation?: { byId?: string; by: string; at: string; note?: string };
  incident?: {
    at: string;
    reason: IncidentReason;
    detail?: string;
    action: IncidentAction;
    takeoverId?: string;
    notified: boolean;
    outcome?: { kind: 'resumed' | 'ended'; at?: string; teacherId?: string; by?: string };
    reportedBy: string;
    reportedAt: string;
  };
  supportLog: { id: string; kind: SupportKind; note: string; by: string; at: string }[];
  teacherLog?: {
    content: string;
    homework?: string;
    issues?: string;
    coverage: LessonCoverage;
    coveredChapterIds: string[];
    endedAt: string;
    author: string;
    at: string;
  };
  rescheduledFrom?: { date: string; startTime: string; endTime: string; reason?: string };
  cancelledBy?: PersonAt;
  rowVersion: number;
}

interface ClassResult {
  class: ServerClass;
  warnings: string[];
}

export interface SessionResult {
  session: ServerSession;
  warnings: string[];
}

/** Lớp trùng lịch GV / phòng (DT-02.E8) */
export interface ScheduleConflict {
  classId: string;
  code: string;
  kind: 'teacher' | 'room';
  personId?: string;
  detail: string;
}

/** Xem trước "Tạo lịch tự động" */
export interface SchedulePreview {
  from: string;
  count: number;
  endDate?: string;
  examCount: number;
  kept: number;
  replaced: number;
  overridesLost: number;
  hasCurriculum: boolean;
  skipped: { date: string; name: string }[];
}

// ---------------- Đổi dữ liệu ----------------

const classVersions = new Map<string, number>();
const sessionVersions = new Map<string, number>();
/** Ghi chú thật của lớp — ô ghi chú trên giao diện hiện "Tạm dừng: …" khi lớp tạm dừng, không gửi chuỗi đó lên server */
const classNotes = new Map<string, string | undefined>();
/** Lớp do server quản lý — chỉ thay buổi của các lớp này khi tải lại (lớp mock khác giữ nguyên) */
const serverClasses = new Set<string>();

export function toClass(c: ServerClass): ClassInfo {
  const id = fromGuid(c.id);
  classVersions.set(id, c.rowVersion);
  classNotes.set(id, c.note);
  serverClasses.add(id);
  return {
    id,
    courseId: fromGuid(c.courseId),
    curriculumId: c.curriculumId ? fromGuid(c.curriculumId) : undefined,
    code: c.code,
    name: c.name,
    courseLabel: c.courseLabel,
    branchId: fromGuid(c.branchId),
    teacherIds: fromGuids(c.teacherIds),
    assistantIds: c.assistantIds.length ? fromGuids(c.assistantIds) : undefined,
    room: c.room,
    online: c.online,
    meetingUrl: c.meetingUrl,
    plannedStartDate: c.plannedStartDate,
    plannedEndDate: c.plannedEndDate,
    actualStartDate: c.actualStartDate,
    actualEndDate: c.actualEndDate,
    weekdays: c.weekdays as Weekday[],
    startTime: c.startTime,
    endTime: c.endTime,
    totalHours: c.totalHours,
    minStudents: c.minStudents,
    maxStudents: c.maxStudents,
    fee: c.fee ?? 0,
    status: c.status,
    previousClassId: c.previousClassId ? fromGuid(c.previousClassId) : undefined,
    cancelReason: c.cancelReason,
    // Frontend hiện lý do tạm dừng trong ghi chú
    note: c.pauseReason ? `Tạm dừng: ${c.pauseReason}` : c.note,
    createdAt: c.createdAt,
  };
}

export function toSession(s: ServerSession): ClassSession {
  const id = fromGuid(s.id);
  sessionVersions.set(id, s.rowVersion);
  return {
    id,
    classId: fromGuid(s.classId),
    no: s.no,
    date: s.date,
    startTime: s.startTime,
    endTime: s.endTime,
    kind: s.kind,
    lessonId: s.lessonId ? fromGuid(s.lessonId) : undefined,
    examId: s.examId ? fromGuid(s.examId) : undefined,
    // Frontend đánh dấu buổi học bù chưa xác nhận bằng trạng thái "makeup"
    status: s.origin === 'makeup' && s.status === 'scheduled' ? 'makeup' : s.status,
    origin: s.origin === 'regular' ? undefined : s.origin,
    makeupForId: s.makeupForId ? fromGuid(s.makeupForId) : undefined,
    teacherIds: s.teacherIds ? fromGuids(s.teacherIds) : undefined,
    assistantIds: s.assistantIds ? fromGuids(s.assistantIds) : undefined,
    room: s.room,
    note: s.note,
    attendance: s.attendance,
    records: s.records.length
      ? s.records.map((r) => ({
          studentId: fromGuid(r.studentId),
          status: r.status,
          note: r.note,
          checkInAt: r.checkInAt,
          method: r.method,
          leftAt: r.leftAt,
          leaveReason: r.leaveReason,
          leaveRequestId: r.leaveRequestId ? fromGuid(r.leaveRequestId) : undefined,
          madeUp:
            r.madeUp && (r.madeUp.status === 'present' || r.madeUp.status === 'late')
              ? { sessionId: fromGuid(r.madeUp.sessionId), classCode: r.madeUp.classCode ?? '', status: r.madeUp.status }
              : undefined,
          makeupFor: r.makeupFor
            ? { sessionId: fromGuid(r.makeupFor.sessionId), classId: r.makeupFor.classId ? fromGuid(r.makeupFor.classId) : '', leaveRequestId: r.makeupFor.leaveRequestId ? fromGuid(r.makeupFor.leaveRequestId) : '' }
            : undefined,
        }))
      : undefined,
    attendanceBy: s.attendanceBy ? { name: s.attendanceBy.name, at: s.attendanceBy.at } : undefined,
    teacherAttendance: s.teacherAttendance.length
      ? s.teacherAttendance.map((t) => ({ ...t, teacherId: fromGuid(t.teacherId), substituteId: t.substituteId ? fromGuid(t.substituteId) : undefined }))
      : undefined,
    confirmation: s.confirmation ? { by: s.confirmation.by, at: s.confirmation.at, note: s.confirmation.note } : undefined,
    incident: s.incident
      ? {
          at: s.incident.at,
          reason: s.incident.reason,
          detail: s.incident.detail,
          action: s.incident.action,
          takeoverId: s.incident.takeoverId ? fromGuid(s.incident.takeoverId) : undefined,
          notified: s.incident.notified,
          outcome: s.incident.outcome
            ? {
                kind: s.incident.outcome.kind,
                at: s.incident.outcome.at ?? '',
                teacherId: s.incident.outcome.teacherId ? fromGuid(s.incident.outcome.teacherId) : undefined,
                by: s.incident.outcome.by ?? '',
              }
            : undefined,
          reportedBy: s.incident.reportedBy,
          reportedAt: s.incident.reportedAt,
        }
      : undefined,
    supportLog: s.supportLog.length ? s.supportLog.map((l) => ({ id: l.id, kind: l.kind, note: l.note, by: l.by, at: l.at })) : undefined,
    teacherLog: s.teacherLog
      ? {
          content: s.teacherLog.content,
          homework: s.teacherLog.homework,
          issues: s.teacherLog.issues,
          coverage: s.teacherLog.coverage,
          coveredChapterIds: fromGuids(s.teacherLog.coveredChapterIds),
          endedAt: s.teacherLog.endedAt,
          author: s.teacherLog.author,
          at: s.teacherLog.at,
        }
      : undefined,
    rescheduledFrom: s.rescheduledFrom,
    cancelledBy: s.cancelledBy ? { name: s.cancelledBy.name, at: s.cancelledBy.at } : undefined,
  };
}

const bySchedule = (a: ClassSession, b: ClassSession) => a.classId.localeCompare(b.classId) || a.date.localeCompare(b.date) || a.no - b.no;

export function upsertClass(c: ServerClass): ClassInfo {
  const cls = toClass(c);
  useClassStore.setState((s) => ({
    classes: s.classes.some((x) => x.id === cls.id) ? s.classes.map((x) => (x.id === cls.id ? cls : x)) : [cls, ...s.classes],
  }));
  return cls;
}

export function upsertSession(x: ServerSession): ClassSession {
  const session = toSession(x);
  useClassStore.setState((s) => ({
    sessions: (s.sessions.some((y) => y.id === session.id) ? s.sessions.map((y) => (y.id === session.id ? session : y)) : [...s.sessions, session]).sort(bySchedule),
  }));
  return session;
}

/** Thay các buổi khớp `drop` (của lớp server) bằng danh sách mới */
export function replaceSessions(list: ServerSession[], drop: (s: ClassSession) => boolean) {
  const fresh = list.map(toSession);
  const ids = new Set(fresh.map((s) => s.id));
  useClassStore.setState((s) => ({
    sessions: [...s.sessions.filter((x) => !ids.has(x.id) && !(serverClasses.has(x.classId) && drop(x))), ...fresh].sort(bySchedule),
  }));
}

// ---------------- Đọc ----------------

/** Tải lớp + buổi ±45 ngày. Không có quyền trang Lớp học (GV, Sale…) → giữ dữ liệu giả cho các trang chưa nối */
export async function loadClasses() {
  try {
    const list = await api<ServerClass[]>('/academic/classes');
    serverClasses.clear();
    useClassStore.setState({ classes: list.map(toClass) });
  } catch (e) {
    if (e instanceof ApiError && e.status === 403) return;
    throw e;
  }
  const today = dayjs();
  await loadSessionRange(today.subtract(45, 'day').format('YYYY-MM-DD'), today.add(45, 'day').format('YYYY-MM-DD')).catch(() => undefined);
}

/** Mọi buổi của 1 lớp (tab Buổi học) */
export async function loadClassSessions(classId: string) {
  const list = await api<ServerSession[]>(`/academic/classes/${toGuid(classId)}/sessions`);
  replaceSessions(list, (s) => s.classId === classId);
}

/** Buổi trong khoảng ngày (trang Buổi học — tối đa ~3 tháng) */
export async function loadSessionRange(from: string, to: string) {
  try {
    const list = await api<ServerSession[]>(`/academic/class-sessions?from=${from}&to=${to}`);
    replaceSessions(list, (s) => s.date >= from && s.date <= to);
  } catch (e) {
    if (!(e instanceof ApiError) || e.status !== 403) throw e;
  }
}

async function refreshClass(classId: string) {
  upsertClass(await api<ServerClass>(`/academic/classes/${toGuid(classId)}`));
}

// ---------------- Lớp học ----------------

export type ClassInput = Omit<ClassInfo, 'id' | 'createdAt' | 'status' | 'actualStartDate' | 'actualEndDate' | 'cancelReason'>;

/** Ghi chú gửi server: giữ ghi chú thật nếu ô ghi chú chỉ đang hiện lý do tạm dừng */
const noteOf = (v: ClassInput, id?: string) => {
  const shown = id ? useClassStore.getState().classes.find((c) => c.id === id)?.note : undefined;
  return (id && v.note === shown && shown?.startsWith('Tạm dừng: ') ? classNotes.get(id) : v.note) || null;
};

const classBody = (v: ClassInput, id?: string) => ({
  courseId: toGuid(v.courseId),
  curriculumId: v.curriculumId ? toGuid(v.curriculumId) : null,
  code: v.code,
  name: v.name,
  branchId: toGuid(v.branchId),
  teacherIds: toGuids(v.teacherIds),
  assistantIds: toGuids(v.assistantIds ?? []),
  roomId: v.online ? null : (roomIdOf(v.branchId, v.room) ? toGuid(roomIdOf(v.branchId, v.room)!) : null),
  online: Boolean(v.online),
  meetingUrl: v.online ? (v.meetingUrl ?? null) : null,
  plannedStartDate: v.plannedStartDate,
  plannedEndDate: v.plannedEndDate ?? null,
  weekdays: v.weekdays,
  startTime: v.startTime,
  endTime: v.endTime,
  totalHours: v.totalHours,
  minStudents: v.minStudents,
  maxStudents: v.maxStudents,
  fee: v.fee,
  previousClassId: v.previousClassId ? toGuid(v.previousClassId) : null,
  note: noteOf(v, id),
});

/** DT-02.E8 — lớp trùng GV / phòng (để báo trước khi lưu) */
export async function checkClass(v: ClassInput, exceptId?: string) {
  return api<{ sessionCount: number; plannedEndDate?: string; conflicts: ScheduleConflict[] }>('/academic/classes/check', {
    body: { ...classBody(v), exceptId: exceptId ? toGuid(exceptId) : null },
  });
}

/** DT-02 — mở lớp, sinh lịch luôn */
export async function createClass(v: ClassInput): Promise<{ cls: ClassInfo; warnings: string[] }> {
  const r = await api<ClassResult>('/academic/classes', { body: { ...classBody(v), generateSessions: true } });
  const cls = upsertClass(r.class);
  await loadClassSessions(cls.id);
  return { cls, warnings: r.warnings };
}

/** DT-07 — sửa lớp (giờ mới áp cho buổi chưa dạy) */
export async function updateClass(id: string, v: ClassInput): Promise<string[]> {
  const r = await api<ClassResult>(`/academic/classes/${toGuid(id)}`, {
    method: 'PUT',
    body: { ...classBody(v, id), applyToUpcoming: true, rowVersion: classVersions.get(id) ?? 0 },
  });
  upsertClass(r.class);
  await loadClassSessions(id);
  return r.warnings;
}

async function classAction(id: string, action: string, body?: unknown): Promise<string[]> {
  const r = await api<ClassResult>(`/academic/classes/${toGuid(id)}/${action}`, { method: 'POST', body });
  upsertClass(r.class);
  await loadClassSessions(id);
  return r.warnings;
}

/** DT-03 */
export const startClass = (id: string, actualStartDate: string, reschedule: boolean) => classAction(id, 'start', { actualStartDate, reschedule });
/** DT-06 */
export const pauseClass = (id: string, reason: string) => classAction(id, 'pause', { reason });
export const resumeClass = (id: string) => classAction(id, 'resume');
/** DT-04 — server hủy các buổi chưa dạy */
export const finishClass = (id: string, actualEndDate: string) => classAction(id, 'finish', { actualEndDate });
/** DT-05 — phải gõ đúng mã lớp */
export const cancelClass = (id: string, reason: string, confirmCode: string) => classAction(id, 'cancel', { reason, confirmCode });

// ---------------- Sinh lịch (DT-08) ----------------

export interface ScheduleOptions {
  from?: string;
  weekdays?: number[];
  startTime?: string;
  endTime?: string;
  count?: number;
  skipHolidays?: boolean;
}

export const previewSchedule = (classId: string, o: ScheduleOptions) =>
  api<SchedulePreview>(`/academic/classes/${toGuid(classId)}/sessions/preview`, { body: o });

/** Thay buổi chưa dạy bằng lịch mới → tải lại lớp (ngày kết thúc dự kiến) + buổi */
export async function generateSchedule(classId: string, o: ScheduleOptions): Promise<SchedulePreview> {
  const plan = await api<SchedulePreview>(`/academic/classes/${toGuid(classId)}/sessions/generate`, { body: o });
  await Promise.all([refreshClass(classId), loadClassSessions(classId)]);
  return plan;
}

// ---------------- Buổi học ----------------

/** Dò trùng khi xếp 1 buổi — chỉ cảnh báo */
export const checkSession = (v: { classId: string; date: string; startTime: string; endTime: string; teacherIds?: string[]; roomId?: string; exceptId?: string }) =>
  api<string[]>('/academic/class-sessions/check', {
    body: {
      classId: toGuid(v.classId),
      date: v.date,
      startTime: v.startTime,
      endTime: v.endTime,
      teacherIds: v.teacherIds ? toGuids(v.teacherIds) : null,
      roomId: v.roomId ? toGuid(v.roomId) : null,
      exceptId: v.exceptId ? toGuid(v.exceptId) : null,
    },
  });

/** DT-10 — học bù (mang bài của buổi gốc) / học thêm */
export async function createSession(v: {
  cls: ClassInfo;
  origin: 'makeup' | 'extra';
  makeupForId?: string;
  date: string;
  startTime: string;
  endTime: string;
  teacherIds?: string[];
  room?: string;
  note?: string;
  lessonId?: string;
}): Promise<string[]> {
  const roomId = roomIdOf(v.cls.branchId, v.room);
  const r = await api<SessionResult>('/academic/class-sessions', {
    body: {
      classId: toGuid(v.cls.id),
      origin: v.origin,
      makeupForId: v.makeupForId ? toGuid(v.makeupForId) : null,
      date: v.date,
      startTime: v.startTime,
      endTime: v.endTime,
      teacherIds: v.teacherIds ? toGuids(v.teacherIds) : null,
      roomId: roomId ? toGuid(roomId) : null,
      note: v.note || null,
      lessonId: v.lessonId ? toGuid(v.lessonId) : null,
    },
  });
  upsertSession(r.session);
  return r.warnings;
}

/** DT-10 — dời / đổi GV / đổi phòng cho riêng 1 buổi (GV, phòng giống lớp = theo lớp) */
export async function editSession(
  s: ClassSession,
  cls: ClassInfo,
  v: { date: string; startTime: string; endTime: string; teacherIds?: string[]; room?: string; reason: string },
): Promise<string[]> {
  const roomId = roomIdOf(cls.branchId, v.room);
  const r = await api<SessionResult>(`/academic/class-sessions/${toGuid(s.id)}`, {
    method: 'PUT',
    body: {
      date: v.date,
      startTime: v.startTime,
      endTime: v.endTime,
      teacherIds: v.teacherIds ? toGuids(v.teacherIds) : null,
      assistantIds: s.assistantIds ? toGuids(s.assistantIds) : null,
      roomId: roomId ? toGuid(roomId) : null,
      reason: v.reason,
      rowVersion: sessionVersions.get(s.id) ?? 0,
    },
  });
  upsertSession(r.session);
  return r.warnings;
}

async function sessionAction(id: string, action: string, body?: unknown): Promise<string[]> {
  const r = await api<SessionResult>(`/academic/class-sessions/${toGuid(id)}/${action}`, { method: 'POST', body });
  const s = upsertSession(r.session);
  if (action === 'cancel' || action === 'confirm' || action === 'unconfirm') await refreshClass(s.classId).catch(() => undefined);
  return r.warnings;
}

/** DT-11 — chỉ trước giờ bắt đầu */
export const cancelSession = (id: string, reason: string) => sessionAction(id, 'cancel', { reason });

/** DT-13 — báo sự cố (server tự chỉnh chấm công GV theo cách xử lý) */
export const reportIncident = (
  id: string,
  v: { at: string; reason: IncidentReason; detail?: string; action: IncidentAction; takeoverId?: string; notified: boolean; takeoverAt?: string },
) =>
  sessionAction(id, 'incident', {
    at: v.at,
    reason: v.reason,
    detail: v.detail ?? null,
    action: v.action,
    takeoverId: v.takeoverId ? toGuid(v.takeoverId) : null,
    notified: v.notified,
    takeoverAt: v.takeoverAt ?? null,
  });

/** DT-13 — học tiếp lúc `at` (teacherId trống = GV cũ dạy tiếp) hoặc cho nghỉ */
export const resolveIncident = (id: string, outcome: 'resumed' | 'ended', at?: string, teacherId?: string) =>
  sessionAction(id, 'incident/outcome', { outcome, teacherId: teacherId ? toGuid(teacherId) : null, at: at ?? null });

/** Nhật ký hỗ trợ của nhân viên trong buổi */
export const addSupportEntry = (id: string, kind: SupportKind, note: string) => sessionAction(id, 'support', { kind, note });

/** DT-15 */
export const confirmSession = (id: string, note?: string) => sessionAction(id, 'confirm', { note: note ?? null });
export const unconfirmSession = (id: string) => sessionAction(id, 'unconfirm');

/** DT-15 — xác nhận hàng loạt: buổi lỗi trả lý do, buổi hợp lệ vẫn được xác nhận */
export async function confirmSessions(ids: string[]) {
  const r = await api<{ confirmed: number; failed: { id: string; code: string; message: string }[] }>('/academic/class-sessions/confirm', {
    body: { ids: toGuids(ids) },
  });
  const classIds = new Set(ids.map((id) => useClassStore.getState().sessions.find((s) => s.id === id)?.classId).filter(Boolean) as string[]);
  await Promise.all([...classIds].map((c) => loadClassSessions(c).catch(() => undefined)));
  return { confirmed: r.confirmed, failed: r.failed.map((f) => ({ ...f, id: fromGuid(f.id) })) };
}
