import { create } from 'zustand';
import { useNotificationStore } from '@/features/notifications/notificationStore';
import { useStudentStore } from '@/features/student/studentStore';
import type { PaymentMethod } from '@/features/student/types';
import { formatCurrency } from '@/lib/format';
import { recordActivity } from './activity';
import { getSettings } from './settings';
import { useSystemStore } from './systemStore';
import type { PermOverride, RoleAssignment } from './types';

/**
 * DUYỆT HAI NGƯỜI (maker–checker) cho thao tác nhạy cảm:
 *  - Hoàn tiền học phí (từ mức tiền ở Cài đặt chung)
 *  - Hủy hóa đơn
 *  - Cấp quyền tài chính (gán role có quyền finance.*, hoặc cấp lẻ quyền finance.*)
 * Người tạo yêu cầu KHÔNG được tự duyệt. Thao tác chỉ thực hiện khi người thứ 2 bấm duyệt.
 * Mọi bước (tạo / duyệt / từ chối / rút lại) ghi Nhật ký hệ thống.
 */

export type ApprovalKind = 'refund' | 'void_invoice' | 'finance_perm';
export type ApprovalStatus = 'pending' | 'approved' | 'rejected' | 'withdrawn';

export const APPROVAL_KIND: Record<ApprovalKind, string> = {
  refund: 'Hoàn tiền học phí',
  void_invoice: 'Hủy hóa đơn',
  finance_perm: 'Cấp quyền tài chính',
};
export const APPROVAL_STATUS: Record<ApprovalStatus, { label: string; tone: 'warning' | 'success' | 'danger' | 'neutral' }> = {
  pending: { label: 'Chờ duyệt', tone: 'warning' },
  approved: { label: 'Đã duyệt', tone: 'success' },
  rejected: { label: 'Từ chối', tone: 'danger' },
  withdrawn: { label: 'Đã rút lại', tone: 'neutral' },
};

export type ApprovalPayload =
  | { kind: 'refund'; invoiceId: string; invoiceCode: string; amount: number; method: PaymentMethod; reason: string }
  | { kind: 'void_invoice'; invoiceId: string; invoiceCode: string; reason: string }
  | { kind: 'finance_perm'; userId: string; userName: string; change: { type: 'override'; override: Omit<PermOverride, 'id' | 'at' | 'by'> } | { type: 'roles'; roles: RoleAssignment[]; before: string; after: string } };

export interface Person {
  id: string;
  name: string;
}

export interface ApprovalRequest {
  id: string;
  code: string;
  kind: ApprovalKind;
  /** Mô tả ngắn: "Hoàn 2.000.000đ — HD-CG-2611-0003 · Nguyễn Văn An" */
  title: string;
  detail?: string;
  branchId?: string;
  payload: ApprovalPayload;
  requestedBy: Person;
  requestedAt: string;
  status: ApprovalStatus;
  decidedBy?: Person;
  decidedAt?: string;
  /** Lý do từ chối / ghi chú khi duyệt */
  note?: string;
}

/** Mã quyền tài chính cần duyệt 2 người khi cấp */
export const isFinancePerm = (perm: string) => perm === '*' || perm.startsWith('finance.');

/** Ai được duyệt loại yêu cầu này (ngoài điều kiện "không phải người tạo") */
export function canApproveKind(kind: ApprovalKind, can: (perm: string) => boolean): boolean {
  if (!can('system.approvals')) return false;
  // Cấp quyền: người duyệt là cấp quản trị (xem được Nhật ký hệ thống) — không bắt buộc có quyền phân quyền, để Super Admin gửi thì Admin duyệt được
  return kind === 'finance_perm' ? can('system.audit') || can('system.user-permissions') : can('finance.invoices');
}

/** Hoàn tiền số này có phải qua duyệt không (Cài đặt chung › Hoàn tiền cần duyệt từ) */
export const refundNeedsApproval = (amount: number) => amount >= getSettings().safety.refundApprovalMin;

const now = () => new Date().toISOString();
let seq = 0;

interface ApprovalState {
  requests: ApprovalRequest[];
  request: (input: Pick<ApprovalRequest, 'kind' | 'title' | 'detail' | 'branchId' | 'payload'>, by: Person) => ApprovalRequest;
  approve: (id: string, by: Person, note?: string) => void;
  reject: (id: string, by: Person, note: string) => void;
  withdraw: (id: string, by: Person) => void;
}

/** Thực hiện thao tác thật sau khi được duyệt */
function execute(r: ApprovalRequest, approver: Person) {
  const p = r.payload;
  if (p.kind === 'refund') {
    useStudentStore.getState().addRefund(p.invoiceId, { amount: p.amount, method: p.method, reason: p.reason, refundedAt: now(), by: r.requestedBy.name, approvedBy: approver.name });
  } else if (p.kind === 'void_invoice') {
    useStudentStore.getState().voidInvoice(p.invoiceId, { reason: p.reason, requestedBy: r.requestedBy.name, approvedBy: approver.name, at: now() });
  } else {
    const sys = useSystemStore.getState();
    const actor = { name: approver.name, role: `duyệt yêu cầu ${r.code} của ${r.requestedBy.name}` };
    if (p.change.type === 'override') sys.addOverride(p.userId, p.change.override, actor);
    else sys.updateAccess(p.userId, { roles: p.change.roles }, actor, { severity: 'critical', action: 'Đổi vai trò (có quyền tài chính)', changes: [{ field: 'Vai trò', before: p.change.before, after: p.change.after }] });
  }
}

