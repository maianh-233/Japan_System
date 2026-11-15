import { create } from 'zustand';
import { MOCK_ASSIGNMENTS, MOCK_CLASSES, MOCK_SESSIONS } from '@/mocks/data/classes';
import { MOCK_EXAM_RESULTS, MOCK_SUBMISSIONS } from '@/mocks/data/submissions';
import type { Assignment, ClassInfo, ClassSession, ExamResult, Submission } from './types';

/**
 * Store LỚP HỌC GIẢ LẬP (trong bộ nhớ): lớp, buổi học, bài tập.
 * Ghi danh + học phí nằm ở studentStore. Thao tác chạm cả 2 store (hủy lớp, kết thúc lớp) → useClassActions.
 */

export type ClassInput = Omit<ClassInfo, 'id' | 'createdAt' | 'status' | 'actualStartDate' | 'actualEndDate' | 'cancelReason'>;

interface ClassState {
  classes: ClassInfo[];
  sessions: ClassSession[];
  assignments: Assignment[];
  submissions: Submission[];
  examResults: ExamResult[];

  addClass: (input: ClassInput) => ClassInfo;
  updateClass: (id: string, patch: Partial<ClassInfo>) => void;
  isCodeTaken: (code: string, excludeId?: string) => boolean;

  /** Thay các buổi CHƯA DẠY (scheduled) của lớp bằng danh sách mới; giữ nguyên buổi đã dạy / hủy / học bù */
  replaceUpcomingSessions: (classId: string, next: ClassSession[]) => void;
  updateSession: (id: string, patch: Partial<ClassSession>) => void;
  addSession: (s: ClassSession) => void;

  addAssignment: (a: Omit<Assignment, 'id' | 'assignedAt' | 'maxScore'> & { maxScore?: number }) => void;
  removeAssignment: (id: string) => void;

  gradeSubmission: (id: string, score: number, comment: string | undefined, by: string) => void;
  updateExamResult: (id: string, patch: Partial<ExamResult>) => void;
}

const now = () => new Date().toISOString();

export const useClassStore = create<ClassState>((set, get) => ({
  classes: MOCK_CLASSES,
  sessions: MOCK_SESSIONS,
  assignments: MOCK_ASSIGNMENTS,
  submissions: MOCK_SUBMISSIONS,
  examResults: MOCK_EXAM_RESULTS,

  addClass: (input) => {
    const c: ClassInfo = { ...input, id: `C-${Date.now()}`, status: 'enrolling', createdAt: now() };
    set((s) => ({ classes: [c, ...s.classes] }));
    return c;
  },
  updateClass: (id, patch) => set((s) => ({ classes: s.classes.map((c) => (c.id === id ? { ...c, ...patch } : c)) })),
  isCodeTaken: (code, excludeId) => get().classes.some((c) => c.id !== excludeId && c.code.toLowerCase() === code.trim().toLowerCase()),

  replaceUpcomingSessions: (classId, next) =>
    set((s) => ({
      sessions: [...s.sessions.filter((x) => x.classId !== classId || x.status !== 'scheduled'), ...next].sort(
        (a, b) => a.classId.localeCompare(b.classId) || a.date.localeCompare(b.date) || a.no - b.no,
      ),
    })),
  updateSession: (id, patch) => set((s) => ({ sessions: s.sessions.map((x) => (x.id === id ? { ...x, ...patch } : x)) })),
  addSession: (x) => set((s) => ({ sessions: [...s.sessions, x].sort((a, b) => a.date.localeCompare(b.date)) })),

  addAssignment: (a) =>
    set((s) => ({ assignments: [{ maxScore: 10, ...a, id: `A-${Date.now()}`, assignedAt: now() }, ...s.assignments] })),
  removeAssignment: (id) =>
    set((s) => ({ assignments: s.assignments.filter((a) => a.id !== id), submissions: s.submissions.filter((x) => x.assignmentId !== id) })),

  gradeSubmission: (id, score, comment, by) =>
    set((s) => ({ submissions: s.submissions.map((x) => (x.id === id ? { ...x, score, comment, gradedAt: now(), gradedBy: by } : x)) })),
  updateExamResult: (id, patch) => set((s) => ({ examResults: s.examResults.map((r) => (r.id === id ? { ...r, ...patch } : r)) })),
}));
