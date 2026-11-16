import dayjs from 'dayjs';
import { CircleDashed, CircleCheck, Hourglass, MessageCircle, PhoneOff, XCircle, type LucideIcon } from 'lucide-react';
import type { Tone } from '@/components/common';
import type { Course } from '@/features/academic/courses/types';
import type { Lead, LeadStudentLink, Student } from '@/features/crm/types';
import { ALERT_RULES, type StudentStats } from '../classes/studentInsights';
import { effectiveStart } from '../classes/constants';
import type { ClassInfo, StudentEvaluation } from '../classes/types';
import type { PromotionCandidate, PromotionStatus } from './types';

/** Giữ chỗ đến trước ngày khai giảng lớp B bao nhiêu ngày */
export const HOLD_DAYS_BEFORE_START = 7;

type View = PromotionStatus | 'released';

export const PROMOTION_STATUS: Record<View, { label: string; tone: Tone; icon: LucideIcon }> = {
  pending: { label: 'Chờ tư vấn', tone: 'warning', icon: CircleDashed },
  consulting: { label: 'Đang tư vấn', tone: 'info', icon: MessageCircle },
  unreachable: { label: 'Chưa liên hệ được', tone: 'neutral', icon: PhoneOff },
  confirmed: { label: 'Đã ghi danh', tone: 'success', icon: CircleCheck },
  declined: { label: 'Không học tiếp', tone: 'danger', icon: XCircle },
  released: { label: 'Hết hạn giữ chỗ', tone: 'neutral', icon: Hourglass },
};

export const DECLINE_REASONS = ['Bận lịch học / công việc', 'Học phí', 'Chuyển sang trung tâm khác', 'Tự học / tạm nghỉ', 'Đã đạt mục tiêu', 'Muốn lớp khác (giờ học, giáo viên)', 'Khác'];

const today = () => dayjs().format('YYYY-MM-DD');

/** Hạn giữ chỗ mặc định cho lớp B (không sớm hơn 3 ngày tới) */
export function holdUntilFor(cls: ClassInfo) {
  const d = dayjs(effectiveStart(cls)).subtract(HOLD_DAYS_BEFORE_START, 'day');
  const min = dayjs().add(3, 'day');
  return (d.isBefore(min) ? min : d).format('YYYY-MM-DD');
}

/** Đang chiếm chỗ trong lớp B? (chưa chốt / từ chối và còn hạn) */
export const seatActive = (c: PromotionCandidate) => c.seatHeld && (c.status === 'pending' || c.status === 'consulting' || c.status === 'unreachable') && (!c.holdUntil || c.holdUntil >= today());

/** Trạng thái hiển thị (thêm "Hết hạn giữ chỗ" suy ra từ ngày) */
export const viewStatus = (c: PromotionCandidate): View =>
  c.seatHeld && c.holdUntil && c.holdUntil < today() && (c.status === 'pending' || c.status === 'consulting' || c.status === 'unreachable') ? 'released' : c.status;

/** Còn cần Sale xử lý */
export const isOpen = (c: PromotionCandidate) => c.status === 'pending' || c.status === 'consulting' || c.status === 'unreachable';

// ---------------- Khóa / lớp kế tiếp ----------------

/** Khóa kế tiếp: đầu vào = đầu ra của khóa hiện tại; ưu tiên cùng nhóm, cùng hình thức, cùng loại GV & học offline/online */
export function nextCoursesOf(course: Course | undefined, courses: Course[]): Course[] {
  if (!course) return [];
  const score = (c: Course) => (c.category === course.category ? 4 : 0) + (c.format === course.format ? 2 : 0) + (c.mode === course.mode ? 1 : 0) + (c.teacherType === course.teacherType ? 1 : 0);
  return courses
    .filter((c) => c.status === 'active' && c.levelFrom === course.levelTo && c.id !== course.id)
    .sort((a, b) => score(b) - score(a));
}

/** Lớp có thể lên: các lớp "Chờ khai giảng"; ưu tiên lớp mở riêng cho lớp A, khóa kế tiếp, cùng chi nhánh */
export function targetClassesFor(from: ClassInfo, classes: ClassInfo[], nextCourseIds: string[]) {
  const score = (c: ClassInfo) => (c.previousClassId === from.id ? 8 : 0) + (nextCourseIds.includes(c.courseId) ? 4 : 0) + (c.branchId === from.branchId ? 2 : 0);
  return classes
    .filter((c) => c.status === 'enrolling' && c.id !== from.id)
    .map((c) => ({ cls: c, recommended: score(c) >= 4 }))
    .sort((a, b) => score(b.cls) - score(a.cls) || effectiveStart(a.cls).localeCompare(effectiveStart(b.cls)));
}

