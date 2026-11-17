/**
 * Nối trang Nhân viên / Giáo viên với backend thật (user story 04):
 *   /api/v1/hr/employees  → quyền hr.employees.*  (nhân viên)
 *   /api/v1/hr/teachers   → quyền hr.teachers.*   (giáo viên, trợ giảng)
 * Đọc rồi ĐỔ VÀO useHrStore.employees với Id mock (e-huy, t-ha...) — các module còn chạy dữ liệu giả vẫn tra được tên.
 * `useHrVisibility` giữ danh sách hồ sơ server cho xem theo từng trang (phạm vi quyền all / branch / own).
 */
import { create } from 'zustand';
import { api, ApiError } from '@/lib/api/http';
import { fromGuid, fromGuids, toGuid, toGuids } from '@/lib/api/ids';
import { useHrStore } from './hrStore';
import type { Contract, ContractType, Department, Employee, EmployeeKind, EmployeeStatus, ShiftKey, TeacherProfile } from './types';
import type { QLevel } from '@/features/academic/questions/types';

type Group = 'staff' | 'teacher';

interface ServerContract {
  type: ContractType;
  no: string;
  start: string;
  end?: string | null;
  baseSalary?: number | null;
  allowance?: number | null;
}

interface ServerEmployee {
  id: string;
  code: string;
  fullName: string;
  gender: Employee['gender'];
  dob?: string | null;
  phone: string;
  email: string;
  branchId: string;
  kind: EmployeeKind;
  department: Department;
  position: string;
  shiftBased: boolean;
  status: EmployeeStatus;
  joinedAt: string;
  quitAt?: string | null;
  quitReason?: string | null;
  quitScheduled: boolean;
  managerId?: string | null;
  managerName?: string | null;
  contract: ServerContract;
  leaveBalance: number;
  hasAccount: boolean;
  teacher?: {
    type: TeacherProfile['type'];
    levels: QLevel[];
    certificates: string[];
    rate?: { offline: number; online: number } | null;
    availability: Record<string, ShiftKey[]>;
    extraBranchIds: string[];
    bio?: string | null;
  } | null;
  note?: string | null;
  salaryHidden: boolean;
  version: number;
  contractHistory?: ServerContract[] | null;
}

interface ListResult {
  items: ServerEmployee[];
  total: number;
}

/** Kết quả thao tác kèm cảnh báo không chặn (vd còn người báo cáo cho nhân sự vừa nghỉ) */
interface ResultWithWarnings {
  employee: ServerEmployee;
  warnings: string[];
}

/** Hồ sơ mỗi trang được xem — `null` = chưa tải / không có quyền (trang dùng toàn bộ store như chế độ giả) */
export const useHrVisibility = create<Record<Group, Set<string> | null>>(() => ({ staff: null, teacher: null }));

const versions = new Map<string, number>();
const groupOf = (kind: EmployeeKind): Group => (kind === 'staff' ? 'staff' : 'teacher');
const base = (g: Group) => (g === 'staff' ? '/hr/employees' : '/hr/teachers');

const toContract = (c: ServerContract): Contract => ({
  type: c.type,
  no: c.no,
  start: c.start,
  end: c.end ?? undefined,
  baseSalary: c.baseSalary ?? undefined,
  allowance: c.allowance ?? undefined,
});

function toEmployee(s: ServerEmployee): Employee {
  const id = fromGuid(s.id);
  versions.set(id, s.version);
  return {
    id,
    code: s.code,
    fullName: s.fullName,
    gender: s.gender,
    dob: s.dob ?? undefined,
    phone: s.phone,
    email: s.email,
    branchId: fromGuid(s.branchId),
    kind: s.kind,
    department: s.department,
    position: s.position,
    shiftBased: s.shiftBased,
    status: s.status,
    joinedAt: s.joinedAt,
    quitAt: s.quitAt ?? undefined,
    quitReason: s.quitReason ?? undefined,
    quitScheduled: s.quitScheduled || undefined,
    managerId: s.managerId ? fromGuid(s.managerId) : undefined,
    contract: toContract(s.contract),
    leaveBalance: s.leaveBalance,
    hasAccount: s.hasAccount,
    teacher: s.teacher
      ? {
          type: s.teacher.type,
          levels: s.teacher.levels,
          certificates: s.teacher.certificates,
          rate: s.teacher.rate ?? { offline: 0, online: 0 },
          availability: Object.fromEntries(Object.entries(s.teacher.availability).map(([d, shifts]) => [Number(d), shifts])),
          extraBranchIds: s.teacher.extraBranchIds.length ? fromGuids(s.teacher.extraBranchIds) : undefined,
          bio: s.teacher.bio ?? undefined,
        }
      : undefined,
    note: s.note ?? undefined,
    salaryHidden: s.salaryHidden || undefined,
    contractHistory: s.contractHistory?.map(toContract),
  };
}

