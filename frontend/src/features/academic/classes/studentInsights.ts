import { useMemo } from 'react';
import dayjs from 'dayjs';
import { summarizeInvoice, useStudentStore, type InvoiceSummary } from '@/features/student/studentStore';
import type { Enrollment, Invoice } from '@/features/student/types';
import type { Curriculum } from '../curriculum/types';
import { useCurriculumStore } from '../curriculum/curriculumStore';
import { useClassStore } from './classStore';
import type { Assignment, AttendanceStatus, CareLog, ClassInfo, ClassSession, ExamResult, Submission } from './types';
import { getSettings } from '@/features/system/settings';

/** Ngưỡng cảnh báo — chỉnh ở 1 chỗ */
/** Ngưỡng cảnh báo bỏ học — đọc từ Cài đặt chung (Hệ thống › Cài đặt chung) */
export const ALERT_RULES = {
  /** Vắng không phép từ N buổi */
  get absent() {
    return getSettings().atRisk.absent;
  },
  /** Vắng liên tiếp từ N buổi */
  get streak() {
    return getSettings().atRisk.streak;
  },
  /** Chuyên cần dưới N% (khi đã học ≥ 4 buổi) */
  get attendanceRate() {
    return getSettings().atRisk.attendanceRate;
  },
  /** Tỉ lệ nộp bài dưới N% */
  get submitRate() {
    return getSettings().atRisk.submitRate;
  },
  /** Điểm (bài tập / thi) dưới N% */
  get scorePct() {
    return getSettings().atRisk.scorePct;
  },
};

export interface AlertFlag {
  key: 'absent' | 'streak' | 'rate' | 'submit' | 'score' | 'exam' | 'tuition';
  label: string;
  tone: 'danger' | 'warning';
}

export interface StudentStats {
  enrollment: Enrollment;
  attendance: {
    timeline: { sessionId: string; no: number; date: string; status?: AttendanceStatus; note?: string; madeUp?: string }[];
    sessions: number;
    present: number;
    late: number;
    excused: number;
    absent: number;
    /** Buổi vắng có phép đã học bù ở lớp khác (tính như có mặt) */
    madeUp: number;
    /** % có mặt (có mặt + muộn + đã học bù) / số buổi đã điểm danh */
    rate?: number;
    /** Số buổi vắng liên tiếp gần nhất */
    streak: number;
  };
  assignments: { list: { assignment: Assignment; submission?: Submission; overdue: boolean }[]; due: number; submitted: number; late: number; avgPct?: number };
  exams: { list: { examId: string; title: string; result: ExamResult; total: number; max: number; pct: number }[]; avgPct?: number };
  tuition?: InvoiceSummary;
  careLogs: CareLog[];
  flags: AlertFlag[];
  /** Điểm rủi ro để sắp xếp (cao = cần chú ý trước) */
  risk: number;
}

const pct = (a: number, b: number) => (b ? Math.round((a / b) * 100) : undefined);

export interface InsightSource {
  sessions: ClassSession[];
  assignments: Assignment[];
  submissions: Submission[];
  examResults: ExamResult[];
  careLogs: CareLog[];
  enrollments: Enrollment[];
  invoices: Invoice[];
  curriculum?: Curriculum;
}

/** Gom số liệu học tập của mọi học viên trong lớp (kể cả đã nghỉ / chuyển lớp — để xem lại hồ sơ) */
export function useClassStudentStats(cls: ClassInfo) {
  const sessions = useClassStore((s) => s.sessions);
  const assignments = useClassStore((s) => s.assignments);
  const submissions = useClassStore((s) => s.submissions);
  const examResults = useClassStore((s) => s.examResults);
  const careLogs = useClassStore((s) => s.careLogs);
  const enrollments = useStudentStore((s) => s.enrollments);
  const invoices = useStudentStore((s) => s.invoices);
  const curriculum = useCurriculumStore((s) => s.curricula.find((c) => c.id === cls.curriculumId));

  return useMemo(
    () => buildClassStats(cls, { sessions, assignments, submissions, examResults, careLogs, enrollments, invoices, curriculum }),
    [sessions, assignments, submissions, examResults, careLogs, enrollments, invoices, curriculum, cls],
  );
}

/** Số liệu của TẤT CẢ lớp (trang Cảnh báo bỏ học, Chứng nhận) */
export function useAllStudentStats(classes: ClassInfo[]) {
  const sessions = useClassStore((s) => s.sessions);
  const assignments = useClassStore((s) => s.assignments);
  const submissions = useClassStore((s) => s.submissions);
  const examResults = useClassStore((s) => s.examResults);
  const careLogs = useClassStore((s) => s.careLogs);
  const enrollments = useStudentStore((s) => s.enrollments);
  const invoices = useStudentStore((s) => s.invoices);
  const curricula = useCurriculumStore((s) => s.curricula);
  return useMemo(
    () =>
      classes.map((cls) => ({
        cls,
        ...buildClassStats(cls, { sessions, assignments, submissions, examResults, careLogs, enrollments, invoices, curriculum: curricula.find((c) => c.id === cls.curriculumId) }),
      })),
    [classes, sessions, assignments, submissions, examResults, careLogs, enrollments, invoices, curricula],
  );
}