// ---------------- Gợi ý ----------------

export interface Suggestion {
  /** Gợi ý giữ chỗ */
  hold: boolean;
  source: 'teacher' | 'stats';
  reason: string;
}

/** Học viên nào nên được giữ chỗ: ưu tiên đề xuất trong nhận xét cuối khóa của GV, không có thì xét chuyên cần + điểm thi */
export function suggestionFor(st: StudentStats | undefined, ev: StudentEvaluation | undefined): Suggestion {
  if (ev) {
    const hold = ev.recommendation === 'next_level' || ev.recommendation === 'advanced' || ev.recommendation === 'jlpt';
    return { hold, source: 'teacher', reason: hold ? 'GV đề xuất học tiếp' : ev.recommendation === 'repeat' ? 'GV đề xuất học lại / ôn tập' : 'GV có đề xuất riêng' };
  }
  const att = st?.attendance.rate;
  const exam = st?.exams.avgPct;
  const goodAtt = att !== undefined && att >= ALERT_RULES.attendanceRate;
  const goodExam = exam === undefined || exam >= ALERT_RULES.scorePct;
  return {
    hold: goodAtt && goodExam,
    source: 'stats',
    reason: goodAtt && goodExam ? `Chuyên cần ${att}%${exam !== undefined ? ` · thi ${exam}%` : ''}` : !goodAtt ? `Chuyên cần thấp (${att ?? 0}%)` : `Điểm thi thấp (${exam}%)`,
  };
}

// ---------------- Người quyết định ----------------

export interface Decider {
  /** self: học viên tự quyết · guardian: phụ huynh / người giám hộ quyết định */
  kind: 'self' | 'guardian';
  /** Lead của người quyết định (phụ huynh, hoặc lead của chính học viên) */
  leadId?: string;
  name: string;
  phone?: string;
  /** Nhóm theo người liên hệ (1 phụ huynh nhiều con → 1 nhóm) */
  groupKey: string;
  saleId?: string;
  minor: boolean;
}

export const ageOf = (dob?: string) => (dob ? dayjs().diff(dayjs(dob), 'year') : undefined);

/**
 * Ai quyết định học tiếp:
 *  - Có phụ huynh là người liên hệ chính (quan hệ "Con") hoặc học viên dưới 18 tuổi có người thân liên kết → PHỤ HUYNH
 *  - Còn lại → học viên tự quyết
 * Sale phụ trách = Sale của lead người quyết định (không có thì Sale của lead bất kỳ liên kết với học viên).
 */
export function deciderOf(studentId: string, students: Student[], leads: Lead[], links: LeadStudentLink[]): Decider {
  const st = students.find((s) => s.id === studentId);
  const mine = links.filter((l) => l.studentId === studentId);
  const age = ageOf(st?.dob);
  const minor = age !== undefined && age < 18;
  const guardianLink = mine.find((l) => l.primary && l.relationship !== 'self') ?? mine.find((l) => l.relationship === 'child') ?? (minor ? mine.find((l) => l.relationship !== 'self') : undefined);
  const leadOf = (id?: string) => leads.find((l) => l.id === id);
  const anySale = mine.map((l) => leadOf(l.leadId)?.assigneeId).find(Boolean);
  if (guardianLink && (minor || guardianLink.relationship === 'child')) {
    const g = leadOf(guardianLink.leadId);
    return { kind: 'guardian', leadId: g?.id, name: g?.fullName ?? 'Phụ huynh', phone: g?.phone, groupKey: `lead:${guardianLink.leadId}`, saleId: g?.assigneeId ?? anySale, minor };
  }
  const selfLead = leadOf(mine.find((l) => l.relationship === 'self')?.leadId);
  return { kind: 'self', leadId: selfLead?.id, name: st?.fullName ?? studentId, phone: st?.phone ?? selfLead?.phone, groupKey: `student:${studentId}`, saleId: selfLead?.assigneeId ?? anySale, minor };
}
