/**
 * Nối Lead (Tuyển sinh) với backend thật (/api/v1/crm/leads — user story 05, CRM-01 → CRM-08).
 * Đọc rồi ĐỔ VÀO useCrmStore (Id mock nhờ seedIds) — các trang khác (bảng điều khiển, tìm kiếm, phụ huynh) vẫn đọc store như trước.
 *  - Danh sách: nạp cả lead trong phạm vi (kể cả đã ẩn), lọc / tìm tại chỗ. Lịch sử tư vấn + HV liên kết nạp khi mở chi tiết lead.
 *  - Chia lead tự động, SLA, dòng hệ thống trong lịch sử tư vấn do server làm.
 * ⏳ Trang Phụ huynh (HV-12) còn ghi liên kết vào dữ liệu giả — chỉ đọc lại liên kết thật của lead đã mở.
 */
import { api, ApiError } from '@/lib/api/http';
import { fromGuid, toGuid } from '@/lib/api/ids';
import { refreshStudent } from '@/features/student/studentApi';
import { useStudentStore } from '@/features/student/studentStore';
import { refreshInvoicesSoon } from '@/features/finance/financeApi';
import type { Enrollment } from '@/features/student/types';
import { useCrmStore, type LeadInput } from './crmStore';
import type { ConsultChannel, ConsultResult, Consultation, Lead, LeadSource, LeadStatus, LeadStudentLink, NotifyChannel, Relationship } from './types';

interface ServerLead {
  id: string;
  code: string;
  fullName: string;
  phone: string;
  email?: string | null;
  source: LeadSource;
  status: LeadStatus;
  branchId: string;
  assigneeId?: string | null;
  assigneeName?: string | null;
  interest?: string | null;
  note?: string | null;
  lostReason?: string | null;
  nextFollowUpAt?: string | null;
  lastContactAt?: string | null;
  createdAt: string;
  hidden: boolean;
  hiddenAt?: string | null;
  notifyChannels: NotifyChannel[];
  campaignId?: string | null;
  slaEscalatedAt?: string | null;
  slaReassignedAt?: string | null;
  slaOverdue: boolean;
  slaMinutes: number;
  followUpOverdue: boolean;
  linkedStudents: number;
  version: number;
}

interface ServerConsultation {
  id: string;
  leadId: string;
  channel: ConsultChannel;
  result: ConsultResult;
  content: string;
  staffName: string;
  createdAt: string;
  nextFollowUpAt?: string | null;
  system: boolean;
}

interface ServerLink {
  leadId: string;
  studentId: string;
  studentCode: string;
  studentName: string;
  studentDob?: string | null;
  relationship: Relationship;
  createdAt: string;
  primary: boolean;
  billing: boolean;
  notify: boolean;
}

interface ServerDetail {
  lead: ServerLead;
  consultations: ServerConsultation[];
  students: ServerLink[];
}

export interface LeadWarning {
  code: string;
  message: string;
  leadId?: string;
}

interface ServerWarning {
  code: string;
  message: string;
  leadId?: string | null;
}

interface Paged<T> {
  items: T[];
  total: number;
}

const versions = new Map<string, number>();
const opt = <T>(v: T | null | undefined) => v ?? undefined;
const toWarning = (w: ServerWarning): LeadWarning => ({ code: w.code, message: w.message, leadId: w.leadId ? fromGuid(w.leadId) : undefined });

export function toLead(l: ServerLead): Lead {
  const id = fromGuid(l.id);
  versions.set(id, l.version);
  return {
    id,
    code: l.code,
    fullName: l.fullName,
    phone: l.phone,
    email: opt(l.email),
    source: l.source,
    status: l.status,
    branchId: fromGuid(l.branchId),
    assigneeId: l.assigneeId ? fromGuid(l.assigneeId) : undefined,
    interest: opt(l.interest),
    note: opt(l.note),
    lostReason: opt(l.lostReason),
    nextFollowUpAt: opt(l.nextFollowUpAt),
    lastContactAt: opt(l.lastContactAt),
    createdAt: l.createdAt,
    createdBy: '',
    hidden: l.hidden,
    hiddenAt: opt(l.hiddenAt),
    notifyChannels: l.notifyChannels,
    campaignId: l.campaignId ? fromGuid(l.campaignId) : undefined,
    slaEscalatedAt: opt(l.slaEscalatedAt),
    slaReassignedAt: opt(l.slaReassignedAt),
    slaOverdue: l.slaOverdue,
    slaMinutes: l.slaMinutes,
    linkedStudents: l.linkedStudents,
  };
}

