/**
 * Nối Nhân sự mở rộng với backend thật (user story 13, giai đoạn 21):
 *   chấm công   /api/v1/hr/me/{check-in|check-out|timesheet} · /hr/timesheets       (hr.timesheets.*)
 *   đơn từ      /hr/me/requests · /hr/requests?tab=todo|mine|all                   (hr.requests.*; duyệt theo lượt)
 *   tuyển dụng  /hr/jobs · /hr/candidates                                          (hr.recruitment.*)
 *   onboarding  /hr/onboardings                                                    (hr.onboarding.*)
 * Dữ liệu ĐỔ VÀO useHrStore với Id mock (e-huy, cg…) như hrApi.ts; trạng thái ngày chấm công lấy bản server (serverDays.ts).
 */
import { USE_API } from '@/lib/api/config';
import { api, ApiError } from '@/lib/api/http';
import { replaceBy } from '@/lib/api/merge';
import { reportError } from '@/lib/api/report';
import { fromGuid, toGuid } from '@/lib/api/ids';
import { useHrStore } from './hrStore';
import { setServerDay } from './serverDays';
import type { DayState } from './hrUtils';
import type { Candidate, CandidateStage, Department, EmployeeKind, HrRequest, Job, Onboarding, OnboardingItem, RequestStatus, RequestType, TimeRecord } from './types';

// ---------- Kiểu server ----------

interface ServerEdited {
  by: string;
  reason: string;
}

interface ServerDayDto {
  date: string;
  state: DayState;
  expected?: { from: string; to: string; label: string };
  checkIn?: string;
  checkOut?: string;
  lateMin: number;
  earlyMin: number;
  workedMin: number;
  problem: boolean;
  recordId?: string;
  edited?: ServerEdited;
}

interface ServerTimesheetEmployee {
  id: string;
  leaveBalance: number;
}

interface ServerRecord {
  id: string;
  employeeId: string;
  date: string;
  checkIn?: string;
  checkOut?: string;
  edited?: ServerEdited;
}

interface PunchResult {
  record: ServerRecord;
  today: ServerDayDto;
  warning?: string;
}

interface ServerStep {
  order: number;
  role: string;
  approver: string;
  approverId?: string;
  decision?: 'approved' | 'rejected';
  at?: string;
  comment?: string;
  onBehalf: boolean;
}

interface ServerRequest {
  id: string;
  code: string;
  type: RequestType;
  employeeId: string;
  from: string;
  to: string;
  days?: number;
  paidLeave?: boolean;
  swap?: { myShiftId: string; withEmployeeId: string; theirShiftId: string };
  amount?: number;
  title?: string;
  reason: string;
  status: RequestStatus;
  steps: ServerStep[];
  createdAt: string;
  canDecide: boolean;
  canCancel: boolean;
}

interface ServerJob {
  id: string;
  title: string;
  branchId: string;
  department: Department;
  kind: EmployeeKind;
  quantity: number;
  salaryRange?: string;
  status: Job['status'];
  postedAt: string;
  deadline: string;
  channels: string[];
  description?: string;
}

interface ServerCandidate {
  id: string;
  jobId: string;
  fullName: string;
  phone: string;
  email: string;
  source: string;
  stage: CandidateStage;
  appliedAt: string;
  cvName?: string;
  hasCv: boolean;
  japanese?: string;
  expectedSalary?: number;
  interview?: { at: string; interviewer: string; interviewerId: string; mode: 'offline' | 'online'; score?: number; note?: string };
  offer?: { salary: number; startDate: string; sentAt: string; accepted?: boolean };
  rejectReason?: string;
  notes: { at: string; by: string; text: string }[];
}

interface CandidateResult {
  candidate: ServerCandidate;
  job: ServerJob;
  onboardingId?: string;
  warnings: string[];
}

