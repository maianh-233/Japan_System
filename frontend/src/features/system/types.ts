import type { Branch } from '@/types/auth';

/**
 * HỆ THỐNG
 *
 *  Quyền = MODULE → NHÓM (mục menu) → HÀNH ĐỘNG, kèm PHẠM VI
 *     mã quyền: "crm.leads.update"   phạm vi: own (của mình) | branch (chi nhánh được gán) | all (toàn hệ thống)
 *
 *  Role  ── grants: { mã quyền → phạm vi }
 *  Người dùng ── roles: [{ role, chi nhánh áp dụng }]  (1 người có thể là Giáo vụ ở CG và Sale ở Q1)
 *            └─ overrides: cấp thêm / chặn quyền lẻ, có thời hạn
 *  Quyền hiệu lực = hợp các role (lấy phạm vi rộng nhất) + cấp lẻ − bị chặn.
 *  Chỉ cấp được quyền MÌNH ĐANG CÓ, với phạm vi không rộng hơn của mình.
 */

export type PermAction = 'view' | 'create' | 'update' | 'delete' | 'export' | 'approve';
export type PermScope = 'own' | 'branch' | 'all';

export interface Role {
  id: string;
  name: string;
  description: string;
  /** Role mặc định của hệ thống — không xóa được, Super Admin không sửa được */
  system: boolean;
  /** Toàn quyền (Super Admin) */
  superAdmin?: boolean;
  grants: Record<string, PermScope>;
  updatedAt: string;
  updatedBy: string;
}

export interface RoleAssignment {
  roleId: string;
  /** Chi nhánh áp dụng — rỗng = mọi chi nhánh */
  branchIds: string[];
}

export interface PermOverride {
  id: string;
  /** Mã quyền "module.nhóm.hành-động" */
  perm: string;
  effect: 'grant' | 'deny';
  scope?: PermScope;
  /** Hết hạn — bỏ trống = vô thời hạn */
  until?: string;
  reason: string;
  by: string;
  at: string;
}

export type AccountStatus = 'active' | 'invited' | 'locked';

export interface UserAccess {
  /** = Employee.id */
  userId: string;
  email: string;
  status: AccountStatus;
  roles: RoleAssignment[];
  overrides: PermOverride[];
  lastLoginAt?: string;
  lockedReason?: string;
}

// ---------------- Chi nhánh ----------------

export type BranchStatus = 'active' | 'preparing' | 'closed';

export interface BranchProfile extends Branch {
  phone: string;
  email: string;
  managerId?: string;
  /** Giờ mở cửa, vd "T2–T7 08:00–21:30 · CN 08:00–17:00" */
  openHours: string;
  status: BranchStatus;
  openedAt: string;
  /** Tài khoản nhận học phí (VietQR) */
  bank?: { bank: string; account: string; holder: string };
  /** Tiền tố số hóa đơn */
  invoicePrefix: string;
  note?: string;
}

// ---------------- Tích hợp ----------------

export type IntegrationCategory = 'notify' | 'payment' | 'meeting' | 'lead';
export type IntegrationStatus = 'connected' | 'error' | 'disconnected';

export interface IntegrationField {
  key: string;
  label: string;
  secret?: boolean;
  placeholder?: string;
}

export interface Integration {
  id: string;
  name: string;
  category: IntegrationCategory;
  description: string;
  status: IntegrationStatus;
  /** Tài khoản / trang đã kết nối */
  account?: string;
  connectedAt?: string;
  connectedBy?: string;
  lastSyncAt?: string;
  error?: string;
  fields: IntegrationField[];
  config: Record<string, string>;
  /** Sự kiện dùng kênh này (tích = đang bật) */
  events: { key: string; label: string; on: boolean }[];
  /** Số liệu 30 ngày */
  usage?: { label: string; value: number; failed?: number };
  /** URL webhook hệ thống cung cấp cho bên ngoài gọi vào */
  webhook?: string;
}

export interface IntegrationLog {
  id: string;
  integrationId: string;
  at: string;
  ok: boolean;
  message: string;
}

// ---------------- Nhật ký ----------------

export type AuditCategory = 'auth' | 'permission' | 'data' | 'export' | 'finance' | 'setting';
export type AuditSeverity = 'info' | 'warning' | 'critical';

export interface AuditLog {
  id: string;
  at: string;
  actor: string;
  actorRole: string;
  branchId?: string;
  category: AuditCategory;
  severity: AuditSeverity;
  /** Hành động ngắn: "Cấp quyền", "Xóa học viên", "Xuất Excel" */
  action: string;
  /** Đối tượng bị tác động */
  target: string;
  detail?: string;
  /** Thay đổi trước → sau */
  changes?: { field: string; before?: string; after?: string }[];
  ip: string;
  device: string;
}
