import type { Question, QLevel, QSkill } from '@/features/academic/questions/types';

const LEVELS: QLevel[] = ['N5', 'N4', 'N3', 'N2', 'N1'];

/** Trình độ của lớp suy từ lộ trình (lớp thiếu nhi / kaiwa → N5) */
export const levelOf = (curriculumLevel?: string): QLevel => (LEVELS.includes(curriculumLevel as QLevel) ? (curriculumLevel as QLevel) : 'N5');

/** Số ngẫu nhiên có hạt giống — mỗi học viên 1 đề cố định cho 1 bài */
function rng(seed: number) {
  let a = seed >>> 0;
  return () => {
    a = (a + 0x6d2b79f5) >>> 0;
    let t = Math.imul(a ^ (a >>> 15), a | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}
export const hashSeed = (s: string) => [...s].reduce((h, c) => (Math.imul(h, 31) + c.charCodeAt(0)) | 0, 7);

interface Criteria {
  level: QLevel;
  skills: QSkill[];
  lessonIds?: string[];
  difficulty?: 'easy' | 'medium' | 'hard' | 'mixed';
  count: number;
  seed: string;
}

/**
 * Bốc câu ĐÃ DUYỆT theo tiêu chí. Ưu tiên câu gắn đúng bài học → cùng trình độ → trình độ thấp hơn 1 bậc (bù khi thiếu).
 */
export function pickQuiz(questions: Question[], c: Criteria): Question[] {
  const r = rng(hashSeed(c.seed));
  const ok = (q: Question) => q.status === 'approved' && (!c.skills.length || c.skills.includes(q.skill)) && (!c.difficulty || c.difficulty === 'mixed' || q.difficulty === c.difficulty);
  const shuffle = (l: Question[]) => l.map((q) => ({ q, k: r() })).sort((a, b) => a.k - b.k).map((x) => x.q);
  const li = LEVELS.indexOf(c.level);
  const byLesson = shuffle(questions.filter((q) => ok(q) && c.lessonIds?.length && q.lessonIds.some((id) => c.lessonIds!.includes(id))));
  const sameLevel = shuffle(questions.filter((q) => ok(q) && q.level === c.level && !byLesson.includes(q)));
  const lower = li > 0 ? shuffle(questions.filter((q) => ok(q) && q.level === LEVELS[li - 1])) : [];
  return [...byLesson, ...sameLevel, ...lower].slice(0, c.count);
}

/** Đề luyện thi JLPT: trộn mọi kỹ năng của 1 trình độ */
export const mockExam = (questions: Question[], level: QLevel, count: number, seed: string) => pickQuiz(questions, { level, skills: [], count, seed });
