/**
 * Nối Phụ huynh / người giám hộ (HV-12) với backend thật: /api/v1/crm/guardians — quyền students.contacts (giáo vụ không cần quyền Tuyển sinh).
 * Phụ huynh là lead của Crm → ĐỔ VÀO useCrmStore (leads + links) như trang Tuyển sinh; các trang khác (hồ sơ HV, lên lớp, cổng) đọc store như trước.
 * Nạp sau Tuyển sinh: danh sách liên kết HV ↔ người liên hệ thay hẳn dữ liệu giả.
 */
import { api, ApiError } from '@/lib/api/http';
import { fromGuid, toGuid } from '@/lib/api/ids';
import { useCrmStore } from '@/features/crm/crmStore';
import type { Lead, LeadSource, LeadStatus, LeadStudentLink, NotifyChannel, Relationship } from '@/features/crm/types';

interface ServerLink {
  studentId: string;
  relationship: Relationship;
  primary: boolean;
  billing: boolean;
  notify: boolean;
  createdAt: string;
}

interface ServerGuardian {
  id: string;
  code: string;
  fullName: string;
  phone: string;
  email?: string | null;
  branchId: string;
  source: LeadSource;
  status: LeadStatus;
  assigneeId?: string | null;
  note?: string | null;
  notifyChannels: NotifyChannel[];
  hidden: boolean;
  createdAt: string;
  students: ServerLink[];
  version: number;
}

interface ServerWarning {
  code: string;
  message: string;
}

const versions = new Map<string, number>();

const toLink = (leadId: string, k: ServerLink): LeadStudentLink => ({
  leadId,
  studentId: fromGuid(k.studentId),
  relationship: k.relationship,
  primary: k.primary,
  billing: k.billing,
  notify: k.notify,
  createdAt: k.createdAt,
});

/** Thông tin liên hệ của lead từ server — lead đã có trong store (nạp từ Tuyển sinh) giữ các trường khác */
function toLead(g: ServerGuardian, old?: Lead): Lead {
  return {
    ...old,
    id: fromGuid(g.id),
    code: g.code,
    fullName: g.fullName,
    phone: g.phone,
    email: g.email ?? undefined,
    branchId: fromGuid(g.branchId),
    source: g.source,
    status: g.status,
    assigneeId: g.assigneeId ? fromGuid(g.assigneeId) : undefined,
    note: g.note ?? undefined,
    notifyChannels: g.notifyChannels,
    hidden: g.hidden,
    createdAt: g.createdAt,
    createdBy: old?.createdBy ?? '',
    linkedStudents: g.students.length,
  };
}

/** Thay / thêm người liên hệ + toàn bộ liên kết của họ; `onlyThese` = thay hẳn mọi liên kết trong store (lúc nạp) */
function apply(list: ServerGuardian[], onlyThese = false) {
  useCrmStore.setState((s) => {
    const byId = new Map(s.leads.map((l) => [l.id, l]));
    const fresh = list.map((g) => {
      versions.set(fromGuid(g.id), g.version);
      return toLead(g, byId.get(fromGuid(g.id)));
    });
    const ids = new Set(fresh.map((l) => l.id));
    const links = list.flatMap((g) => g.students.map((k) => toLink(fromGuid(g.id), k)));
    // Mỗi HV tối đa 1 liên hệ chính → bỏ cờ ở lead khác (server đã tắt)
    const primaryOf = new Set(links.filter((k) => k.primary).map((k) => k.studentId));
    const kept = onlyThese
      ? []
      : s.links.filter((k) => !ids.has(k.leadId)).map((k) => (k.primary && primaryOf.has(k.studentId) ? { ...k, primary: false } : k));
    return {
      leads: [...fresh.filter((l) => !byId.has(l.id)), ...s.leads.map((l) => (ids.has(l.id) ? fresh.find((x) => x.id === l.id)! : l))],
      links: [...kept, ...links],
    };
  });
}

/** Danh sách phụ huynh trong phạm vi (thay hẳn liên kết giả). Không có quyền → bỏ liên kết giả; liên kết thật nạp khi mở chi tiết lead */
export async function loadGuardians() {
  try {
    apply(await api<ServerGuardian[]>('/crm/guardians'), true);
  } catch (e) {
    if (e instanceof ApiError && e.status === 403) useCrmStore.setState({ links: [] });
    else throw e;
  }
}