/** Ghi hồ sơ vào store (thay bản cũ cùng Id) và đánh dấu được xem ở trang của nó */
function upsert(s: ServerEmployee): Employee {
  const e = toEmployee(s);
  useHrStore.setState((st) => ({
    employees: st.employees.some((x) => x.id === e.id) ? st.employees.map((x) => (x.id === e.id ? e : x)) : [...st.employees, e],
  }));
  useHrVisibility.setState((v) => {
    const g = groupOf(e.kind);
    return v[g] ? { [g]: new Set([...v[g]!, e.id]) } : {};
  });
  return e;
}

async function loadGroup(g: Group) {
  const items: ServerEmployee[] = [];
  try {
    for (let page = 1; ; page++) {
      const r = await api<ListResult>(`${base(g)}?includeQuit=true&page=${page}&pageSize=100`);
      items.push(...r.items);
      if (!r.items.length || items.length >= r.total) break;
    }
  } catch (e) {
    if (e instanceof ApiError && e.status === 403) {
      useHrVisibility.setState({ [g]: null });
      return;
    }
    throw e;
  }
  const loaded = items.map(toEmployee);
  const ids = new Set(loaded.map((e) => e.id));
  useHrStore.setState((st) => ({ employees: [...st.employees.filter((x) => !ids.has(x.id)), ...loaded] }));
  useHrVisibility.setState({ [g]: ids });
}

/** Tải hồ sơ cả 2 trang mà người dùng được xem (không có quyền trang nào → giữ dữ liệu cũ) */
export async function loadEmployees() {
  await Promise.all([loadGroup('staff'), loadGroup('teacher')]);
}

/** Chi tiết 1 hồ sơ (kèm lịch sử hợp đồng) */
export async function loadEmployee(id: string, kind: EmployeeKind) {
  return upsert(await api<ServerEmployee>(`${base(groupOf(kind))}/${toGuid(id)}`));
}

// ---------- Thêm / sửa (NS-03, NS-04) ----------

/** Dữ liệu form (Id giao diện) — `kind`, hợp đồng chỉ dùng khi thêm */
export type EmployeeInput = Omit<Employee, 'id' | 'code' | 'joinedAt' | 'leaveBalance' | 'hasAccount'>;

const teacherBody = (t?: TeacherProfile) =>
  t
    ? {
        type: t.type,
        levels: t.levels,
        certificates: t.certificates,
        rate: t.rate,
        extraBranchIds: toGuids(t.extraBranchIds ?? []),
        bio: t.bio ?? null,
      }
    : null;

const fields = (v: EmployeeInput) => ({
  fullName: v.fullName,
  gender: v.gender,
  dob: v.dob ?? null,
  phone: v.phone,
  email: v.email,
  branchId: toGuid(v.branchId),
  department: v.department,
  position: v.position,
  managerId: v.managerId ? toGuid(v.managerId) : null,
  shiftBased: v.shiftBased,
  status: v.status,
  teacher: teacherBody(v.teacher),
  note: v.note ?? null,
});

export async function createEmployee(v: EmployeeInput): Promise<Employee> {
  const c = v.contract;
  const s = await api<ServerEmployee>(base(groupOf(v.kind)), {
    body: {
      kind: v.kind,
      ...fields(v),
      contract: { type: c.type, start: c.start, end: c.end ?? null, baseSalary: c.baseSalary ?? null, allowance: c.allowance ?? null },
    },
  });
  return upsert(s);
}

