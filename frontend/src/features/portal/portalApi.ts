/**
 * Nối CỔNG GV / HV / PH và Đơn chờ xử lý của giáo vụ với backend thật (giai đoạn 15 — user story 07 DT-09 → DT-19, 06 HV-14).
 * Đọc rồi ĐỔ VÀO store cũ (usePortalStore, useClassStore, useCrmStore, useStudentStore, usePromotionStore) để các trang giữ nguyên;
 * mỗi thao tác ghi: gọi server → cập nhật store bằng kết quả server. Lỗi ném ApiError (thông điệp tiếng Việt) cho trang hiển thị.
 * Id: server GUID ↔ Id mock nhờ seedIds (fromGuid / toGuid).
 */
import dayjs from 'dayjs';
import { api, ApiError } from '@/lib/api/http';
import { fromGuid, fromGuids, toGuid, toGuids } from '@/lib/api/ids';
import { replaceSessions, toClass, toSession, upsertSession, type ServerClass, type ServerSession, type SessionResult } from '@/features/academic/classes/classApi';
import { useClassStore } from '@/features/academic/classes/classStore';
import type { AttendanceRecord, AttendanceStatus, ClassInfo, ClassSession, EvalRecommendation, LessonCoverage, StaffRole, StudentEvaluation } from '@/features/academic/classes/types';
import { usePromotionStore } from '@/features/academic/promotion/promotionStore';
import type { PromotionCandidate } from '@/features/academic/promotion/types';
import { useCrmStore } from '@/features/crm/crmStore';
import type { Student } from '@/features/crm/types';
import { useStudentStore } from '@/features/student/studentStore';
import type { Enrollment, EnrollmentStatus } from '@/features/student/types';
import { usePortalStore } from './portalStore';
import type { CancelRequest, LeaveRequest, SubInvite } from './types';

// ---------------- Kiểu dữ liệu server ----------------

interface ServerRequestSession {
  no: number;
  date: string;
  startTime: string;
  endTime: string;
  status: string;
}

interface ServerCancelRequest {
  id: string;
  sessionId: string;
  classId: string;
  classCode: string;
  branchId: string;
  session?: ServerRequestSession;
  teacherId: string;
  teacherName: string;
  reasonType: string;
  reason: string;
  makeupAt?: string;
  status: CancelRequest['status'];
  createdAt: string;
  decidedById?: string;
  decidedBy?: string;
  decidedAt?: string;
  note?: string;
}

interface ServerInvite {
  id: string;
  sessionId: string;
  classId: string;
  classCode: string;
  session?: ServerRequestSession;
  cancelRequestId?: string;
  fromTeacherId: string;
  toTeacherId: string;
  toTeacherName: string;
  reason: string;
  invitedById: string;
  invitedBy: string;
  createdAt: string;
  respondBy: string;
  status: SubInvite['status'];
  respondedAt?: string;
  declineReason?: string;
}

interface ServerLeaveRequest {
  id: string;
  studentId: string;
  studentName: string;
  classId: string;
  classCode: string;
  sessionId: string;
  session?: ServerRequestSession;
  reasonType: string;
  reason: string;
  by: 'student' | 'parent';
  byName: string;
  status: LeaveRequest['status'];
  createdAt: string;
  decidedBy?: string;
  decidedAt?: string;
  makeupSessionId?: string;
  makeup?: { sessionId: string; classId: string; classCode: string; date: string; startTime: string; endTime: string };
  makeupResult?: LeaveRequest['makeupResult'];
  note?: string;
}

interface Decision<T> {
  request: T;
  warnings: string[];
}

interface ServerPortalStudent {
  studentId: string;
  enrollmentId: string;
  code: string;
  fullName: string;
  status: EnrollmentStatus;
  reservedUntil?: string;
}

interface ServerChild {
  id: string;
  code: string;
  fullName: string;
  dob?: string;
  branchId: string;
}

interface ServerLearningClass {
  studentId: string;
  enrollmentId: string;
  status: EnrollmentStatus;
  enrolledAt: string;
  endedAt?: string;
  reservedUntil?: string;
  class: ServerClass;
}