const toConsultation = (c: ServerConsultation): Consultation => ({
  id: fromGuid(c.id),
  leadId: fromGuid(c.leadId),
  channel: c.channel,
  result: c.result,
  content: c.content,
  staffName: c.staffName,
  createdAt: c.createdAt,
  nextFollowUpAt: opt(c.nextFollowUpAt),
  system: c.system,
});

const toLink = (k: ServerLink): LeadStudentLink => ({
  leadId: fromGuid(k.leadId),
  studentId: fromGuid(k.studentId),
  relationship: k.relationship,
  createdAt: k.createdAt,
  primary: k.primary,
  billing: k.billing,
  notify: k.notify,
});

/** Thay / thêm lead trong store (lead mới lên đầu) */
function upsertLeads(list: ServerLead[]): Lead[] {
  const incoming = list.map(toLead);
  useCrmStore.setState((s) => {
    const byId = new Map(incoming.map((l) => [l.id, l]));
    const kept = s.leads.map((l) => byId.get(l.id) ?? l);
    const fresh = incoming.filter((l) => !s.leads.some((x) => x.id === l.id));
    return { leads: [...fresh, ...kept] };
  });
  return incoming;
}

/** Thay toàn bộ liên kết HV của 1 lead (HV chưa có trong store → thêm bản tối thiểu để hiện tên / mã) */
function replaceLinks(leadId: string, list: ServerLink[]) {
  const links = list.map(toLink);
  useCrmStore.setState((s) => {
    const known = new Set(s.students.map((x) => x.id));
    const missing = list
      .filter((k) => !known.has(fromGuid(k.studentId)))
      .map((k) => ({
        id: fromGuid(k.studentId),
        code: k.studentCode,
        fullName: k.studentName,
        dob: opt(k.studentDob),
        branchId: s.leads.find((l) => l.id === leadId)?.branchId ?? '',
        joinedAt: k.createdAt,
        hidden: false,
      }));
    // Cờ "liên hệ chính" mỗi HV chỉ 1 → bỏ cờ ở lead khác của cùng HV khi lead này là chính
    const primaryOf = new Set(links.filter((k) => k.primary).map((k) => k.studentId));
    const others = s.links
      .filter((k) => k.leadId !== leadId)
      .map((k) => (k.primary && primaryOf.has(k.studentId) ? { ...k, primary: false } : k));
    return {
      links: [...others, ...links],
      students: missing.length ? [...s.students, ...missing] : s.students,
      leads: s.leads.map((l) => (l.id === leadId ? { ...l, linkedStudents: links.length } : l)),
    };
  });
}

function applyDetail(d: ServerDetail): Lead {
  const [lead] = upsertLeads([d.lead]);
  const consults = d.consultations.map(toConsultation);
  useCrmStore.setState((s) => ({ consultations: [...consults, ...s.consultations.filter((c) => c.leadId !== lead.id)] }));
  replaceLinks(lead.id, d.students);
  return lead;
}

// ---------------- Đọc ----------------

async function allPages(hidden: boolean): Promise<ServerLead[]> {
  const out: ServerLead[] = [];
  for (let page = 1; ; page++) {
    const r = await api<Paged<ServerLead>>(`/crm/leads?hidden=${hidden}&page=${page}&pageSize=100`);
    out.push(...r.items);
    if (out.length >= r.total || r.items.length === 0) return out;
  }
}

/** Mọi lead trong phạm vi (own = lead mình phụ trách), kể cả đã ẩn. Không có quyền → danh sách rỗng */
export async function loadLeads() {
  try {
    const [visible, hidden] = await Promise.all([allPages(false), allPages(true)]);
    versions.clear();
    useCrmStore.setState({ leads: [...visible, ...hidden].map(toLead), consultations: [] });
  } catch (e) {
    if (e instanceof ApiError && e.status === 403) useCrmStore.setState({ leads: [], consultations: [] });
    else throw e;
  }
}

