import dayjs from 'dayjs';
import { Ban, CalendarClock, CheckCircle2, ListChecks, PauseCircle, PlayCircle, Upload, type LucideIcon } from 'lucide-react';
import type { Tone } from '@/components/common';
import type { TeacherType } from '../courses/types';
import type { AssignmentType, ClassInfo, ClassStatus, QuizDifficulty, QuizSkill, SessionStatus, Weekday } from './types';

type Meta = { label: string; tone: Tone; icon: LucideIcon };

export const CLASS_STATUS: Record<ClassStatus, Meta & { hint: string }> = {
  enrolling: { label: 'Chờ khai giảng', tone: 'info', icon: CalendarClock, hint: 'Đang tuyển học viên — sửa được toàn bộ thông tin' },
  ongoing: { label: 'Đang diễn ra', tone: 'success', icon: PlayCircle, hint: 'Lớp đang học' },
  paused: { label: 'Tạm dừng', tone: 'warning', icon: PauseCircle, hint: 'Tạm ngưng (nghỉ dài, thiếu giáo viên...)' },
  finished: { label: 'Đã kết thúc', tone: 'neutral', icon: CheckCircle2, hint: 'Đã học xong' },
  cancelled: { label: 'Bị hủy', tone: 'danger', icon: Ban, hint: 'Lớp bị hủy' },
};
export const CLASS_STATUS_OPTIONS = (Object.keys(CLASS_STATUS) as ClassStatus[]).map((k) => ({ value: k, label: CLASS_STATUS[k].label }));

/** Lớp còn "mở" (chưa kết thúc / chưa hủy) */
export const isOpenClass = (c: Pick<ClassInfo, 'status'>) => c.status === 'enrolling' || c.status === 'ongoing' || c.status === 'paused';

export const SESSION_STATUS: Record<SessionStatus, { label: string; tone: Tone }> = {
  scheduled: { label: 'Sắp diễn ra', tone: 'info' },
  completed: { label: 'Đã dạy', tone: 'success' },
  cancelled: { label: 'Đã hủy', tone: 'danger' },
  makeup: { label: 'Học bù', tone: 'warning' },
};

/** Tiền tố mã lớp theo loại giáo viên của khóa */
export const CODE_PREFIX: Record<TeacherType, string> = { vn: 'GVV_', jp: 'GVN_', mixed: 'GVVN_' };

export const WEEKDAYS: { value: Weekday; short: string; label: string }[] = [
  { value: 1, short: 'T2', label: 'Thứ 2' },
  { value: 2, short: 'T3', label: 'Thứ 3' },
  { value: 3, short: 'T4', label: 'Thứ 4' },
  { value: 4, short: 'T5', label: 'Thứ 5' },
  { value: 5, short: 'T6', label: 'Thứ 6' },
  { value: 6, short: 'T7', label: 'Thứ 7' },
  { value: 7, short: 'CN', label: 'Chủ nhật' },
];

/** dayjs: 0 = CN → Weekday 7 */
export const weekdayOf = (d: dayjs.Dayjs): Weekday => (((d.day() + 6) % 7) + 1) as Weekday;

/** Giáo viên (dữ liệu giả — sau lấy từ Nhân sự → Giáo viên) */
export const TEACHERS: { id: string; name: string; type: 'vn' | 'jp'; branchIds: string[] }[] = [
  { id: 't-ha', name: 'Nguyễn Thu Hà', type: 'vn', branchIds: ['cg', 'q1'] },
  { id: 't-anh', name: 'Trần Mai Anh', type: 'vn', branchIds: ['cg'] },
  { id: 't-long', name: 'Phạm Đức Long', type: 'vn', branchIds: ['cg', 'hc'] },
  { id: 't-thu', name: 'Lê Minh Thư', type: 'vn', branchIds: ['q1'] },
  { id: 't-hanh', name: 'Võ Thị Hạnh', type: 'vn', branchIds: ['hc'] },
  { id: 't-sato', name: 'Sato Yuki', type: 'jp', branchIds: ['q1', 'cg'] },
  { id: 't-tanaka', name: 'Tanaka Ken', type: 'jp', branchIds: ['cg', 'hc'] },
];
export const teacherName = (id: string) => TEACHERS.find((t) => t.id === id)?.name ?? id;
export const teacherNames = (ids: string[]) => ids.map(teacherName).join(', ');

/** Phòng học theo chi nhánh (dữ liệu giả — sau lấy từ Đào tạo → Phòng học) */
export const ROOMS: Record<string, string[]> = {
  cg: ['P.201', 'P.205', 'P.301', 'P.302'],
  q1: ['P.101', 'P.102', 'P.203'],
  hc: ['P.A1', 'P.A2', 'Xưởng thực hành'],
};

