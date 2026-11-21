/**
 * Nối Chia lead & SLA, Test xếp lớp, Chiến dịch với backend thật (/api/v1/crm — CRM-09 → CRM-13, CRM-16).
 * Đọc rồi ĐỔ VÀO useAdmissionStore (Id mock nhờ seedIds). Server chạy chia lead, quét SLA, bốc câu, chấm điểm, hết hạn link.
 * Store giữ TÊN người (hiển thị như chế độ mock) — đổi tên ↔ Id nhân viên khi gọi server; khóa quảng bá đổi nhóm khóa ↔ Id khóa học.
 */
import { api, ApiError } from '@/lib/api/http';
import { fromGuid, toGuid } from '@/lib/api/ids';
import { useCourseStore } from '@/features/academic/courses/courseStore';
import { useHrStore } from '@/features/hr/hrStore';
import type { AssignmentLog, Campaign, CampaignStatus, LeadRule, LeadSource, PlacementAttempt, PlacementSection, PlacementTest, PlacementTestStatus, RouteMethod } from '../types';
import { useAdmissionStore, type CampaignInput, type RuleInput, type TestInput } from './admissionStore';

// ---------------- Đổi tên người / khóa ----------------

const nameOf = (guid: string) => {
  const id = fromGuid(guid);
  return useHrStore.getState().employees.find((e) => e.id === id)?.fullName ?? id;
};
/** Tên → Id nhân viên (bỏ phần "(chức vụ)" nếu có); không thấy → null */
const personGuid = (name: string) => {
  const clean = name.replace(/\s*\(.*\)\s*$/, '').trim();
  const e = useHrStore.getState().employees.find((x) => x.fullName === clean || x.id === name);
  return e ? toGuid(e.id) : null;
};
/** Id khóa học → nhóm khóa (n5..n1 theo cấp đầu ra của khóa JLPT, còn lại theo nhóm) */
const courseCode = (guid: string) => {
  const c = useCourseStore.getState().courses.find((x) => x.id === fromGuid(guid));
  return c ? (c.category === 'jlpt' ? c.levelTo.toLowerCase() : c.category) : undefined;
};
const courseGuids = (codes: string[]) =>
  useCourseStore
    .getState()
    .courses.filter((c) => codes.some((code) => c.category === code || (c.category === 'jlpt' && c.levelTo.toLowerCase() === code)))
    .map((c) => toGuid(c.id));

const versions = new Map<string, number>();
const remember = (id: string, v: number) => {
  versions.set(id, v);
  return id;
};
const versionOf = (id: string) => versions.get(id) ?? 0;

/** Không có quyền xem trang → để rỗng (không lẫn dữ liệu giả) */
async function orEmpty<T>(p: Promise<T>, empty: T): Promise<T> {
  try {
    return await p;
  } catch (e) {
    if (e instanceof ApiError && e.status === 403) return empty;
    throw e;
  }
}

// ======================== CRM-09: quy tắc chia lead ========================

interface ServerRule {
  id: string;
  name: string;
  priority: number;
  active: boolean;
  sources: LeadSource[];
  branchIds: string[];
  interests: string[];
  method: RouteMethod;
  saleIds: string[];
  onlyOnShift: boolean;
  slaMinutes: number;
  escalateToIds: string[];
  reassignOnBreach: boolean;
  cursor: number;
  warnings: string[];
  version: number;
}

/** Cảnh báo của quy tắc (Sale đã nghỉ việc…) theo Id quy tắc */
export const ruleWarnings = new Map<string, string[]>();

const toRule = (r: ServerRule): LeadRule => {
  const id = remember(fromGuid(r.id), r.version);
  ruleWarnings.set(id, r.warnings);
  return {
    id,
    name: r.name,
    active: r.active,
    sources: r.sources,
    branchIds: r.branchIds.map(fromGuid),
    interests: r.interests,
    method: r.method,
    saleIds: r.saleIds.map(fromGuid),
    onlyOnShift: r.onlyOnShift,
    slaMinutes: r.slaMinutes,
    escalateTo: r.escalateToIds.map(nameOf),
    reassignOnBreach: r.reassignOnBreach,
    cursor: r.cursor,
  };
};

