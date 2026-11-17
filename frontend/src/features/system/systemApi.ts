/**
 * Nối trang Vai trò & quyền, Phân quyền người dùng, Duyệt cấp quyền tài chính với backend thật (/api/v1/system/*).
 * Đọc từ API rồi ĐỔ VÀO các store sẵn có (useSystemStore, useApprovalStore) với Id mock — giao diện giữ nguyên.
 * Mọi thay đổi: gọi API → tải lại → store cập nhật. Server kiểm quyền / duyệt 2 người; giao diện chỉ hiển thị kết quả.
 */
import { api, ApiError } from '@/lib/api/http';
import { fromGuid, fromGuids, toGuid, toGuids } from '@/lib/api/ids';
import { useApprovalStore, type ApprovalRequest } from './approvals';
import { useSystemStore } from './systemStore';
import type { PermOverride, PermScope, Role, RoleAssignment, UserAccess } from './types';

// ---------- Kiểu dữ liệu backend ----------

interface ServerRole {
  id: string;
  name: string;
  description: string;
  system: boolean;
  superAdmin: boolean;
  grants: Record<string, PermScope>;
  userCount: number;
  updatedAt?: string;
  updatedBy?: string;
  version: number;
}

interface ServerUser {
  accountId: string;
  userId: string;
  name: string;
  email: string;
  status: UserAccess['status'];
  roles: { roleId: string; branchIds: string[] }[];
  overrides: (Omit<PermOverride, 'id'> & { id: string; expired: boolean })[];
  lastLoginAt?: string;
  lockedReason?: string;
}

interface ServerApproval {
  id: string;
  code: string;
  kind: 'refund' | 'void_invoice' | 'finance_permission';
  title: string;
  detail?: string;
  branchId?: string;
  payload: {
    type: 'user_roles' | 'user_override' | 'role_grants';
    accountId?: string;
    userName?: string;
    roles?: { roleId: string; branchIds: string[] }[];
    override?: { permission: string; effect: 'grant' | 'deny'; scope?: PermScope; until?: string; reason: string };
    roleName?: string;
    before?: string;
    after?: string;
  };
  requestedBy: { id: string; name: string };
  requestedAt: string;
  status: ApprovalRequest['status'];
  decidedBy?: { id: string; name: string };
  decidedAt?: string;
  note?: string;
}

/** Kết quả thay đổi quyền: có `pendingApprovalId` = chưa áp dụng, đang chờ người thứ 2 duyệt */
export interface ChangeResult {
  pendingApprovalId?: string;
  message?: string;
  warnings?: string[];
  keptCells?: string[];
}

// ---------- Bảng phụ: version role (chống sửa đè), Id tài khoản theo nhân viên ----------
const roleVersion = new Map<string, number>();
const accountOf = new Map<string, string>();
/** Tài khoản backend → Id người trong giao diện (nhân viên) — để nhận ra "yêu cầu của tôi" */
const userOfAccount = new Map<string, string>();

/** Ghi nhớ tài khoản của chính người đang đăng nhập (để nhận ra "yêu cầu của tôi" cả khi không xem được danh sách người dùng) */
export function rememberAccount(accountId: string, userId: string) {
  accountOf.set(userId, accountId);
  userOfAccount.set(accountId, userId);
}

const account = (userId: string) => {
  const id = accountOf.get(userId);
  if (!id) throw new ApiError('Không tìm thấy tài khoản của nhân viên này — tải lại trang.', 'system.user.not_found');
  return id;
};

const toAssignment = (r: { roleId: string; branchIds: string[] }): RoleAssignment => ({ roleId: fromGuid(r.roleId), branchIds: fromGuids(r.branchIds) });
const toServerAssignment = (r: RoleAssignment) => ({ roleId: toGuid(r.roleId), branchIds: toGuids(r.branchIds) });

function toRole(r: ServerRole): Role {
  roleVersion.set(fromGuid(r.id), r.version);
  return {
    id: fromGuid(r.id),
    name: r.name,
    description: r.description,
    system: r.system,
    superAdmin: r.superAdmin || undefined,
    grants: r.grants,
    updatedAt: r.updatedAt ?? '',
    updatedBy: r.updatedBy ?? '',
  };
}

function toAccess(u: ServerUser): UserAccess {
  const userId = fromGuid(u.userId);
  accountOf.set(userId, u.accountId);
  userOfAccount.set(u.accountId, userId);
  return {
    userId,
    email: u.email,
    status: u.status,
    roles: u.roles.map(toAssignment),
    overrides: u.overrides.map(({ expired: _expired, ...o }) => o),
    lastLoginAt: u.lastLoginAt,
    lockedReason: u.lockedReason,
  };
}

function toApproval(r: ServerApproval): ApprovalRequest {
  const p = r.payload;
  const userId = p.accountId ? (userOfAccount.get(p.accountId) ?? p.accountId) : '';
  const person = (x: { id: string; name: string }) => ({ id: userOfAccount.get(x.id) ?? x.id, name: x.name });
  return {
    id: r.id,
    code: r.code,
    kind: 'finance_perm',
    title: r.title,
    detail: r.detail,
    branchId: r.branchId ? fromGuid(r.branchId) : undefined,
    payload: {
      kind: 'finance_perm',
      userId,
      userName: p.userName ?? p.roleName ?? '',
      change:
        p.type === 'user_override' && p.override
          ? { type: 'override', override: { perm: p.override.permission, effect: p.override.effect, scope: p.override.scope, until: p.override.until, reason: p.override.reason } }
          : { type: 'roles', roles: (p.roles ?? []).map(toAssignment), before: p.before ?? '', after: p.after ?? (p.roleName ? `Vai trò ${p.roleName}` : '') },
    },
    requestedBy: person(r.requestedBy),
    requestedAt: r.requestedAt,
    status: r.status,
    decidedBy: r.decidedBy ? person(r.decidedBy) : undefined,
    decidedAt: r.decidedAt,
    note: r.note,
  };
}

