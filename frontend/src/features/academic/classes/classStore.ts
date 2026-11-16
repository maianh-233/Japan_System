import { create } from 'zustand';
import { MOCK_SESSIONS } from '@/mocks/data/attendance';
import { MOCK_ASSIGNMENTS, MOCK_CLASSES } from '@/mocks/data/classes';
import { MOCK_ANNOUNCEMENTS, MOCK_CARE_LOGS, MOCK_EVALUATIONS, MOCK_TEACHER_REVIEWS } from '@/mocks/data/classExtras';
import { MOCK_EXAM_RESULTS, MOCK_SUBMISSIONS } from '@/mocks/data/submissions';
import type { Assignment, CareLog, ClassAnnouncement, ClassInfo, ClassSession, ExamResult, StudentEvaluation, Submission, TeacherReview } from './types';
import { moveToTrash, registerRestorer } from '@/features/system/trash';

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
  announcements: ClassAnnouncement[];
  teacherReviews: TeacherReview[];
  careLogs: CareLog[];

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
  /** Học viên nộp bài (Cổng học viên). Nộp lại → thay bài cũ (bài quiz giữ lần điểm cao nhất) */
  submitAssignment: (sub: Omit<Submission, 'id'>) => void;
  updateExamResult: (id: string, patch: Partial<ExamResult>) => void;

  addAnnouncement: (a: Omit<ClassAnnouncement, 'id'>) => void;
  addCareLog: (c: Omit<CareLog, 'id' | 'at'>) => void;

  updateAssignment: (id: string, patch: Partial<Assignment>) => void;

  evaluations: StudentEvaluation[];
  /** Tạo mới hoặc sửa nhận xét cuối khóa (mỗi học viên / lớp 1 bản) */
  saveEvaluation: (e: Omit<StudentEvaluation, 'id' | 'createdAt' | 'updatedAt'>) => void;
}

const now = () => new Date().toISOString();

export const useClassStore = create<ClassState>((set, get) => ({
  classes: MOCK_CLASSES,
  sessions: MOCK_SESSIONS,
  assignments: MOCK_ASSIGNMENTS,
  submissions: MOCK_SUBMISSIONS,
  examResults: MOCK_EXAM_RESULTS,
  announcements: MOCK_ANNOUNCEMENTS,
  teacherReviews: MOCK_TEACHER_REVIEWS,
  careLogs: MOCK_CARE_LOGS,

  addClass: (input) => {
    const c: ClassInfo = { ...input, id: `C-${Date.now()}`, status: 'enrolling', createdAt: now() };
    set((s) => ({ classes: [c, ...s.classes] }));
    return c;
  },
  updateClass: (id, patch) => set((s) => ({ classes: s.classes.map((c) => (c.id === id ? { ...c, ...patch } : c)) })),
  isCodeTaken: (code, excludeId) => get().classes.some((c) => c.id !== excludeId && c.code.toLowerCase() === code.trim().toLowerCase()),

  replaceUpcomingSessions: (classId, next) =>
    set((s) => ({
      // Buổi học thêm (extra) không thuộc lịch khóa → giữ lại
      sessions: [...s.sessions.filter((x) => x.classId !== classId || x.status !== 'scheduled' || x.origin === 'extra'), ...next].sort(
        (a, b) => a.classId.localeCompare(b.classId) || a.date.localeCompare(b.date) || a.no - b.no,
      ),
    })),
  updateSession: (id, patch) => set((s) => ({ sessions: s.sessions.map((x) => (x.id === id ? { ...x, ...patch } : x)) })),
  addSession: (x) => set((s) => ({ sessions: [...s.sessions, x].sort((a, b) => a.date.localeCompare(b.date)) })),

  addAssignment: (a) =>
    set((s) => ({ assignments: [{ maxScore: 10, ...a, id: `A-${Date.now()}`, assignedAt: now() }, ...s.assignments] })),
  removeAssignment: (id) => {
    const a = get().assignments.find((x) => x.id === id);
    if (a) {
      const cls = get().classes.find((c) => c.id === a.classId);
      const subs = get().submissions.filter((x) => x.assignmentId === id);
      moveToTrash({ kind: 'assignment', label: a.title, detail: `${cls?.code ?? a.classId} · ${subs.length} bài nộp`, branchId: cls?.branchId, payload: { assignment: a, submissions: subs } });
    }
    set((s) => ({ assignments: s.assignments.filter((x) => x.id !== id), submissions: s.submissions.filter((x) => x.assignmentId !== id) }));
  },

  submitAssignment: (sub) =>
    set((s) => {
      const old = s.submissions.find((x) => x.assignmentId === sub.assignmentId && x.studentId === sub.studentId);
      if (!old) return { submissions: [...s.submissions, { ...sub, id: `SUB-${Date.now()}` }] };
      // Quiz: cộng lượt làm, giữ kết quả cao nhất
      const quiz = sub.quiz && old.quiz ? (sub.quiz.correct >= old.quiz.correct ? { ...sub.quiz, attempts: old.quiz.attempts + 1 } : { ...old.quiz, attempts: old.quiz.attempts + 1 }) : sub.quiz;
      const better = !old.quiz || !sub.quiz || sub.quiz.correct >= old.quiz.correct;
      return { submissions: s.submissions.map((x) => (x.id === old.id ? { ...x, ...(better ? sub : {}), quiz, score: better ? sub.score : x.score, gradedAt: better ? sub.gradedAt : x.gradedAt } : x)) };
    }),
  gradeSubmission: (id, score, comment, by) =>
    set((s) => ({ submissions: s.submissions.map((x) => (x.id === id ? { ...x, score, comment, gradedAt: now(), gradedBy: by } : x)) })),
  updateExamResult: (id, patch) => set((s) => ({ examResults: s.examResults.map((r) => (r.id === id ? { ...r, ...patch } : r)) })),

  addAnnouncement: (a) => set((s) => ({ announcements: [{ ...a, id: `AN-${Date.now()}` }, ...s.announcements] })),
  addCareLog: (c) => set((s) => ({ careLogs: [{ ...c, id: `CL-${Date.now()}`, at: now() }, ...s.careLogs] })),

  updateAssignment: (id, patch) => set((s) => ({ assignments: s.assignments.map((x) => (x.id === id ? { ...x, ...patch } : x)) })),

  evaluations: MOCK_EVALUATIONS,
  saveEvaluation: (e) =>
    set((s) => {
      const old = s.evaluations.find((x) => x.classId === e.classId && x.studentId === e.studentId);
      if (old) return { evaluations: s.evaluations.map((x) => (x.id === old.id ? { ...x, ...e, updatedAt: now() } : x)) };
      return { evaluations: [{ ...e, id: `EV-${Date.now()}`, createdAt: now(), updatedAt: now() }, ...s.evaluations] };
    }),
}));

registerRestorer<{ assignment: Assignment; submissions: Submission[] }>('assignment', ({ assignment, submissions }) =>
  useClassStore.setState((s) => ({ assignments: [assignment, ...s.assignments], submissions: [...s.submissions, ...submissions] })),
);
