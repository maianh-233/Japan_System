/**
 * Nối trang Khóa học với backend thật (/api/v1/academic/courses — user story 09, KH-01 → KH-06).
 * Đọc rồi ĐỔ VÀO useCourseStore.courses (Id mock CRS-01… nhờ seedIds) + useCurriculumStore.links (khóa ↔ lộ trình)
 * — các trang còn chạy dữ liệu giả (Lớp học, Tuyển sinh…) vẫn tra được khóa.
 * Người không có quyền trang Khóa học → danh sách rút gọn (/options: khóa đang tuyển sinh) cho ô chọn.
 */
import { api, ApiError } from '@/lib/api/http';
import { fromGuid, fromGuids, toGuid, toGuids } from '@/lib/api/ids';
import { useCurriculumStore } from '../curriculum/curriculumStore';
import type { CourseCurriculum } from '../curriculum/types';
import { useCourseStore, type CourseInput } from './courseStore';
import type { Course, CourseCategory, CourseFormat, CourseStatus, JlptLevel, StudyMode, TeacherType } from './types';

interface ServerCourse {
  id: string;
  code: string;
  name: string;
  category: CourseCategory;
  levelFrom: JlptLevel;
  levelTo: JlptLevel;
  format: CourseFormat;
  teacherType: TeacherType;
  mode: StudyMode;
  sessions: number;
  minutesPerSession: number;
  sessionsPerWeek: number;
  fee: number;
  maxStudents?: number | null;
  branchIds: string[];
  description?: string | null;
  status: CourseStatus;
  curricula: { curriculumId: string; isDefault: boolean; linkedAt: string }[];
  openClasses: string[];
  createdAt: string;
  updatedAt: string;
  rowVersion: number;
}

interface ServerOption {
  id: string;
  code: string;
  name: string;
  category: CourseCategory;
  levelFrom: JlptLevel;
  levelTo: JlptLevel;
  format: CourseFormat;
  teacherType: TeacherType;
  mode: StudyMode;
  fee: number;
  branchIds: string[];
  defaultCurriculumId?: string | null;
}

const versions = new Map<string, number>();

function toCourse(c: ServerCourse): Course {
  const id = fromGuid(c.id);
  versions.set(id, c.rowVersion);
  return {
    id,
    code: c.code,
    name: c.name,
    category: c.category,
    levelFrom: c.levelFrom,
    levelTo: c.levelTo,
    format: c.format,
    teacherType: c.teacherType,
    mode: c.mode,
    sessions: c.sessions,
    minutesPerSession: c.minutesPerSession,
    sessionsPerWeek: c.sessionsPerWeek,
    fee: c.fee,
    maxStudents: c.maxStudents ?? undefined,
    branchIds: fromGuids(c.branchIds),
    description: c.description ?? undefined,
    status: c.status,
    createdAt: c.createdAt,
    updatedAt: c.updatedAt,
  };
}

const linksOf = (c: ServerCourse): CourseCurriculum[] =>
  c.curricula.map((l) => ({ courseId: fromGuid(c.id), curriculumId: fromGuid(l.curriculumId), isDefault: l.isDefault, linkedAt: l.linkedAt }));

/** Ghi 1 khóa (vừa thêm / sửa) vào store: khóa + liên kết lộ trình của khóa đó */
export function upsertCourse(c: ServerCourse): Course {
  const course = toCourse(c);
  useCourseStore.setState((s) => ({
    courses: s.courses.some((x) => x.id === course.id) ? s.courses.map((x) => (x.id === course.id ? course : x)) : [course, ...s.courses],
  }));
  useCurriculumStore.setState((s) => ({ links: [...s.links.filter((l) => l.courseId !== course.id), ...linksOf(c)] }));
  return course;
}

/** Tải khóa học. Không có quyền trang → khóa đang tuyển sinh (/options) cho ô chọn ở trang khác */
export async function loadCourses() {
  try {
    const list = await api<ServerCourse[]>('/academic/courses');
    useCourseStore.setState({ courses: list.map(toCourse) });
    useCurriculumStore.setState({ links: list.flatMap(linksOf) });
  } catch (e) {
    if (!(e instanceof ApiError) || e.status !== 403) throw e;
    const options = await api<ServerOption[]>('/academic/courses/options');
    useCourseStore.setState({
      courses: options.map((o) => ({
        id: fromGuid(o.id),
        code: o.code,
        name: o.name,
        category: o.category,
        levelFrom: o.levelFrom,
        levelTo: o.levelTo,
        format: o.format,
        teacherType: o.teacherType,
        mode: o.mode,
        sessions: 0,
        minutesPerSession: 0,
        sessionsPerWeek: 0,
        fee: o.fee,
        branchIds: fromGuids(o.branchIds),
        status: 'active',
        createdAt: '',
        updatedAt: '',
      })),
    });
  }
}

const body = (v: CourseInput) => ({
  code: v.code,
  name: v.name,
  category: v.category,
  levelFrom: v.levelFrom,
  levelTo: v.levelTo,
  format: v.format,
  teacherType: v.teacherType,
  mode: v.mode,
  sessions: v.sessions,
  minutesPerSession: v.minutesPerSession,
  sessionsPerWeek: v.sessionsPerWeek,
  fee: v.fee,
  maxStudents: v.format === 'group' ? (v.maxStudents ?? null) : null,
  branchIds: toGuids(v.branchIds),
  description: v.description || null,
});

/** KH-02 thêm · KH-03 nhân bản (server tự gắn các lộ trình của khóa gốc) */
export async function createCourse(v: CourseInput, cloneFromId?: string): Promise<Course> {
  return upsertCourse(await api<ServerCourse>('/academic/courses', { body: { ...body(v), cloneFromId: cloneFromId ? toGuid(cloneFromId) : null } }));
}

/** KH-04 — server chặn sửa đè (rowVersion) */
export async function updateCourse(id: string, v: CourseInput) {
  upsertCourse(await api<ServerCourse>(`/academic/courses/${toGuid(id)}`, { method: 'PUT', body: { ...body(v), rowVersion: versions.get(id) ?? 0 } }));
}

/** KH-05 — mở tuyển sinh cần lộ trình mặc định đã phát hành */
export const activateCourse = async (id: string) => void upsertCourse(await api<ServerCourse>(`/academic/courses/${toGuid(id)}/activate`, { method: 'POST' }));

export const pauseCourse = async (id: string) => void upsertCourse(await api<ServerCourse>(`/academic/courses/${toGuid(id)}/pause`, { method: 'POST' }));

/** KH-06 */
export const linkCurriculum = async (courseId: string, curriculumId: string, isDefault: boolean) =>
  void upsertCourse(await api<ServerCourse>(`/academic/courses/${toGuid(courseId)}/curricula`, { body: { curriculumId: toGuid(curriculumId), isDefault } }));

export const setDefaultCurriculum = async (courseId: string, curriculumId: string) =>
  void upsertCourse(await api<ServerCourse>(`/academic/courses/${toGuid(courseId)}/curricula/${toGuid(curriculumId)}/default`, { method: 'PUT' }));

export const unlinkCurriculum = async (courseId: string, curriculumId: string) =>
  void upsertCourse(await api<ServerCourse>(`/academic/courses/${toGuid(courseId)}/curricula/${toGuid(curriculumId)}`, { method: 'DELETE' }));
