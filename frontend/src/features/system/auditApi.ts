/**
 * Nối trang Nhật ký hệ thống với backend (/api/v1/system/audit-logs — user story 10, NK-01 / NK-02).
 * Server lọc theo phạm vi quyền + bộ lọc (chi nhánh, loại, mức độ, người, khoảng ngày ≤ 1 năm, mặc định 30 ngày);
 * trang tải tối đa {@link MAX_ROWS} dòng mới nhất rồi tìm / sắp xếp tại chỗ như trước.
 * Xuất: server trả dữ liệu (đã ghi nhật ký "Xuất nhật ký hệ thống"), trình duyệt ghi file .xlsx.
 */
import dayjs from 'dayjs';
import { api } from '@/lib/api/http';
import { fromGuid, toGuid } from '@/lib/api/ids';
import { buildXlsx, downloadBlob } from '@/lib/spreadsheet';
import { ALL_BRANCHES } from '@/types/auth';
import { AUDIT_CATEGORIES, AUDIT_SEVERITY } from './constants';
import type { AuditCategory, AuditLog, AuditSeverity } from './types';

/** Số dòng tối đa tải về trang (100 / lần) — cần xem cũ hơn thì thu hẹp khoảng ngày */
export const MAX_ROWS = 1000;
const PAGE = 100;

interface ServerAuditLog {
  id: string;
  at: string;
  actorId?: string | null;
  actor: string;
  actorRole: string;
  branchId?: string | null;
  category: AuditCategory | 'security';
  severity: AuditSeverity;
  action: string;
  target: string;
  detail?: string | null;
  changes: { field: string; before?: string | null; after?: string | null }[];
  ip: string;
  device: string;
}

export interface AuditFilter {
  branchId: string;
  category?: string;
  severity?: string;
  actorId?: string;
  /** YYYY-MM-DD theo giờ Việt Nam */
  from?: string;
  to?: string;
}

export interface AuditStats {
  today: number;
  criticalWeek: number;
  exportWeek: number;
  failedLoginWeek: number;
}

export interface AuditActor {
  id: string;
  name: string;
  role: string;
}

const toLog = (a: ServerAuditLog): AuditLog => ({
  id: a.id,
  at: a.at,
  actor: a.actor,
  actorRole: a.actorRole,
  branchId: a.branchId ? fromGuid(a.branchId) : undefined,
  // "security" (spam đăng nhập, token dùng lại) hiển thị chung nhóm Đăng nhập
  category: a.category === 'security' ? 'auth' : a.category,
  severity: a.severity,
  action: a.action,
  target: a.target,
  detail: a.detail ?? undefined,
  changes: a.changes.map((c) => ({ field: c.field, before: c.before ?? undefined, after: c.after ?? undefined })),
  ip: a.ip,
  device: a.device,
});

function query(f: AuditFilter, extra: Record<string, string | number> = {}) {
  const p = new URLSearchParams();
  if (f.branchId !== ALL_BRANCHES) p.set('branchId', toGuid(f.branchId));
  if (f.category) p.set('category', f.category);
  if (f.severity) p.set('severity', f.severity);
  if (f.actorId) p.set('actorId', f.actorId);
  if (f.from) p.set('from', f.from);
  if (f.to) p.set('to', f.to);
  Object.entries(extra).forEach(([k, v]) => p.set(k, String(v)));
  return p.toString();
}

/** Tải dòng nhật ký theo bộ lọc, mới nhất trước — tối đa {@link MAX_ROWS} dòng. `total` = tổng số dòng khớp trên server. */
export async function fetchAuditLogs(f: AuditFilter): Promise<{ rows: AuditLog[]; total: number }> {
  const rows: AuditLog[] = [];
  let total = 0;
  for (let page = 1; rows.length < MAX_ROWS; page++) {
    const r = await api<{ items: ServerAuditLog[]; total: number }>(`/system/audit-logs?${query(f, { page, pageSize: PAGE })}`);
    total = r.total;
    rows.push(...r.items.map(toLog));
    if (r.items.length < PAGE || rows.length >= total) break;
  }
  return { rows, total };
}

export const fetchAuditStats = (branchId: string) =>
  api<AuditStats>(`/system/audit-logs/stats${branchId === ALL_BRANCHES ? '' : `?branchId=${toGuid(branchId)}`}`);

export const fetchAuditActors = () => api<AuditActor[]>('/system/audit-logs/actors');

/** NK-02 — xuất theo bộ lọc (≤ 10 000 dòng mới nhất) ra file .xlsx. Trả số dòng đã xuất + có bị cắt bớt không. */
export async function exportAuditLogs(f: AuditFilter, branchName: (id?: string) => string): Promise<{ count: number; truncated: boolean }> {
  const r = await api<{ items: ServerAuditLog[]; total: number; truncated: boolean }>(`/system/audit-logs/export?${query(f)}`);
  const logs = r.items.map(toLog);
  const sheet = [
    ['Thời điểm', 'Người thực hiện', 'Vai trò', 'Chi nhánh', 'Loại', 'Mức độ', 'Hành động', 'Đối tượng', 'Chi tiết', 'Thay đổi', 'IP', 'Thiết bị'],
    ...logs.map((a) => [
      dayjs(a.at).format('DD/MM/YYYY HH:mm:ss'),
      a.actor,
      a.actorRole,
      branchName(a.branchId),
      AUDIT_CATEGORIES[a.category]?.label ?? a.category,
      AUDIT_SEVERITY[a.severity].label,
      a.action,
      a.target,
      a.detail ?? '',
      (a.changes ?? []).map((c) => `${c.field}: ${c.before ?? '—'} → ${c.after ?? '—'}`).join('\n'),
      a.ip,
      a.device,
    ]),
  ];
  downloadBlob(buildXlsx(sheet, { sheetName: 'Nhật ký', widths: [18, 22, 16, 16, 12, 12, 26, 30, 40, 50, 16, 24] }),
    `nhat-ky-he-thong-${dayjs().format('YYYYMMDD-HHmm')}.xlsx`);
  return { count: logs.length, truncated: r.truncated };
}
