/**
 * Nối Học viên & Ghi danh với backend thật (/api/v1/students — user story 06, giai đoạn 14).
 * Đọc rồi ĐỔ VÀO store cũ (hồ sơ HV ở useCrmStore.students, ghi danh ở useStudentStore.enrollments; Id mock nhờ seedIds)
 * — trang khác (Tuyển sinh, Tài chính, cổng HV / PH) vẫn đọc store như trước.
 * ⏳ Phụ huynh (Crm), hóa đơn (Finance) còn dữ liệu giả: ghi danh / chuyển lớp / cho nghỉ xong thì tạo / hủy hóa đơn giả ở trình duyệt.
 */
import { USE_API } from '@/lib/api/config';
import { api, ApiError } from '@/lib/api/http';
import { fromGuid, toGuid } from '@/lib/api/ids';
import { useClassStore } from '@/features/academic/classes/classStore';
import { useCrmStore } from '@/features/crm/crmStore';
import type { Student } from '@/features/crm/types';
import { addMockInvoice, cancelMockInvoice, useStudentStore } from './studentStore';
import type { Enrollment, EnrollmentStatus, Gender } from './types';

interface ServerStudent {
  id: string;
  code: string;
  fullName: string;
  gender?: Gender;
  dob?: string;
  phone?: string;
  email?: string;
  address?: string;
  note?: string;
  branchId: string;
  joinedAt: string;
  hidden: boolean;
  hiddenAt?: string;
  studyStatus: string;
  activeClassIds: string[];
  rowVersion: number;
}

interface ServerEnrollment {
  id: string;
  studentId: string;
  classId: string;
  classCode?: string;
  branchId: string;
  status: EnrollmentStatus;
  enrolledAt: string;
  endedAt?: string;
  note?: string;
  reservedUntil?: string;
  transferredTo?: string;
  transferredFrom?: string;
  certificate?: { code: string; issuedAt: string; sentTo: string; sentBy: string; sendCount: number; exception?: string };
}

interface EnrollmentResult {
  enrollments: ServerEnrollment[];
  warnings: string[];
}

const versions = new Map<string, number>();

function toStudent(s: ServerStudent): Student {
  const id = fromGuid(s.id);
  versions.set(id, s.rowVersion);
  return {
    id,
    code: s.code,
    fullName: s.fullName,
    gender: s.gender,
    dob: s.dob,
    phone: s.phone,
    email: s.email,
    address: s.address,
    note: s.note,
    branchId: fromGuid(s.branchId),
    joinedAt: s.joinedAt,
    hidden: s.hidden,
    hiddenAt: s.hiddenAt,
  };
}

const toEnrollment = (e: ServerEnrollment): Enrollment => ({
  id: fromGuid(e.id),
  studentId: fromGuid(e.studentId),
  classId: fromGuid(e.classId),
  status: e.status,
  enrolledAt: e.enrolledAt,
  endedAt: e.endedAt,
  note: e.note,
  reservedUntil: e.reservedUntil,
  transferredTo: e.transferredTo ? fromGuid(e.transferredTo) : undefined,
  transferredFrom: e.transferredFrom ? fromGuid(e.transferredFrom) : undefined,
  certificate: e.certificate,
});

function upsertStudents(list: ServerStudent[]) {
  const fresh = list.map(toStudent);
  const ids = new Set(fresh.map((s) => s.id));
  useCrmStore.setState((s) => ({ students: [...s.students.filter((x) => !ids.has(x.id)), ...fresh] }));
  return fresh;
}

function upsertEnrollments(list: ServerEnrollment[]) {
  const fresh = list.map(toEnrollment);
  const ids = new Set(fresh.map((e) => e.id));
  useStudentStore.setState((s) => ({ enrollments: [...s.enrollments.filter((x) => !ids.has(x.id)), ...fresh] }));
  return fresh;
}

// ---------------- Đọc ----------------

/** Hồ sơ HV (kể cả đã ẩn) + mọi ghi danh trong phạm vi. Không có quyền → giữ dữ liệu giả cho trang chưa nối */
export async function loadStudents() {
  try {
    const list = await api<ServerStudent[]>('/students?includeHidden=true');
    versions.clear();
    useCrmStore.setState({ students: list.map(toStudent) });
  } catch (e) {
    if (!(e instanceof ApiError) || e.status !== 403) throw e;
  }
  await loadEnrollments().catch(() => undefined);
}

/** Ghi danh trong phạm vi (xem hồ sơ HV hoặc xem lớp) */
export async function loadEnrollments(classId?: string) {
  try {
    const list = await api<ServerEnrollment[]>(`/students/enrollments${classId ? `?classId=${toGuid(classId)}` : ''}`);
    if (classId) {
      const fresh = list.map(toEnrollment);
      useStudentStore.setState((s) => ({ enrollments: [...s.enrollments.filter((x) => x.classId !== classId), ...fresh] }));
    } else useStudentStore.setState({ enrollments: list.map(toEnrollment) });
  } catch (e) {
    if (!(e instanceof ApiError) || e.status !== 403) throw e;
  }
}

async function refreshStudent(id: string) {
  upsertStudents([await api<ServerStudent>(`/students/${toGuid(id)}`)]);
}

// ---------------- Hồ sơ ----------------

export interface StudentInput {
  fullName: string;
  gender?: Gender;
  dob?: string;
  phone?: string;
  email?: string;
  address?: string;
  note?: string;
  branchId: string;
}

