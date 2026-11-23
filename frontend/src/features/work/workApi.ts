/**
 * Nối Công việc với backend thật (user story 14, giai đoạn 21):
 *   task        /api/v1/work/tasks · /work/me/tasks · /work/team/{reports|tasks}     (work.tasks.* / người dính tới task / cấp dưới)
 *   xếp ca      /work/shifts · /work/me/shift-registrations                          (work.shifts.*; NV theo ca)
 *   bàn giao    /work/handovers                                                     (work.handover.*; người gửi / nhận)
 * Dữ liệu đổ vào useWorkStore (Id mock khi có). Ở chế độ API, file này THAY các hàm thao tác nhanh của store
 * (kéo Kanban, tick checklist, bình luận, bấm ô ca, báo rảnh / bận, tick việc tồn): cập nhật lạc quan → gọi API → lỗi thì hoàn tác.
 */
import dayjs from 'dayjs';
import { USE_API } from '@/lib/api/config';
import { api, ApiError } from '@/lib/api/http';
import { replaceBy } from '@/lib/api/merge';
import { fromGuid, fromGuids, toGuid, toGuids } from '@/lib/api/ids';
import { optimistic, reportError, reportInfo } from '@/lib/api/report';
import type { ShiftKey } from '@/features/hr/types';
import type { Handover, ShiftAssignment, ShiftAvailability, ShiftAvailabilityStatus, Task, TaskStatus } from './types';
import { useWorkStore } from './workStore';

// ---------- Kiểu server ----------

interface ServerTask {
  id: string;
  code: string;
  title: string;
  description?: string;
  assigneeId: string;
  creatorId: string;
  watcherIds: string[];
  priority: Task['priority'];
  status: TaskStatus;
  due?: string;
  repeat: Task['repeat'];
  link?: { type: 'lead' | 'class' | 'student'; id: string; label: string };
  checklist: { id: string; text: string; done: boolean }[];
  comments: { at: string; by: string; byId: string; text: string }[];
  createdAt: string;
  completedAt?: string;
  canEdit: boolean;
  canDelete: boolean;
  canMove: boolean;
}

interface ServerShift {
  id: string;
  employeeId: string;
  date: string;
  shift: ShiftKey;
}

interface ServerAvailability {
  employeeId: string;
  date: string;
  shift: ShiftKey;
  status: ShiftAvailabilityStatus;
}

interface ServerHandover {
  id: string;
  branchId: string;
  date: string;
  fromShift: ShiftKey;
  fromId: string;
  toId: string;
  cash: { expected: number; counted: number; receivedCount?: number; note?: string };
  leads: { leadId: string; note: string }[];
  pending: { id: string; text: string; done: boolean }[];
  issues?: string;
  status: Handover['status'];
  createdAt: string;
  receivedAt?: string;
  receiverNote?: string;
}

export interface HandoverDraft {
  date: string;
  shift: ShiftKey;
  myShifts: ShiftKey[];
  handedShifts: ShiftKey[];
  openingFloat: number;
  cashCollected: number;
  cashExpected: number;
  receiverId?: string;
  receivers: { id: string; fullName: string; shiftsToday: ShiftKey[] }[];
  leads: { id: string; code: string; fullName: string; phone: string; status: string }[];
}

// ---------- Đổi kiểu ----------

function toTask(t: ServerTask): Task {
  return {
    id: fromGuid(t.id),
    code: t.code,
    title: t.title,
    description: t.description,
    assigneeId: fromGuid(t.assigneeId),
    creatorId: fromGuid(t.creatorId),
    watcherIds: fromGuids(t.watcherIds),
    priority: t.priority,
    status: t.status,
    due: t.due,
    repeat: t.repeat,
    link: t.link ? { ...t.link, id: fromGuid(t.link.id) } : undefined,
    checklist: t.checklist.map((c) => ({ ...c, id: fromGuid(c.id) })),
    comments: t.comments.map((c) => ({ at: c.at, by: c.by, text: c.text })),
    createdAt: t.createdAt,
    completedAt: t.completedAt,
    canEdit: t.canEdit,
    canDelete: t.canDelete,
    canMove: t.canMove,
  };
}