const ruleBody = (v: RuleInput) => ({
  name: v.name,
  active: v.active,
  sources: v.sources,
  branchIds: v.branchIds.map(toGuid),
  interests: v.interests,
  method: v.method,
  saleIds: v.saleIds.map(toGuid),
  onlyOnShift: v.onlyOnShift,
  slaMinutes: v.slaMinutes,
  escalateToIds: v.escalateTo.map(personGuid).filter((x): x is string => Boolean(x)),
  reassignOnBreach: v.reassignOnBreach,
});

function upsertRule(r: ServerRule) {
  const rule = toRule(r);
  useAdmissionStore.setState((s) => ({
    rules: s.rules.some((x) => x.id === rule.id) ? s.rules.map((x) => (x.id === rule.id ? rule : x)) : [...s.rules, rule],
  }));
  return rule;
}

export async function loadRules() {
  const list = await orEmpty(api<ServerRule[]>('/crm/lead-rules'), []);
  useAdmissionStore.setState({ rules: [...list].sort((a, b) => a.priority - b.priority).map(toRule) });
}

export const saveRule = async (id: string | undefined, v: RuleInput) =>
  upsertRule(
    await api<ServerRule>(id ? `/crm/lead-rules/${toGuid(id)}` : '/crm/lead-rules', {
      method: id ? 'PUT' : 'POST',
      body: { ...ruleBody(v), version: id ? versionOf(id) : 0 },
    }),
  );

export async function deleteRule(id: string) {
  await api(`/crm/lead-rules/${toGuid(id)}`, { method: 'DELETE' });
  useAdmissionStore.setState((s) => ({ rules: s.rules.filter((r) => r.id !== id) }));
}

/** Đổi thứ tự ưu tiên: gửi cả danh sách Id theo thứ tự mới */
export async function reorderRules(ids: string[]) {
  const list = await api<ServerRule[]>('/crm/lead-rules/order', { method: 'PUT', body: { ids: ids.map(toGuid) } });
  useAdmissionStore.setState({ rules: [...list].sort((a, b) => a.priority - b.priority).map(toRule) });
}

interface ServerLog {
  id: string;
  leadId: string;
  saleId?: string | null;
  ruleId?: string | null;
  at: string;
  reason: string;
}

const toLog = (l: ServerLog): AssignmentLog => ({
  id: fromGuid(l.id),
  leadId: fromGuid(l.leadId),
  saleId: l.saleId ? fromGuid(l.saleId) : undefined,
  ruleId: l.ruleId ? fromGuid(l.ruleId) : undefined,
  at: l.at,
  reason: l.reason,
});

/** Nhật ký chia lead mới nhất (100 dòng) — hoặc của 1 lead (đưa lên đầu store) */
export async function loadAssignmentLogs(leadId?: string): Promise<AssignmentLog[]> {
  const r = await orEmpty(
    api<{ items: ServerLog[] }>(`/crm/lead-assignment-logs?pageSize=100${leadId ? `&leadId=${toGuid(leadId)}` : ''}`),
    { items: [] },
  );
  const logs = r.items.map(toLog);
  if (leadId) useAdmissionStore.setState((s) => ({ logs: [...logs, ...s.logs.filter((x) => x.leadId !== leadId)] }));
  else useAdmissionStore.setState({ logs });
  return logs;
}

// ======================== CRM-12, 13: test xếp lớp ========================

interface ServerTest {
  id: string;
  code: string;
  name: string;
  description?: string | null;
  sections: PlacementSection[];
  questionIds: string[];
  durationMin: number;
  bands: PlacementTest['bands'];
  status: PlacementTestStatus;
  author: string;
  createdAt: string;
  attempts: number;
  version: number;
}

const toTest = (t: ServerTest): PlacementTest => ({
  id: remember(fromGuid(t.id), t.version),
  code: t.code,
  name: t.name,
  description: t.description ?? undefined,
  sections: t.sections,
  questionIds: t.questionIds.map(fromGuid),
  durationMin: t.durationMin,
  bands: t.bands,
  status: t.status,
  author: t.author,
  createdAt: t.createdAt,
});