interface ServerOnboarding {
  id: string;
  candidateId?: string;
  fullName: string;
  phone: string;
  email: string;
  position: string;
  department: Department;
  kind: EmployeeKind;
  branchId: string;
  startDate: string;
  mentor: string;
  mentorId: string;
  items: OnboardingItem[];
  employeeId?: string;
  createdAt: string;
}

// ---------- Đổi kiểu ----------

function toRecord(r: ServerRecord): TimeRecord {
  return { id: fromGuid(r.id), employeeId: fromGuid(r.employeeId), date: r.date, checkIn: r.checkIn, checkOut: r.checkOut, edited: r.edited };
}

/** Ghi ngày server vào bộ đệm trạng thái; trả bản ghi chấm công (nếu có) */
function absorbDay(employeeId: string, d: ServerDayDto): TimeRecord | undefined {
  setServerDay(employeeId, d.date, { state: d.state, exp: d.expected, lateMin: d.lateMin, earlyMin: d.earlyMin, worked: d.workedMin });
  if (!d.recordId) return undefined;
  return { id: fromGuid(d.recordId), employeeId, date: d.date, checkIn: d.checkIn, checkOut: d.checkOut, edited: d.edited };
}

/** Thay bản ghi của các nhân viên trong khoảng ngày bằng bản server (ngày không còn bản ghi thì bỏ) */
function putRecords(employeeIds: string[], from: string, to: string, records: TimeRecord[]) {
  const ids = new Set(employeeIds);
  useHrStore.setState((s) => ({
    records: [...s.records.filter((r) => !(ids.has(r.employeeId) && r.date >= from && r.date <= to)), ...records],
  }));
}

function setLeaveBalance(employeeId: string, leaveBalance: number) {
  useHrStore.setState((s) => ({ employees: s.employees.map((e) => (e.id === employeeId ? { ...e, leaveBalance } : e)) }));
}

function toRequest(r: ServerRequest): HrRequest {
  return {
    id: fromGuid(r.id),
    code: r.code,
    type: r.type,
    employeeId: fromGuid(r.employeeId),
    from: r.from,
    to: r.to,
    days: r.days,
    paidLeave: r.paidLeave,
    swap: r.swap ? { myShiftId: fromGuid(r.swap.myShiftId), withEmployeeId: fromGuid(r.swap.withEmployeeId), theirShiftId: fromGuid(r.swap.theirShiftId) } : undefined,
    amount: r.amount,
    title: r.title,
    reason: r.reason,
    status: r.status,
    steps: r.steps.map((s) => ({
      role: s.role,
      approver: s.approver,
      approverId: s.approverId ? fromGuid(s.approverId) : undefined,
      decision: s.decision,
      at: s.at,
      comment: s.comment,
      onBehalf: s.onBehalf || undefined,
    })),
    createdAt: r.createdAt,
    canDecide: r.canDecide,
    canCancel: r.canCancel,
  };
}

const upsertRequests = (items: ServerRequest[]) =>
  useHrStore.setState((s) => ({ requests: replaceBy(s.requests, items.map(toRequest), (r) => r.id) }));

function toJob(j: ServerJob): Job {
  return {
    id: fromGuid(j.id),
    title: j.title,
    branchId: fromGuid(j.branchId),
    department: j.department,
    kind: j.kind,
    quantity: j.quantity,
    salaryRange: j.salaryRange,
    status: j.status,
    postedAt: j.postedAt,
    deadline: j.deadline,
    channels: j.channels,
    description: j.description,
  };
}

function toCandidate(c: ServerCandidate): Candidate {
  return {
    id: fromGuid(c.id),
    jobId: fromGuid(c.jobId),
    fullName: c.fullName,
    phone: c.phone,
    email: c.email,
    source: c.source,
    stage: c.stage,
    appliedAt: c.appliedAt,
    cvName: c.cvName,
    hasCv: c.hasCv,
    japanese: c.japanese,
    expectedSalary: c.expectedSalary,
    interview: c.interview ? { ...c.interview, interviewerId: fromGuid(c.interview.interviewerId) } : undefined,
    offer: c.offer,
    rejectReason: c.rejectReason,
    notes: c.notes,
  };
}

