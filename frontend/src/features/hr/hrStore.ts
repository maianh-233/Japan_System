import dayjs from 'dayjs';
import { create } from 'zustand';
import { MOCK_EMPLOYEES } from '@/mocks/data/employees';
import { MOCK_CANDIDATES, MOCK_JOBS, MOCK_ONBOARDINGS, MOCK_REQUESTS, MOCK_TIME_RECORDS } from '@/mocks/data/hr';
import { useWorkStore } from '@/features/work/workStore';
import type { Candidate, Employee, HrRequest, Job, Onboarding, TimeRecord } from './types';

/** Store NHÂN SỰ giả lập. Khi có backend: thay hàm bằng gọi API, giữ nguyên chữ ký. */

export type EmployeeInput = Omit<Employee, 'id' | 'code'>;

interface HrState {
  employees: Employee[];
  records: TimeRecord[];
  requests: HrRequest[];
  jobs: Job[];
  candidates: Candidate[];
  onboardings: Onboarding[];

  addEmployee: (e: EmployeeInput) => Employee;
  updateEmployee: (id: string, patch: Partial<Employee>) => void;

  /** Check-in / check-out (bấm nút) */
  punch: (employeeId: string) => 'in' | 'out';
  /** Quản lý sửa công */
  editRecord: (employeeId: string, date: string, patch: { checkIn?: string; checkOut?: string }, by: string, reason: string) => void;

  addRequest: (r: Omit<HrRequest, 'id' | 'code' | 'createdAt' | 'status'>) => HrRequest;
  /** Duyệt / từ chối bước hiện tại. Bước cuối duyệt → áp dụng (trừ phép, đổi ca) */
  decide: (id: string, approver: string, decision: 'approved' | 'rejected', comment?: string) => void;
  cancelRequest: (id: string) => void;

  addJob: (j: Omit<Job, 'id' | 'postedAt'>) => void;
  updateJob: (id: string, patch: Partial<Job>) => void;
  addCandidate: (c: Omit<Candidate, 'id' | 'appliedAt' | 'notes'>) => void;
  updateCandidate: (id: string, patch: Partial<Candidate>) => void;
  noteCandidate: (id: string, by: string, text: string) => void;

  addOnboarding: (o: Omit<Onboarding, 'id' | 'createdAt'>) => Onboarding;
  updateOnboarding: (id: string, patch: Partial<Onboarding>) => void;
}

let seq = 300;
const now = () => new Date().toISOString();

export const useHrStore = create<HrState>((set, get) => ({
  employees: MOCK_EMPLOYEES,
  records: MOCK_TIME_RECORDS,
  requests: MOCK_REQUESTS,
  jobs: MOCK_JOBS,
  candidates: MOCK_CANDIDATES,
  onboardings: MOCK_ONBOARDINGS,

  addEmployee: (e) => {
    const n = get().employees.length + 1;
    const emp: Employee = { ...e, id: `e-${Date.now()}`, code: `${e.kind === 'staff' ? 'NV' : e.kind === 'teacher' ? 'GV' : 'TG'}${String(100 + n).padStart(4, '0')}` };
    set((s) => ({ employees: [...s.employees, emp] }));
    return emp;
  },
  updateEmployee: (id, patch) => set((s) => ({ employees: s.employees.map((e) => (e.id === id ? { ...e, ...patch } : e)) })),

  punch: (employeeId) => {
    const date = dayjs().format('YYYY-MM-DD');
    const t = dayjs().format('HH:mm');
    const rec = get().records.find((r) => r.employeeId === employeeId && r.date === date);
    if (!rec) {
      set((s) => ({ records: [...s.records, { id: `TR-${Date.now()}`, employeeId, date, checkIn: t }] }));
      return 'in';
    }
    set((s) => ({ records: s.records.map((r) => (r === rec ? { ...r, checkOut: t } : r)) }));
    return 'out';
  },
  editRecord: (employeeId, date, patch, by, reason) =>
    set((s) => {
      const rec = s.records.find((r) => r.employeeId === employeeId && r.date === date);
      if (rec) return { records: s.records.map((r) => (r === rec ? { ...r, ...patch, edited: { by, reason } } : r)) };
      return { records: [...s.records, { id: `TR-${Date.now()}`, employeeId, date, ...patch, edited: { by, reason } }] };
    }),

  addRequest: (r) => {
    const req: HrRequest = { ...r, id: `RQ-${Date.now()}`, code: `DT-0${++seq}`, createdAt: now(), status: 'pending' };
    set((s) => ({ requests: [req, ...s.requests] }));
    return req;
  },
  decide: (id, approver, decision, comment) => {
    const req = get().requests.find((r) => r.id === id);
    if (!req || req.status !== 'pending') return;
    const idx = req.steps.findIndex((st) => !st.decision);
    const steps = req.steps.map((st, i) => (i === idx ? { ...st, approver: st.approver || approver, decision, at: now(), comment } : st));
    const last = idx === steps.length - 1;
    const status = decision === 'rejected' ? 'rejected' : last ? 'approved' : 'pending';
    set((s) => ({ requests: s.requests.map((r) => (r.id === id ? { ...r, steps, status } : r)) }));
    if (status !== 'approved') return;
    // Áp dụng khi duyệt xong
    if (req.type === 'leave' && req.paidLeave && req.days) {
      set((s) => ({ employees: s.employees.map((e) => (e.id === req.employeeId ? { ...e, leaveBalance: Math.max(0, e.leaveBalance - req.days!) } : e)) }));
    }
    if (req.type === 'shift_swap' && req.swap) useWorkStore.getState().swapShifts(req.swap.myShiftId, req.swap.theirShiftId);
  },
  cancelRequest: (id) => set((s) => ({ requests: s.requests.map((r) => (r.id === id ? { ...r, status: 'cancelled' } : r)) })),

  addJob: (j) => set((s) => ({ jobs: [{ ...j, id: `JB-${Date.now()}`, postedAt: dayjs().format('YYYY-MM-DD') }, ...s.jobs] })),
  updateJob: (id, patch) => set((s) => ({ jobs: s.jobs.map((j) => (j.id === id ? { ...j, ...patch } : j)) })),
  addCandidate: (c) => set((s) => ({ candidates: [{ ...c, id: `CD-${Date.now()}`, appliedAt: now(), notes: [] }, ...s.candidates] })),
  updateCandidate: (id, patch) => set((s) => ({ candidates: s.candidates.map((c) => (c.id === id ? { ...c, ...patch } : c)) })),
  noteCandidate: (id, by, text) => set((s) => ({ candidates: s.candidates.map((c) => (c.id === id ? { ...c, notes: [...c.notes, { at: now(), by, text }] } : c)) })),

  addOnboarding: (o) => {
    const ob: Onboarding = { ...o, id: `OB-${Date.now()}`, createdAt: now() };
    set((s) => ({ onboardings: [ob, ...s.onboardings] }));
    return ob;
  },
  updateOnboarding: (id, patch) => set((s) => ({ onboardings: s.onboardings.map((o) => (o.id === id ? { ...o, ...patch } : o)) })),
}));