interface ServerStudentSession {
  id: string;
  studentId: string;
  classId: string;
  no: number;
  date: string;
  startTime: string;
  endTime: string;
  kind: 'lesson' | 'exam';
  status: 'scheduled' | 'completed' | 'cancelled';
  origin: 'regular' | 'makeup' | 'extra';
  teacherIds: string[];
  room?: string;
  online: boolean;
  guest: boolean;
  attendance?: { status: AttendanceStatus; checkInAt?: string; note?: string; madeUp: boolean };
}

/** Buổi học bù chọn được (DT-18 bước 2) */
export interface MakeupOption {
  sessionId: string;
  classId: string;
  classCode: string;
  className: string;
  no: number;
  date: string;
  startTime: string;
  endTime: string;
  room?: string;
  online: boolean;
  seatsLeft: number;
}

/** GV gợi ý dạy thay */
export interface SubstituteCandidate {
  teacherId: string;
  fullName: string;
  code: string;
  sameBranch: boolean;
  busyWith?: string;
}

/** Mã QR của buổi (DT-09 bước 2) */
export interface SessionQr {
  payload: string;
  code: string;
  opensAt: string;
  closesAt: string;
  isOpen: boolean;
}

export interface ScanResult {
  sessionId: string;
  classId: string;
  classCode: string;
  className: string;
  no: number;
  status: AttendanceStatus;
  checkInAt?: string;
}

interface ServerPromotionOffer {
  id: string;
  studentId: string;
  fromClassId: string;
  toClassId: string;
  seatHeld: boolean;
  holdUntil?: string;
  interest?: { at: string; by: 'student' | 'parent' };
}

interface ServerPromotion {
  id: string;
  studentId: string;
  fromClassId: string;
  toClassId?: string;
  seatHeld: boolean;
  holdUntil?: string;
  status: PromotionCandidate['status'];
  source: PromotionCandidate['source'];
  handedOverAt?: string;
  saleId?: string;
  interest?: { at: string; by: 'student' | 'parent' };
  declineReason?: string;
  enrollmentId?: string;
  confirmedAt?: string;
  confirmedBy?: string;
  notes: { id: string; at: string; by: string; text: string; channel?: string }[];
  createdAt: string;
  createdBy?: string;
}

interface ServerEvaluation {
  id: string;
  classId: string;
  studentId: string;
  teacherId: string;
  author: string;
  skills: StudentEvaluation['skills'];
  attitude: number;
  strengths?: string;
  weaknesses?: string;
  recommendation: EvalRecommendation;
  advice?: string;
  createdAt: string;
  updatedAt: string;
}

// ---------------- Đổi dữ liệu ----------------

const toCancelRequest = (r: ServerCancelRequest): CancelRequest => ({
  id: fromGuid(r.id),
  sessionId: fromGuid(r.sessionId),
  classId: fromGuid(r.classId),
  teacherId: fromGuid(r.teacherId),
  reasonType: r.reasonType,
  reason: r.reason,
  makeupAt: r.makeupAt,
  status: r.status,
  createdAt: r.createdAt,
  decidedBy: r.decidedBy,
  decidedAt: r.decidedAt,
  note: r.note,
});

const toInvite = (i: ServerInvite): SubInvite => ({
  id: fromGuid(i.id),
  sessionId: fromGuid(i.sessionId),
  classId: fromGuid(i.classId),
  fromTeacherId: fromGuid(i.fromTeacherId),
  toTeacherId: fromGuid(i.toTeacherId),
  reason: i.reason,
  invitedBy: i.invitedBy,
  createdAt: i.createdAt,
  respondBy: i.respondBy,
  status: i.status,
  respondedAt: i.respondedAt,
  declineReason: i.declineReason,
});

