import { useMemo } from 'react';
import dayjs, { type Dayjs } from 'dayjs';
import { useClassStore } from '@/features/academic/classes/classStore';
import type { Assignment, AttendanceStatus, ClassInfo, ClassSession, SessionLog, Submission } from '@/features/academic/classes/types';
import { useCurriculumStore } from '@/features/academic/curriculum/curriculumStore';
import type { Curriculum } from '@/features/academic/curriculum/types';
import { summarizeInvoice, useStudentStore } from '@/features/student/studentStore';
import type { Enrollment, Invoice } from '@/features/student/types';
import { formatCurrency } from '@/lib/format';
import { hashString } from '@/lib/text';

/**
 * BÁO CÁO TUẦN cho phụ huynh — dựng hoàn toàn từ dữ liệu sẵn có (không cần giáo viên viết thêm):
 *  chuyên cần từng buổi · bài tập tới hạn trong tuần (đã nộp / điểm / nhận xét) · điểm thi · nhật ký giáo viên · tuần tới · học phí.
 * Tuần tính từ thứ 2 đến Chủ nhật.
 */

export type WeekAttendance = AttendanceStatus | 'none' | 'upcoming' | 'cancelled';

export interface WeeklyReport {
  studentId: string;
  weekStart: Dayjs;
  weekEnd: Dayjs;
  classes: ClassInfo[];
  sessions: { session: ClassSession; cls: ClassInfo; status: WeekAttendance; checkInAt?: string; log?: SessionLog }[];
  attendance: { held: number; present: number; late: number; excused: number; absent: number; unmarked: number };
  assignments: { a: Assignment; cls: ClassInfo; sub?: Submission; score?: number; overdue: boolean }[];
  exams: { title: string; cls: ClassInfo; total: number; max: number; pct: number; comment?: string; absent: boolean }[];
  next: { sessions: number; first?: { session: ClassSession; cls: ClassInfo }; exams: { title: string; date: string }[]; due: { title: string; dueDate: string }[] };
  tuition: { remaining: number; dueDate?: string; overdue: boolean };
}

/** Thứ 2 của tuần chứa ngày `d` */
export const weekStartOf = (d: Dayjs) => d.subtract((d.day() + 6) % 7, 'day').startOf('day');
export const weekLabel = (start: Dayjs) => `${start.format('DD/MM')}–${start.add(6, 'day').format('DD/MM')}`;
/** Mã link xem báo cáo (giả lập — khi có backend: token ký số, có hạn) */
export const reportLink = (studentId: string, start: Dayjs) => `jpcenter.vn/bc/${Math.abs(hashString(`${studentId}:${start.format('YYYY-MM-DD')}`)).toString(36).slice(0, 6).toUpperCase()}`;

/** Điểm quy về thang của bài (quiz: đúng / tổng × thang điểm) */
const scoreOf = (a: Assignment, s?: Submission) => (s?.score !== undefined ? s.score : s?.quiz ? Math.round((s.quiz.correct / s.quiz.total) * a.maxScore * 10) / 10 : undefined);