function toOnboarding(o: ServerOnboarding): Onboarding {
  return {
    id: fromGuid(o.id),
    candidateId: o.candidateId ? fromGuid(o.candidateId) : undefined,
    fullName: o.fullName,
    phone: o.phone,
    email: o.email,
    position: o.position,
    department: o.department,
    kind: o.kind,
    branchId: fromGuid(o.branchId),
    startDate: o.startDate,
    mentor: o.mentor,
    mentorId: fromGuid(o.mentorId),
    items: o.items,
    employeeId: o.employeeId ? fromGuid(o.employeeId) : undefined,
    createdAt: o.createdAt,
  };
}

const upsertJob = (j: ServerJob) => useHrStore.setState((s) => ({ jobs: replaceBy(s.jobs, [toJob(j)], (x) => x.id) }));
const upsertCandidate = (c: ServerCandidate) => useHrStore.setState((s) => ({ candidates: replaceBy(s.candidates, [toCandidate(c)], (x) => x.id) }));
const upsertOnboarding = (o: ServerOnboarding) => {
  const ob = toOnboarding(o);
  useHrStore.setState((s) => ({ onboardings: replaceBy(s.onboardings, [ob], (x) => x.id) }));
  return ob;
};

function absorbCandidate(r: CandidateResult) {
  upsertJob(r.job);
  upsertCandidate(r.candidate);
  return r;
}

const ignore403 = async <T,>(p: Promise<T>): Promise<T | undefined> => {
  try {
    return await p;
  } catch (e) {
    if (e instanceof ApiError && (e.status === 403 || e.status === 404)) return undefined;
    throw e;
  }
};

// ---------- Chấm công (CC-01 → CC-04) ----------

/** CC-02 — bảng công tháng của tôi ("YYYY-MM") */
export async function loadMyTimesheet(month: string) {
  const r = await api<{ employee: ServerTimesheetEmployee; days: ServerDayDto[]; leaveBalance: number }>(`/hr/me/timesheet?month=${month}`);
  const me = fromGuid(r.employee.id);
  const records = r.days.map((d) => absorbDay(me, d)).filter((x): x is TimeRecord => Boolean(x));
  setLeaveBalance(me, r.leaveBalance);
  putRecords([me], r.days[0]?.date ?? '', r.days[r.days.length - 1]?.date ?? '', records);
}

/** CC-03 — lưới toàn bộ nhân viên trong phạm vi (403 → bỏ qua) */
export async function loadTimesheets(month: string) {
  const r = await ignore403(api<{ rows: { employee: ServerTimesheetEmployee; days: ServerDayDto[] }[] }>(`/hr/timesheets?month=${month}`));
  if (!r) return;
  const records: TimeRecord[] = [];
  const ids: string[] = [];
  for (const row of r.rows) {
    const id = fromGuid(row.employee.id);
    ids.push(id);
    records.push(...row.days.map((d) => absorbDay(id, d)).filter((x): x is TimeRecord => Boolean(x)));
  }
  const first = r.rows[0]?.days[0]?.date;
  const last = r.rows[0]?.days[r.rows[0].days.length - 1]?.date;
  if (first && last) putRecords(ids, first, last, records);
}

/** CC-01 — check-in / check-out bằng giờ server. Trả giờ + cảnh báo (muộn / về sớm). */
export async function punch(kind: 'in' | 'out'): Promise<{ time?: string; warning?: string }> {
  const r = await api<PunchResult>(`/hr/me/check-${kind}`, { method: 'POST' });
  const rec = toRecord(r.record);
  absorbDay(rec.employeeId, r.today);
  putRecords([rec.employeeId], rec.date, rec.date, [rec]);
  return { time: kind === 'in' ? rec.checkIn : rec.checkOut, warning: r.warning };
}