const toLeave = (r: ServerLeaveRequest): LeaveRequest => ({
  id: fromGuid(r.id),
  studentId: fromGuid(r.studentId),
  classId: fromGuid(r.classId),
  sessionId: fromGuid(r.sessionId),
  reasonType: r.reasonType,
  reason: r.reason,
  by: r.by,
  byName: r.byName,
  status: r.status,
  createdAt: r.createdAt,
  decidedBy: r.decidedBy,
  decidedAt: r.decidedAt,
  makeupSessionId: r.makeupSessionId ? fromGuid(r.makeupSessionId) : undefined,
  makeupResult: r.makeupResult,
  note: r.note,
});

const toPromotion = (p: ServerPromotion): PromotionCandidate => ({
  id: fromGuid(p.id),
  studentId: fromGuid(p.studentId),
  fromClassId: fromGuid(p.fromClassId),
  toClassId: p.toClassId ? fromGuid(p.toClassId) : undefined,
  seatHeld: p.seatHeld,
  holdUntil: p.holdUntil,
  status: p.status,
  source: p.source,
  handedOverAt: p.handedOverAt,
  saleId: p.saleId ? fromGuid(p.saleId) : undefined,
  interest: p.interest,
  declineReason: p.declineReason,
  enrollmentId: p.enrollmentId ? fromGuid(p.enrollmentId) : undefined,
  confirmedAt: p.confirmedAt,
  confirmedBy: p.confirmedBy,
  notes: p.notes.map((n) => ({ id: n.id, at: n.at, by: n.by, text: n.text, channel: n.channel as PromotionCandidate['notes'][number]['channel'] })),
  createdAt: p.createdAt,
  createdBy: p.createdBy ?? '',
});

const toEvaluation = (e: ServerEvaluation): StudentEvaluation => ({
  id: fromGuid(e.id),
  classId: fromGuid(e.classId),
  studentId: fromGuid(e.studentId),
  teacherId: fromGuid(e.teacherId),
  author: e.author,
  skills: e.skills,
  attitude: e.attitude,
  strengths: e.strengths,
  weaknesses: e.weaknesses,
  recommendation: e.recommendation,
  advice: e.advice,
  createdAt: e.createdAt,
  updatedAt: e.updatedAt,
});

/** Thay / thêm phần tử theo id */
function merge<T extends { id: string }>(list: T[], items: T[]): T[] {
  const ids = new Set(items.map((x) => x.id));
  return [...items, ...list.filter((x) => !ids.has(x.id))];
}

const upsertCancel = (r: ServerCancelRequest) => {
  const x = toCancelRequest(r);
  usePortalStore.setState((s) => ({ cancelRequests: merge(s.cancelRequests, [x]) }));
  return x;
};
const upsertInvites = (list: ServerInvite[]) => {
  const xs = list.map(toInvite);
  usePortalStore.setState((s) => ({ subInvites: merge(s.subInvites, xs) }));
  return xs;
};
const upsertLeave = (r: ServerLeaveRequest) => {
  const x = toLeave(r);
  usePortalStore.setState((s) => ({ leaveRequests: merge(s.leaveRequests, [x]) }));
  return x;
};

/** Đọc danh sách — không có quyền (403) thì trả rỗng để trang hiện "không có dữ liệu" thay vì dữ liệu giả */
async function listOrEmpty<T>(path: string): Promise<T[]> {
  try {
    return await api<T[]>(path);
  } catch (e) {
    if (e instanceof ApiError && e.status === 403) return [];
    throw e;
  }
}

const range = (back: number, ahead: number) => ({
  from: dayjs().subtract(back, 'day').format('YYYY-MM-DD'),
  to: dayjs().add(ahead, 'day').format('YYYY-MM-DD'),
});

// ---------------- Tải dữ liệu theo cổng ----------------

/** Nhân viên: Đơn chờ xử lý (đơn hủy buổi, đơn xin nghỉ, lời mời dạy thay) */
export async function loadStaffRequests() {
  const [cancels, leaves, invites] = await Promise.all([
    listOrEmpty<ServerCancelRequest>('/academic/requests/cancel'),
    listOrEmpty<ServerLeaveRequest>('/academic/requests/leave'),
    listOrEmpty<ServerInvite>('/academic/substitute-invites'),
  ]);
  usePortalStore.setState({ cancelRequests: cancels.map(toCancelRequest), leaveRequests: leaves.map(toLeave), subInvites: invites.map(toInvite) });
}

