import type { ShiftKey } from '@/features/hr/types';

/**
 * CÔNG VIỆC
 *  - Task: cấp trên giao cho cấp dưới (hạn, ưu tiên, người theo dõi, lặp lại, gắn lead / lớp / học viên)
 *  - Xếp ca: nhân viên "làm theo ca" (Sale, lễ tân) — xếp theo tuần, NV đăng ký ca mong muốn trước
 *  - Bàn giao ca: cuối ca ghi lại tiền mặt tại quầy, lead đang dở, việc tồn → ca sau xác nhận đã nhận
 */

// ---------------- Task ----------------

export type TaskStatus = 'todo' | 'doing' | 'review' | 'done';
export type TaskPriority = 'low' | 'normal' | 'high' | 'urgent';
export type TaskRepeat = 'none' | 'daily' | 'weekly' | 'monthly';

export interface Task {
  id: string;
  code: string;
  title: string;
  description?: string;
  assigneeId: string;
  creatorId: string;
  watcherIds: string[];
  priority: TaskPriority;
  status: TaskStatus;
  due?: string;
  repeat: TaskRepeat;
  /** Gắn với đối tượng nghiệp vụ */
  link?: { type: 'lead' | 'class' | 'student'; id: string; label: string };
  checklist: { id: string; text: string; done: boolean }[];
  comments: { at: string; by: string; text: string }[];
  createdAt: string;
  completedAt?: string;
  /** Chế độ API — quyền của người đang xem (server tính) */
  canEdit?: boolean;
  canDelete?: boolean;
  canMove?: boolean;
}

// ---------------- Xếp ca ----------------

export interface ShiftAssignment {
  id: string;
  employeeId: string;
  date: string;
  shift: ShiftKey;
}

/** Nhân viên đăng ký ca mong muốn cho tuần tới */
export interface ShiftPreference {
  employeeId: string;
  date: string;
  shift: ShiftKey;
}

export type ShiftAvailabilityStatus = 'available' | 'busy';

export interface ShiftAvailability extends ShiftPreference {
  status: ShiftAvailabilityStatus;
}

// ---------------- Bàn giao ca ----------------

export type HandoverStatus = 'pending' | 'received' | 'disputed';

export interface Handover {
  id: string;
  branchId: string;
  date: string;
  fromShift: ShiftKey;
  fromId: string;
  toId: string;
  cash: {
    /** Hệ thống tính: tiền mặt thu trong ca + tồn đầu ca */
    expected: number;
    /** NV đếm thực tế */
    counted: number;
    /** Người nhận đếm lại */
    receivedCount?: number;
    note?: string;
  };
  leads: { leadId: string; note: string }[];
  /** `id` có ở chế độ API (tick việc tồn) */
  pending: { id?: string; text: string; done?: boolean }[];
  issues?: string;
  status: HandoverStatus;
  createdAt: string;
  receivedAt?: string;
  receiverNote?: string;
}