/** Hàm thuần: số liệu học viên của 1 lớp */
export function buildClassStats(cls: ClassInfo, { sessions, assignments, submissions, examResults, careLogs, enrollments, invoices, curriculum }: InsightSource) {
  {
    const today = dayjs().format('YYYY-MM-DD');
    const taken = sessions
      .filter((s) => s.classId === cls.id && s.records && s.status !== 'cancelled')
      .sort((a, b) => a.date.localeCompare(b.date) || a.startTime.localeCompare(b.startTime));
    const classAssignments = assignments.filter((a) => a.classId === cls.id).sort((a, b) => a.dueDate.localeCompare(b.dueDate));
    const map = new Map<string, StudentStats>();

    enrollments
      .filter((e) => e.classId === cls.id)
      .forEach((e) => {
        // ---- Chuyên cần
        const timeline = taken.map((s) => {
          const r = s.records!.find((x) => x.studentId === e.studentId);
          return { sessionId: s.id, no: s.no, date: s.date, status: r?.status, note: r?.note, madeUp: r?.madeUp?.classCode };
        });
        const marked = timeline.filter((t) => t.status);
        const count = (k: AttendanceStatus) => marked.filter((t) => t.status === k).length;
        let streak = 0;
        for (let i = marked.length - 1; i >= 0 && marked[i].status === 'absent'; i--) streak++;
        const attendance = {
          timeline,
          sessions: marked.length,
          present: count('present'),
          late: count('late'),
          excused: count('excused'),
          absent: count('absent'),
          madeUp: marked.filter((t) => t.madeUp).length,
          rate: pct(count('present') + count('late') + marked.filter((t) => t.madeUp).length, marked.length),
          streak,
        };

        // ---- Bài tập
        const aList = classAssignments.map((a) => ({
          assignment: a,
          submission: submissions.find((x) => x.assignmentId === a.id && x.studentId === e.studentId),
          overdue: a.dueDate < today,
        }));
        const due = aList.filter((x) => x.overdue);
        const graded = aList.filter((x) => x.submission?.score !== undefined);
        const assignmentsStat = {
          list: aList,
          due: due.length,
          submitted: due.filter((x) => x.submission).length,
          late: aList.filter((x) => x.submission && dayjs(x.submission.submittedAt).isAfter(dayjs(x.assignment.dueDate).endOf('day'))).length,
          avgPct: graded.length ? Math.round(graded.reduce((n, x) => n + (x.submission!.score! / x.assignment.maxScore) * 100, 0) / graded.length) : undefined,
        };

        // ---- Bài thi
        const eList = examResults
          .filter((r) => r.classId === cls.id && r.studentId === e.studentId)
          .map((r) => {
            const total = r.sections.reduce((n, x) => n + x.score, 0);
            const max = r.sections.reduce((n, x) => n + x.max, 0);
            return { examId: r.examId, title: curriculum?.exams.find((x) => x.id === r.examId)?.title ?? 'Bài thi', result: r, total, max, pct: r.status === 'done' && max ? Math.round((total / max) * 100) : 0 };
          });
        const doneExams = eList.filter((x) => x.result.status === 'done');
        const exams = { list: eList, avgPct: doneExams.length ? Math.round(doneExams.reduce((n, x) => n + x.pct, 0) / doneExams.length) : undefined };

        const inv = invoices.find((i) => i.enrollmentId === e.id);
        const tuition = inv && summarizeInvoice(inv);

        // ---- Cờ cảnh báo (chỉ cho học viên đang học)
        const flags: AlertFlag[] = [];
        if (e.status === 'studying') {
          if (attendance.absent >= ALERT_RULES.absent) flags.push({ key: 'absent', label: `Vắng không phép ${attendance.absent} buổi`, tone: 'danger' });
          if (streak >= ALERT_RULES.streak) flags.push({ key: 'streak', label: `Vắng ${streak} buổi liên tiếp`, tone: 'danger' });
          if (attendance.rate !== undefined && attendance.sessions >= 4 && attendance.rate < ALERT_RULES.attendanceRate)
            flags.push({ key: 'rate', label: `Chuyên cần ${attendance.rate}%`, tone: 'warning' });
          const sr = pct(assignmentsStat.submitted, assignmentsStat.due);
          if (assignmentsStat.due >= 2 && sr !== undefined && sr < ALERT_RULES.submitRate)
            flags.push({ key: 'submit', label: `Nộp bài ${assignmentsStat.submitted}/${assignmentsStat.due}`, tone: 'warning' });
          if (assignmentsStat.avgPct !== undefined && assignmentsStat.avgPct < ALERT_RULES.scorePct)
            flags.push({ key: 'score', label: `Điểm bài tập TB ${assignmentsStat.avgPct}%`, tone: 'warning' });
          eList.forEach((x) => {
            if (x.result.status === 'absent') flags.push({ key: 'exam', label: `Vắng ${x.title.toLowerCase()}`, tone: 'danger' });
            else if (x.pct < ALERT_RULES.scorePct) flags.push({ key: 'exam', label: `${x.title}: ${x.total}/${x.max}`, tone: 'danger' });
          });
          if (tuition?.status === 'overdue') flags.push({ key: 'tuition', label: 'Nợ học phí quá hạn', tone: 'warning' });
        }
        const logs = careLogs.filter((c) => c.classId === cls.id && c.studentId === e.studentId);
        map.set(e.studentId, {
          enrollment: e,
          attendance,
          assignments: assignmentsStat,
          exams,
          tuition,
          careLogs: logs,
          flags,
          risk: flags.reduce((n, f) => n + (f.tone === 'danger' ? 3 : 1), 0),
        });
      });
    return { map, sessions: taken };
  }
}
