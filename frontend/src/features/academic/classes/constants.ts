import dayjs from 'dayjs';
import { useMemo } from 'react';
import { Ban, CalendarClock, CheckCircle2, ListChecks, PauseCircle, PlayCircle, Upload, type LucideIcon } from 'lucide-react';
import type { Tone } from '@/components/common';
import { useHrStore } from '@/features/hr/hrStore';
import type { Employee } from '@/features/hr/types';
import type { TeacherType } from '../courses/types';
import type { AssignmentType, ClassInfo, ClassStatus, EvalRecommendation, EvalSkill, LessonCoverage, QuizDifficulty, QuizSkill, SessionStatus, Weekday } from './types';

type Meta = { label: string; tone: Tone; icon: LucideIcon };

export const CLASS_STATUS: Record<ClassStatus, Meta & { hint: string }> = {
  enrolling: { label: 'Chờ khai giảng', tone: 'info', icon: CalendarClock, hint: 'Đang tuyển học viên — sửa được toàn bộ thông tin' },
  ongoing: { label: 'Đang diễn ra', tone: 'success', icon: PlayCircle, hint: 'Lớp đang học' },
  paused: { label: 'Tạm dừng', tone: 'warning', icon: PauseCircle, hint: 'Tạm ngưng (nghỉ dài, thiếu giáo viên...)' },
  finished: { label: 'Đã kết thúc', tone: 'neutral', icon: CheckCircle2, hint: 'Đã học xong' },
  cancelled: { label: 'Bị hủy', tone: 'danger', icon: Ban, hint: 'Lớp bị hủy' },
};
export const CLASS_STATUS_OPTIONS = (Object.keys(CLASS_STATUS) as ClassStatus[]).map((k) => ({ value: k, label: CLASS_STATUS[k].label }));

/** Tiến độ buổi học — GV báo cáo khi bấm "Kết thúc buổi học" */
export const LESSON_COVERAGE: Record<LessonCoverage, { label: string; tone: Tone; hint: string }> = {
  full: { label: 'Dạy xong bài', tone: 'success', hint: 'Đã dạy hết nội dung theo lộ trình' },
  partial: { label: 'Dạy được một phần', tone: 'warning', hint: 'Phần còn lại dời sang buổi sau' },
  none: { label: 'Chưa dạy được', tone: 'danger', hint: 'Buổi chủ yếu ôn tập / có sự cố — giáo vụ cân nhắc xếp bù' },
};

/** Nhận xét cuối khóa — kỹ năng chấm sao */
export const EVAL_SKILLS: Record<EvalSkill, string> = {
  vocab: 'Từ vựng – Kanji',
  grammar: 'Ngữ pháp',
  reading: 'Đọc hiểu',
  listening: 'Nghe hiểu',
  speaking: 'Hội thoại',
};

/** Đề xuất bước tiếp theo — Sale dựa vào để tư vấn */
export const EVAL_RECOMMENDATION: Record<EvalRecommendation, { label: string; tone: Tone; hint: string }> = {
  next_level: { label: 'Lên khóa tiếp theo', tone: 'success', hint: 'Đủ nền tảng học trình độ kế tiếp' },
  advanced: { label: 'Lên lớp nâng cao', tone: 'primary', hint: 'Học tốt — phù hợp lộ trình bậc Nâng cao' },
  jlpt: { label: 'Luyện thi JLPT', tone: 'info', hint: 'Sẵn sàng ôn thi chứng chỉ trình độ vừa học' },
  repeat: { label: 'Nên học lại / ôn tập', tone: 'warning', hint: 'Còn hổng kiến thức — học lại khóa hoặc lớp ôn ngắn' },
  other: { label: 'Khác (xem ghi chú)', tone: 'neutral', hint: 'Đề xuất riêng, ghi rõ ở phần ghi chú' },
};

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

/**
 * Giáo viên / trợ giảng — LẤY TỪ NHÂN SỰ (hrStore), không giữ danh sách riêng.
 * branchIds = chi nhánh chính + chi nhánh dạy thêm. Người đã nghỉ việc / đang nghỉ dài hạn không được xếp mới
 * (chỉ còn xuất hiện trong bộ lọc khi gọi với `includeInactive`).
 */
export interface TeachingStaff {
  id: string;
  name: string;
  type: 'vn' | 'jp';
  branchIds: string[];
  /** Đang làm (chính thức / thử việc) — xếp lớp, dạy thay được */
  available: boolean;
}

const toStaff = (e: Employee): TeachingStaff => ({
  id: e.id,
  name: e.fullName,
  type: e.teacher?.type ?? 'vn',
  branchIds: [e.branchId, ...(e.teacher?.extraBranchIds ?? []).filter((b) => b !== e.branchId)],
  available: e.status === 'active' || e.status === 'probation',
});

export const teachersOf = (employees: Employee[], includeInactive = false) =>
  employees.filter((e) => e.kind === 'teacher').map(toStaff).filter((t) => includeInactive || t.available);
export const assistantsOf = (employees: Employee[], includeInactive = false) =>
  employees.filter((e) => e.kind === 'assistant').map(toStaff).filter((t) => includeInactive || t.available);

/** Dùng ngoài component (hàm thuần, xử lý dữ liệu) */
export const getTeachers = (includeInactive = false) => teachersOf(useHrStore.getState().employees, includeInactive);
export const getAssistants = (includeInactive = false) => assistantsOf(useHrStore.getState().employees, includeInactive);
export const isAssistant = (id: string) => useHrStore.getState().employees.some((e) => e.id === id && e.kind === 'assistant');

/** Dùng trong component — tự cập nhật khi Nhân sự thêm / sửa / cho nghỉ */
export function useTeachers(includeInactive = false): TeachingStaff[] {
  const employees = useHrStore((s) => s.employees);
  return useMemo(() => teachersOf(employees, includeInactive), [employees, includeInactive]);
}
export function useAssistants(includeInactive = false): TeachingStaff[] {
  const employees = useHrStore((s) => s.employees);
  return useMemo(() => assistantsOf(employees, includeInactive), [employees, includeInactive]);
}

/** Tên GV hoặc trợ giảng (cả người đã nghỉ — để hiện đúng lịch sử) */
export const teacherName = (id: string) => useHrStore.getState().employees.find((e) => e.id === id)?.fullName ?? id;
export const teacherNames = (ids: string[]) => ids.map(teacherName).join(', ');

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
