import { useCallback, useMemo } from 'react';
import { useCrmStore } from '@/features/crm/crmStore';
import { useStudentStore } from '@/features/student/studentStore';
import { deciderOf, seatActive, type Decider } from './promotionUtils';
import { usePromotionStore } from './promotionStore';

/** Tra người quyết định (học viên / phụ huynh) của 1 học viên */
export function useDecider(): (studentId: string) => Decider {
  const students = useCrmStore((s) => s.students);
  const leads = useCrmStore((s) => s.leads);
  const links = useCrmStore((s) => s.links);
  return useCallback((id: string) => deciderOf(id, students, leads, links), [students, leads, links]);
}

/** Sĩ số lớp (đích) gồm chỗ đang được giữ cho học viên lên lớp */
export function useSeats(classId: string, max: number) {
  const enrollments = useStudentStore((s) => s.enrollments);
  const candidates = usePromotionStore((s) => s.candidates);
  return useMemo(() => {
    const enrolled = enrollments.filter((e) => e.classId === classId && (e.status === 'studying' || e.status === 'reserved')).length;
    const held = candidates.filter((c) => c.toClassId === classId && seatActive(c)).length;
    return { enrolled, held, max, free: Math.max(0, max - enrolled - held) };
  }, [enrollments, candidates, classId, max]);
}
