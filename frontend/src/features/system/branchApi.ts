/**
 * Nối trang Chi nhánh với backend thật (/api/v1/system/branches — user story 03).
 * Đọc rồi ĐỔ VÀO useSystemStore.branches với Id mock (cg, q1...) — mọi trang chọn chi nhánh dùng chung.
 * Người không có quyền trang Chi nhánh vẫn lấy được danh sách rút gọn (/options) cho ô chọn.
 */
import { create } from 'zustand';
import { api, ApiError } from '@/lib/api/http';
import { fromGuid, toGuid } from '@/lib/api/ids';
import { useSystemStore } from './systemStore';
import type { BranchProfile, BranchStatus } from './types';

interface ServerBranch {
  id: string;
  code: string;
  name: string;
  address: string;
  phone: string;
  email: string;
  managerId?: string | null;
  managerName?: string | null;
  openHours: string;
  status: BranchStatus;
  openedAt: string;
  bank?: { bank: string; account: string; holder: string } | null;
  invoicePrefix: string;
  note?: string | null;
  stats: BranchStats;
  version: number;
}

/** Số liệu từ server — `null` = module nguồn chưa có (Đào tạo, Học viên, Tài chính) */
export interface BranchStats {
  staff: number;
  teachers: number;
  rooms: number | null;
  activeClasses: number | null;
  students: number | null;
  collected: number | null;
  receivable: number | null;
}

interface ServerOption {
  id: string;
  code: string;
  name: string;
  status: BranchStatus;
}

interface StatusResult {
  branch: ServerBranch;
  warnings: string[];
}

/** Số liệu + tên quản lý theo chi nhánh (Id giao diện) — chỉ có khi xem được trang Chi nhánh */
export const useBranchMeta = create<{ stats: Record<string, BranchStats>; managerNames: Record<string, string>; loaded: boolean }>(() => ({
  stats: {},
  managerNames: {},
  loaded: false,
}));

const versions = new Map<string, number>();

function toProfile(b: ServerBranch): BranchProfile {
  const id = fromGuid(b.id);
  versions.set(id, b.version);
  return {
    id,
    code: b.code,
    name: b.name,
    address: b.address,
    phone: b.phone,
    email: b.email,
    managerId: b.managerId ? fromGuid(b.managerId) : undefined,
    openHours: b.openHours,
    status: b.status,
    openedAt: b.openedAt,
    bank: b.bank ?? undefined,
    invoicePrefix: b.invoicePrefix,
    note: b.note ?? undefined,
  };
}

function upsert(b: ServerBranch) {
  const profile = toProfile(b);
  useSystemStore.setState((s) => ({
    branches: s.branches.some((x) => x.id === profile.id) ? s.branches.map((x) => (x.id === profile.id ? profile : x)) : [...s.branches, profile],
  }));
  useBranchMeta.setState((s) => ({
    stats: { ...s.stats, [profile.id]: b.stats },
    managerNames: { ...s.managerNames, ...(b.managerName ? { [profile.id]: b.managerName } : {}) },
  }));
}

/** Tải chi nhánh. Không có quyền trang → danh sách rút gọn (giữ chi tiết cũ nếu có, chỉ cập nhật tên / trạng thái). */
export async function loadBranches() {
  try {
    const list = await api<ServerBranch[]>('/system/branches');
    useSystemStore.setState({ branches: list.map(toProfile) });
    useBranchMeta.setState({
      stats: Object.fromEntries(list.map((b) => [fromGuid(b.id), b.stats])),
      managerNames: Object.fromEntries(list.filter((b) => b.managerName).map((b) => [fromGuid(b.id), b.managerName!])),
      loaded: true,
    });
  } catch (e) {
    if (!(e instanceof ApiError) || e.status !== 403) throw e;
    const options = await api<ServerOption[]>('/system/branches/options');
    useSystemStore.setState((s) => ({
      branches: options.map((o) => {
        const id = fromGuid(o.id);
        const old = s.branches.find((b) => b.id === id);
        return old
          ? { ...old, code: o.code, name: o.name, status: o.status }
          : { id, code: o.code, name: o.name, status: o.status, address: '', phone: '', email: '', openHours: '', openedAt: '', invoicePrefix: o.code };
      }),
    }));
  }
}

export interface BranchInput {
  code: string;
  name: string;
  address: string;
  phone: string;
  email: string;
  managerId?: string;
  openHours: string;
  openedAt: string;
  invoicePrefix?: string;
  bank?: { bank: string; account: string; holder: string };
  note?: string;
}

const body = (v: BranchInput) => ({
  name: v.name,
  address: v.address,
  phone: v.phone,
  email: v.email,
  managerId: v.managerId ? toGuid(v.managerId) : null,
  openHours: v.openHours,
  openedAt: v.openedAt,
  invoicePrefix: v.invoicePrefix || null,
  bank: v.bank ?? null,
  note: v.note || null,
});

export async function createBranch(v: BranchInput): Promise<BranchProfile> {
  const b = await api<ServerBranch>('/system/branches', { body: { code: v.code, ...body(v) } });
  upsert(b);
  return toProfile(b);
}

export async function updateBranch(id: string, v: BranchInput) {
  upsert(await api<ServerBranch>(`/system/branches/${toGuid(id)}`, { method: 'PUT', body: { ...body(v), version: versions.get(id) ?? 0 } }));
}

async function changeStatus(path: string, payload?: unknown): Promise<string[]> {
  const r = await api<StatusResult>(path, payload === undefined ? { method: 'POST' } : { body: payload });
  upsert(r.branch);
  return r.warnings;
}

/** Khai trương (CN-05) — trả cảnh báo không chặn (chưa có tài khoản ngân hàng / quản lý) */
export const openBranch = (id: string) => changeStatus(`/system/branches/${toGuid(id)}/open`);

/** Đóng (CN-06) — server kiểm mã xác nhận và nhân sự còn lại */
export const closeBranch = (id: string, confirmCode: string) => changeStatus(`/system/branches/${toGuid(id)}/close`, { confirmCode });

export const reopenBranch = (id: string) => changeStatus(`/system/branches/${toGuid(id)}/reopen`);