/** CC-04 — sửa công (lý do bắt buộc) */
export async function editRecord(employeeId: string, date: string, v: { checkIn?: string; checkOut?: string }, reason: string) {
  const d = await api<ServerDayDto>(`/hr/timesheets/${toGuid(employeeId)}/${date}`, {
    method: 'PUT',
    body: { checkIn: v.checkIn ?? null, checkOut: v.checkOut ?? null, reason },
  });
  const rec = absorbDay(employeeId, d);
  putRecords([employeeId], date, date, rec ? [rec] : []);
}

// ---------- Đơn từ (DN-01 → DN-04) ----------

/** Đơn chờ tôi duyệt + đơn của tôi + tất cả trong phạm vi (không có hr.requests.view → bỏ tab "all") */
export async function loadRequests() {
  const page = (tab: string) => api<{ items: ServerRequest[] }>(`/hr/requests?tab=${tab}&pageSize=100`);
  const [todo, mine, all] = await Promise.all([page('todo'), api<{ items: ServerRequest[] }>('/hr/me/requests?pageSize=100'), ignore403(page('all'))]);
  const items = [...(all?.items ?? []), ...mine.items, ...todo.items];
  useHrStore.setState({ requests: replaceBy([], items.map(toRequest), (r) => r.id) });
}

export interface RequestInput {
  /** Bỏ trống / chính mình = đơn của tôi; người khác = tạo hộ (hr.requests.create) */
  employeeId?: string;
  type: RequestType;
  from?: string;
  to?: string;
  paidLeave?: boolean;
  amount?: number;
  title?: string;
  swap?: { myShiftId: string; withEmployeeId: string; theirShiftId: string };
  reason: string;
}

/** DN-01 — gửi đơn; server dựng cấp duyệt + thông báo người duyệt */
export async function createRequest(v: RequestInput, meId?: string): Promise<HrRequest> {
  const onBehalf = v.employeeId && v.employeeId !== meId;
  const body = {
    type: v.type,
    from: v.from ?? null,
    to: v.to ?? null,
    paidLeave: v.paidLeave ?? null,
    amount: v.amount ?? null,
    title: v.title ?? null,
    swap: v.swap ? { myShiftId: toGuid(v.swap.myShiftId), withEmployeeId: toGuid(v.swap.withEmployeeId), theirShiftId: toGuid(v.swap.theirShiftId) } : null,
    reason: v.reason,
    employeeId: onBehalf ? toGuid(v.employeeId!) : null,
  };
  const r = await api<ServerRequest>(onBehalf ? '/hr/requests' : '/hr/me/requests', { body });
  upsertRequests([r]);
  return toRequest(r);
}

/** DN-02 — duyệt / từ chối bước hiện tại (bước cuối duyệt → server trừ phép / đổi ca) */
export async function decideRequest(id: string, decision: 'approved' | 'rejected', comment?: string) {
  const r = await api<ServerRequest>(`/hr/requests/${toGuid(id)}/${decision === 'approved' ? 'approve' : 'reject'}`, {
    body: decision === 'approved' ? { comment: comment ?? null } : { reason: comment ?? '' },
  });
  upsertRequests([r]);
  const req = toRequest(r);
  // Đã duyệt nghỉ có lương → phép năm người gửi giảm (server đã trừ)
  if (req.status === 'approved' && req.type === 'leave' && req.paidLeave && req.days)
    useHrStore.setState((s) => ({ employees: s.employees.map((e) => (e.id === req.employeeId ? { ...e, leaveBalance: Math.max(0, e.leaveBalance - req.days!) } : e)) }));
  // Đổi ca: Work đổi người ở nền → tải lại lịch ca của tôi sau chốc lát
  if (req.status === 'approved' && req.type === 'shift_swap')
    setTimeout(() => void import('@/features/work/workApi').then((m) => m.loadMyShifts()).catch(() => undefined), 1500);
  return req;
}

/** DN-03 — người gửi hủy đơn còn chờ */
export async function cancelRequest(id: string) {
  upsertRequests([await api<ServerRequest>(`/hr/me/requests/${toGuid(id)}/cancel`, { method: 'POST' })]);
}

