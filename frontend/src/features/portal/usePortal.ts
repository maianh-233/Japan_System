import { useMemo } from 'react';
import dayjs from 'dayjs';
import { useClassStore } from '@/features/academic/classes/classStore';
import { buildClassStats, type StudentStats } from '@/features/academic/classes/studentInsights';
import { sessionRoom, sessionTeachers, startOf } from '@/features/academic/classes/sessionUtils';
import { teacherNames } from '@/features/academic/classes/constants';
import type { ClassInfo, ClassSession } from '@/features/academic/classes/types';
import { useCurriculumStore } from '@/features/academic/curriculum/curriculumStore';
import { useCrmStore } from '@/features/crm/crmStore';
import type { Student } from '@/features/crm/types';
import { useStudentStore } from '@/features/student/studentStore';
import type { Enrollment } from '@/features/student/types';
import { useAuth } from '@/hooks/useAuth';
import { usePortalStore } from './portalStore';

/**
 * Tài khoản demo → hồ sơ (khi có backend: API trả về teacherId / studentId / danh sách con).
 */
const TEACHER_OF: Record<string, string> = { 'u-teacher': 't-ha' };
const STUDENT_OF: Record<string, string> = { 'u-student': 'ST-1' };
const PARENT_LEAD_OF: Record<string, string> = { 'u-parent': 'L-0001' };

export function useTeacherId(): string {
  const { user } = useAuth();
  return user?.portalOwnerId ?? (user && TEACHER_OF[user.id]) ?? (user?.portal === 'teacher' ? user.id : 't-ha');
}

/** Lớp GV đang / đã dạy (là GV chính của lớp) */
export function useTeachingClasses(): ClassInfo[] {
  const id = useTeacherId();
  const classes = useClassStore((s) => s.classes);
  return useMemo(() => classes.filter((c) => c.teacherIds.includes(id) && c.status !== 'cancelled'), [classes, id]);
}

/** Danh sách con của phụ huynh (qua liên kết lead ↔ học viên) */
export function useChildren(): Student[] {
  const { user } = useAuth();
  const links = useCrmStore((s) => s.links);
  const students = useCrmStore((s) => s.students);
  const leadId = user?.portalOwnerId ?? (user ? PARENT_LEAD_OF[user.id] : undefined);
  return useMemo(
    () => links.filter((k) => k.leadId === leadId).map((k) => students.find((s) => s.id === k.studentId)).filter((s): s is Student => !!s),
    [links, students, leadId],
  );
}

/** Học viên đang xem: cổng học viên = chính mình; cổng phụ huynh = con đang chọn */
export function useStudentId(): string {
  const { user } = useAuth();
  const children = useChildren();
  const childId = usePortalStore((s) => s.childId);
  if (user?.portal === 'parent') return (childId && children.some((c) => c.id === childId) ? childId : children[0]?.id) ?? 'ST-1';
  return user?.portalOwnerId ?? (user && STUDENT_OF[user.id]) ?? 'ST-1';
}

export interface MyClass {
  cls: ClassInfo;
  enrollment: Enrollment;
  stats: StudentStats | undefined;
}

/** Lớp của học viên (mọi ghi danh) + số liệu học tập của học viên trong từng lớp */
export function useStudentLearning(studentId: string): MyClass[] {
  const classes = useClassStore((s) => s.classes);
  const sessions = useClassStore((s) => s.sessions);
  const assignments = useClassStore((s) => s.assignments);
  const submissions = useClassStore((s) => s.submissions);
  const examResults = useClassStore((s) => s.examResults);
  const careLogs = useClassStore((s) => s.careLogs);
  const enrollments = useStudentStore((s) => s.enrollments);
  const invoices = useStudentStore((s) => s.invoices);
  const curricula = useCurriculumStore((s) => s.curricula);

  return useMemo(() => {
    const mine = enrollments.filter((e) => e.studentId === studentId);
    return mine
      .map((enrollment): MyClass | null => {
        const cls = classes.find((c) => c.id === enrollment.classId);
        if (!cls) return null;
        const { map } = buildClassStats(cls, {
          sessions,
          assignments,
          submissions,
          examResults,
          careLogs,
          enrollments,
          invoices,
          curriculum: curricula.find((c) => c.id === cls.curriculumId),
        });
        return { cls, enrollment, stats: map.get(studentId) };
      })
      .filter((x): x is MyClass => !!x)
      .sort((a, b) => Number(b.enrollment.status === 'studying') - Number(a.enrollment.status === 'studying'));
  }, [studentId, classes, sessions, assignments, submissions, examResults, careLogs, enrollments, invoices, curricula]);
}

/** Lớp đang học (ghi danh studying / reserved) */
export const activeOf = (list: MyClass[]) => list.filter((x) => x.enrollment.status === 'studying' || x.enrollment.status === 'reserved');

/** Buổi học → sự kiện thời khóa biểu */
export function sessionEvent(s: ClassSession, cls: ClassInfo, opts?: { subtitle?: string }) {
  const room = sessionRoom(s, cls);
  return {
    id: s.id,
    start: startOf(s),
    end: dayjs(`${s.date}T${s.endTime}`),
    title: `${cls.code.replace(/^GV[VN]*_/, '')}${s.kind === 'exam' ? ' · THI' : ''}`,
    subtitle: opts?.subtitle ?? `Buổi ${s.no} · ${teacherNames(sessionTeachers(s, cls))}`,
    location: cls.online ? 'Online' : (room ?? ''),
    tone: s.status === 'cancelled' ? ('neutral' as const) : s.kind === 'exam' ? ('danger' as const) : s.status === 'makeup' || s.origin === 'makeup' ? ('warning' as const) : cls.online ? ('info' as const) : ('primary' as const),
    cancelled: s.status === 'cancelled',
  };
}

/** Mã lớp ngắn (bỏ tiền tố loại GV) */
export const shortCode = (cls: Pick<ClassInfo, 'code'>) => cls.code.replace(/^GV[VN]*_/, '');