function upsertTest(t: ServerTest) {
  const test = toTest(t);
  useAdmissionStore.setState((s) => ({
    tests: s.tests.some((x) => x.id === test.id) ? s.tests.map((x) => (x.id === test.id ? test : x)) : [test, ...s.tests],
  }));
  return test;
}

export async function loadTests() {
  const list = await orEmpty(api<ServerTest[]>('/crm/placement-tests'), []);
  useAdmissionStore.setState({ tests: list.map(toTest) });
}

/** Tạo / sửa bài test — `redraw` = "Đổi đề" (bốc lại câu); server báo phần thiếu câu đã duyệt */
export const saveTest = async (id: string | undefined, v: Pick<TestInput, 'name' | 'description' | 'sections' | 'durationMin' | 'bands'>, redraw = false) =>
  upsertTest(
    await api<ServerTest>(id ? `/crm/placement-tests/${toGuid(id)}` : '/crm/placement-tests', {
      method: id ? 'PUT' : 'POST',
      body: {
        name: v.name,
        description: v.description ?? null,
        sections: v.sections,
        durationMin: v.durationMin,
        bands: v.bands,
        redraw,
        version: id ? versionOf(id) : 0,
      },
    }),
  );

export const changeTestStatus = async (id: string, status: PlacementTestStatus) =>
  upsertTest(await api<ServerTest>(`/crm/placement-tests/${toGuid(id)}/status`, { body: { status } }));

interface ServerAttempt {
  id: string;
  testId: string;
  leadId: string;
  token: string;
  link: string;
  channel: PlacementAttempt['channel'];
  sentBy: string;
  sentAt: string;
  expiresAt: string;
  status: PlacementAttempt['status'];
  startedAt?: string | null;
  submittedAt?: string | null;
  answers: Record<string, number>;
  correct?: number | null;
  total?: number | null;
  bySkill: PlacementAttempt['bySkill'];
  suggestedLevel?: string | null;
  suggestedCourse?: string | null;
  logged: boolean;
}

/** Link làm bài thật (server ghép theo App:FrontendUrl) theo token */
export const attemptLinks = new Map<string, string>();

const toAttempt = (a: ServerAttempt): PlacementAttempt => {
  attemptLinks.set(a.token, a.link);
  return {
    id: fromGuid(a.id),
    testId: fromGuid(a.testId),
    leadId: fromGuid(a.leadId),
    token: a.token,
    channel: a.channel,
    sentBy: a.sentBy,
    sentAt: a.sentAt,
    expiresAt: a.expiresAt,
    status: a.status,
    startedAt: a.startedAt ?? undefined,
    submittedAt: a.submittedAt ?? undefined,
    answers: Object.fromEntries(Object.entries(a.answers).map(([q, v]) => [fromGuid(q), v])),
    correct: a.correct ?? undefined,
    total: a.total ?? undefined,
    bySkill: a.bySkill,
    suggestedLevel: a.suggestedLevel ?? undefined,
    suggestedCourse: a.suggestedCourse ?? undefined,
    logged: a.logged,
  };
};

function upsertAttempt(a: ServerAttempt) {
  const at = toAttempt(a);
  useAdmissionStore.setState((s) => ({
    attempts: s.attempts.some((x) => x.id === at.id) ? s.attempts.map((x) => (x.id === at.id ? at : x)) : [at, ...s.attempts],
  }));
  return at;
}

export async function loadAttempts() {
  const list = await orEmpty(api<ServerAttempt[]>('/crm/placement-attempts'), []);
  useAdmissionStore.setState({ attempts: list.map(toAttempt) });
}

/** CRM-13 — gửi link test (server ghi lịch sử tư vấn, chuyển lead sang "Đã hẹn test") */
export const sendAttempt = async (v: { testId: string; leadId: string; channel: PlacementAttempt['channel']; validDays: number }) =>
  upsertAttempt(
    await api<ServerAttempt>('/crm/placement-attempts', {
      body: { testId: toGuid(v.testId), leadId: toGuid(v.leadId), channel: v.channel, validDays: v.validDays },
    }),
  );