const studentBody = (v: StudentInput) => ({
  fullName: v.fullName,
  gender: v.gender ?? null,
  dob: v.dob ?? null,
  phone: v.phone || null,
  email: v.email || null,
  address: v.address || null,
  note: v.note || null,
  branchId: toGuid(v.branchId),
});

/** HV-03 — server chuẩn hóa SĐT / email, chặn sửa đè */
export async function updateStudent(id: string, v: StudentInput) {
  upsertStudents([await api<ServerStudent>(`/students/${toGuid(id)}`, { method: 'PUT', body: { ...studentBody(v), rowVersion: versions.get(id) ?? 0 } })]);
}

export interface ImportResult {
  created: Student[];
  errors: { row: number; message: string }[];
  duplicates: { row: number; fullName: string; existingCode: string }[];
}

/** HV-04 — 1 request nhiều dòng; dòng trùng SĐT + họ tên chỉ nhập khi `importDuplicates` */
export async function importStudents(rows: StudentInput[], importDuplicates: boolean): Promise<ImportResult> {
  const r = await api<{ created: ServerStudent[]; errors: ImportResult['errors']; duplicates: ImportResult['duplicates'] }>('/students/import', {
    body: { rows: rows.map(studentBody), importDuplicates },
  });
  return { created: upsertStudents(r.created), errors: r.errors, duplicates: r.duplicates };
}

/** HV-05 — ẩn 1 / nhiều HV; HV còn học bị bỏ qua kèm lý do */
export async function hideStudents(ids: string[]) {
  const r = await api<{ hidden: ServerStudent[]; skipped: { id: string; code: string; reason: string }[] }>('/students/hide', {
    body: { ids: ids.map(toGuid) },
  });
  upsertStudents(r.hidden);
  return { hidden: r.hidden.length, skipped: r.skipped.map((s) => ({ ...s, id: fromGuid(s.id) })) };
}

export const unhideStudent = async (id: string) => void upsertStudents([await api<ServerStudent>(`/students/${toGuid(id)}/unhide`, { method: 'POST' })]);

// ---------------- Ghi danh ----------------

/** Sau thao tác ghi danh: cập nhật ghi danh + hồ sơ HV liên quan (trạng thái học, ẩn) */
async function applyResult(r: EnrollmentResult): Promise<string[]> {
  const fresh = upsertEnrollments(r.enrollments);
  await Promise.all([...new Set(fresh.map((e) => e.studentId))].map((id) => refreshStudent(id).catch(() => undefined)));
  return r.warnings;
}

const branchOfClass = (classId: string) => useClassStore.getState().classes.find((c) => c.id === classId)?.branchId ?? '';

/** HV-06 — ghi danh HV có sẵn (server kiểm lớp còn chỗ, chưa trùng); hóa đơn giả theo học phí lớp */
export async function enroll(studentId: string, classId: string, opts: { amount?: number; discount?: number; dueDate?: string }) {
  const r = await api<EnrollmentResult>('/students/enrollments', {
    body: { studentId: toGuid(studentId), classId: toGuid(classId), amount: opts.amount ?? null, discount: opts.discount ?? 0, dueDate: opts.dueDate ?? null },
  });
  const warnings = await applyResult(r);
  const e = r.enrollments[0];
  const fee = opts.amount ?? useClassStore.getState().classes.find((c) => c.id === classId)?.fee ?? 0;
  addMockInvoice(fromGuid(e.id), branchOfClass(classId), fee, opts.discount ?? 0, opts.dueDate);
  return warnings;
}

/** HV-07 */
export const reserveEnrollment = async (id: string, until: string, reason: string) =>
  applyResult(await api<EnrollmentResult>(`/students/enrollments/${toGuid(id)}/reserve`, { body: { until, reason } }));

/** HV-08 */
export const resumeEnrollment = async (id: string) =>
  applyResult(await api<EnrollmentResult>(`/students/enrollments/${toGuid(id)}/resume`, { method: 'POST' }));

/** HV-10 — hủy công nợ còn lại của hóa đơn giả */
export async function dropEnrollment(id: string, reason: string) {
  const warnings = await applyResult(await api<EnrollmentResult>(`/students/enrollments/${toGuid(id)}/drop`, { body: { reason } }));
  cancelMockInvoice(id);
  return warnings;
}

/** HV-09 — chuyển 1 / nhiều HV sang 1 lớp: hóa đơn cũ hủy phần còn lại, hóa đơn mới = học phí − khấu trừ */
export async function transferEnrollments(
  targetClassId: string,
  items: { enrollmentId: string; amount: number; credit: number }[],
  dueDate: string,
  reason: string,
) {
  const r = await api<EnrollmentResult>('/students/enrollments/transfer', {
    body: {
      targetClassId: toGuid(targetClassId),
      items: items.map((i) => ({ enrollmentId: toGuid(i.enrollmentId), amount: i.amount, credit: i.credit })),
      dueDate,
      reason,
    },
  });
  const warnings = await applyResult(r);
  for (const item of items) {
    cancelMockInvoice(item.enrollmentId);
    const next = r.enrollments.find((e) => e.transferredFrom && fromGuid(e.transferredFrom) === item.enrollmentId);
    if (next) addMockInvoice(fromGuid(next.id), branchOfClass(targetClassId), item.amount, item.credit, dueDate);
  }
  return warnings;
}

/** Lớp kết thúc / bị hủy: server tự cập nhật ghi danh (chạy nền) → tải lại sau vài giây */
export function reloadClassEnrollmentsSoon(classId: string) {
  if (!USE_API) return;
  setTimeout(() => void loadEnrollments(classId).catch(() => undefined), 2500);
}