/** Sửa hồ sơ — trả cảnh báo (đổi chi nhánh → nhớ sửa phân quyền, đổi email khi đã có tài khoản...) */
export async function updateEmployee(id: string, kind: EmployeeKind, v: EmployeeInput): Promise<string[]> {
  const r = await api<ResultWithWarnings>(`${base(groupOf(kind))}/${toGuid(id)}`, {
    method: 'PUT',
    body: { ...fields(v), version: versions.get(id) ?? 0 },
  });
  upsert(r.employee);
  return r.warnings;
}

// ---------- Hợp đồng, nghỉ việc, nghỉ dài hạn, lịch rảnh (NS-06 → NS-10) ----------

/** Gia hạn / ký mới — loại bỏ trống: thử việc → xác định thời hạn, còn lại giữ loại cũ; lương bỏ trống = giữ */
export async function renewContract(e: Employee, v: { type?: ContractType; end?: string; baseSalary?: number; allowance?: number }) {
  upsert(
    await api<ServerEmployee>(`${base(groupOf(e.kind))}/${toGuid(e.id)}/contracts`, {
      body: { type: v.type ?? null, end: v.end ?? null, baseSalary: v.baseSalary ?? null, allowance: v.allowance ?? null },
    }),
  );
}

/** Cho nghỉ việc — ngày ở tương lai = hẹn ngày. Server khóa tài khoản (qua sự kiện) khi nghỉ có hiệu lực. */
export async function quitEmployee(e: Employee, reason: string, quitAt?: string): Promise<string[]> {
  const r = await api<ResultWithWarnings>(`${base(groupOf(e.kind))}/${toGuid(e.id)}/quit`, { body: { reason, quitAt: quitAt ?? null } });
  upsert(r.employee);
  // Tài khoản bị khóa ở nền (outbox) — tải lại danh sách tài khoản sau chốc lát
  setTimeout(() => void import('@/features/system/systemApi').then((m) => m.loadUsers()).catch(() => undefined), 1500);
  return r.warnings;
}

export async function setLongLeave(e: Employee, onLeave: boolean) {
  upsert(await api<ServerEmployee>(`${base(groupOf(e.kind))}/${toGuid(e.id)}/${onLeave ? 'long-leave' : 'return'}`, { method: 'POST' }));
}

export async function saveAvailability(e: Employee, availability: Record<number, ShiftKey[]>) {
  upsert(await api<ServerEmployee>(`/hr/teachers/${toGuid(e.id)}/availability`, { method: 'PUT', body: { availability } }));
}

/** NS-07.E9 — hủy lịch hẹn nghỉ việc (chưa tới ngày) */
export async function cancelScheduledQuit(e: Employee) {
  upsert(await api<ServerEmployee>(`${base(groupOf(e.kind))}/${toGuid(e.id)}/quit`, { method: 'DELETE' }));
}

// ---------- Hồ sơ của tôi (NS-11) — không cần mã quyền ----------

/**
 * Tải hồ sơ của chính người đăng nhập vào store (để `useMe()` tìm thấy). API này không trả lương:
 * nếu store đã có bản đầy đủ (người có quyền HR) thì giữ lương / đơn giá cũ, chỉ cập nhật phần còn lại.
 */
export async function loadMyProfile(): Promise<Employee> {
  const me = toEmployee(await api<ServerEmployee>('/hr/me'));
  const old = useHrStore.getState().employees.find((x) => x.id === me.id);
  const merged: Employee =
    old && !old.salaryHidden
      ? {
          ...me,
          salaryHidden: undefined,
          contract: { ...me.contract, baseSalary: old.contract.baseSalary, allowance: old.contract.allowance },
          teacher: me.teacher && old.teacher ? { ...me.teacher, rate: old.teacher.rate } : me.teacher,
          contractHistory: old.contractHistory,
        }
      : me;
  useHrStore.setState((st) => ({
    employees: old ? st.employees.map((x) => (x.id === me.id ? merged : x)) : [...st.employees, merged],
  }));
  return merged;
}

/** Tự sửa SĐT, ngày sinh (NS-11) */
export async function updateMyContact(phone: string, dob?: string) {
  await api('/hr/me/contact', { method: 'PUT', body: { phone, dob: dob ?? null } });
  return loadMyProfile();
}