/** Cổng GV: lớp mình dạy (+ HV), buổi -30 → +60 ngày, đơn hủy buổi, lời mời dạy thay */
export async function loadTeacherPortal() {
  const classes = await api<{ class: ServerClass; students: ServerPortalStudent[] }[]>('/academic/portal/teaching/classes');
  const students: Student[] = [];
  const enrollments: Enrollment[] = [];
  classes.forEach((c) =>
    c.students.forEach((s) => {
      const id = fromGuid(s.studentId);
      if (!students.some((x) => x.id === id))
        students.push({ id, code: s.code, fullName: s.fullName, branchId: fromGuid(c.class.branchId), joinedAt: '', hidden: false } as Student);
      enrollments.push({
        id: fromGuid(s.enrollmentId),
        studentId: id,
        classId: fromGuid(c.class.id),
        status: s.status,
        enrolledAt: '',
        reservedUntil: s.reservedUntil,
      } as Enrollment);
    }),
  );
  useClassStore.setState({ classes: classes.map((c) => toClass(c.class)) });
  useCrmStore.setState({ students });
  useStudentStore.setState({ enrollments });

  const r = range(30, 60); // API giới hạn ≤ 3 tháng
  const [sessions, cancels, invites] = await Promise.all([
    api<ServerSession[]>(`/academic/portal/teaching/sessions?from=${r.from}&to=${r.to}`),
    api<ServerCancelRequest[]>('/academic/portal/teaching/cancel-requests'),
    api<ServerInvite[]>('/academic/portal/teaching/substitute-invites'),
  ]);
  const mine = sessions.map(toSession);
  // Lời mời dạy thay trỏ tới buổi CHƯA thuộc lịch của mình → thêm buổi / lớp tạm từ thông tin lời mời để trang Lời mời hiển thị
  const known = new Set(mine.map((s) => s.id));
  const extraClasses = new Map<string, ClassInfo>();
  invites.forEach((i) => {
    const id = fromGuid(i.sessionId);
    if (known.has(id) || !i.session) return;
    known.add(id);
    const classId = fromGuid(i.classId);
    mine.push({ id, classId, no: i.session.no, date: i.session.date, startTime: i.session.startTime, endTime: i.session.endTime, status: 'scheduled', kind: 'lesson' } as ClassSession);
    if (!useClassStore.getState().classes.some((c) => c.id === classId))
      extraClasses.set(classId, { id: classId, code: i.classCode, name: i.classCode, teacherIds: [fromGuid(i.fromTeacherId)], weekdays: [], status: 'ongoing' } as unknown as ClassInfo);
  });
  useClassStore.setState((s) => ({ sessions: mine, classes: [...s.classes, ...extraClasses.values()] }));
  usePortalStore.setState({ cancelRequests: cancels.map(toCancelRequest), subInvites: invites.map(toInvite) });
}

/** Cổng GV: buổi trong khoảng (Thời khóa biểu tuần khác) */
export async function loadTeachingRange(from: string, to: string) {
  replaceSessions(await api<ServerSession[]>(`/academic/portal/teaching/sessions?from=${from}&to=${to}`), (s) => s.date >= from && s.date <= to);
}

/** Buổi HV (1 dòng / HV × buổi) → buổi của store, gom điểm danh của các con vào cùng 1 buổi */
function toLearningSessions(list: ServerStudentSession[]): ClassSession[] {
  const map = new Map<string, ClassSession>();
  list.forEach((x) => {
    const id = fromGuid(x.id);
    const s =
      map.get(id) ??
      ({
        id,
        classId: fromGuid(x.classId),
        no: x.no,
        date: x.date,
        startTime: x.startTime,
        endTime: x.endTime,
        kind: x.kind,
        status: x.origin === 'makeup' && x.status === 'scheduled' ? 'makeup' : x.status,
        origin: x.origin === 'regular' ? undefined : x.origin,
        teacherIds: fromGuids(x.teacherIds),
        room: x.room,
        records: [],
      } as ClassSession);
    if (x.attendance) {
      const rec: AttendanceRecord = { studentId: fromGuid(x.studentId), status: x.attendance.status, checkInAt: x.attendance.checkInAt, note: x.attendance.note };
      s.records = [...(s.records ?? []), rec];
    }
    map.set(id, s);
  });
  return [...map.values()].map((s) => (s.records?.length ? s : { ...s, records: undefined }));
}