/** Câu trong đề (kèm đáp án — chỉ nhân viên) để xem bài làm của lead */
export interface TestQuestion {
  id: string;
  code: string;
  level: string;
  skill: PlacementSection['skill'];
  stem: string;
  options: string[];
  answer: number;
}

export async function loadTestQuestions(testId: string): Promise<TestQuestion[]> {
  const list = await api<(Omit<TestQuestion, 'answer'> & { answer?: number | null })[]>(`/crm/placement-tests/${toGuid(testId)}/questions`);
  return list.map((q) => ({ ...q, id: fromGuid(q.id), answer: q.answer ?? -1 }));
}

/** Ghi kết quả vào lịch sử tư vấn + khóa quan tâm của lead */
export const logAttempt = async (id: string) => upsertAttempt(await api<ServerAttempt>(`/crm/placement-attempts/${toGuid(id)}/log`, { method: 'POST' }));

// ======================== CRM-16: chiến dịch ========================

export interface CampaignStats {
  leads: number;
  won: number;
  targetPct?: number | null;
  winRate?: number | null;
  costPerLead?: number | null;
  costPerWin?: number | null;
}

interface ServerCampaign {
  id: string;
  code: string;
  name: string;
  channel: LeadSource;
  branchIds: string[];
  startDate: string;
  endDate: string;
  budget: number;
  spent: number;
  targetLeads?: number | null;
  status: CampaignStatus;
  ownerId: string;
  owner: string;
  utm?: string | null;
  courseIds: string[];
  note?: string | null;
  createdAt: string;
  stats: CampaignStats;
  version: number;
}

/** Số liệu server tính (lead / chốt theo phạm vi quyền) theo Id chiến dịch */
export const campaignStats = new Map<string, CampaignStats>();

const toCampaign = (c: ServerCampaign): Campaign => {
  const id = remember(fromGuid(c.id), c.version);
  campaignStats.set(id, c.stats);
  return {
    id,
    code: c.code,
    name: c.name,
    channel: c.channel,
    branchIds: c.branchIds.map(fromGuid),
    startDate: c.startDate,
    endDate: c.endDate,
    budget: c.budget,
    spent: c.spent,
    targetLeads: c.targetLeads ?? undefined,
    status: c.status,
    owner: c.owner,
    utm: c.utm ?? undefined,
    courses: [...new Set(c.courseIds.map(courseCode).filter((x): x is string => Boolean(x)))],
    note: c.note ?? undefined,
    createdAt: c.createdAt,
  };
};

function upsertCampaign(c: ServerCampaign) {
  const cp = toCampaign(c);
  useAdmissionStore.setState((s) => ({
    campaigns: s.campaigns.some((x) => x.id === cp.id) ? s.campaigns.map((x) => (x.id === cp.id ? cp : x)) : [cp, ...s.campaigns],
  }));
  return cp;
}

export async function loadCampaigns() {
  const list = await orEmpty(api<ServerCampaign[]>('/crm/campaigns'), []);
  useAdmissionStore.setState({ campaigns: list.map(toCampaign) });
}

/** Tạo / sửa chiến dịch (trạng thái đổi riêng) */
export async function saveCampaign(id: string | undefined, v: CampaignInput) {
  const ownerId = personGuid(v.owner);
  if (!ownerId) throw new ApiError('Chọn người phụ trách trong danh sách nhân viên', 'crm.campaign.owner_required', 400);
  return upsertCampaign(
    await api<ServerCampaign>(id ? `/crm/campaigns/${toGuid(id)}?version=${versionOf(id)}` : '/crm/campaigns', {
      method: id ? 'PUT' : 'POST',
      body: {
        code: v.code,
        name: v.name,
        channel: v.channel,
        branchIds: v.branchIds.map(toGuid),
        startDate: v.startDate,
        endDate: v.endDate,
        budget: v.budget,
        spent: v.spent,
        targetLeads: v.targetLeads ?? null,
        ownerId,
        utm: v.utm || null,
        courseIds: courseGuids(v.courses),
        note: v.note ?? null,
      },
    }),
  );
}

export const changeCampaignStatus = async (id: string, status: CampaignStatus) =>
  upsertCampaign(await api<ServerCampaign>(`/crm/campaigns/${toGuid(id)}/status`, { body: { status } }));
