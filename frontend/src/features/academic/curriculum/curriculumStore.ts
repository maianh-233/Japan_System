import { create } from 'zustand';
import { MOCK_COURSE_CURRICULA, MOCK_CURRICULA } from '@/mocks/data/curricula';
import type { CourseCurriculum, Curriculum, CurriculumStatus } from './types';

export { CURRICULUM_STATUS } from './constants';

/**
 * Store LỘ TRÌNH GIẢ LẬP (trong bộ nhớ). Khi có backend: thay bằng gọi API, giữ chữ ký.
 * Sửa nội dung (lesson / chapter / tài liệu / mốc thi) = `updateCurriculum(id, { lessons, exams })`
 * — component tự tính mảng mới, store chỉ ghi lại.
 */

export type CurriculumInput = Omit<Curriculum, 'id' | 'updatedAt' | 'lessons' | 'exams'>;

interface CurriculumState {
  curricula: Curriculum[];
  links: CourseCurriculum[];

  addCurriculum: (input: CurriculumInput, author: string) => Curriculum;
  updateCurriculum: (id: string, patch: Partial<Curriculum>) => void;
  setStatus: (id: string, status: CurriculumStatus) => void;
  /** Tạo bản nháp từ lộ trình có sẵn (phiên bản mới / biến thể nâng cao). Gắn sang các khóa của bản gốc (không mặc định). */
  cloneCurriculum: (id: string, overrides: Partial<Curriculum>, author: string) => Curriculum;

  /** Gắn lộ trình vào khóa. `asDefault` → bỏ mặc định của lộ trình khác trong khóa */
  link: (courseId: string, curriculumId: string, asDefault: boolean) => void;
  unlink: (courseId: string, curriculumId: string) => void;
  setDefault: (courseId: string, curriculumId: string) => void;
}

const now = () => new Date().toISOString();
const uid = (p: string) => `${p}-${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 6)}`;

/** Sao chép sâu nội dung, đổi toàn bộ id (để bản sao không trùng id với bản gốc) */
function cloneContent(c: Curriculum): Pick<Curriculum, 'lessons' | 'exams'> {
  const lessonIdMap = new Map<string, string>();
  const lessons = c.lessons.map((l) => {
    const id = uid('L');
    lessonIdMap.set(l.id, id);
    return {
      ...l,
      id,
      chapters: l.chapters.map((ch) => ({ ...ch, id: uid('C'), materials: ch.materials.map((m) => ({ ...m, id: uid('M') })) })),
    };
  });
  const exams = c.exams.map((e) => ({
    ...e,
    id: uid('EX'),
    afterLessonId: lessonIdMap.get(e.afterLessonId) ?? lessons[lessons.length - 1]?.id ?? '',
  }));
  return { lessons, exams };
}

export const useCurriculumStore = create<CurriculumState>((set, get) => ({
  curricula: MOCK_CURRICULA,
  links: MOCK_COURSE_CURRICULA,

  addCurriculum: (input, author) => {
    const c: Curriculum = { ...input, id: uid('CUR'), lessons: [], exams: [], author, updatedAt: now() };
    set((s) => ({ curricula: [c, ...s.curricula] }));
    return c;
  },

  updateCurriculum: (id, patch) =>
    set((s) => ({ curricula: s.curricula.map((c) => (c.id === id ? { ...c, ...patch, updatedAt: now() } : c)) })),

  setStatus: (id, status) =>
    set((s) => ({ curricula: s.curricula.map((c) => (c.id === id ? { ...c, status, updatedAt: now() } : c)) })),

  cloneCurriculum: (id, overrides, author) => {
    const src = get().curricula.find((c) => c.id === id)!;
    const copy: Curriculum = { ...src, ...cloneContent(src), ...overrides, id: uid('CUR'), status: 'draft', author, updatedAt: now() };
    const courseIds = get().links.filter((l) => l.curriculumId === id).map((l) => l.courseId);
    set((s) => ({
      curricula: [copy, ...s.curricula],
      links: [...s.links, ...courseIds.map((courseId) => ({ courseId, curriculumId: copy.id, isDefault: false, linkedAt: now() }))],
    }));
    return copy;
  },

  link: (courseId, curriculumId, asDefault) => {
    if (get().links.some((l) => l.courseId === courseId && l.curriculumId === curriculumId)) return;
    // Khóa chưa có lộ trình nào → lộ trình đầu tiên tự thành mặc định
    const first = !get().links.some((l) => l.courseId === courseId);
    const isDefault = asDefault || first;
    set((s) => ({
      links: [
        ...s.links.map((l) => (isDefault && l.courseId === courseId ? { ...l, isDefault: false } : l)),
        { courseId, curriculumId, isDefault, linkedAt: now() },
      ],
    }));
  },

  unlink: (courseId, curriculumId) =>
    set((s) => ({ links: s.links.filter((l) => !(l.courseId === courseId && l.curriculumId === curriculumId)) })),

  setDefault: (courseId, curriculumId) =>
    set((s) => ({
      links: s.links.map((l) => (l.courseId === courseId ? { ...l, isDefault: l.curriculumId === curriculumId } : l)),
    })),
}));

/** Thống kê nhanh của lộ trình */
export function curriculumStats(c: Curriculum) {
  const chapters = c.lessons.reduce((n, l) => n + l.chapters.length, 0);
  const materials = c.lessons.reduce((n, l) => n + l.chapters.reduce((m, ch) => m + ch.materials.length, 0), 0);
  const quizzes = c.lessons.filter((l) => l.hasQuiz).length;
  /** Chapter chưa có tài liệu nào — cảnh báo trước khi phát hành */
  const emptyChapters = c.lessons.reduce((n, l) => n + l.chapters.filter((ch) => ch.materials.length === 0).length, 0);
  return { lessons: c.lessons.length, chapters, materials, quizzes, exams: c.exams.length, emptyChapters };
}

/** "v1.2" → "v1.3"; "v2.0" + major → "v3.0" */
export function nextVersion(v: string, major = false): string {
  const m = v.match(/^v?(\d+)\.(\d+)/);
  if (!m) return 'v1.0';
  return major ? `v${Number(m[1]) + 1}.0` : `v${m[1]}.${Number(m[2]) + 1}`;
}

/** Phạm vi kiến thức của mốc thi: từ bài sau mốc thi liền trước → bài `afterLessonId` (số thứ tự 1-based) */
export function examScope(c: Pick<Curriculum, 'lessons' | 'exams'>, examId: string): { from: number; to: number } | null {
  const idx = (lessonId: string) => c.lessons.findIndex((l) => l.id === lessonId);
  const exam = c.exams.find((e) => e.id === examId);
  if (!exam) return null;
  const to = idx(exam.afterLessonId);
  if (to < 0) return null;
  const prev = c.exams
    .filter((e) => e.id !== examId && e.kind !== exam.kind && idx(e.afterLessonId) < to)
    .map((e) => idx(e.afterLessonId))
    .sort((a, b) => b - a)[0];
  // Cuối kỳ: mặc định thi toàn khóa; giữa kỳ: từ đầu (hoặc sau mốc trước) đến bài hiện tại
  const from = exam.kind === 'final' ? 0 : prev !== undefined ? prev + 1 : 0;
  return { from: from + 1, to: to + 1 };
}