/** Cổng HV / PH: HV của tài khoản (PH → các con + liên kết), lớp & ghi danh, buổi -45 → +45 ngày, đơn xin nghỉ, lớp kỳ sau */
export async function loadLearningPortal(ownerId?: string, isParent = false) {
  const children = await api<ServerChild[]>('/academic/portal/learning/students');
  const students = children.map(
    (c) => ({ id: fromGuid(c.id), code: c.code, fullName: c.fullName, dob: c.dob, branchId: fromGuid(c.branchId), joinedAt: '', hidden: false }) as Student,
  );
  useCrmStore.setState((s) => ({
    students,
    // PH: liên kết lead ↔ con lấy từ server (useChildren đọc links)
    links: isParent && ownerId ? students.map((x) => ({ leadId: ownerId, studentId: x.id, relationship: 'child' as const, createdAt: '' })) : s.links,
  }));

  const r = range(45, 45); // API giới hạn ≤ 3 tháng
  const [classes, sessions, leaves, offers] = await Promise.all([
    api<ServerLearningClass[]>('/academic/portal/learning/classes'),
    api<ServerStudentSession[]>(`/academic/portal/learning/sessions?from=${r.from}&to=${r.to}`),
    api<ServerLeaveRequest[]>('/academic/portal/learning/leave-requests'),
    api<ServerPromotionOffer[]>('/academic/portal/learning/promotions'),
  ]);
  const seen = new Set<string>();
  useClassStore.setState({
    classes: classes.filter((c) => !seen.has(c.class.id) && seen.add(c.class.id)).map((c) => toClass(c.class)),
    sessions: toLearningSessions(sessions),
  });
  useStudentStore.setState({
    enrollments: classes.map(
      (c) =>
        ({
          id: fromGuid(c.enrollmentId),
          studentId: fromGuid(c.studentId),
          classId: fromGuid(c.class.id),
          status: c.status,
          enrolledAt: c.enrolledAt,
          endedAt: c.endedAt,
          reservedUntil: c.reservedUntil,
        }) as Enrollment,
    ),
  });
  usePortalStore.setState({ leaveRequests: leaves.map(toLeave) });
  // Lớp kỳ sau gợi ý: store lên lớp chỉ giữ phần HV được xem (không ghi chú tư vấn / Sale)
  usePromotionStore.setState({
    candidates: offers.map(
      (o) =>
        ({
          id: fromGuid(o.id),
          studentId: fromGuid(o.studentId),
          fromClassId: fromGuid(o.fromClassId),
          toClassId: fromGuid(o.toClassId),
          seatHeld: o.seatHeld,
          holdUntil: o.holdUntil,
          status: 'pending',
          source: 'manual',
          interest: o.interest,
          notes: [],
          createdAt: '',
          createdBy: '',
        }) as PromotionCandidate,
    ),
  });
}

// ---------------- Đơn hủy buổi & dạy thay (DT-16) ----------------

export const submitCancelRequest = async (v: { sessionId: string; reasonType: string; reason: string; makeupAt?: string }) =>
  upsertCancel(
    await api<ServerCancelRequest>('/academic/portal/teaching/cancel-requests', {
      body: { sessionId: toGuid(v.sessionId), reasonType: v.reasonType, reason: v.reason, makeupAt: v.makeupAt ?? null },
    }),
  );

export async function withdrawCancelRequest(id: string) {
  await api(`/academic/portal/teaching/cancel-requests/${toGuid(id)}`, { method: 'DELETE' });
  usePortalStore.setState((s) => ({ cancelRequests: s.cancelRequests.filter((x) => x.id !== id) }));
}

