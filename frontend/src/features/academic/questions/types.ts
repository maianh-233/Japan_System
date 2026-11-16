import type { QuizSkill } from '../classes/types';

/**
 * Ngân hàng câu hỏi — nguồn câu hỏi DUY NHẤT cho:
 *  - Bài tập trắc nghiệm của lớp (bốc theo tiêu chí: bài học, kỹ năng, độ khó, số câu)
 *  - Kỳ thi giữa kỳ / cuối kỳ (mốc thi trong lộ trình) và test xếp lớp
 *  Chỉ câu ĐÃ DUYỆT mới được bốc vào đề.
 *
 *  Nội dung tiếng Nhật hỗ trợ furigana: viết 漢字{かんじ} → hiển thị chữ nhỏ phía trên.
 */

export type QLevel = 'N5' | 'N4' | 'N3' | 'N2' | 'N1';
export type QSkill = QuizSkill;

/**
 * Dạng câu theo đề JLPT:
 *  - choice:  chọn 1 trong 4 đáp án (đọc Kanji, chọn từ, ngữ pháp, đọc hiểu, nghe hiểu)
 *  - star:    sắp xếp câu — 4 mảnh, hỏi mảnh nằm ở vị trí ★
 */
export type QFormat = 'choice' | 'star';
export type QDifficulty = 'easy' | 'medium' | 'hard';

/** Nháp → chờ duyệt → đã duyệt (được dùng trong đề) · ngừng dùng (lộ đề, sai sót) */
export type QStatus = 'draft' | 'review' | 'approved' | 'retired';

export interface Question {
  id: string;
  code: string;
  level: QLevel;
  skill: QSkill;
  format: QFormat;
  difficulty: QDifficulty;
  /** Đề bài — hỗ trợ furigana 漢字{かんじ}, chỗ trống ＿＿, vị trí ★ */
  stem: string;
  /** Đoạn văn (đọc hiểu) — nhiều câu có thể dùng chung đoạn */
  passage?: string;
  /** File nghe (nghe hiểu) */
  audio?: { name: string; durationSec: number };
  /** Lời thoại file nghe — chỉ GV / nhân viên xem */
  transcript?: string;
  options: string[];
  /** Vị trí đáp án đúng (0-based) */
  answer: number;
  explanation?: string;
  /** Gắn bài học trong lộ trình (để bốc câu theo bài) */
  curriculumId?: string;
  lessonIds: string[];
  tags: string[];
  status: QStatus;
  author: string;
  createdAt: string;
  updatedAt: string;
  /** Người duyệt / lý do trả lại */
  review?: { by: string; at: string; note?: string };
  /** Thống kê từ các lần học viên làm bài */
  stats: { used: number; attempts: number; correct: number; picks?: number[] };
}
