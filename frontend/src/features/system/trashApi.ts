/**
 * Thùng rác với backend (/api/v1/system/trash — user story 10, TR-01 → TR-03).
 * Server chỉ giữ con trỏ tới bản ghi đã xóa mềm ở module sở hữu; khôi phục / xóa hẳn do module đó làm.
 * Loại đã có backend: vai trò (`system.role`). Lead / học viên bị ẨN không ở đây — trang tự ghép từ danh sách lead / học viên.
 */
import { api } from '@/lib/api/http';
import { fromGuid } from '@/lib/api/ids';
import { loadRoles } from './systemApi';
import type { TrashKind } from './trash';

interface ServerTrashItem {
  id: string;
  kind: string;
  label: string;
  detail?: string | null;
  branchId?: string | null;
  deletedAt: string;
  deletedBy: string;
  purgeAfter: string;
  daysLeft: number;
  restorable: boolean;
}

export interface ServerTrashRow {
  id: string;
  kind: TrashKind;
  label: string;
  detail?: string;
  branchId?: string;
  deletedAt: string;
  deletedBy: string;
  daysLeft: number;
  restorable: boolean;
}

/** Loại "module.entity" của server → loại của giao diện (TRASH_KIND) */
const KINDS: Record<string, TrashKind> = {
  'system.role': 'role',
  'crm.message_template': 'message_template',
  'crm.lead_rule': 'lead_rule',
  'academic.assignment': 'assignment',
  'finance.pay_adjustment': 'pay_adjust',
  'library.teacher_document': 'teacher_doc',
  'finance.invoice': 'invoice',
};

/** Sau khi khôi phục: tải lại dữ liệu của loại đó để trang tương ứng thấy ngay */
const RELOAD: Partial<Record<TrashKind, () => Promise<unknown>>> = {
  role: loadRoles,
};

export async function fetchTrash(): Promise<ServerTrashRow[]> {
  const items = await api<ServerTrashItem[]>('/system/trash');
  return items.map((t) => ({
    id: t.id,
    kind: KINDS[t.kind] ?? (t.kind as TrashKind),
    label: t.label,
    detail: t.detail ?? undefined,
    branchId: t.branchId ? fromGuid(t.branchId) : undefined,
    deletedAt: t.deletedAt,
    deletedBy: t.deletedBy,
    daysLeft: t.daysLeft,
    restorable: t.restorable,
  }));
}

/** Khôi phục — trả ghi chú của module sở hữu (vd "khôi phục với tên … (khôi phục)" khi trùng tên) */
export async function restoreTrash(row: ServerTrashRow): Promise<string | undefined> {
  const r = await api<{ note?: string | null }>(`/system/trash/${row.id}/restore`, { method: 'POST' });
  await RELOAD[row.kind]?.().catch(() => undefined);
  return r.note ?? undefined;
}

export const purgeTrash = (id: string) => api(`/system/trash/${id}`, { method: 'DELETE' });