export const decideCancelRequest = async (id: string, approve: boolean, note?: string) =>
  upsertCancel(await api<ServerCancelRequest>(`/academic/requests/cancel/${toGuid(id)}/${approve ? 'approve' : 'reject'}`, { body: { note: note ?? null } }));

export async function respondInvite(id: string, accept: boolean, reason?: string) {
  const r = await api<ServerInvite>(`/academic/portal/teaching/substitute-invites/${toGuid(id)}/${accept ? 'accept' : 'decline'}`, {
    body: accept ? undefined : { reason: reason ?? null },
    method: 'POST',
  });
  upsertInvites([r]);
  // Nhận lời → buổi đổi GV, các lời mời khác của buổi hết hiệu lực
  if (accept) await loadTeacherPortal().catch(() => undefined);
}

export async function inviteSubstitutes(v: { sessionId: string; cancelRequestId?: string; teacherIds: string[]; respondBy: string; reason: string }) {
  const list = await api<ServerInvite[]>('/academic/substitute-invites', {
    body: {
      sessionId: toGuid(v.sessionId),
      cancelRequestId: v.cancelRequestId ? toGuid(v.cancelRequestId) : null,
      teacherIds: toGuids(v.teacherIds),
      respondBy: v.respondBy,
      reason: v.reason,
    },
  });
  upsertInvites(list);
  // Đơn hủy buổi gắn kèm được tự duyệt "Đã mời GV … dạy thay"
  if (v.cancelRequestId) await loadStaffRequests().catch(() => undefined);
  return list.map(toInvite);
}

export const withdrawInvite = async (id: string) =>
  upsertInvites([await api<ServerInvite>(`/academic/substitute-invites/${toGuid(id)}/withdraw`, { method: 'POST' })])[0];

export const substituteCandidates = async (sessionId: string) =>
  (await api<(SubstituteCandidate & { teacherId: string })[]>(`/academic/substitute-invites/candidates?sessionId=${toGuid(sessionId)}`)).map((c) => ({
    ...c,
    teacherId: fromGuid(c.teacherId),
  }));

// ---------------- Đơn xin nghỉ & học bù (DT-18) ----------------

export const submitLeaveRequest = async (v: { studentId: string; sessionId: string; reasonType: string; reason: string; makeupSessionId?: string }) =>
  upsertLeave(
    await api<ServerLeaveRequest>('/academic/portal/learning/leave-requests', {
      body: {
        studentId: toGuid(v.studentId),
        sessionId: toGuid(v.sessionId),
        reasonType: v.reasonType,
        reason: v.reason,
        makeupSessionId: v.makeupSessionId ? toGuid(v.makeupSessionId) : null,
      },
    }),
  );

export async function withdrawLeaveRequest(id: string) {
  await api(`/academic/portal/learning/leave-requests/${toGuid(id)}`, { method: 'DELETE' });
  usePortalStore.setState((s) => ({ leaveRequests: s.leaveRequests.filter((x) => x.id !== id) }));
}

export const chooseMakeup = async (id: string, makeupSessionId?: string) =>
  upsertLeave(
    await api<ServerLeaveRequest>(`/academic/portal/learning/leave-requests/${toGuid(id)}/makeup`, {
      method: 'PUT',
      body: { makeupSessionId: makeupSessionId ? toGuid(makeupSessionId) : null },
    }),
  );

/** Buổi học bù chọn được. Lớp của buổi bù chưa có trong store → thêm tạm để trang hiện mã lớp / giờ */
export async function makeupOptions(sessionId: string, studentId: string): Promise<MakeupOption[]> {
  const list = await api<MakeupOption[]>(
    `/academic/portal/learning/leave-requests/makeup-options?sessionId=${toGuid(sessionId)}&studentId=${toGuid(studentId)}`,
  );
  return list.map((o) => ({ ...o, sessionId: fromGuid(o.sessionId), classId: fromGuid(o.classId) }));
}