export interface GuardianSaveResult {
  leadId: string;
  /** SĐT trùng người liên hệ có sẵn → đã liên kết thêm vào người đó */
  reused: boolean;
  warnings: string[];
}

const saved = (r: { guardian: ServerGuardian; reused: boolean; warnings: ServerWarning[] }): GuardianSaveResult => {
  apply([r.guardian]);
  return { leadId: fromGuid(r.guardian.id), reused: r.reused, warnings: r.warnings.filter((w) => w.code !== 'crm.guardian.reused').map((w) => w.message) };
};

export interface GuardianCreateInput {
  fullName: string;
  phone: string;
  email?: string;
  branchId: string;
  notifyChannels?: NotifyChannel[];
  note?: string;
  relationship: Exclude<Relationship, 'self'>;
  studentIds: string[];
  primary?: boolean;
}

/** HV-12 bước 3 (cũng dùng khi nhập HV từ Excel có cột phụ huynh) */
export const createGuardian = async (v: GuardianCreateInput) =>
  saved(
    await api('/crm/guardians', {
      body: {
        fullName: v.fullName,
        phone: v.phone,
        email: v.email ?? null,
        branchId: toGuid(v.branchId),
        notifyChannels: v.notifyChannels ?? [],
        note: v.note ?? null,
        relationship: v.relationship,
        studentIds: v.studentIds.map(toGuid),
        primary: Boolean(v.primary),
      },
    }),
  );

export const updateGuardian = async (
  id: string,
  v: { fullName: string; phone: string; email?: string; branchId: string; notifyChannels?: NotifyChannel[]; note?: string },
) =>
  saved(
    await api(`/crm/guardians/${toGuid(id)}`, {
      method: 'PUT',
      body: {
        fullName: v.fullName,
        phone: v.phone,
        email: v.email ?? null,
        branchId: toGuid(v.branchId),
        notifyChannels: v.notifyChannels ?? [],
        note: v.note ?? null,
        version: versions.get(id) ?? 0,
      },
    }),
  );

// ---------------- Liên kết HV (luật CRM-08) ----------------

interface ServerLeadLink {
  leadId: string;
  studentId: string;
  relationship: Relationship;
  createdAt: string;
  primary: boolean;
  billing: boolean;
  notify: boolean;
}

/** Thay liên kết của 1 người liên hệ theo kết quả server; trả cảnh báo (đổi liên hệ chính, HV chưa có người quyết định…) */
function applyLinks(leadId: string, r: { students: ServerLeadLink[]; warnings: ServerWarning[] }) {
  const links = r.students.map((k) => ({ ...toLink(leadId, k), leadId }));
  const primaryOf = new Set(links.filter((k) => k.primary).map((k) => k.studentId));
  useCrmStore.setState((s) => ({
    links: [
      ...s.links.filter((k) => k.leadId !== leadId).map((k) => (k.primary && primaryOf.has(k.studentId) ? { ...k, primary: false } : k)),
      ...links,
    ],
    leads: s.leads.map((l) => (l.id === leadId ? { ...l, linkedStudents: links.length } : l)),
  }));
  return r.warnings.map((w) => w.message);
}

export interface LinkFlags {
  relationship: Relationship;
  primary: boolean;
  billing: boolean;
  notify: boolean;
}

const url = (leadId: string, studentId?: string) => `/crm/guardians/${toGuid(leadId)}/students${studentId ? `/${toGuid(studentId)}` : ''}`;

export const linkGuardianStudent = async (leadId: string, studentId: string, f: LinkFlags) =>
  applyLinks(leadId, await api(url(leadId), { body: { studentId: toGuid(studentId), ...f } }));

export const updateGuardianLink = async (leadId: string, studentId: string, f: LinkFlags) =>
  applyLinks(leadId, await api(url(leadId, studentId), { method: 'PUT', body: f }));

export const unlinkGuardianStudent = async (leadId: string, studentId: string) =>
  applyLinks(leadId, await api(url(leadId, studentId), { method: 'DELETE' }));

/** Cờ hiện tại của liên kết (để đổi 1 cờ, giữ các cờ khác) */
export function currentFlags(leadId: string, studentId: string): LinkFlags | undefined {
  const k = useCrmStore.getState().links.find((x) => x.leadId === leadId && x.studentId === studentId);
  return k && { relationship: k.relationship, primary: Boolean(k.primary), billing: Boolean(k.billing), notify: Boolean(k.notify) };
}