/** Chi tiết lead: thông tin mới nhất + lịch sử tư vấn + HV liên kết */
export const loadLead = async (id: string) => applyDetail(await api<ServerDetail>(`/crm/leads/${toGuid(id)}`));

/** Lead khác trùng SĐT (CRM-02.E3) — chỉ cảnh báo */
export async function findDuplicates(phone: string, exceptId?: string): Promise<LeadWarning[]> {
  const q = new URLSearchParams({ phone });
  if (exceptId) q.set('exceptId', toGuid(exceptId));
  return (await api<ServerWarning[]>(`/crm/leads/duplicates?${q}`)).map(toWarning);
}

// ---------------- Ghi ----------------

const body = (v: LeadInput) => ({
  fullName: v.fullName,
  phone: v.phone,
  email: v.email || null,
  source: v.source,
  branchId: toGuid(v.branchId),
  interest: v.interest ?? null,
  note: v.note ?? null,
  campaignId: v.campaignId ? toGuid(v.campaignId) : null,
  assigneeId: v.assigneeId ? toGuid(v.assigneeId) : null,
  nextFollowUpAt: v.nextFollowUpAt ?? null,
  notifyChannels: v.notifyChannels ?? [],
});

export interface LeadSavedResult {
  lead: Lead;
  warnings: LeadWarning[];
}

/** CRM-02 — không chọn Sale → server tự chia theo quy tắc (lý do trong nhật ký chia lead) */
export async function createLead(v: LeadInput): Promise<LeadSavedResult> {
  const r = await api<{ lead: ServerLead; warnings: ServerWarning[] }>('/crm/leads', {
    body: { ...body(v), status: v.status, lostReason: v.status === 'lost' ? (v.lostReason ?? null) : null },
  });
  return { lead: upsertLeads([r.lead])[0], warnings: r.warnings.map(toWarning) };
}

/** CRM-04 — sửa thông tin (chống sửa đè); đổi giai đoạn gọi riêng (server kiểm luật chuyển giai đoạn) */
export async function updateLead(id: string, v: LeadInput): Promise<LeadSavedResult> {
  const r = await api<{ lead: ServerLead; warnings: ServerWarning[] }>(`/crm/leads/${toGuid(id)}`, {
    method: 'PUT',
    body: { ...body(v), version: versions.get(id) ?? 0 },
  });
  let lead = upsertLeads([r.lead])[0];
  if (v.status !== lead.status || (v.status === 'lost' && v.lostReason !== lead.lostReason)) lead = await changeLeadStatus(id, v.status, v.lostReason);
  return { lead, warnings: r.warnings.map(toWarning) };
}

export const changeLeadStatus = async (id: string, status: LeadStatus, lostReason?: string) =>
  upsertLeads([await api<ServerLead>(`/crm/leads/${toGuid(id)}/status`, { body: { status, lostReason: lostReason ?? null } })])[0];

export const assignLead = async (id: string, saleId: string, note?: string) =>
  upsertLeads([await api<ServerLead>(`/crm/leads/${toGuid(id)}/assign`, { body: { saleId: toGuid(saleId), note: note ?? null } })])[0];

/** CRM-05 — server tự chuyển Mới → Đang tư vấn, cập nhật liên hệ gần nhất / hẹn gọi lại */
export const addConsultation = async (
  leadId: string,
  v: { channel: ConsultChannel; result: ConsultResult; content: string; nextFollowUpAt?: string },
) =>
  applyDetail(
    await api<ServerDetail>(`/crm/leads/${toGuid(leadId)}/consultations`, {
      body: { channel: v.channel, result: v.result, content: v.content, nextFollowUpAt: v.nextFollowUpAt ?? null },
    }),
  );

export const setLeadHidden = async (id: string, hidden: boolean) =>
  upsertLeads([await api<ServerLead>(`/crm/leads/${toGuid(id)}/${hidden ? 'hide' : 'unhide'}`, { method: 'POST' })])[0];

// ---------------- CRM-08: học viên liên kết ----------------

export interface LinkFlags {
  relationship: Relationship;
  primary?: boolean;
  billing?: boolean;
  notify?: boolean;
}

const flags = (f: LinkFlags) => ({ relationship: f.relationship, primary: Boolean(f.primary), billing: Boolean(f.billing), notify: Boolean(f.notify) });