const upsertTasks = (items: ServerTask[]) => useWorkStore.setState((s) => ({ tasks: replaceBy(s.tasks, items.map(toTask), (t) => t.id) }));

const toShift = (s: ServerShift): ShiftAssignment => ({ id: fromGuid(s.id), employeeId: fromGuid(s.employeeId), date: s.date, shift: s.shift });
const toAvailability = (a: ServerAvailability): ShiftAvailability => ({ ...a, employeeId: fromGuid(a.employeeId) });

function toHandover(h: ServerHandover): Handover {
  return {
    id: fromGuid(h.id),
    branchId: fromGuid(h.branchId),
    date: h.date,
    fromShift: h.fromShift,
    fromId: fromGuid(h.fromId),
    toId: fromGuid(h.toId),
    cash: h.cash,
    leads: h.leads.map((l) => ({ leadId: fromGuid(l.leadId), note: l.note })),
    pending: h.pending.map((p) => ({ id: p.id, text: p.text, done: p.done })),
    issues: h.issues,
    status: h.status,
    createdAt: h.createdAt,
    receivedAt: h.receivedAt,
    receiverNote: h.receiverNote,
  };
}

const upsertHandovers = (items: ServerHandover[]) =>
  useWorkStore.setState((s) => ({ handovers: replaceBy(s.handovers, items.map(toHandover), (h) => h.id) }));

/** Ghi ca + rảnh / bận của các NV trong khoảng ngày (thay bản cũ cùng phạm vi); `prefs` = các ô báo rảnh */
function putShifts(employeeIds: string[], from: string, to: string, shifts: ShiftAssignment[], availability: ShiftAvailability[]) {
  const ids = new Set(employeeIds);
  const inRange = (x: { employeeId: string; date: string }) => ids.has(x.employeeId) && x.date >= from && x.date <= to;
  useWorkStore.setState((s) => {
    const nextAvailability = [...s.availability.filter((x) => !inRange(x)), ...availability];
    return {
      shifts: [...s.shifts.filter((x) => !inRange(x)), ...shifts],
      availability: nextAvailability,
      prefs: nextAvailability.filter((a) => a.status === 'available').map(({ employeeId, date, shift }) => ({ employeeId, date, shift })),
    };
  });
}

// ---------- Task (CV-01 → CV-06) ----------

/** Task toàn hệ thống (nếu có quyền) + việc của tôi + task tôi giao / theo dõi + task cấp dưới */
export async function loadTasks(canViewAll: boolean) {
  const list = (url: string) => api<{ items: ServerTask[] }>(url).then((r) => r.items).catch(() => [] as ServerTask[]);
  const parts = await Promise.all([
    canViewAll ? list('/work/tasks?scope=all') : Promise.resolve([]),
    list('/work/me/tasks'),
    list('/work/tasks?scope=assigned'),
    list('/work/tasks?scope=watching'),
    list('/work/team/tasks'),
  ]);
  useWorkStore.setState({ tasks: replaceBy([], parts.flat().map(toTask), (t) => t.id) });
}

export interface TaskInput {
  title: string;
  description?: string;
  assigneeId: string;
  watcherIds?: string[];
  priority: Task['priority'];
  due?: string;
  repeat: Task['repeat'];
  link?: { type: 'lead' | 'class' | 'student'; id: string };
  checklist?: string[];
}

const taskBody = (v: TaskInput) => ({
  title: v.title,
  description: v.description ?? null,
  assigneeId: toGuid(v.assigneeId),
  watcherIds: toGuids(v.watcherIds ?? []),
  priority: v.priority,
  due: v.due ?? null,
  repeat: v.repeat,
  link: v.link ? { type: v.link.type, id: toGuid(v.link.id) } : null,
  checklist: v.checklist ?? null,
});

/** CV-01 — giao task (Task toàn hệ thống, work.tasks.create) */
export async function createTask(v: TaskInput) {
  const t = await api<ServerTask>('/work/tasks', { body: taskBody(v) });
  upsertTasks([t]);
  return toTask(t);
}