// ---------- Tuyển dụng (TD-01 → TD-04) ----------

export async function loadRecruitment() {
  const [jobs, candidates] = await Promise.all([api<ServerJob[]>('/hr/jobs'), api<ServerCandidate[]>('/hr/candidates')]);
  useHrStore.setState({ jobs: jobs.map(toJob), candidates: candidates.map(toCandidate) });
}

export type JobInput = Pick<Job, 'title' | 'branchId' | 'department' | 'kind' | 'quantity' | 'salaryRange' | 'deadline' | 'channels' | 'description'>;

/** TD-01 — đăng / sửa tin; `status` khác hiện tại → gọi thêm tạm dừng / mở lại / đóng */
export async function saveJob(id: string | undefined, v: JobInput, status?: Job['status']) {
  const body = { ...v, branchId: toGuid(v.branchId), salaryRange: v.salaryRange ?? null, description: v.description ?? null, channels: v.channels ?? [] };
  let job = await api<ServerJob>(id ? `/hr/jobs/${toGuid(id)}` : '/hr/jobs', { method: id ? 'PUT' : 'POST', body });
  if (status && status !== job.status) {
    const action = status === 'open' ? 'reopen' : status === 'paused' ? 'pause' : 'close';
    job = await api<ServerJob>(`/hr/jobs/${job.id}/${action}`, { method: 'POST' });
  }
  upsertJob(job);
  return toJob(job);
}

export async function deleteJob(id: string) {
  await api(`/hr/jobs/${toGuid(id)}`, { method: 'DELETE' });
  useHrStore.setState((s) => ({ jobs: s.jobs.filter((j) => j.id !== id) }));
}

export type CandidateInput = Pick<Candidate, 'jobId' | 'fullName' | 'phone' | 'email' | 'source' | 'japanese' | 'expectedSalary'>;

/** TD-02 — thêm ứng viên (kèm file CV nếu có) */
export async function addCandidate(v: CandidateInput, cv?: File) {
  const r = absorbCandidate(
    await api<CandidateResult>('/hr/candidates', {
      body: { ...v, jobId: toGuid(v.jobId), japanese: v.japanese ?? null, expectedSalary: v.expectedSalary ?? null },
    }),
  );
  if (cv) await uploadCv(fromGuid(r.candidate.id), cv);
}

export async function uploadCv(candidateId: string, file: File) {
  const form = new FormData();
  form.append('file', file);
  absorbCandidate(await api<CandidateResult>(`/hr/candidates/${toGuid(candidateId)}/cv`, { body: form }));
}

/** Link tạm mở CV */
export const cvLink = (candidateId: string) => api<{ url: string; name: string }>(`/hr/candidates/${toGuid(candidateId)}/cv/link`);

/** TD-02 — kéo sang Ứng tuyển / Sàng lọc, hoặc lùi về giai đoạn đã có thông tin */
export const moveCandidate = async (id: string, stage: CandidateStage) =>
  void absorbCandidate(await api<CandidateResult>(`/hr/candidates/${toGuid(id)}/stage`, { body: { stage } }));

/** TD-03 — hẹn / sửa phỏng vấn (người phỏng vấn là nhân sự) */
export const scheduleInterview = async (id: string, v: { at: string; interviewerId: string; mode: 'offline' | 'online'; score?: number; note?: string }) =>
  void absorbCandidate(
    await api<CandidateResult>(`/hr/candidates/${toGuid(id)}/interview`, {
      body: { at: v.at, interviewerId: toGuid(v.interviewerId), mode: v.mode, score: v.score ?? null, note: v.note ?? null },
    }),
  );

export const sendOffer = async (id: string, salary: number, startDate: string) =>
  void absorbCandidate(await api<CandidateResult>(`/hr/candidates/${toGuid(id)}/offer`, { body: { salary, startDate } }));

