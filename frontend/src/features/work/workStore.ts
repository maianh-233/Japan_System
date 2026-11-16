import { create } from 'zustand';
import type { ShiftKey } from '@/features/hr/types';
import { MOCK_HANDOVERS, MOCK_SHIFT_AVAILABILITY, MOCK_SHIFT_PREFS, MOCK_SHIFTS, MOCK_TASKS } from '@/mocks/data/work';
import type { Handover, ShiftAssignment, ShiftAvailability, ShiftAvailabilityStatus, ShiftPreference, Task } from './types';

/** Store CÔNG VIỆC giả lập: task, xếp ca, bàn giao ca. */

interface WorkState {
  tasks: Task[];
  shifts: ShiftAssignment[];
  prefs: ShiftPreference[];
  availability: ShiftAvailability[];
  handovers: Handover[];

  addTask: (t: Omit<Task, 'id' | 'code' | 'createdAt' | 'comments' | 'checklist'> & { checklist?: Task['checklist'] }) => Task;
  updateTask: (id: string, patch: Partial<Task>) => void;
  commentTask: (id: string, by: string, text: string) => void;

  toggleShift: (employeeId: string, date: string, shift: ShiftKey) => void;
  setAvailability: (employeeId: string, date: string, shift: ShiftKey, status?: ShiftAvailabilityStatus) => void;
  /** Áp dụng ca đăng ký của nhân viên cho các ngày */
  applyPrefs: (dates: string[], employeeIds: string[]) => number;
  /** Chép lịch tuần trước sang tuần này (chỉ ô còn trống) */
  copyWeek: (fromDates: string[], toDates: string[], employeeIds: string[]) => number;
  /** Đổi ca giữa 2 người (duyệt đơn đổi ca) */
  swapShifts: (aId: string, bId: string) => void;

  addHandover: (h: Omit<Handover, 'id' | 'createdAt' | 'status'>) => Handover;
  updateHandover: (id: string, patch: Partial<Handover>) => void;
}

let seq = 200;
const now = () => new Date().toISOString();
const isBusy = (availability: ShiftAvailability[], employeeId: string, date: string, shift: ShiftKey) =>
  availability.some((item) => item.employeeId === employeeId && item.date === date && item.shift === shift && item.status === 'busy');

export const useWorkStore = create<WorkState>((set, get) => ({
  tasks: MOCK_TASKS,
  shifts: MOCK_SHIFTS,
  prefs: MOCK_SHIFT_PREFS,
  availability: MOCK_SHIFT_AVAILABILITY,
  handovers: MOCK_HANDOVERS,

  addTask: (t) => {
    const task: Task = { ...t, id: `TK-${Date.now()}`, code: `TK-0${++seq}`, createdAt: now(), comments: [], checklist: t.checklist ?? [] };
    set((s) => ({ tasks: [task, ...s.tasks] }));
    return task;
  },
  updateTask: (id, patch) =>
    set((s) => ({
      tasks: s.tasks.map((t) => (t.id === id ? { ...t, ...patch, completedAt: patch.status ? (patch.status === 'done' ? now() : undefined) : t.completedAt } : t)),
    })),
  commentTask: (id, by, text) => set((s) => ({ tasks: s.tasks.map((t) => (t.id === id ? { ...t, comments: [...t.comments, { at: now(), by, text }] } : t)) })),

  toggleShift: (employeeId, date, shift) =>
    set((s) => {
      const has = s.shifts.find((x) => x.employeeId === employeeId && x.date === date && x.shift === shift);
      if (!has && isBusy(s.availability, employeeId, date, shift)) return s;
      return { shifts: has ? s.shifts.filter((x) => x !== has) : [...s.shifts, { id: `SH-${Date.now()}-${Math.random().toString(36).slice(2, 6)}`, employeeId, date, shift }] };
    }),

  setAvailability: (employeeId, date, shift, status) =>
    set((state) => {
      const current = state.availability.find((item) => item.employeeId === employeeId && item.date === date && item.shift === shift);
      const availability = status
        ? current
          ? state.availability.map((item) => (item === current ? { ...item, status } : item))
          : [...state.availability, { employeeId, date, shift, status }]
        : state.availability.filter((item) => !(item.employeeId === employeeId && item.date === date && item.shift === shift));
      const prefs = status === 'available'
        ? state.prefs.some((item) => item.employeeId === employeeId && item.date === date && item.shift === shift)
          ? state.prefs
          : [...state.prefs, { employeeId, date, shift }]
        : state.prefs.filter((item) => !(item.employeeId === employeeId && item.date === date && item.shift === shift));
      return { availability, prefs };
    }),

  applyPrefs: (dates, employeeIds) => {
    const add = get().prefs.filter(
      (p) =>
        dates.includes(p.date) &&
        employeeIds.includes(p.employeeId) &&
        !get().shifts.some((x) => x.employeeId === p.employeeId && x.date === p.date && x.shift === p.shift) &&
        !isBusy(get().availability, p.employeeId, p.date, p.shift),
    );
    set((s) => ({ shifts: [...s.shifts, ...add.map((p, i) => ({ id: `SH-p${Date.now()}-${i}`, ...p }))] }));
    return add.length;
  },

  copyWeek: (fromDates, toDates, employeeIds) => {
    const add: ShiftAssignment[] = [];
    fromDates.forEach((fd, i) => {
      const td = toDates[i];
      get()
        .shifts.filter((x) => x.date === fd && employeeIds.includes(x.employeeId))
        .forEach((x) => {
          if (
            !get().shifts.some((y) => y.employeeId === x.employeeId && y.date === td && y.shift === x.shift) &&
            !isBusy(get().availability, x.employeeId, td, x.shift)
          ) {
            add.push({ id: `SH-c${Date.now()}-${add.length}`, employeeId: x.employeeId, date: td, shift: x.shift });
          }
        });
    });
    set((s) => ({ shifts: [...s.shifts, ...add] }));
    return add.length;
  },

  swapShifts: (aId, bId) =>
    set((s) => {
      const a = s.shifts.find((x) => x.id === aId);
      const b = s.shifts.find((x) => x.id === bId);
      if (!a || !b) return s;
      return { shifts: s.shifts.map((x) => (x.id === aId ? { ...x, employeeId: b.employeeId } : x.id === bId ? { ...x, employeeId: a.employeeId } : x)) };
    }),

  addHandover: (h) => {
    const item: Handover = { ...h, id: `HO-${Date.now()}`, createdAt: now(), status: 'pending' };
    set((s) => ({ handovers: [item, ...s.handovers] }));
    return item;
  },
  updateHandover: (id, patch) => set((s) => ({ handovers: s.handovers.map((h) => (h.id === id ? { ...h, ...patch } : h)) })),
}));
