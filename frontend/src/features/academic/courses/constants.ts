import { Building2, CirclePause, CirclePlay, FilePen, Languages, Monitor, User, Users, type LucideIcon } from 'lucide-react';
import type { Tone } from '@/components/common';
import { formatCurrency } from '@/lib/format';
import type { Course, CourseCategory, CourseFormat, CourseStatus, JlptLevel, StudyMode, TeacherType } from './types';

type Meta = { label: string; tone: Tone; icon: LucideIcon };
const toOptions = <K extends string>(rec: Record<K, { label: string } | string>) =>
  (Object.keys(rec) as K[]).map((k) => ({ value: k, label: typeof rec[k] === 'string' ? (rec[k] as string) : (rec[k] as { label: string }).label }));

export const LEVELS: Record<JlptLevel, string> = {
  beginner: 'Nhập môn',
  N5: 'N5',
  N4: 'N4',
  N3: 'N3',
  N2: 'N2',
  N1: 'N1',
};
export const LEVEL_ORDER: JlptLevel[] = ['beginner', 'N5', 'N4', 'N3', 'N2', 'N1'];
export const LEVEL_OPTIONS = toOptions(LEVELS);

export const CATEGORIES: Record<CourseCategory, string> = {
  jlpt: 'Luyện thi JLPT',
  kaiwa: 'Giao tiếp (Kaiwa)',
  kids: 'Thiếu nhi',
  business: 'Tiếng Nhật doanh nghiệp',
  xkld: 'Đi làm / XKLĐ',
};
export const CATEGORY_OPTIONS = toOptions(CATEGORIES);

export const FORMATS: Record<CourseFormat, Meta & { short: string }> = {
  group: { label: 'Lớp nhóm', short: 'Nhóm', tone: 'primary', icon: Users },
  private: { label: '1 kèm 1', short: '1-1', tone: 'warning', icon: User },
};
export const FORMAT_OPTIONS = toOptions(FORMATS);

export const TEACHER_TYPES: Record<TeacherType, Meta & { short: string }> = {
  vn: { label: 'Giáo viên Việt', short: 'GV Việt', tone: 'info', icon: Languages },
  jp: { label: 'Giáo viên Nhật', short: 'GV Nhật', tone: 'primary', icon: Languages },
  mixed: { label: 'Kết hợp Việt + Nhật', short: 'Việt + Nhật', tone: 'success', icon: Languages },
};
export const TEACHER_TYPE_OPTIONS = toOptions(TEACHER_TYPES);

export const STUDY_MODES: Record<StudyMode, Meta> = {
  offline: { label: 'Offline', tone: 'neutral', icon: Building2 },
  online: { label: 'Online', tone: 'info', icon: Monitor },
};
export const STUDY_MODE_OPTIONS = toOptions(STUDY_MODES);

export const COURSE_STATUS: Record<CourseStatus, Meta> = {
  active: { label: 'Đang tuyển sinh', tone: 'success', icon: CirclePlay },
  paused: { label: 'Ngừng tuyển sinh', tone: 'neutral', icon: CirclePause },
  draft: { label: 'Nháp', tone: 'warning', icon: FilePen },
};
export const COURSE_STATUS_OPTIONS = toOptions(COURSE_STATUS);

/** "N4 → N3", "Mới bắt đầu → N5", "N3" */
export const levelRange = (c: Pick<Course, 'levelFrom' | 'levelTo'>) =>
  c.levelFrom === c.levelTo ? LEVELS[c.levelTo] : `${LEVELS[c.levelFrom]} → ${LEVELS[c.levelTo]}`;

/** Tổng học phí trọn khóa của 1 học viên */
export const courseTotalFee = (c: Pick<Course, 'format' | 'fee' | 'sessions'>) => (c.format === 'private' ? c.fee * c.sessions : c.fee);

/** "6.800.000 ₫ / khóa" hoặc "450.000 ₫ / buổi" */
export const feeLabel = (c: Pick<Course, 'format' | 'fee'>) => `${formatCurrency(c.fee)} / ${c.format === 'private' ? 'buổi' : 'khóa'}`;

/** Tổng số giờ học */
export const totalHours = (c: Pick<Course, 'sessions' | 'minutesPerSession'>) => Math.round((c.sessions * c.minutesPerSession) / 60);
