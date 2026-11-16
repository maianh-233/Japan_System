import { create } from 'zustand';
import { MOCK_AUDIT_LOGS, MOCK_BRANCH_PROFILES, MOCK_INTEGRATION_LOGS, MOCK_INTEGRATIONS, MOCK_ROLES, MOCK_USER_ACCESS } from '@/mocks/data/system';
import { useHrStore } from '@/features/hr/hrStore';
import type { AuditLog, BranchProfile, Integration, IntegrationLog, PermOverride, Role, UserAccess } from './types';
import { moveToTrash, registerRestorer } from './trash';

/**
 * Store HỆ THỐNG giả lập. Mọi thay đổi cấu hình / phân quyền đều GHI NHẬT KÝ (audit) — backend làm tương tự.
 * Khi có backend: thay hàm bằng gọi API, giữ nguyên chữ ký.
 */

export interface Actor {
  name: string;
  role: string;
}

type AuditInput = Pick<AuditLog, 'category' | 'severity' | 'action' | 'target'> & Partial<Pick<AuditLog, 'detail' | 'changes' | 'branchId'>>;

interface SystemState {
  branches: BranchProfile[];
  roles: Role[];
  access: UserAccess[];
  integrations: Integration[];
  intLogs: IntegrationLog[];
  audits: AuditLog[];

  audit: (actor: Actor, input: AuditInput) => void;

  saveBranch: (b: BranchProfile, actor: Actor) => void;

  saveRole: (role: Omit<Role, 'updatedAt' | 'updatedBy'>, actor: Actor, changes?: AuditLog['changes']) => void;
  removeRole: (id: string, actor: Actor) => void;

  /** Cấp tài khoản mới (trạng thái: đã mời) */
  addAccess: (a: UserAccess, actor: Actor) => void;
  updateAccess: (userId: string, patch: Partial<UserAccess>, actor: Actor, log: Omit<AuditInput, 'category' | 'target'>) => void;
  addOverride: (userId: string, o: Omit<PermOverride, 'id' | 'at' | 'by'>, actor: Actor) => void;
  removeOverride: (userId: string, overrideId: string, actor: Actor) => void;

  updateIntegration: (id: string, patch: Partial<Integration>, actor: Actor, action: string) => void;
  addIntLog: (l: Omit<IntegrationLog, 'id' | 'at'>) => void;
}

let seq = 1000;
const now = () => new Date().toISOString();
const DEVICE = 'Chrome · Windows';
const nameOf = (userId: string) => useHrStore.getState().employees.find((e) => e.id === userId)?.fullName ?? userId;