/** CV-04 — giao việc cấp dưới: nhiều người → mỗi người 1 task */
export async function createTeamTasks(assigneeIds: string[], v: Omit<TaskInput, 'assigneeId'>) {
  const body: Partial<ReturnType<typeof taskBody>> = taskBody({ ...v, assigneeId: assigneeIds[0] });
  delete body.assigneeId;
  const list = await api<ServerTask[]>('/work/team/tasks', { body: { ...body, assigneeIds: toGuids(assigneeIds) } });
  upsertTasks(list);
  return list.map(toTask);
}

export async function deleteTask(id: string) {
  await api(`/work/tasks/${toGuid(id)}`, { method: 'DELETE' });
  useWorkStore.setState((s) => ({ tasks: s.tasks.filter((t) => t.id !== id) }));
}

/** Đổi trạng thái (kèm bình luận tùy chọn — vd lý do "Yêu cầu làm lại"); server sinh lần lặp kế tiếp */
export async function moveTask(id: string, status: TaskStatus, comment?: string) {
  const r = await api<{ task: ServerTask; next?: ServerTask }>(`/work/tasks/${toGuid(id)}/status`, { body: { status, comment: comment ?? null } });
  upsertTasks(r.next ? [r.task, r.next] : [r.task]);
  return r.next ? toTask(r.next) : undefined;
}

const FIELD_KEYS = ['title', 'description', 'assigneeId', 'watcherIds', 'priority', 'due', 'repeat', 'link'] as const;

/** Thay `updateTask(id, patch)` của store: trạng thái → /status; checklist → thêm / tick / xóa từng mục; trường khác → PUT */
async function syncTask(before: Task, patch: Partial<Task>) {
  const id = toGuid(before.id);
  const after: Task = { ...before, ...patch };
  if (FIELD_KEYS.some((k) => k in patch)) {
    upsertTasks([
      await api<ServerTask>(`/work/tasks/${id}`, {
        method: 'PUT',
        body: taskBody({
          title: after.title,
          description: after.description,
          assigneeId: after.assigneeId,
          watcherIds: after.watcherIds,
          priority: after.priority,
          due: after.due,
          repeat: after.repeat,
          link: after.link ? { type: after.link.type, id: after.link.id } : undefined,
        }),
      }),
    ]);
  }
  if (patch.checklist) {
    const old = new Map(before.checklist.map((c) => [c.id, c]));
    const now = new Map(patch.checklist.map((c) => [c.id, c]));
    for (const c of patch.checklist) {
      const prev = old.get(c.id);
      if (!prev) upsertTasks([await api<ServerTask>(`/work/tasks/${id}/checklist`, { body: { text: c.text } })]);
      else if (prev.done !== c.done)
        upsertTasks([await api<ServerTask>(`/work/tasks/${id}/checklist/${toGuid(c.id)}`, { method: 'PUT', body: { done: c.done } })]);
    }
    for (const c of before.checklist)
      if (!now.has(c.id)) upsertTasks([await api<ServerTask>(`/work/tasks/${id}/checklist/${toGuid(c.id)}`, { method: 'DELETE' })]);
  }
  if (patch.status && patch.status !== before.status) {
    const next = await moveTask(before.id, patch.status);
    if (next) reportInfo(`Đã tạo lần lặp tiếp theo — hạn ${dayjs(next.due).format('DD/MM')}`);
  }
}

// ---------- Xếp ca (XC-01 → XC-05) ----------

export interface ShiftWeek {
  canEdit: boolean;
  coverage: { date: string; shift: ShiftKey; count: number; minimum: number; short: boolean }[];
}