async function links(leadId: string, call: Promise<{ students: ServerLink[]; warnings: ServerWarning[] }>) {
  const r = await call;
  replaceLinks(leadId, r.students);
  return r.warnings.map(toWarning);
}

export const linkStudent = (leadId: string, studentId: string, f: LinkFlags) =>
  links(leadId, api(`/crm/leads/${toGuid(leadId)}/students`, { body: { studentId: toGuid(studentId), ...flags(f) } }));

export const updateStudentLink = (leadId: string, studentId: string, f: LinkFlags) =>
  links(leadId, api(`/crm/leads/${toGuid(leadId)}/students/${toGuid(studentId)}`, { method: 'PUT', body: flags(f) }));

export const unlinkStudent = (leadId: string, studentId: string) =>
  links(leadId, api(`/crm/leads/${toGuid(leadId)}/students/${toGuid(studentId)}`, { method: 'DELETE' }));

// ---------------- CRM-07: chốt lead → học viên + ghi danh ----------------

export interface EnrollRequest {
  who: 'self' | 'linked' | 'other';
  studentId?: string;
  student?: { fullName: string; phone?: string; dob?: string; relationship: Relationship };
  classId?: string;
  discount: number;
  dueDate?: string;
}

export interface EnrollOutcome {
  studentId: string;
  studentCode: string;
  studentName: string;
  enrollmentId?: string;
}

/** 1 lần bấm "Hoàn tất" = 1 khóa chống gửi trùng (gửi lại cùng khóa → server trả kết quả cũ) */
export async function enrollLead(leadId: string, req: EnrollRequest, idempotencyKey: string): Promise<EnrollOutcome> {
  const r = await api<{ lead: ServerDetail; studentId: string; studentCode: string; studentName: string; enrollmentId?: string | null; amount?: number | null; discount: number }>(
    `/crm/leads/${toGuid(leadId)}/enroll`,
    {
      headers: { 'Idempotency-Key': idempotencyKey },
      body: {
        who: req.who,
        studentId: req.studentId ? toGuid(req.studentId) : null,
        student: req.student ? { ...req.student, phone: req.student.phone || null, dob: req.student.dob ?? null, gender: null } : null,
        classId: req.classId ? toGuid(req.classId) : null,
        amount: null,
        discount: req.discount,
        dueDate: req.dueDate ?? null,
      },
    },
  );
  applyDetail(r.lead);
  const studentId = fromGuid(r.studentId);
  await refreshStudent(studentId).catch(() => undefined);
  let enrollmentId: string | undefined;
  if (r.enrollmentId && req.classId) {
    enrollmentId = fromGuid(r.enrollmentId);
    const e: Enrollment = { id: enrollmentId, studentId, classId: req.classId, status: 'studying', enrolledAt: new Date().toISOString() };
    useStudentStore.setState((s) => ({ enrollments: [...s.enrollments.filter((x) => x.id !== enrollmentId), e] }));
    // Tài chính lập hóa đơn ở nền khi nghe StudentEnrolled (TC-04)
    refreshInvoicesSoon();
  }
  return { studentId, studentCode: r.studentCode, studentName: r.studentName, enrollmentId };
}

// ---------------- CRM-03: nhập Excel ----------------

export interface LeadImportOutcome {
  created: number;
  errors: { row: number; message: string }[];
  warnings: { row: number; message: string }[];
}

/** 1 request cả lô; `row` = số dòng trong file để báo lỗi */
export async function importLeads(rows: { row: number; value: LeadInput }[]): Promise<LeadImportOutcome> {
  const r = await api<{ created: number; leads: ServerLead[]; errors: { row: number; message: string }[]; warnings: { row: number; message: string }[] }>(
    '/crm/leads/import',
    {
      body: {
        rows: rows.map(({ row, value: v }) => ({
          row,
          fullName: v.fullName,
          phone: v.phone,
          email: v.email ?? null,
          source: v.source,
          interest: v.interest ?? null,
          branchId: toGuid(v.branchId),
          assigneeId: v.assigneeId ? toGuid(v.assigneeId) : null,
          note: v.note ?? null,
        })),
      },
    },
  );
  upsertLeads(r.leads);
  return { created: r.created, errors: r.errors, warnings: r.warnings };
}

/** Thông báo lỗi server thân thiện */
export const errorText = (e: unknown, fallback: string) => (e instanceof ApiError ? e.message : fallback);