export const useSystemStore = create<SystemState>((set, get) => ({
  branches: MOCK_BRANCH_PROFILES,
  roles: MOCK_ROLES,
  access: MOCK_USER_ACCESS,
  integrations: MOCK_INTEGRATIONS,
  intLogs: MOCK_INTEGRATION_LOGS,
  audits: MOCK_AUDIT_LOGS,

  audit: (actor, input) =>
    set((s) => ({ audits: [{ ...input, id: `AU-${++seq}`, at: now(), actor: actor.name, actorRole: actor.role, ip: '113.190.12.8', device: DEVICE }, ...s.audits] })),

  saveBranch: (b, actor) => {
    const old = get().branches.find((x) => x.id === b.id);
    set((s) => ({ branches: old ? s.branches.map((x) => (x.id === b.id ? b : x)) : [...s.branches, b] }));
    const changes: AuditLog['changes'] = old
      ? (['code', 'name', 'address', 'phone', 'email', 'status', 'openHours', 'managerId', 'openedAt', 'invoicePrefix', 'note'] as const)
          .filter((k) => old[k] !== b[k])
          .map((k) => ({ field: k, before: String(old[k] ?? '—'), after: String(b[k] ?? '—') }))
      : undefined;
    if (old && (old.bank?.bank !== b.bank?.bank || old.bank?.account !== b.bank?.account || old.bank?.holder !== b.bank?.holder)) {
      const bankLabel = (bank: BranchProfile['bank']) => (bank ? `${bank.bank} · ••••${bank.account.slice(-4)}` : 'Chưa khai báo');
      changes?.push({ field: 'Tài khoản nhận học phí', before: bankLabel(old.bank), after: bankLabel(b.bank) });
    }
    get().audit(actor, { category: 'setting', severity: old?.status !== b.status && b.status === 'closed' ? 'critical' : 'info', action: old ? 'Sửa chi nhánh' : 'Thêm chi nhánh', target: b.name, changes, branchId: b.id });
  },

  saveRole: (role, actor, changes) => {
    const exists = get().roles.some((r) => r.id === role.id);
    const full: Role = { ...role, updatedAt: now(), updatedBy: actor.name };
    set((s) => ({ roles: exists ? s.roles.map((r) => (r.id === role.id ? full : r)) : [...s.roles, full] }));
    get().audit(actor, { category: 'permission', severity: 'warning', action: exists ? 'Sửa role' : 'Tạo role', target: role.name, changes });
  },
  removeRole: (id, actor) => {
    const r = get().roles.find((x) => x.id === id);
    if (r) moveToTrash({ kind: 'role', label: r.name, detail: `${Object.keys(r.grants).length} quyền`, payload: r, deletedBy: actor.name });
    set((s) => ({ roles: s.roles.filter((x) => x.id !== id) }));
    get().audit(actor, { category: 'permission', severity: 'critical', action: 'Xóa role', target: r?.name ?? id });
  },

  addAccess: (a, actor) => {
    set((s) => ({ access: [a, ...s.access] }));
    const roles = a.roles.map((r) => get().roles.find((x) => x.id === r.roleId)?.name).join(', ');
    get().audit(actor, { category: 'permission', severity: 'warning', action: 'Cấp tài khoản', target: nameOf(a.userId), detail: `${a.email} · role: ${roles}` });
  },
  updateAccess: (userId, patch, actor, log) => {
    set((s) => ({ access: s.access.map((a) => (a.userId === userId ? { ...a, ...patch } : a)) }));
    get().audit(actor, { ...log, category: 'permission', target: nameOf(userId) });
  },
  addOverride: (userId, o, actor) => {
    const ov: PermOverride = { ...o, id: `OV-${++seq}`, at: now(), by: actor.name };
    set((s) => ({ access: s.access.map((a) => (a.userId === userId ? { ...a, overrides: [ov, ...a.overrides] } : a)) }));
    get().audit(actor, { category: 'permission', severity: 'warning', action: o.effect === 'grant' ? 'Cấp quyền lẻ' : 'Chặn quyền', target: nameOf(userId), detail: `${o.perm} — ${o.reason}` });
  },
  removeOverride: (userId, overrideId, actor) => {
    const o = get().access.find((a) => a.userId === userId)?.overrides.find((x) => x.id === overrideId);
    set((s) => ({ access: s.access.map((a) => (a.userId === userId ? { ...a, overrides: a.overrides.filter((x) => x.id !== overrideId) } : a)) }));
    get().audit(actor, { category: 'permission', severity: 'warning', action: o?.effect === 'deny' ? 'Bỏ chặn quyền' : 'Thu hồi quyền lẻ', target: nameOf(userId), detail: o?.perm });
  },

  updateIntegration: (id, patch, actor, action) => {
    const it = get().integrations.find((x) => x.id === id);
    set((s) => ({ integrations: s.integrations.map((x) => (x.id === id ? { ...x, ...patch } : x)) }));
    get().audit(actor, { category: 'setting', severity: action.includes('Ngắt') ? 'critical' : 'warning', action, target: it?.name ?? id });
  },
  addIntLog: (l) => set((s) => ({ intLogs: [{ ...l, id: `IL-${++seq}`, at: now() }, ...s.intLogs] })),
}));

registerRestorer<Role>('role', (r) => useSystemStore.setState((s) => ({ roles: [...s.roles, r] })));
