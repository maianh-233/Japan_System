/**
 * Du học / XKLĐ (HV-17, giai đoạn 23) — backend Students: /api/v1/students/abroad-cases.
 * Đổ vào `useAbroadStore.cases` như dữ liệu giả cũ. Server tự ghi nhật ký khi tạo / chuyển bước / dừng / mở lại; kiểm điều kiện sang bước.
 */
import type { QLevel } from '@/features/academic/questions/types';
import { api } from '@/lib/api/http';
import { fromGuid, toGuid } from '@/lib/api/ids';
import { useAbroadStore, type AbroadCase, type DocState, type Program, type Stage } from './abroad';

interface ServerCase {
  id: string;
  studentId: string;
  studentCode: string;
  studentName: string;
  branchId: string;
  program: Program;
  partner: string;
  destination: string;
  job?: string | null;
  intake: string;
  jlptRequired?: string | null;
  stage: Stage;
  cancelled?: { reason: string; at: string } | null;
  docs: { id: string; name: string; state: DocState }[];
  interview?: { date: string; result?: 'pass' | 'fail' | null } | null;
  coe?: { submittedAt?: string | null; grantedAt?: string | null } | null;
  visa?: { appliedAt?: string | null; issuedAt?: string | null } | null;
  flightDate?: string | null;
  fee: { total: number; paid: number };
  assigneeId: string;
  assignee: string;
  notes: { at: string; by: string; text: string }[];
  createdAt: string;
  rowVersion: number;
}

const versions = new Map<string, number>();
const u = <T>(v: T | null | undefined) => v ?? undefined;

function toCase(c: ServerCase): AbroadCase {
  const id = fromGuid(c.id);
  versions.set(id, c.rowVersion);
  return {
    id,
    studentId: fromGuid(c.studentId),
    program: c.program,
    partner: c.partner,
    destination: c.destination,
    job: u(c.job),
    intake: c.intake,
    jlptRequired: (c.jlptRequired ?? undefined) as QLevel | undefined,
    stage: c.stage,
    cancelled: u(c.cancelled),
    docs: c.docs.map((d) => ({ id: d.id, name: d.name, state: d.state })),
    interview: c.interview ? { date: c.interview.date, result: u(c.interview.result) } : undefined,
    coe: c.coe ? { submittedAt: u(c.coe.submittedAt), grantedAt: u(c.coe.grantedAt) } : undefined,
    visa: c.visa ? { appliedAt: u(c.visa.appliedAt), issuedAt: u(c.visa.issuedAt) } : undefined,
    flightDate: u(c.flightDate),
    fee: c.fee,
    assignee: c.assignee,
    assigneeId: fromGuid(c.assigneeId),
    notes: c.notes,
    createdAt: c.createdAt,
    studentName: c.studentName,
    studentCode: c.studentCode,
    branchId: fromGuid(c.branchId),
  };
}

function upsert(c: ServerCase) {
  const item = toCase(c);
  useAbroadStore.setState((s) => ({ cases: s.cases.some((x) => x.id === item.id) ? s.cases.map((x) => (x.id === item.id ? item : x)) : [item, ...s.cases] }));
  return item;
}

const base = '/students/abroad-cases';
const path = (id: string) => `${base}/${toGuid(id)}`;

/** HV-17 — mọi hồ sơ trong phạm vi (cả hồ sơ đã dừng — trang lọc ẩn / hiện) */
export async function loadAbroad() {
  const list = await api<ServerCase[]>(`${base}?includeCancelled=true`);
  useAbroadStore.setState({ cases: list.map(toCase) });
}

export interface AbroadInput {
  program: Program;
  partner: string;
  destination: string;
  job?: string;
  intake: string;
  jlptRequired?: QLevel;
  feeTotal: number;
  assigneeId: string;
}

const body = (v: AbroadInput) => ({
  program: v.program,
  partner: v.partner,
  destination: v.destination,
  job: v.job ?? null,
  intake: v.intake,
  jlptRequired: v.jlptRequired ?? null,
  feeTotal: v.feeTotal,
  assigneeId: toGuid(v.assigneeId),
});

/** Tạo hồ sơ — checklist giấy tờ theo chương trình, ghi chú "Tạo hồ sơ." */
export const createAbroad = async (studentId: string, v: AbroadInput) => upsert(await api<ServerCase>(base, { body: { ...body(v), studentId: toGuid(studentId) } }));

/** Sửa thông tin chung (kiểm sửa đè) — gửi đủ bộ */
export async function updateAbroad(c: AbroadCase, patch: Partial<AbroadInput>) {
  const v: AbroadInput = {
    program: c.program,
    partner: c.partner,
    destination: c.destination,
    job: c.job,
    intake: c.intake,
    jlptRequired: c.jlptRequired,
    feeTotal: c.fee.total,
    assigneeId: c.assigneeId ?? '',
    ...patch,
  };
  return upsert(await api<ServerCase>(path(c.id), { method: 'PUT', body: { ...body(v), studentId: toGuid(c.studentId), rowVersion: versions.get(c.id) ?? 0 } }));
}

/** Sang bước kế (server kiểm điều kiện, tự ghi nhật ký) */
export const advanceAbroad = async (id: string) => upsert(await api<ServerCase>(`${path(id)}/advance`, { method: 'POST' }));

export const setAbroadDoc = async (id: string, docId: string, state: DocState) =>
  upsert(await api<ServerCase>(`${path(id)}/documents/${docId}`, { method: 'PUT', body: { state } }));

/** Mốc thời gian — gửi đủ bộ (trường trống = xóa) */
export async function setAbroadMilestones(c: AbroadCase, patch: Partial<Pick<AbroadCase, 'interview' | 'coe' | 'visa' | 'flightDate'>>) {
  const n = { ...c, ...patch };
  return upsert(
    await api<ServerCase>(`${path(c.id)}/milestones`, {
      method: 'PUT',
      body: {
        interviewDate: n.interview?.date ?? null,
        interviewResult: n.interview?.result ?? null,
        coeSubmittedAt: n.coe?.submittedAt ?? null,
        coeGrantedAt: n.coe?.grantedAt ?? null,
        visaAppliedAt: n.visa?.appliedAt ?? null,
        visaIssuedAt: n.visa?.issuedAt ?? null,
        flightDate: n.flightDate ?? null,
      },
    }),
  );
}

export const payAbroad = async (id: string, amount: number) => upsert(await api<ServerCase>(`${path(id)}/payments`, { body: { amount } }));
export const noteAbroad = async (id: string, text: string) => upsert(await api<ServerCase>(`${path(id)}/notes`, { body: { text } }));
export const cancelAbroad = async (id: string, reason: string) => upsert(await api<ServerCase>(`${path(id)}/cancel`, { body: { text: reason } }));
export const reopenAbroad = async (id: string) => upsert(await api<ServerCase>(`${path(id)}/reopen`, { method: 'POST' }));
