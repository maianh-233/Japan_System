import dayjs from 'dayjs';
import { create } from 'zustand';
import { MOCK_ASSIGN_LOGS, MOCK_ATTEMPTS, MOCK_CAMPAIGNS, MOCK_LEAD_RULES, MOCK_PLACEMENT_TESTS } from '@/mocks/data/admissions';
import type { AssignmentLog, Campaign, LeadRule, PlacementAttempt, PlacementTest } from '../types';

/**
 * Store TUYỂN SINH giả lập: quy tắc chia lead, bài test xếp lớp, lượt làm bài, chiến dịch.
 * Khi có backend: thay hàm bằng gọi API, giữ nguyên chữ ký.
 */

export type RuleInput = Omit<LeadRule, 'id' | 'cursor'>;
export type TestInput = Omit<PlacementTest, 'id' | 'code' | 'createdAt' | 'author'>;
export type CampaignInput = Omit<Campaign, 'id' | 'createdAt'>;

interface AdmissionState {
  rules: LeadRule[];
  logs: AssignmentLog[];
  tests: PlacementTest[];
  attempts: PlacementAttempt[];
  campaigns: Campaign[];

  addRule: (input: RuleInput) => void;
  updateRule: (id: string, patch: Partial<LeadRule>) => void;
  removeRule: (id: string) => void;
  /** Đổi thứ tự ưu tiên (dir = -1 lên, 1 xuống) */
  moveRule: (id: string, dir: -1 | 1) => void;
  addLog: (log: Omit<AssignmentLog, 'id' | 'at'>) => void;

  addTest: (input: TestInput, author: string) => PlacementTest;
  updateTest: (id: string, patch: Partial<PlacementTest>) => void;
  sendTest: (a: Pick<PlacementAttempt, 'testId' | 'leadId' | 'channel' | 'sentBy'>, validDays: number) => PlacementAttempt;
  updateAttempt: (id: string, patch: Partial<PlacementAttempt>) => void;

  addCampaign: (input: CampaignInput) => Campaign;
  updateCampaign: (id: string, patch: Partial<Campaign>) => void;
}

let seq = 100;
const now = () => new Date().toISOString();
const token = () => Math.random().toString(36).slice(2, 10);

export const useAdmissionStore = create<AdmissionState>((set) => ({
  rules: MOCK_LEAD_RULES,
  logs: MOCK_ASSIGN_LOGS,
  tests: MOCK_PLACEMENT_TESTS,
  attempts: MOCK_ATTEMPTS,
  campaigns: MOCK_CAMPAIGNS,

  addRule: (input) => set((s) => ({ rules: [...s.rules, { ...input, id: `R-${++seq}`, cursor: -1 }] })),
  updateRule: (id, patch) => set((s) => ({ rules: s.rules.map((r) => (r.id === id ? { ...r, ...patch } : r)) })),
  removeRule: (id) => set((s) => ({ rules: s.rules.filter((r) => r.id !== id) })),
  moveRule: (id, dir) =>
    set((s) => {
      const i = s.rules.findIndex((r) => r.id === id);
      const j = i + dir;
      if (i < 0 || j < 0 || j >= s.rules.length) return s;
      const rules = [...s.rules];
      [rules[i], rules[j]] = [rules[j], rules[i]];
      return { rules };
    }),
  addLog: (log) => set((s) => ({ logs: [{ ...log, id: `AL-${++seq}`, at: now() }, ...s.logs] })),

  addTest: (input, author) => {
    const n = ++seq;
    const t: PlacementTest = { ...input, id: `PT-${n}`, code: `PT-${n}`, author, createdAt: now() };
    set((s) => ({ tests: [t, ...s.tests] }));
    return t;
  },
  updateTest: (id, patch) => set((s) => ({ tests: s.tests.map((t) => (t.id === id ? { ...t, ...patch } : t)) })),
  sendTest: (a, validDays) => {
    const at: PlacementAttempt = { ...a, id: `AT-${++seq}`, token: token(), sentAt: now(), expiresAt: dayjs().add(validDays, 'day').toISOString(), status: 'sent' };
    set((s) => ({ attempts: [at, ...s.attempts] }));
    return at;
  },
  updateAttempt: (id, patch) => set((s) => ({ attempts: s.attempts.map((a) => (a.id === id ? { ...a, ...patch } : a)) })),

  addCampaign: (input) => {
    const c: Campaign = { ...input, id: `CP-${++seq}`, createdAt: now() };
    set((s) => ({ campaigns: [c, ...s.campaigns] }));
    return c;
  },
  updateCampaign: (id, patch) => set((s) => ({ campaigns: s.campaigns.map((c) => (c.id === id ? { ...c, ...patch } : c)) })),
}));