export async function decideLeaveRequest(id: string, approve: boolean, note?: string) {
  const r = await api<Decision<ServerLeaveRequest>>(`/academic/requests/leave/${toGuid(id)}/${approve ? 'approve' : 'reject'}`, {
    body: { note: note ?? null },
  });
  return { request: upsertLeave(r.request), warnings: r.warnings };
}

// ---------------- Điểm danh, chấm công, báo cáo buổi (DT-09, 12, 14) ----------------

const teaching = (sessionId: string) => `/academic/portal/teaching/sessions/${toGuid(sessionId)}`;
const staffSession = (sessionId: string) => `/academic/class-sessions/${toGuid(sessionId)}`;

const sessionResult = (r: SessionResult) => ({ session: upsertSession(r.session), warnings: r.warnings });

/** Lưu cả danh sách điểm danh — GV trên cổng (<paramref name="asTeacher"/>) hoặc nhân viên điểm hộ */
export const saveAttendance = async (sessionId: string, records: AttendanceRecord[], asTeacher: boolean) =>
  sessionResult(
    await api<SessionResult>(`${asTeacher ? teaching(sessionId) : staffSession(sessionId)}/attendance`, {
      method: 'PUT',
      body: {
        records: records.map((r) => ({
          studentId: toGuid(r.studentId),
          status: r.status,
          checkInAt: r.checkInAt ?? null,
          note: r.note ?? null,
          leftAt: r.leftAt ?? null,
          leaveReason: r.leaveReason ?? null,
        })),
      },
    }),
  );

export const getSessionQr = (sessionId: string, asTeacher: boolean) =>
  api<SessionQr>(`${asTeacher ? teaching(sessionId) : staffSession(sessionId)}/qr`);

export const teacherCheck = async (sessionId: string, out: boolean) =>
  sessionResult(await api<SessionResult>(`${teaching(sessionId)}/${out ? 'check-out' : 'check-in'}`, { method: 'POST' }));

export const submitReport = async (
  sessionId: string,
  v: { content: string; homework?: string; issues?: string; coverage: LessonCoverage; coveredChapterIds: string[] },
) =>
  sessionResult(
    await api<SessionResult>(`${teaching(sessionId)}/report`, {
      method: 'PUT',
      body: { ...v, homework: v.homework ?? null, issues: v.issues ?? null, coveredChapterIds: toGuids(v.coveredChapterIds) },
    }),
  );

/** Giáo vụ chấm công hộ GV / trợ giảng (DT-12 bước 3) */
export const recordTeacherAttendance = async (
  sessionId: string,
  v: { teacherId: string; role: StaffRole; status: 'present' | 'late' | 'absent'; checkIn?: string; checkOut?: string; substituteId?: string; note?: string },
) =>
  sessionResult(
    await api<SessionResult>(`${staffSession(sessionId)}/teacher-attendance`, {
      method: 'PUT',
      body: {
        teacherId: toGuid(v.teacherId),
        role: v.role,
        status: v.status,
        checkIn: v.checkIn ?? null,
        checkOut: v.checkOut ?? null,
        substituteId: v.substituteId ? toGuid(v.substituteId) : null,
        note: v.note ?? null,
      },
    }),
  );

/** HV quét QR (nội dung mã) hoặc nhập mã 6 ký tự → cập nhật điểm danh của HV trên buổi trong store */
export async function scanAttendance(text: string, studentId: string): Promise<ScanResult> {
  const t = text.trim();
  const r = await api<ScanResult>('/academic/portal/learning/attendance/scan', { body: t.includes('|') ? { payload: t } : { code: t } });
  const result = { ...r, sessionId: fromGuid(r.sessionId), classId: fromGuid(r.classId) };
  const rec: AttendanceRecord = { studentId, status: r.status, checkInAt: r.checkInAt, method: 'qr' };
  useClassStore.setState((s) => ({
    sessions: s.sessions.map((x) => (x.id === result.sessionId ? { ...x, records: [...(x.records ?? []).filter((y) => y.studentId !== studentId), rec] } : x)),
  }));
  return result;
}

