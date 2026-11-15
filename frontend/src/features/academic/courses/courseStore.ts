import { create } from 'zustand';
import { MOCK_COURSES } from '@/mocks/data/courses';
import type { Course, CourseStatus } from './types';

/**
 * Store KHÓA HỌC GIẢ LẬP (trong bộ nhớ, mất khi tải lại trang).
 * Khi có backend: thay bằng gọi API, giữ nguyên chữ ký.
 */

export type CourseInput = Omit<Course, 'id' | 'createdAt' | 'updatedAt'>;

interface CourseState {
  courses: Course[];
  addCourse: (input: CourseInput) => Course;
  updateCourse: (id: string, patch: Partial<CourseInput>) => void;
  setStatus: (id: string, status: CourseStatus) => void;
  /** Mã khóa đã tồn tại? (không phân biệt hoa thường) */
  isCodeTaken: (code: string, excludeId?: string) => boolean;
}

const now = () => new Date().toISOString();

export const useCourseStore = create<CourseState>((set, get) => ({
  courses: MOCK_COURSES,

  addCourse: (input) => {
    const course: Course = { ...input, id: `CRS-${Date.now()}`, createdAt: now(), updatedAt: now() };
    set((s) => ({ courses: [course, ...s.courses] }));
    return course;
  },

  updateCourse: (id, patch) =>
    set((s) => ({ courses: s.courses.map((c) => (c.id === id ? { ...c, ...patch, updatedAt: now() } : c)) })),

  setStatus: (id, status) =>
    set((s) => ({ courses: s.courses.map((c) => (c.id === id ? { ...c, status, updatedAt: now() } : c)) })),

  isCodeTaken: (code, excludeId) => get().courses.some((c) => c.id !== excludeId && c.code.toLowerCase() === code.trim().toLowerCase()),
}));