export const declineOffer = async (id: string) =>
  void absorbCandidate(await api<CandidateResult>(`/hr/candidates/${toGuid(id)}/offer-declined`, { method: 'POST' }));

export const rejectCandidate = async (id: string, reason: string) =>
  void absorbCandidate(await api<CandidateResult>(`/hr/candidates/${toGuid(id)}/reject`, { body: { reason } }));

export const noteCandidate = async (id: string, text: string) =>
  void absorbCandidate(await api<CandidateResult>(`/hr/candidates/${toGuid(id)}/notes`, { body: { text } }));

/** TD-04 — nhận việc: server tạo phiếu onboarding, đủ người → tự đóng tin. Trả cảnh báo (vd "tự đóng tin"). */
export async function hireCandidate(id: string): Promise<string[]> {
  const r = absorbCandidate(await api<CandidateResult>(`/hr/candidates/${toGuid(id)}/hire`, { method: 'POST' }));
  await loadOnboardings().catch(() => undefined);
  return r.warnings;
}

// ---------- Onboarding (OB-01, OB-02) ----------

export async function loadOnboardings() {
  useHrStore.setState({ onboardings: (await api<ServerOnboarding[]>('/hr/onboardings')).map(toOnboarding) });
}

export type OnboardingInput = Pick<Onboarding, 'fullName' | 'phone' | 'email' | 'position' | 'department' | 'kind' | 'branchId' | 'startDate'> & { mentorId: string };

export const createOnboarding = async (v: OnboardingInput) =>
  upsertOnboarding(await api<ServerOnboarding>('/hr/onboardings', { body: { ...v, branchId: toGuid(v.branchId), mentorId: toGuid(v.mentorId) } }));

/** Tick / bỏ tick 1 mục (lạc quan ở trang — lỗi thì tải lại) */
export const tickOnboarding = async (id: string, key: string, done: boolean) =>
  upsertOnboarding(await api<ServerOnboarding>(`/hr/onboardings/${toGuid(id)}/items/${key}`, { method: 'PUT', body: { done } }));

/** OB-02 — tạo hồ sơ từ phiếu (server gọi lại NS-03) rồi tải hồ sơ mới vào danh sách nhân sự */
export async function createEmployeeFromOnboarding(
  id: string,
  v: { gender: 'male' | 'female'; teacherType?: 'vn' | 'jp'; levels?: string[]; rateOffline?: number; rateOnline?: number },
) {
  const ob = upsertOnboarding(
    await api<ServerOnboarding>(`/hr/onboardings/${toGuid(id)}/employee`, {
      body: { gender: v.gender, teacherType: v.teacherType ?? null, levels: v.levels ?? null, rateOffline: v.rateOffline ?? null, rateOnline: v.rateOnline ?? null },
    }),
  );
  if (ob.employeeId) {
    const m = await import('./hrApi');
    return m.loadEmployee(ob.employeeId, ob.kind);
  }
  return undefined;
}

// ---------- Thay hàm của store (chế độ API) ----------

if (USE_API) {
  useHrStore.setState({
    records: [],
    requests: [],
    jobs: [],
    candidates: [],
    onboardings: [],
    // Duyệt / từ chối, hủy đơn: gọi từ nhiều trang (Đơn từ, Hộp duyệt chung) → chạy nền, lỗi thì báo
    decide: (id, _approver, decision, comment) => void decideRequest(id, decision, comment).catch(reportError),
    cancelRequest: (id) => void cancelRequest(id).catch(reportError),
  });
}

/** Nạp sau đăng nhập — theo quyền xem trang */
export async function loadHrOps(canView: (page: string) => boolean) {
  const quiet = (p: Promise<unknown>) => p.catch(() => undefined);
  await Promise.all([
    quiet(loadRequests()),
    canView('hr.recruitment') ? quiet(loadRecruitment()) : Promise.resolve(),
    canView('hr.onboarding') ? quiet(loadOnboardings()) : Promise.resolve(),
  ]);
}