/** Tải lại 1 buổi (GV / nhân viên) — dùng khi nhận realtime "HV vừa quét QR" */
export async function refreshSession(sessionId: string, asTeacher: boolean) {
  upsertSession(await api<ServerSession>(asTeacher ? teaching(sessionId) : staffSession(sessionId)));
}

// ---------------- Lên lớp (DT-17) ----------------

export async function loadPromotions(classId: string) {
  const list = (await api<ServerPromotion[]>(`/academic/classes/${toGuid(classId)}/promotions`)).map(toPromotion);
  usePromotionStore.setState((s) => ({ candidates: [...list, ...s.candidates.filter((c) => c.fromClassId !== classId)] }));
  return list;
}

export async function addPromotions(classId: string, studentIds: string[], source: PromotionCandidate['source']) {
  const list = (await api<ServerPromotion[]>(`/academic/classes/${toGuid(classId)}/promotions`, { body: { studentIds: toGuids(studentIds), source } })).map(
    toPromotion,
  );
  usePromotionStore.setState((s) => ({ candidates: [...list, ...s.candidates.filter((c) => c.fromClassId !== classId)] }));
}

export async function updatePromotion(id: string, v: { toClassId?: string; holdSeat: boolean; holdUntil?: string }) {
  const r = await api<{ promotion: ServerPromotion; warnings: string[] }>(`/academic/promotions/${toGuid(id)}`, {
    method: 'PUT',
    body: { toClassId: v.toClassId ? toGuid(v.toClassId) : null, holdSeat: v.holdSeat, holdUntil: v.holdUntil ?? null },
  });
  const p = toPromotion(r.promotion);
  usePromotionStore.setState((s) => ({ candidates: s.candidates.map((c) => (c.id === p.id ? p : c)) }));
  return r.warnings;
}

export async function handOverPromotions(classId: string, ids?: string[]) {
  const list = (await api<ServerPromotion[]>(`/academic/classes/${toGuid(classId)}/promotions/hand-over`, { body: { ids: ids ? toGuids(ids) : null } })).map(
    toPromotion,
  );
  usePromotionStore.setState((s) => ({ candidates: [...list, ...s.candidates.filter((c) => c.fromClassId !== classId)] }));
  return list.filter((c) => c.handedOverAt).length;
}

export async function markInterest(id: string) {
  const o = await api<ServerPromotionOffer>(`/academic/portal/learning/promotions/${toGuid(id)}/interest`, { method: 'POST' });
  usePromotionStore.setState((s) => ({ candidates: s.candidates.map((c) => (c.id === id ? { ...c, interest: o.interest } : c)) }));
}

// ---------------- Nhận xét cuối khóa (DT-19) ----------------

export async function loadEvaluations(classId: string, asTeacher: boolean) {
  const path = asTeacher ? `/academic/portal/teaching/classes/${toGuid(classId)}/evaluations` : `/academic/classes/${toGuid(classId)}/evaluations`;
  const list = (await api<ServerEvaluation[]>(path)).map(toEvaluation);
  useClassStore.setState((s) => ({ evaluations: [...list, ...s.evaluations.filter((e) => e.classId !== classId)] }));
}

export async function saveEvaluation(
  e: Pick<StudentEvaluation, 'classId' | 'studentId' | 'skills' | 'attitude' | 'strengths' | 'weaknesses' | 'recommendation' | 'advice'>,
) {
  const saved = toEvaluation(
    await api<ServerEvaluation>(`/academic/portal/teaching/classes/${toGuid(e.classId)}/evaluations/${toGuid(e.studentId)}`, {
      method: 'PUT',
      body: {
        skills: e.skills,
        attitude: e.attitude,
        strengths: e.strengths ?? null,
        weaknesses: e.weaknesses ?? null,
        recommendation: e.recommendation,
        advice: e.advice ?? null,
      },
    }),
  );
  useClassStore.setState((s) => ({ evaluations: merge(s.evaluations, [saved]) }));
  return saved;
}