/** XC-01 — lưới tuần của 1 chi nhánh (Thứ 2 = `week`) → ca + rảnh / bận của NV chi nhánh vào store */
export async function loadShiftWeek(branchId: string, week: string): Promise<ShiftWeek | undefined> {
  let r: { employees: { id: string }[]; assignments: ServerShift[]; registrations: ServerAvailability[]; canEdit: boolean; coverage: ShiftWeek['coverage'] };
  try {
    r = await api(`/work/shifts?branchId=${toGuid(branchId)}&week=${week}`);
  } catch (e) {
    if (e instanceof ApiError && e.status === 403) return undefined;
    throw e;
  }
  const to = dayjs(week).add(6, 'day').format('YYYY-MM-DD');
  putShifts(fromGuids(r.employees.map((e) => e.id)), week, to, r.assignments.map(toShift), r.registrations.map(toAvailability));
  return { canEdit: r.canEdit, coverage: r.coverage };
}

/** XC-02 — xếp theo đăng ký / chép tuần trước (1 request). Trả số ca thêm. */
export async function fillWeek(branchId: string, week: string, mode: 'registrations' | 'copy') {
  const added = await api<number>(`/work/shifts/${mode === 'copy' ? 'copy-week' : 'apply-registrations'}`, {
    body: { branchId: toGuid(branchId), week },
  });
  await loadShiftWeek(branchId, week);
  return added;
}

/** XC-04 — lịch 2 tuần của tôi (ca đã xếp + rảnh / bận). Không làm theo ca → bỏ qua. */
export async function loadMyShifts(from?: string) {
  let r: { from: string; to: string; assignments: ServerShift[]; registrations: ServerAvailability[] };
  try {
    r = await api(`/work/me/shift-registrations${from ? `?from=${from}` : ''}`);
  } catch (e) {
    if (e instanceof ApiError && (e.status === 403 || e.status === 404)) return;
    throw e;
  }
  const me = r.assignments[0]?.employeeId ?? r.registrations[0]?.employeeId;
  const { useAuthStore } = await import('@/stores/authStore');
  const meId = me ? fromGuid(me) : useAuthStore.getState().user?.id;
  if (meId) putShifts([meId], r.from, r.to, r.assignments.map(toShift), r.registrations.map(toAvailability));
}

// ---------- Bàn giao ca (BG-01 → BG-03) ----------

export async function loadHandovers() {
  const r = await api<{ pendingForMe: ServerHandover[]; items: ServerHandover[] }>('/work/handovers');
  useWorkStore.setState({ handovers: replaceBy([], [...r.items, ...r.pendingForMe].map(toHandover), (h) => h.id) });
}

/** BG-01 — gợi ý của server: ca của tôi, người nhận ca kế tiếp, tiền mặt hệ thống, lead đang mở */
export async function handoverDraft(shift?: ShiftKey): Promise<HandoverDraft> {
  const d = await api<HandoverDraft>(`/work/handovers/draft${shift ? `?shift=${shift}` : ''}`);
  return {
    ...d,
    receiverId: d.receiverId ? fromGuid(d.receiverId) : undefined,
    receivers: d.receivers.map((r) => ({ ...r, id: fromGuid(r.id) })),
    leads: d.leads.map((l) => ({ ...l, id: fromGuid(l.id) })),
  };
}

export interface HandoverInput {
  shift: ShiftKey;
  toId: string;
  cashCounted: number;
  cashNote?: string;
  issues?: string;
  leads?: { leadId: string; note: string }[];
  pending?: string[];
}

/** BG-01 — tạo bàn giao (tiền mặt hệ thống do server tính) */
export async function createHandover(v: HandoverInput) {
  const h = await api<ServerHandover>('/work/handovers', {
    body: {
      shift: v.shift,
      toId: toGuid(v.toId),
      cashCounted: v.cashCounted,
      cashNote: v.cashNote ?? null,
      issues: v.issues ?? null,
      leads: (v.leads ?? []).map((l) => ({ leadId: toGuid(l.leadId), note: l.note })),
      pending: v.pending ?? [],
    },
  });
  upsertHandovers([h]);
  return toHandover(h);
}

/** BG-02 — người nhận đếm lại tiền → Đã nhận / Lệch tiền */
export async function receiveHandover(id: string, counted: number, note?: string) {
  const h = await api<ServerHandover>(`/work/handovers/${toGuid(id)}/receive`, { body: { counted, note: note ?? null } });
  upsertHandovers([h]);
  return toHandover(h);
}

