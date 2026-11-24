/**
 * Thi JLPT (HV-16, giai đoạn 23) — backend Students: /api/v1/students/jlpt-sessions, /jlpt-registrations, cổng HV / PH /portal/learning/jlpt.
 * Đổ vào `useJlptStore` (kỳ thi + lượt đăng ký) như dữ liệu giả cũ. Kết quả đỗ / trượt server tính — trang vẫn dùng `judge()` (cùng quy chế).
 */
import type { QLevel } from '@/features/academic/questions/types';
import { api } from '@/lib/api/http';
import { fromGuid, toGuid, toGuids } from '@/lib/api/ids';
import { useJlptStore, type DocKey, type JlptReg, type JlptSession, type JlptStatus } from './jlpt';

interface ServerSession {
  id: string;
  label: string;
  examDate: string;
  regClose: string;
  resultDate: string;
  fees: Record<string, number>;
  venues: string[];
  closed: boolean;
}

interface ServerReg {
  id: string;
  studentId: string;
  studentCode: string;
  studentName: string;
  branchId: string;
  sessionId: string;
  level: string;
  venue: string;
  status: JlptStatus;
  docs: Record<DocKey, boolean>;
  fee: number;
  paidAt?: string | null;
  admitNo?: string | null;
  scores?: number[] | null;
  certificate?: { no: string; receivedAt: string; receivedBy: string; handedAt?: string | null; handedBy?: string | null; handedTo?: string | null } | null;
  note?: string | null;
  createdAt: string;
}

const toSession = (s: ServerSession): JlptSession => ({
  id: fromGuid(s.id),
  label: s.label,
  examDate: s.examDate,
  regClose: s.regClose,
  resultDate: s.resultDate,
  fees: s.fees as Record<QLevel, number>,
  venues: s.venues,
});

const u = <T>(v: T | null | undefined) => v ?? undefined;

const toReg = (r: ServerReg): JlptReg => ({
  id: fromGuid(r.id),
  studentId: fromGuid(r.studentId),
  sessionId: fromGuid(r.sessionId),
  level: r.level as QLevel,
  venue: r.venue,
  status: r.status,
  docs: r.docs,
  paidAt: u(r.paidAt),
  admitNo: u(r.admitNo),
  scores: u(r.scores),
  certificate: r.certificate
    ? { ...r.certificate, handedAt: u(r.certificate.handedAt), handedBy: u(r.certificate.handedBy), handedTo: u(r.certificate.handedTo) }
    : undefined,
  note: u(r.note),
  createdAt: r.createdAt,
  studentName: r.studentName,
  studentCode: r.studentCode,
  branchId: fromGuid(r.branchId),
  fee: r.fee,
});

function upsert(list: ServerReg[]) {
  const fresh = list.map(toReg);
  const ids = new Set(fresh.map((r) => r.id));
  useJlptStore.setState((s) => ({ regs: [...s.regs.filter((r) => !ids.has(r.id)), ...fresh] }));
  return fresh;
}

const regs = '/students/jlpt-registrations';
const regPath = (id: string) => `${regs}/${toGuid(id)}`;

/** HV-16 — kỳ thi + mọi lượt đăng ký trong phạm vi chi nhánh */
export async function loadJlpt() {
  const [sessions, list] = await Promise.all([api<ServerSession[]>('/students/jlpt-sessions'), api<ServerReg[]>(regs)]);
  useJlptStore.setState({ sessions: sessions.map(toSession), regs: list.map(toReg) });
}

/** Đăng ký nhiều HV 1 lần — HV đã đăng ký kỳ / ngoài phạm vi bị bỏ qua (kèm lý do); quá hạn nhận hồ sơ chỉ cảnh báo */
export async function registerJlpt(sessionId: string, studentIds: string[], level: QLevel, venue: string) {
  const r = await api<{ created: ServerReg[]; skipped: { studentId: string; studentCode?: string | null; message: string }[]; warnings: string[] }>(regs, {
    body: { sessionId: toGuid(sessionId), studentIds: toGuids(studentIds), level, venue },
  });
  upsert(r.created);
  return { created: r.created.length, skipped: r.skipped.map((x) => `${x.studentCode ?? 'HV'}: ${x.message}`), warnings: r.warnings };
}

/** Cấp độ / địa điểm / giấy tờ / ghi chú (trước khi nộp hội đồng) — gửi đủ bộ */
export async function updateJlptReg(r: JlptReg, patch: Partial<Pick<JlptReg, 'level' | 'venue' | 'docs' | 'note'>>) {
  const next = { ...r, ...patch };
  return upsert([
    await api<ServerReg>(regPath(r.id), { method: 'PUT', body: { level: next.level, venue: next.venue, docs: next.docs, note: next.note ?? null } }),
  ])[0];
}

export type JlptStepName = 'pay' | 'submit' | 'admit' | 'result' | 'absent' | 'cancel' | 'cert-received' | 'cert-handed';

/** Chuyển bước 1 lượt; dữ liệu kèm theo tùy bước (số báo danh, điểm, lý do hủy, số chứng chỉ, người nhận) */
export const jlptStep = async (id: string, step: JlptStepName, body?: { admitNo?: string; scores?: number[]; note?: string; no?: string; handedTo?: string }) =>
  upsert([await api<ServerReg>(`${regPath(id)}/${step}`, { method: 'POST', body: body ?? {} })])[0];

/** Hàng loạt (1 request): đóng lệ phí / đã nộp hội đồng — lượt không chuyển được trả lý do */
export async function jlptBulk(ids: string[], step: 'pay' | 'submit') {
  const r = await api<{ updated: ServerReg[]; skipped: { id: string; message: string }[] }>(`${regs}/${step}`, { body: { ids: toGuids(ids) } });
  upsert(r.updated);
  return { updated: r.updated.length, skipped: r.skipped.map((x) => ({ id: fromGuid(x.id), message: x.message })) };
}

// ---------------- Cổng HV / PH ----------------

const child = (studentId?: string) => (studentId ? `?studentId=${toGuid(studentId)}` : '');

/** Kỳ thi + lượt đăng ký của HV (PH: con đang chọn) */
export async function loadMyJlpt(studentId?: string) {
  const r = await api<{ sessions: ServerSession[]; registrations: ServerReg[] }>(`/students/portal/learning/jlpt${child(studentId)}`);
  useJlptStore.setState({ sessions: r.sessions.map(toSession), regs: r.registrations.map(toReg) });
}

/** HV tự đăng ký kỳ còn nhận hồ sơ (PH đăng ký cho con) */
export const registerMyJlpt = async (studentId: string | undefined, sessionId: string, level: QLevel, venue: string) =>
  upsert([
    await api<ServerReg>('/students/portal/learning/jlpt-registrations', {
      body: { studentId: studentId ? toGuid(studentId) : null, sessionId: toGuid(sessionId), level, venue },
    }),
  ])[0];
