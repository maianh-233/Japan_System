import { create } from 'zustand';
import { MOCK_QUESTIONS } from '@/mocks/data/questions';
import type { QStatus, Question } from './types';

/**
 * Store NGÂN HÀNG CÂU HỎI. Chế độ API: dữ liệu nạp / cập nhật qua `questionApi.ts`; các hàm dưới chỉ dùng ở chế độ mock.
 */

export type QuestionInput = Omit<Question, 'id' | 'code' | 'createdAt' | 'updatedAt' | 'stats' | 'review' | 'status' | 'author'>;

interface QuestionState {
  questions: Question[];
  add: (input: QuestionInput, author: string, status: QStatus) => Question;
  update: (id: string, patch: Partial<QuestionInput>) => void;
  /** Đổi trạng thái (gửi duyệt / duyệt / trả lại / ngừng dùng) */
  setStatus: (ids: string[], status: QStatus, review?: { by: string; note?: string }) => void;
  duplicate: (id: string, author: string) => Question | undefined;
}

let seq = 1000;
const now = () => new Date().toISOString();
const codeOf = (q: Pick<Question, 'level' | 'skill'>) => `${q.level}-${q.skill.slice(0, 2).toUpperCase()}-${String(++seq).padStart(4, '0')}`;

export const useQuestionStore = create<QuestionState>((set, get) => ({
  // Chế độ API vẫn khởi tạo bằng mock: Test xếp lớp, bài tập / bài thi (chưa có backend) tham chiếu câu theo Id mock;
  // trang Ngân hàng câu hỏi nạp dữ liệu thật đè lên (Id câu mẫu đổi về Id mock qua seedIds)
  questions: MOCK_QUESTIONS,

  add: (input, author, status) => {
    const q: Question = { ...input, id: `Q-${Date.now()}`, code: codeOf(input), status, author, createdAt: now(), updatedAt: now(), stats: { used: 0, attempts: 0, correct: 0 } };
    set((s) => ({ questions: [q, ...s.questions] }));
    return q;
  },

  // Sửa câu đã duyệt → quay về chờ duyệt (nội dung đổi phải kiểm tra lại)
  update: (id, patch) =>
    set((s) => ({
      questions: s.questions.map((q) => (q.id === id ? { ...q, ...patch, updatedAt: now(), status: q.status === 'approved' ? 'review' : q.status } : q)),
    })),

  setStatus: (ids, status, review) =>
    set((s) => ({
      questions: s.questions.map((q) => (ids.includes(q.id) ? { ...q, status, updatedAt: now(), review: review ? { ...review, at: now() } : q.review } : q)),
    })),

  duplicate: (id, author) => {
    const src = get().questions.find((q) => q.id === id);
    if (!src) return undefined;
    const q: Question = { ...src, id: `Q-${Date.now()}`, code: codeOf(src), status: 'draft', author, createdAt: now(), updatedAt: now(), review: undefined, stats: { used: 0, attempts: 0, correct: 0 } };
    set((s) => ({ questions: [q, ...s.questions] }));
    return q;
  },
}));