/** Bỏ qua 403 (người dùng không có quyền trang đó → giữ dữ liệu cũ) */
async function optional<T>(p: Promise<T>): Promise<T | undefined> {
  try {
    return await p;
  } catch (e) {
    if (e instanceof ApiError && (e.status === 403 || e.status === 404)) return undefined;
    throw e;
  }
}

// ---------- Tải dữ liệu ----------

export async function loadRoles() {
  const roles = await optional(api<ServerRole[]>('/system/roles'));
  if (roles) useSystemStore.setState({ roles: roles.map(toRole) });
}

export async function loadUsers() {
  const users = await optional(api<ServerUser[]>('/system/users'));
  if (users) useSystemStore.setState({ access: users.map(toAccess) });
}

export async function loadApprovals() {
  const list = await optional(api<ServerApproval[]>('/system/approvals'));
  if (!list) return;
  const finance = list.filter((x) => x.kind === 'finance_permission').map(toApproval);
  // Hoàn tiền / hủy hóa đơn vẫn chạy bằng dữ liệu giả (module Tài chính chưa có API)
  useApprovalStore.setState((s) => ({ requests: [...finance, ...s.requests.filter((x) => x.kind !== 'finance_perm')] }));
}

/** Sau đăng nhập / tải lại trang (chỉ nhân viên): nạp vai trò, người dùng, yêu cầu duyệt mà người này được xem */
export async function loadSystem() {
  await Promise.all([loadRoles(), loadUsers()]);
  await loadApprovals(); // sau users để đổi tài khoản → nhân viên
}

const afterPermissionChange = () => Promise.all([loadRoles(), loadUsers(), loadApprovals()]);

// ---------- Vai trò (PQ-02 → PQ-04) ----------

export async function createRole(name: string, description: string, copyFromRoleId?: string): Promise<{ id: string } & ChangeResult> {
  const r = await api<{ role: ServerRole } & ChangeResult>('/system/roles', {
    body: { name, description, copyFromRoleId: copyFromRoleId ? toGuid(copyFromRoleId) : null },
  });
  await loadRoles();
  return { ...r, id: fromGuid(r.role.id) };
}

export async function saveRoleGrants(roleId: string, grants: Record<string, PermScope>): Promise<ChangeResult> {
  const r = await api<ChangeResult>(`/system/roles/${toGuid(roleId)}/grants`, {
    method: 'PUT',
    body: { grants, version: roleVersion.get(roleId) ?? 0 },
  });
  await afterPermissionChange();
  return r;
}

export async function deleteRole(roleId: string) {
  await api(`/system/roles/${toGuid(roleId)}`, { method: 'DELETE' });
  await loadRoles();
}

// ---------- Người dùng (PQ-05 → PQ-08) ----------

export async function setUserRoles(userId: string, roles: RoleAssignment[]): Promise<ChangeResult> {
  const r = await api<ChangeResult>(`/system/users/${account(userId)}/roles`, { method: 'PUT', body: { roles: roles.map(toServerAssignment) } });
  await afterPermissionChange();
  return r;
}

export async function addOverride(userId: string, o: Omit<PermOverride, 'id' | 'at' | 'by'>): Promise<ChangeResult> {
  const r = await api<ChangeResult>(`/system/users/${account(userId)}/overrides`, {
    body: { perm: o.perm, effect: o.effect, scope: o.scope ?? null, until: o.until ?? null, reason: o.reason },
  });
  await afterPermissionChange();
  return r;
}

export async function removeOverride(userId: string, overrideId: string) {
  await api(`/system/users/${account(userId)}/overrides/${overrideId}`, { method: 'DELETE' });
  await loadUsers();
}

export async function lockUser(userId: string, reason: string) {
  await api(`/system/users/${account(userId)}/lock`, { body: { reason } });
  await loadUsers();
}

export async function unlockUser(userId: string) {
  await api(`/system/users/${account(userId)}/unlock`, { method: 'POST' });
  await loadUsers();
}

export const resendInvite = (userId: string) => api(`/system/users/${account(userId)}/resend-invite`, { method: 'POST' });

export async function inviteUser(employeeId: string, roleId: string, branchIds: string[]): Promise<ChangeResult> {
  const r = await api<ChangeResult>('/system/users', {
    body: { employeeId: toGuid(employeeId), roles: [{ roleId: toGuid(roleId), branchIds: toGuids(branchIds) }] },
  });
  await afterPermissionChange();
  return r;
}

// ---------- Duyệt 2 người (PQ-09) ----------

export async function approveRequest(id: string, note?: string): Promise<string[]> {
  const r = await api<{ warnings: string[] }>(`/system/approvals/${id}/approve`, { body: { note: note ?? null } });
  await afterPermissionChange();
  return r.warnings ?? [];
}

export async function rejectRequest(id: string, note: string) {
  await api(`/system/approvals/${id}/reject`, { body: { note } });
  await loadApprovals();
}

export async function withdrawRequest(id: string) {
  await api(`/system/approvals/${id}/withdraw`, { method: 'POST' });
  await loadApprovals();
}
