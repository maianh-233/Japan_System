import {
  Archive,
  Ban,
  Info,
  CalendarClock,
  CheckCircle2,
  CircleOff,
  CreditCard,
  Download,
  KeyRound,
  Lock,
  LogIn,
  Mail,
  Megaphone,
  Settings,
  ShieldAlert,
  TriangleAlert,
  Video,
  Wallet,
  Database,
  type LucideIcon,
} from 'lucide-react';
import type { Tone } from '@/components/common';
import { STAFF_MODULES } from '@/config/navigation';
import type { AccountStatus, AuditCategory, AuditSeverity, BranchStatus, IntegrationCategory, IntegrationStatus, PermAction, PermScope } from './types';

// ---------------- Danh mục quyền (sinh từ menu) ----------------

export const PERM_ACTIONS: Record<PermAction, string> = {
  view: 'Xem',
  create: 'Thêm',
  update: 'Sửa',
  delete: 'Xóa / Ẩn',
  export: 'Xuất file',
  approve: 'Duyệt',
};
export const ACTION_KEYS = Object.keys(PERM_ACTIONS) as PermAction[];

export const PERM_SCOPES: Record<PermScope, { label: string; short: string; hint: string; rank: number }> = {
  own: { label: 'Của mình', short: 'Mình', hint: 'Bản ghi do mình tạo / được giao', rank: 1 },
  branch: { label: 'Chi nhánh', short: 'CN', hint: 'Mọi bản ghi thuộc chi nhánh được gán', rank: 2 },
  all: { label: 'Toàn hệ thống', short: 'Tất cả', hint: 'Mọi chi nhánh', rank: 3 },
};
export const SCOPE_KEYS = Object.keys(PERM_SCOPES) as PermScope[];
export const widerScope = (a?: PermScope, b?: PermScope) => (!a ? b : !b ? a : PERM_SCOPES[a].rank >= PERM_SCOPES[b].rank ? a : b);

/** Nhóm chỉ có xem / xuất */
const READ_ONLY = new Set(['dashboard.overview', 'dashboard.reports', 'system.audit']);
/** Nhóm có bước duyệt */
const APPROVABLE = new Set([
  'academic.questions',
  'academic.requests',
  'academic.sessions',
  'finance.invoices',
  'finance.reconciliation',
  'finance.teacher-payroll',
  'finance.staff-payroll',
  'finance.commissions',
  'hr.timesheets',
  'hr.requests',
  'hr.recruitment',
  'work.shifts',
  'work.handover',
  'library.documents',
]);
/** Module chỉ có phạm vi toàn hệ thống */
export const GLOBAL_ONLY_MODULES = new Set(['system']);

export interface PermGroup {
  key: string;
  label: string;
  actions: PermAction[];
}
export interface PermModule {
  key: string;
  label: string;
  groups: PermGroup[];
}

// Bỏ mục cá nhân (không gắn mã quyền, ai đăng nhập cũng dùng) — vd module "Cá nhân", "Giao việc cấp dưới"
export const PERM_CATALOG: PermModule[] = STAFF_MODULES.filter((m) => m.items.some((it) => it.permission)).map((m) => ({
  key: m.key,
  label: m.label,
  groups: m.items.filter((it) => it.permission).map((it) => {
    const k = `${m.key}.${it.key}`;
    const actions: PermAction[] = READ_ONLY.has(k) ? ['view', 'export'] : ['view', 'create', 'update', 'delete', 'export'];
    if (APPROVABLE.has(k)) actions.push('approve');
    return { key: k, label: it.label, actions };
  }),
}));

export const ALL_PERMS = PERM_CATALOG.flatMap((m) => m.groups.flatMap((g) => g.actions.map((a) => `${g.key}.${a}`)));

/** "crm.leads.update" → "Tuyển sinh › Lead › Sửa" */
export function permLabel(perm: string): string {
  const [mod, grp, act] = perm.split('.');
  const m = PERM_CATALOG.find((x) => x.key === mod);
  const g = m?.groups.find((x) => x.key === `${mod}.${grp}`);
  return `${m?.label ?? mod} › ${g?.label ?? grp} › ${PERM_ACTIONS[act as PermAction] ?? act}`;
}
export const PERM_OPTIONS = ALL_PERMS.map((p) => ({ value: p, label: permLabel(p) }));

// ---------------- Tài khoản ----------------

export const ACCOUNT_STATUS: Record<AccountStatus, { label: string; tone: Tone; icon: LucideIcon }> = {
  active: { label: 'Hoạt động', tone: 'success', icon: CheckCircle2 },
  invited: { label: 'Đã mời', tone: 'info', icon: Mail },
  locked: { label: 'Đã khóa', tone: 'danger', icon: Lock },
};

// ---------------- Chi nhánh ----------------

export const BRANCH_STATUS: Record<BranchStatus, { label: string; tone: Tone; icon: LucideIcon }> = {
  active: { label: 'Đang hoạt động', tone: 'success', icon: CheckCircle2 },
  preparing: { label: 'Đang chuẩn bị', tone: 'info', icon: CalendarClock },
  closed: { label: 'Đã đóng', tone: 'neutral', icon: Archive },
};

// ---------------- Tích hợp ----------------

export const INTEGRATION_CATEGORIES: Record<IntegrationCategory, { label: string; icon: LucideIcon }> = {
  notify: { label: 'Thông báo', icon: Megaphone },
  payment: { label: 'Thanh toán', icon: Wallet },
  meeting: { label: 'Học online', icon: Video },
  lead: { label: 'Nguồn lead', icon: CreditCard },
};

export const INTEGRATION_STATUS: Record<IntegrationStatus, { label: string; tone: Tone; icon: LucideIcon }> = {
  connected: { label: 'Đã kết nối', tone: 'success', icon: CheckCircle2 },
  error: { label: 'Lỗi kết nối', tone: 'danger', icon: TriangleAlert },
  disconnected: { label: 'Chưa kết nối', tone: 'neutral', icon: CircleOff },
};

// ---------------- Nhật ký ----------------

export const AUDIT_CATEGORIES: Record<AuditCategory, { label: string; icon: LucideIcon }> = {
  auth: { label: 'Đăng nhập', icon: LogIn },
  permission: { label: 'Phân quyền', icon: KeyRound },
  data: { label: 'Dữ liệu', icon: Database },
  export: { label: 'Xuất file', icon: Download },
  finance: { label: 'Tài chính', icon: Wallet },
  setting: { label: 'Cấu hình', icon: Settings },
};

export const AUDIT_SEVERITY: Record<AuditSeverity, { label: string; tone: Tone; icon: LucideIcon }> = {
  info: { label: 'Thông thường', tone: 'neutral', icon: Info },
  warning: { label: 'Cần chú ý', tone: 'warning', icon: ShieldAlert },
  critical: { label: 'Nghiêm trọng', tone: 'danger', icon: Ban },
};