// ---------- Nạp sau đăng nhập ----------

export async function loadWork(canView: (page: string) => boolean) {
  const quiet = (p: Promise<unknown>) => p.catch(() => undefined);
  await Promise.all([quiet(loadTasks(canView('work.tasks'))), quiet(loadHandovers()), quiet(loadMyShifts())]);
}

// ---------- Thay hàm thao tác nhanh của store (chế độ API) ----------

if (USE_API) {
  const local = useWorkStore.getState();
  const findTask = (id: string) => useWorkStore.getState().tasks.find((t) => t.id === id);
  const restoreTask = (t: Task) => useWorkStore.setState((s) => ({ tasks: s.tasks.map((x) => (x.id === t.id ? t : x)) }));

  useWorkStore.setState({
    tasks: [],
    shifts: [],
    prefs: [],
    availability: [],
    handovers: [],

    updateTask: (id, patch) => {
      const before = findTask(id);
      if (!before) return;
      local.updateTask(id, patch);
      void optimistic(() => syncTask(before, patch), () => restoreTask(before));
    },

    commentTask: (id, by, text) => {
      const before = findTask(id);
      if (!before) return;
      local.commentTask(id, by, text);
      void optimistic(
        async () => upsertTasks([await api<ServerTask>(`/work/tasks/${toGuid(id)}/comments`, { body: { text } })]),
        () => restoreTask(before),
      );
    },

    toggleShift: (employeeId, date, shift) => {
      const before = useWorkStore.getState().shifts;
      const had = before.some((x) => x.employeeId === employeeId && x.date === date && x.shift === shift);
      local.toggleShift(employeeId, date, shift);
      void optimistic(
        async () => {
          const r = await api<{ assigned: boolean; assignment?: ServerShift; warning?: string }>('/work/shifts/toggle', {
            body: { employeeId: toGuid(employeeId), date, shift, assign: !had },
          });
          // Thay ca tạm bằng ca server (Id thật — dùng cho đơn đổi ca)
          useWorkStore.setState((s) => ({
            shifts: [
              ...s.shifts.filter((x) => !(x.employeeId === employeeId && x.date === date && x.shift === shift)),
              ...(r.assignment ? [toShift(r.assignment)] : []),
            ],
          }));
          if (r.warning) reportInfo(r.warning);
        },
        () => useWorkStore.setState({ shifts: before }),
      );
    },

    setAvailability: (employeeId, date, shift, status) => {
      const { availability, prefs } = useWorkStore.getState();
      local.setAvailability(employeeId, date, shift, status);
      void optimistic(
        () => api('/work/me/shift-registrations', { method: 'PUT', body: { date, shift, status: status ?? null } }),
        () => useWorkStore.setState({ availability, prefs }),
      );
    },

    swapShifts: () => undefined, // server đổi khi duyệt đơn (XC-05)

    updateHandover: (id, patch) => {
      const before = useWorkStore.getState().handovers.find((h) => h.id === id);
      if (!before || !patch.pending) return;
      local.updateHandover(id, patch);
      const changed = patch.pending.find((p, i) => Boolean(p.done) !== Boolean(before.pending[i]?.done));
      if (!changed?.id) return;
      void optimistic(
        async () =>
          upsertHandovers([
            await api<ServerHandover>(`/work/handovers/${toGuid(id)}/items/${changed.id}`, { method: 'PUT', body: { done: Boolean(changed.done) } }),
          ]),
        () => useWorkStore.setState((s) => ({ handovers: s.handovers.map((h) => (h.id === id ? before : h)) })),
      );
    },

    // Các hàm tạo / xếp hàng loạt trả kết quả đồng bộ — ở chế độ API trang gọi hàm async ở trên
    addTask: (t) => {
      reportError(new Error('Chế độ API: giao task qua workApi.createTask'));
      return { ...t, id: '', code: '', createdAt: '', comments: [], checklist: t.checklist ?? [] };
    },
    applyPrefs: () => 0,
    copyWeek: () => 0,
  });
}
