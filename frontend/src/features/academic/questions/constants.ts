import { Archive, BookA, CheckCircle2, CircleDashed, Ear, FileText, Hourglass, Languages, PenLine, Star, type LucideIcon } from 'lucide-react';
import type { Tone } from '@/components/common';
import type { QDifficulty, QFormat, QLevel, QSkill, QStatus } from './types';

type Meta = { label: string; tone: Tone; icon: LucideIcon };

export const Q_LEVELS: QLevel[] = ['N5', 'N4', 'N3', 'N2', 'N1'];

export const Q_SKILLS: Record<QSkill, { label: string; jp: string; icon: LucideIcon }> = {
  vocab: { label: 'Từ vựng', jp: '語彙', icon: BookA },
  kanji: { label: 'Kanji', jp: '漢字', icon: Languages },
  grammar: { label: 'Ngữ pháp', jp: '文法', icon: PenLine },
  reading: { label: 'Đọc hiểu', jp: '読解', icon: FileText },
  listening: { label: 'Nghe hiểu', jp: '聴解', icon: Ear },
};
export const Q_SKILL_OPTIONS = (Object.keys(Q_SKILLS) as QSkill[]).map((k) => ({ value: k, label: Q_SKILLS[k].label }));

export const Q_FORMATS: Record<QFormat, { label: string; hint: string; icon: LucideIcon }> = {
  choice: { label: 'Chọn đáp án', hint: '4 lựa chọn, 1 đáp án đúng', icon: CheckCircle2 },
  star: { label: 'Sắp xếp câu (★)', hint: '4 mảnh câu — chọn mảnh ở vị trí ★', icon: Star },
};

export const Q_DIFFICULTY: Record<QDifficulty, { label: string; tone: Tone }> = {
  easy: { label: 'Dễ', tone: 'success' },
  medium: { label: 'Trung bình', tone: 'info' },
  hard: { label: 'Khó', tone: 'danger' },
};
export const Q_DIFFICULTY_OPTIONS = (Object.keys(Q_DIFFICULTY) as QDifficulty[]).map((k) => ({ value: k, label: Q_DIFFICULTY[k].label }));

export const Q_STATUS: Record<QStatus, Meta & { hint: string }> = {
  draft: { label: 'Nháp', tone: 'neutral', icon: CircleDashed, hint: 'Đang soạn' },
  review: { label: 'Chờ duyệt', tone: 'warning', icon: Hourglass, hint: 'Chờ tổ chuyên môn kiểm tra' },
  approved: { label: 'Đã duyệt', tone: 'success', icon: CheckCircle2, hint: 'Được bốc vào bài tập / đề thi' },
  retired: { label: 'Ngừng dùng', tone: 'danger', icon: Archive, hint: 'Lộ đề / sai sót — không bốc vào đề mới' },
};
export const Q_STATUS_OPTIONS = (Object.keys(Q_STATUS) as QStatus[]).map((k) => ({ value: k, label: Q_STATUS[k].label }));

/** Nhãn đáp án kiểu đề JLPT */
export const OPTION_LABELS = ['1', '2', '3', '4'];

/** Tỉ lệ đúng → nhận xét độ khó thực tế */
export const correctRate = (s: { attempts: number; correct: number }) => (s.attempts ? Math.round((s.correct / s.attempts) * 100) : undefined);
export const rateTone = (pct?: number): Tone => (pct === undefined ? 'neutral' : pct < 40 ? 'danger' : pct < 70 ? 'warning' : 'success');