export function buildWeeklyReport(
  studentId: string,
  weekStart: Dayjs,
  d: { enrollments: Enrollment[]; invoices: Invoice[]; classes: ClassInfo[]; sessions: ClassSession[]; assignments: Assignment[]; submissions: Submission[]; examResults: import('@/features/academic/classes/types').ExamResult[]; curricula: Curriculum[] },
  now = dayjs(),
): WeeklyReport {
  const start = weekStartOf(weekStart);
  const end = start.add(6, 'day').endOf('day');
  const inWeek = (date: string) => !dayjs(date).isBefore(start) && !dayjs(date).isAfter(end);
  const mine = d.enrollments.filter((e) => e.studentId === studentId && ['studying', 'reserved', 'completed'].includes(e.status));
  const classes = mine.map((e) => d.classes.find((c) => c.id === e.classId)).filter((c): c is ClassInfo => !!c);
  const examTitle = (cls: ClassInfo, examId?: string) => d.curricula.find((c) => c.id === cls.curriculumId)?.exams.find((x) => x.id === examId)?.title ?? 'Bài thi';

  const sessions = classes
    .flatMap((cls) =>
      d.sessions
        .filter((s) => s.classId === cls.id && inWeek(s.date))
        .map((session) => {
          const rec = session.records?.find((r) => r.studentId === studentId);
          const status: WeekAttendance =
            session.status === 'cancelled' ? 'cancelled' : dayjs(`${session.date}T${session.startTime}`).isAfter(now) ? 'upcoming' : rec?.madeUp ? rec.madeUp.status : (rec?.status ?? 'none');
          return { session, cls, status, checkInAt: rec?.checkInAt, log: session.teacherLog };
        }),
    )
    .sort((a, b) => `${a.session.date}${a.session.startTime}`.localeCompare(`${b.session.date}${b.session.startTime}`));

  const held = sessions.filter((x) => !['upcoming', 'cancelled'].includes(x.status));
  const attendance = {
    held: held.length,
    present: held.filter((x) => x.status === 'present').length,
    late: held.filter((x) => x.status === 'late').length,
    excused: held.filter((x) => x.status === 'excused').length,
    absent: held.filter((x) => x.status === 'absent').length,
    unmarked: held.filter((x) => x.status === 'none').length,
  };

  const assignments = classes
    .flatMap((cls) =>
      d.assignments
        .filter((a) => a.classId === cls.id && inWeek(a.dueDate))
        .map((a) => {
          const sub = d.submissions.find((s) => s.assignmentId === a.id && s.studentId === studentId);
          return { a, cls, sub, score: scoreOf(a, sub), overdue: !sub && dayjs(a.dueDate).endOf('day').isBefore(now) };
        }),
    )
    .sort((x, y) => x.a.dueDate.localeCompare(y.a.dueDate));

  const exams = sessions
    .filter((x) => x.session.kind === 'exam' && x.session.examId)
    .map((x) => {
      const r = d.examResults.find((e) => e.classId === x.cls.id && e.examId === x.session.examId && e.studentId === studentId);
      if (!r) return null;
      const total = r.sections.reduce((t, s) => t + s.score, 0);
      const max = r.sections.reduce((t, s) => t + s.max, 0) || 1;
      return { title: examTitle(x.cls, x.session.examId), cls: x.cls, total, max, pct: Math.round((total / max) * 100), comment: r.comment, absent: r.status === 'absent' };
    })
    .filter((x): x is NonNullable<typeof x> => !!x);

  // Tuần tới
  const nStart = start.add(7, 'day');
  const nEnd = nStart.add(6, 'day').endOf('day');
  const inNext = (date: string) => !dayjs(date).isBefore(nStart) && !dayjs(date).isAfter(nEnd);
  const nextSessions = classes
    .flatMap((cls) => d.sessions.filter((s) => s.classId === cls.id && s.status !== 'cancelled' && inNext(s.date)).map((session) => ({ session, cls })))
    .sort((a, b) => `${a.session.date}${a.session.startTime}`.localeCompare(`${b.session.date}${b.session.startTime}`));
  const next = {
    sessions: nextSessions.length,
    first: nextSessions[0],
    exams: nextSessions.filter((x) => x.session.kind === 'exam').map((x) => ({ title: examTitle(x.cls, x.session.examId), date: x.session.date })),
    due: classes.flatMap((cls) => d.assignments.filter((a) => a.classId === cls.id && inNext(a.dueDate) && !d.submissions.some((s) => s.assignmentId === a.id && s.studentId === studentId))).map((a) => ({ title: a.title, dueDate: a.dueDate })),
  };

  // Học phí (các ghi danh đang học)
  const invs = d.invoices.filter((i) => mine.some((e) => e.id === i.enrollmentId) && !i.cancelled);
  const owing = invs.map((i) => ({ i, s: summarizeInvoice(i) })).filter((x) => x.s.remaining > 0).sort((a, b) => a.i.dueDate.localeCompare(b.i.dueDate));
  const tuition = { remaining: owing.reduce((t, x) => t + x.s.remaining, 0), dueDate: owing[0]?.i.dueDate, overdue: owing.some((x) => x.s.status === 'overdue') };

  return { studentId, weekStart: start, weekEnd: end, classes, sessions, attendance, assignments, exams, next, tuition };
}

/** Câu tóm tắt ngắn cho tin Zalo (≤ ~200 ký tự) */
export function reportSummary(r: WeeklyReport): string {
  const parts: string[] = [];
  const a = r.attendance;
  if (a.held) parts.push(`Đi học ${a.present + a.late}/${a.held} buổi${a.late ? ` (${a.late} muộn)` : ''}${a.absent ? `, vắng ${a.absent}` : ''}${a.excused ? `, nghỉ phép ${a.excused}` : ''}`);
  else parts.push('Tuần này không có buổi học');
  if (r.assignments.length) {
    const done = r.assignments.filter((x) => x.sub).length;
    const scored = r.assignments.filter((x) => x.score !== undefined);
    const avg = scored.length ? scored.reduce((t, x) => t + (x.score! / x.a.maxScore) * 10, 0) / scored.length : undefined;
    parts.push(`Bài tập ${done}/${r.assignments.length}${avg !== undefined ? `, điểm TB ${avg.toLocaleString('vi-VN', { maximumFractionDigits: 1 })}/10` : ''}`);
  }
  r.exams.forEach((e) => parts.push(e.absent ? `Vắng ${e.title.toLowerCase()}` : `${e.title}: ${e.total}/${e.max}`));
  if (r.next.exams.length) parts.push(`Tuần tới có ${r.next.exams.map((e) => `${e.title.toLowerCase()} (${dayjs(e.date).format('DD/MM')})`).join(', ')}`);
  if (r.tuition.overdue) parts.push(`Học phí quá hạn ${formatCurrency(r.tuition.remaining)}`);
  return parts.join(' · ');
}

/** Báo cáo tuần của 1 học viên (dùng trong cổng phụ huynh) */
export function useWeeklyReport(studentId: string, weekStart: Dayjs): WeeklyReport {
  const enrollments = useStudentStore((s) => s.enrollments);
  const invoices = useStudentStore((s) => s.invoices);
  const classes = useClassStore((s) => s.classes);
  const sessions = useClassStore((s) => s.sessions);
  const assignments = useClassStore((s) => s.assignments);
  const submissions = useClassStore((s) => s.submissions);
  const examResults = useClassStore((s) => s.examResults);
  const curricula = useCurriculumStore((s) => s.curricula);
  const key = weekStart.format('YYYY-MM-DD');
  return useMemo(
    () => buildWeeklyReport(studentId, dayjs(key), { enrollments, invoices, classes, sessions, assignments, submissions, examResults, curricula }),
    [studentId, key, enrollments, invoices, classes, sessions, assignments, submissions, examResults, curricula],
  );
}
