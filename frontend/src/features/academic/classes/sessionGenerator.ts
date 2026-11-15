import dayjs from 'dayjs';
import type { Curriculum } from '../curriculum/types';
import { HOLIDAYS, weekdayOf } from './constants';
import type { ClassSession, Weekday } from './types';

export interface GenerateInput {
  classId: string;
  startDate: string;
  weekdays: Weekday[];
  startTime: string;
  endTime: string;
  /** Tổng số buổi cần sinh (gồm cả buổi thi) */
  count: number;
  skipHolidays: boolean;
  /** Có → gán bài học tuần tự + chèn buổi thi theo mốc thi của lộ trình */
  curriculum?: Pick<Curriculum, 'lessons' | 'exams'>;
  /** Bắt đầu đánh số từ (khi chỉ sinh lại phần còn lại) */
  startNo?: number;
  /** Bỏ qua N buổi đầu của chuỗi bài/thi (đã dạy) */
  skipSlots?: number;
}

export interface GeneratedPlan {
  sessions: ClassSession[];
  /** Ngày bị bỏ qua vì nghỉ lễ */
  skipped: { date: string; name: string }[];
  endDate?: string;
}

/**
 * Chuỗi nội dung theo lộ trình: mỗi bài chiếm ~ (số buổi học ÷ số bài) buổi, buổi thi chèn ngay sau bài của mốc thi.
 * Không có lộ trình → toàn bộ là buổi học chưa gán bài.
 */
export function buildSlots(count: number, curriculum?: Pick<Curriculum, 'lessons' | 'exams'>): { kind: 'lesson' | 'exam'; lessonId?: string; examId?: string }[] {
  if (!curriculum?.lessons.length) return Array.from({ length: count }, () => ({ kind: 'lesson' as const }));
  const { lessons, exams } = curriculum;
  const examCount = Math.min(exams.length, Math.max(0, count - lessons.length));
  const lessonSlots = Math.max(lessons.length, count - examCount);
  const base = Math.floor(lessonSlots / lessons.length);
  let extra = lessonSlots % lessons.length;
  const slots: { kind: 'lesson' | 'exam'; lessonId?: string; examId?: string }[] = [];
  lessons.forEach((l) => {
    const n = base + (extra-- > 0 ? 1 : 0);
    for (let i = 0; i < n; i++) slots.push({ kind: 'lesson', lessonId: l.id });
    exams.filter((e) => e.afterLessonId === l.id).forEach((e) => slots.push({ kind: 'exam', examId: e.id }));
  });
  return slots.slice(0, count);
}

/** Sinh buổi học theo lịch tuần, bỏ ngày lễ, gán bài học / buổi thi theo lộ trình */
export function generateSessions(input: GenerateInput): GeneratedPlan {
  const holidays = new Map(HOLIDAYS.map((h) => [h.date, h.name]));
  const slots = buildSlots(input.count + (input.skipSlots ?? 0), input.curriculum).slice(input.skipSlots ?? 0);
  const sessions: ClassSession[] = [];
  const skipped: GeneratedPlan['skipped'] = [];
  let d = dayjs(input.startDate);
  let guard = 0;
  if (!input.weekdays.length || input.count <= 0) return { sessions, skipped };

  while (sessions.length < input.count && guard++ < 2000) {
    if (input.weekdays.includes(weekdayOf(d))) {
      const key = d.format('YYYY-MM-DD');
      const holiday = holidays.get(key);
      if (input.skipHolidays && holiday) skipped.push({ date: key, name: holiday });
      else {
        const slot = slots[sessions.length] ?? { kind: 'lesson' as const };
        const no = (input.startNo ?? 1) + sessions.length;
        sessions.push({
          id: `${input.classId}-S${no}-${Date.now().toString(36)}`,
          classId: input.classId,
          no,
          date: key,
          startTime: input.startTime,
          endTime: input.endTime,
          kind: slot.kind,
          lessonId: slot.lessonId,
          examId: slot.examId,
          status: 'scheduled',
        });
      }
    }
    d = d.add(1, 'day');
  }
  return { sessions, skipped, endDate: sessions[sessions.length - 1]?.date };
}
