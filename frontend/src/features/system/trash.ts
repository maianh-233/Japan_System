import dayjs from 'dayjs';
import { create } from 'zustand';
import { useAuthStore } from '@/stores/authStore';
import { getSettings } from './settings';

/**
 * THÙNG RÁC — dữ liệu bị XÓA vẫn khôi phục được trong N ngày (Cài đặt chung › Giữ trong Thùng rác, mặc định 30).
 *  - Mỗi store khi xóa gọi `moveToTrash(...)` kèm bản sao dữ liệu, và đăng ký cách khôi phục bằng `registerRestorer(kind, fn)`.
 *  - Dữ liệu chỉ bị ẨN (lead, học viên) không vào đây — trang Thùng rác đọc trực tiếp cờ `hidden` của chúng.
 *  - Quá hạn → tự xóa hẳn (khi có backend: job dọn hằng ngày).
 * Module này không import store nào khác (tránh vòng import) — ghi nhật ký bằng import động.
 */

export type TrashKind = 'message_template' | 'lead_rule' | 'assignment' | 'role' | 'pay_adjust' | 'teacher_doc' | 'invoice';

export const TRASH_KIND: Record<TrashKind | 'lead' | 'student', string> = {
  lead: 'Lead (đã ẩn)',
  student: 'Học viên (đã ẩn)',
  invoice: 'Hóa đơn (đã hủy)',
  message_template: 'Mẫu tin nhắn',
  lead_rule: 'Quy tắc chia lead',
  assignment: 'Bài tập của lớp',
  role: 'Vai trò (role)',
  pay_adjust: 'Điều chỉnh lương',
  teacher_doc: 'Tài liệu giáo viên',
};

export interface TrashItem {
  id: string;
  kind: TrashKind;
  /** Tên hiển thị */
  label: string;
  detail?: string;
  branchId?: string;
  deletedAt: string;
  deletedBy: string;
  /** Bản sao dữ liệu để khôi phục */
  payload: unknown;
}

const restorers = new Map<TrashKind, (payload: never) => void>();

/** Store đăng ký cách đưa dữ liệu trở lại */
export function registerRestorer<P>(kind: TrashKind, fn: (payload: P) => void) {
  restorers.set(kind, fn as (payload: never) => void);
}

const log = (action: string, item: Pick<TrashItem, 'kind' | 'label' | 'detail' | 'branchId'>, severity: 'info' | 'warning' | 'critical' = 'warning') =>
  void import('./activity').then(({ recordActivity }) =>
    recordActivity({ category: 'data', severity, action, target: `${TRASH_KIND[item.kind]}: ${item.label}`, detail: item.detail, branchId: item.branchId }),
  );

interface TrashState {
  items: TrashItem[];
  put: (item: Omit<TrashItem, 'id' | 'deletedAt' | 'deletedBy'> & { deletedBy?: string }) => void;
  restore: (id: string) => boolean;
  purge: (id: string) => void;
}

let seq = 0;

export const useTrashStore = create<TrashState>((set, get) => ({
  items: [],
  put: (item) => {
    const by = item.deletedBy ?? useAuthStore.getState().user?.fullName ?? 'Hệ thống';
    set((s) => ({ items: [{ ...item, id: `TR-${Date.now()}${++seq}`, deletedAt: new Date().toISOString(), deletedBy: by }, ...s.items] }));
    log('Xóa (vào Thùng rác)', item);
  },
  restore: (id) => {
    const it = get().items.find((x) => x.id === id);
    const fn = it && restorers.get(it.kind);
    if (!it || !fn) return false;
    fn(it.payload as never);
    set((s) => ({ items: s.items.filter((x) => x.id !== id) }));
    log('Khôi phục từ Thùng rác', it, 'info');
    return true;
  },
  purge: (id) => {
    const it = get().items.find((x) => x.id === id);
    set((s) => ({ items: s.items.filter((x) => x.id !== id) }));
    if (it) log('Xóa vĩnh viễn', it, 'critical');
  },
}));

export const moveToTrash = (item: Parameters<TrashState['put']>[0]) => useTrashStore.getState().put(item);

/** Còn bao nhiêu ngày trước khi tự xóa hẳn (≤ 0 = đã hết hạn) */
export function daysLeft(deletedAt: string, now = dayjs()): number {
  return getSettings().safety.trashDays - now.startOf('day').diff(dayjs(deletedAt).startOf('day'), 'day');
}
