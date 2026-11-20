/**
 * Nối Cảnh báo bỏ học (HV-14) với backend thật (/api/v1/students/at-risk, /care-logs).
 * Giao diện vẫn tự tính mức rủi ro (gồm cả bài tập / điểm / học phí còn chạy dữ liệu giả); server lưu PHẦN CHĂM SÓC:
 * người phụ trách, tiến độ, hẹn gọi lại, nhật ký liên hệ — theo lớp × HV (chưa có trường hợp thì server tự mở).
 */
import { useMemo } from 'react';
import { api } from '@/lib/api/http';
import { fromGuid, toGuid } from '@/lib/api/ids';
import { useClassStore } from '@/features/academic/classes/classStore';
import type { CareLog } from '@/features/academic/classes/types';
import { useHrStore } from '@/features/hr/hrStore';
import { caseKey, useAtRiskStore, type CareCase, type CareStatus } from './atRisk';

interface ServerCase {
  id: string;
  studentId: string;
  classId: string;
  assigneeId?: string;
  assignee?: string;
  status: CareStatus;
  nextAt?: string;
  updatedAt: string;
}

interface ServerLog {
  id: string;
  studentId: string;
  classId: string;
  channel: CareLog['channel'];
  note: string;
  by: string;
  at: string;
}

const toCase = (c: ServerCase): CareCase => ({ assignee: c.assignee, status: c.status, nextAt: c.nextAt, updatedAt: c.updatedAt });

const toLog = (l: ServerLog): CareLog => ({
  id: fromGuid(l.id),
  classId: fromGuid(l.classId),
  studentId: fromGuid(l.studentId),
  channel: l.channel,
  note: l.note,
  by: l.by,
  at: l.at,
});

/** Mọi trường hợp chăm sóc (kể cả đã ổn định) → store theo khóa lớp × HV */
export async function loadCareCases() {
  const list = await api<ServerCase[]>('/students/at-risk?includeResolved=true');
  useAtRiskStore.setState({ cases: Object.fromEntries(list.map((c) => [caseKey(fromGuid(c.classId), fromGuid(c.studentId)), toCase(c)])) });
}

/** Nhật ký liên hệ của 1 HV trong 1 lớp → thay phần tương ứng trong store lớp */
export async function loadCareLogs(classId: string, studentId: string) {
  const list = (await api<ServerLog[]>(`/students/care-logs?classId=${toGuid(classId)}&studentId=${toGuid(studentId)}`)).map(toLog);
  useClassStore.setState((s) => ({
    careLogs: [...list, ...s.careLogs.filter((l) => !(l.classId === classId && l.studentId === studentId))],
  }));
}

/** Nhật ký liên hệ của cả lớp (tab Học viên của chi tiết lớp) */
export async function loadClassCareLogs(classId: string) {
  const list = (await api<ServerLog[]>(`/students/care-logs?classId=${toGuid(classId)}`)).map(toLog);
  useClassStore.setState((s) => ({ careLogs: [...list, ...s.careLogs.filter((l) => l.classId !== classId)] }));
}

const pair = (classId: string, studentId: string) => `/students/at-risk/classes/${toGuid(classId)}/students/${toGuid(studentId)}`;

/** Giao người / đổi tiến độ / hẹn — người phụ trách chọn theo tên nhân viên (đổi ra Id) */
export async function saveCareCase(classId: string, studentId: string, patch: Partial<CareCase>) {
  const key = caseKey(classId, studentId);
  const current = useAtRiskStore.getState().cases[key] ?? { status: 'new' as CareStatus, updatedAt: '' };
  const next = { ...current, ...patch };
  const assigneeId = next.assignee ? useHrStore.getState().employees.find((e) => e.fullName === next.assignee)?.id : undefined;
  const saved = await api<ServerCase>(pair(classId, studentId), {
    method: 'PUT',
    body: { assigneeId: assigneeId ? toGuid(assigneeId) : null, status: next.status, nextAt: next.nextAt ?? null },
  });
  useAtRiskStore.setState((s) => ({ cases: { ...s.cases, [key]: toCase(saved) } }));
}

/** Ghi nhận liên hệ (case "chưa liên hệ" → "đang liên hệ" do server chuyển) */
export async function addCareLog(classId: string, studentId: string, channel: CareLog['channel'], note: string) {
  const log = toLog(await api<ServerLog>(`${pair(classId, studentId)}/logs`, { body: { channel, note } }));
  useClassStore.setState((s) => ({ careLogs: [log, ...s.careLogs] }));
  await loadCareCases().catch(() => undefined);
}

/** Nhân viên nhận chăm sóc: nhân viên (không phải GV) đang làm việc */
export function useCareStaff(): string[] {
  const employees = useHrStore((s) => s.employees);
  return useMemo(() => employees.filter((e) => e.kind === 'staff' && e.status !== 'quit').map((e) => e.fullName).sort((a, b) => a.localeCompare(b, 'vi')), [employees]);
}
