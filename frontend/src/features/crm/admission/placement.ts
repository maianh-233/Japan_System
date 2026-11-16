import type { Question } from '@/features/academic/questions/types';
import type { PlacementAttempt, PlacementBand, PlacementSection, PlacementTest } from '../types';

/** Logic TEST XẾP LỚP (thuần): bốc câu từ ngân hàng, chấm điểm, gợi ý trình độ. */

/** Câu đủ điều kiện cho 1 phần: đã duyệt, đúng trình độ + kỹ năng */
export const poolOf = (questions: Question[], s: Pick<PlacementSection, 'level' | 'skill'>) =>
  questions.filter((q) => q.status === 'approved' && q.level === s.level && q.skill === s.skill);

/** Bốc câu cho các phần. `shuffle` = bốc ngẫu nhiên (đổi đề), không thì lấy theo thứ tự */
export function pickQuestions(questions: Question[], sections: PlacementSection[], shuffle = false): string[] {
  return sections.flatMap((s) => {
    const pool = poolOf(questions, s);
    const list = shuffle ? [...pool].sort(() => Math.random() - 0.5) : pool;
    return list.slice(0, s.count).map((q) => q.id);
  });
}

/** Bậc gợi ý theo % điểm (bậc có mốc cao nhất ≤ điểm) */
export const bandOf = (bands: PlacementBand[], pct: number) => [...bands].sort((a, b) => b.minPct - a.minPct).find((b) => pct >= b.minPct);

export const pctOf = (a: Pick<PlacementAttempt, 'correct' | 'total'>) => (a.total ? Math.round(((a.correct ?? 0) / a.total) * 100) : 0);

export function grade(
  test: PlacementTest,
  answers: Record<string, number>,
  questions: Question[],
): Pick<PlacementAttempt, 'correct' | 'total' | 'bySkill' | 'suggestedLevel' | 'suggestedCourse'> {
  const qs = test.questionIds.map((id) => questions.find((q) => q.id === id)).filter((q): q is Question => !!q);
  const bySkill: NonNullable<PlacementAttempt['bySkill']> = {};
  let correct = 0;
  qs.forEach((q) => {
    const ok = answers[q.id] === q.answer;
    if (ok) correct++;
    const s = (bySkill[q.skill] ??= { correct: 0, total: 0 });
    s.total++;
    if (ok) s.correct++;
  });
  const total = qs.length;
  const band = bandOf(test.bands, total ? (correct / total) * 100 : 0);
  return { correct, total, bySkill, suggestedLevel: band?.level, suggestedCourse: band?.course };
}

/** Số ngẫu nhiên có hạt giống (để dữ liệu giả ổn định giữa các lần tải) */
function rng(seed: number) {
  let a = seed >>> 0;
  return () => {
    a = (a + 0x6d2b79f5) >>> 0;
    let t = a;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

const LEVEL_IDX = { N5: 0, N4: 1, N3: 2, N2: 3, N1: 4 } as const;
const DIFF = { easy: 0, medium: 0.1, hard: 0.22 } as const;

/** GIẢ LẬP bài làm của thí sinh có năng lực `ability` (0–1) — chỉ dùng cho dữ liệu mẫu / nút "Giả lập nộp bài" */
export function simulateAnswers(questionIds: string[], questions: Question[], ability: number, seed = Date.now()): Record<string, number> {
  const r = rng(seed);
  const out: Record<string, number> = {};
  questionIds.forEach((id) => {
    const q = questions.find((x) => x.id === id);
    if (!q) return;
    const p = Math.min(0.97, Math.max(0.05, ability + 0.25 - LEVEL_IDX[q.level] * 0.2 - DIFF[q.difficulty]));
    const x = r();
    if (x > 0.96) out[id] = -1;
    else if (x < p) out[id] = q.answer;
    else {
      const wrong = q.options.map((_, k) => k).filter((k) => k !== q.answer);
      out[id] = wrong[Math.floor(r() * wrong.length)];
    }
  });
  return out;
}

/** Link làm bài gửi cho lead */
export const testLink = (token: string) => `https://jpcenter.vn/t/${token}`;