/** Ngày nghỉ lễ — bỏ qua khi tự sinh buổi học (Tết âm lịch cần cập nhật hằng năm) */
export const HOLIDAYS: { date: string; name: string }[] = [
  { date: '2026-01-01', name: 'Tết Dương lịch' },
  { date: '2026-04-30', name: 'Giải phóng miền Nam' },
  { date: '2026-05-01', name: 'Quốc tế Lao động' },
  { date: '2026-09-02', name: 'Quốc khánh' },
  { date: '2026-09-01', name: 'Quốc khánh (nghỉ bù)' },
  { date: '2027-01-01', name: 'Tết Dương lịch' },
  ...['2027-02-04', '2027-02-05', '2027-02-06', '2027-02-07', '2027-02-08', '2027-02-09', '2027-02-10'].map((date) => ({ date, name: 'Tết Nguyên đán' })),
  { date: '2027-04-16', name: 'Giỗ Tổ Hùng Vương' },
  { date: '2027-04-30', name: 'Giải phóng miền Nam' },
  { date: '2027-05-01', name: 'Quốc tế Lao động' },
  { date: '2027-09-02', name: 'Quốc khánh' },
];

// ---------------- Tính toán ----------------

const toMin = (t: string) => {
  const [h, m] = t.split(':').map(Number);
  return h * 60 + m;
};

/** Số giờ mỗi buổi (vd 18:00–20:00 → 2) */
export const hoursPerSession = (c: Pick<ClassInfo, 'startTime' | 'endTime'>) => Math.max(0, (toMin(c.endTime) - toMin(c.startTime)) / 60);

/** Tổng số buổi = tổng giờ ÷ giờ mỗi buổi (làm tròn lên) */
export const totalSessionsOf = (c: Pick<ClassInfo, 'startTime' | 'endTime' | 'totalHours'>) => {
  const h = hoursPerSession(c);
  return h ? Math.ceil(c.totalHours / h) : 0;
};

/** "T2-T4-T6 · 18:00–20:00" */
export const scheduleLabel = (c: Pick<ClassInfo, 'weekdays' | 'startTime' | 'endTime'>) =>
  `${[...c.weekdays].sort().map((d) => WEEKDAYS[d - 1].short).join('-')} · ${c.startTime}–${c.endTime}`;

/** Ngày khai giảng hiệu lực (thực tế nếu có, không thì dự kiến) */
export const effectiveStart = (c: Pick<ClassInfo, 'actualStartDate' | 'plannedStartDate'>) => c.actualStartDate ?? c.plannedStartDate;

/** Số ngày khai giảng trễ so với dự kiến */
export const startDelayDays = (c: Pick<ClassInfo, 'actualStartDate' | 'plannedStartDate'>) =>
  c.actualStartDate ? dayjs(c.actualStartDate).diff(dayjs(c.plannedStartDate), 'day') : 0;

// ---------------- Bài tập ----------------

export const ASSIGNMENT_TYPES: Record<AssignmentType, Meta & { hint: string }> = {
  upload: { label: 'Nộp bài', tone: 'neutral', icon: Upload, hint: 'Học viên nộp ảnh chụp vở, file Word/PDF, ghi âm — giáo viên chấm tay' },
  quiz: { label: 'Trắc nghiệm', tone: 'primary', icon: ListChecks, hint: 'Làm trên Cổng học viên, câu hỏi bốc từ Ngân hàng câu hỏi — hệ thống tự chấm' },
};

export const QUIZ_SKILLS: Record<QuizSkill, string> = {
  vocab: 'Từ vựng',
  kanji: 'Kanji',
  grammar: 'Ngữ pháp',
  reading: 'Đọc hiểu',
  listening: 'Nghe hiểu',
};
export const QUIZ_SKILL_OPTIONS = (Object.keys(QUIZ_SKILLS) as QuizSkill[]).map((k) => ({ value: k, label: QUIZ_SKILLS[k] }));

export const QUIZ_DIFFICULTY: Record<QuizDifficulty, string> = { easy: 'Dễ', medium: 'Trung bình', hard: 'Khó', mixed: 'Trộn (30% dễ · 50% TB · 20% khó)' };
export const QUIZ_DIFFICULTY_OPTIONS = (Object.keys(QUIZ_DIFFICULTY) as QuizDifficulty[]).map((k) => ({ value: k, label: QUIZ_DIFFICULTY[k] }));

/**
 * Ước lượng số câu phù hợp trong Ngân hàng câu hỏi (GIẢ LẬP — khi có module Ngân hàng câu hỏi sẽ gọi API đếm thật).
 */
export const estimateBankQuestions = (lessonCount: number, skills: QuizSkill[]) => lessonCount * skills.reduce((n, sk) => n + (sk === 'listening' ? 8 : sk === 'reading' ? 6 : 15), 0);