export const useApprovalStore = create<ApprovalState>((set, get) => ({
  requests: [],

  request: (input, by) => {
    const n = ++seq;
    const r: ApprovalRequest = { ...input, id: `AR-${Date.now()}${n}`, code: `YC${String(n).padStart(4, '0')}`, requestedBy: by, requestedAt: now(), status: 'pending' };
    set((s) => ({ requests: [r, ...s.requests] }));
    recordActivity({
      category: r.kind === 'finance_perm' ? 'permission' : 'finance',
      severity: 'warning',
      action: `Gửi duyệt: ${APPROVAL_KIND[r.kind]}`,
      target: r.title,
      detail: `${r.code}${r.detail ? ` · ${r.detail}` : ''}`,
      branchId: r.branchId,
    });
    useNotificationStore.getState().add('staff', { title: `Cần duyệt: ${APPROVAL_KIND[r.kind]}`, description: `${r.title} — ${by.name} gửi. Người khác phải duyệt.`, tone: 'warning', link: '/admin/system/approvals' });
    return r;
  },

  approve: (id, by, note) => {
    const r = get().requests.find((x) => x.id === id);
    if (!r || r.status !== 'pending') throw new Error('Yêu cầu không còn chờ duyệt');
    if (r.requestedBy.id === by.id || r.requestedBy.name === by.name) throw new Error('Người tạo yêu cầu không được tự duyệt — cần người thứ hai');
    execute(r, by);
    set((s) => ({ requests: s.requests.map((x) => (x.id === id ? { ...x, status: 'approved', decidedBy: by, decidedAt: now(), note } : x)) }));
    recordActivity({
      category: r.kind === 'finance_perm' ? 'permission' : 'finance',
      severity: 'critical',
      action: `Duyệt: ${APPROVAL_KIND[r.kind]}`,
      target: r.title,
      detail: `${r.code} · người tạo ${r.requestedBy.name} · người duyệt ${by.name}${note ? ` · ${note}` : ''}`,
      branchId: r.branchId,
    });
    useNotificationStore.getState().add('staff', { title: `Đã duyệt ${r.code}`, description: `${r.title} — ${by.name} đã duyệt và thực hiện.`, tone: 'success', link: '/admin/system/approvals' });
  },

  reject: (id, by, note) => {
    const r = get().requests.find((x) => x.id === id);
    if (!r || r.status !== 'pending') throw new Error('Yêu cầu không còn chờ duyệt');
    if (r.requestedBy.id === by.id) throw new Error('Người tạo yêu cầu dùng "Rút lại" thay cho từ chối');
    set((s) => ({ requests: s.requests.map((x) => (x.id === id ? { ...x, status: 'rejected', decidedBy: by, decidedAt: now(), note } : x)) }));
    recordActivity({ category: r.kind === 'finance_perm' ? 'permission' : 'finance', severity: 'warning', action: `Từ chối: ${APPROVAL_KIND[r.kind]}`, target: r.title, detail: `${r.code} · ${note}`, branchId: r.branchId });
    useNotificationStore.getState().add('staff', { title: `Bị từ chối: ${r.code}`, description: `${r.title} — ${by.name}: ${note}`, tone: 'danger', link: '/admin/system/approvals' });
  },

  withdraw: (id, by) => {
    const r = get().requests.find((x) => x.id === id);
    if (!r || r.status !== 'pending' || r.requestedBy.id !== by.id) return;
    set((s) => ({ requests: s.requests.map((x) => (x.id === id ? { ...x, status: 'withdrawn', decidedBy: by, decidedAt: now() } : x)) }));
    recordActivity({ category: r.kind === 'finance_perm' ? 'permission' : 'finance', severity: 'info', action: `Rút lại yêu cầu: ${APPROVAL_KIND[r.kind]}`, target: r.title, detail: r.code, branchId: r.branchId });
  },
}));

/** Tổng tiền đang chờ duyệt hoàn của 1 hóa đơn — trừ vào số còn hoàn được để không gửi trùng */
export const pendingRefundOf = (requests: ApprovalRequest[], invoiceId: string) =>
  requests.reduce((t, r) => (r.status === 'pending' && r.payload.kind === 'refund' && r.payload.invoiceId === invoiceId ? t + r.payload.amount : t), 0);
export const pendingVoidOf = (requests: ApprovalRequest[], invoiceId: string) => requests.find((r) => r.status === 'pending' && r.payload.kind === 'void_invoice' && r.payload.invoiceId === invoiceId);

export const refundTitle = (amount: number, code: string, who?: string) => `Hoàn ${formatCurrency(amount)} — ${code}${who ? ` · ${who}` : ''}`;
