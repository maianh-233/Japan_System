import dayjs from 'dayjs';
import type { Course } from '@/features/academic/courses/types';

/**
 * Dữ liệu giả — Khóa học. Phủ đủ các tổ hợp:
 * lớp nhóm / 1 kèm 1 × GV Việt / GV Nhật / kết hợp × offline / online, kèm khóa nháp & ngừng tuyển sinh.
 * Lớp học (mocks/data/students.ts → MOCK_CLASSES) trỏ về khóa qua `courseId`.
 */

const ago = (days: number) => dayjs().subtract(days, 'day').hour(9).minute(0).second(0).toISOString();
const ALL = ['cg', 'q1', 'hc'];

type Seed = Omit<Course, 'createdAt' | 'updatedAt'> & { age: number };

const seeds: Seed[] = [
  {
    id: 'CRS-01', code: 'N5-G-OFF', name: 'JLPT N5 cơ bản', category: 'jlpt', levelFrom: 'beginner', levelTo: 'N5',
    format: 'group', teacherType: 'vn', mode: 'offline', sessions: 48, minutesPerSession: 90, sessionsPerWeek: 3,
    fee: 4_900_000, maxStudents: 20, branchIds: ALL, status: 'active', age: 400,
    description: 'Bảng chữ cái Hiragana, Katakana, 100 Kanji, ngữ pháp và từ vựng N5. Giáo trình Minna no Nihongo 1 (bài 1–25).',
  },
  {
    id: 'CRS-02', code: 'N4-G-OFF', name: 'JLPT N4', category: 'jlpt', levelFrom: 'N5', levelTo: 'N4',
    format: 'group', teacherType: 'vn', mode: 'offline', sessions: 48, minutesPerSession: 120, sessionsPerWeek: 2,
    fee: 5_900_000, maxStudents: 20, branchIds: ALL, status: 'active', age: 380,
    description: 'Minna no Nihongo 2 (bài 26–50), 300 Kanji, luyện đề N4.',
  },
  {
    id: 'CRS-03', code: 'N3-G-OFF', name: 'JLPT N3', category: 'jlpt', levelFrom: 'N4', levelTo: 'N3',
    format: 'group', teacherType: 'vn', mode: 'offline', sessions: 60, minutesPerSession: 90, sessionsPerWeek: 3,
    fee: 6_800_000, maxStudents: 18, branchIds: ['cg', 'q1'], status: 'active', age: 360,
  },
  {
    id: 'CRS-04', code: 'N2-G-ON', name: 'JLPT N2 Online', category: 'jlpt', levelFrom: 'N3', levelTo: 'N2',
    format: 'group', teacherType: 'mixed', mode: 'online', sessions: 72, minutesPerSession: 90, sessionsPerWeek: 2,
    fee: 7_500_000, maxStudents: 15, branchIds: ALL, status: 'active', age: 200,
    description: 'GV Việt dạy ngữ pháp – đọc hiểu, GV Nhật luyện nghe hiểu 1 buổi/tuần. Học qua Zoom.',
  },
  {
    id: 'CRS-05', code: 'KW-B1-G-OFF', name: 'Kaiwa B1 với người Nhật', category: 'kaiwa', levelFrom: 'N4', levelTo: 'N3',
    format: 'group', teacherType: 'jp', mode: 'offline', sessions: 20, minutesPerSession: 120, sessionsPerWeek: 1,
    fee: 5_200_000, maxStudents: 12, branchIds: ['q1'], status: 'active', age: 150,
  },
  {
    id: 'CRS-06', code: 'KIDS-G-OFF', name: 'Tiếng Nhật thiếu nhi (7–12 tuổi)', category: 'kids', levelFrom: 'beginner', levelTo: 'N5',
    format: 'group', teacherType: 'vn', mode: 'offline', sessions: 36, minutesPerSession: 90, sessionsPerWeek: 2,
    fee: 4_500_000, maxStudents: 15, branchIds: ['cg'], status: 'active', age: 180,
  },
  {
    id: 'CRS-07', code: 'XKLD-G-OFF', name: 'Tiếng Nhật đi làm / XKLĐ cấp tốc', category: 'xkld', levelFrom: 'beginner', levelTo: 'N4',
    format: 'group', teacherType: 'vn', mode: 'offline', sessions: 60, minutesPerSession: 180, sessionsPerWeek: 5,
    fee: 8_000_000, maxStudents: 25, branchIds: ['hc'], status: 'active', age: 220,
  },
  {
    id: 'CRS-08', code: 'KW-P-ON-JP', name: 'Kaiwa 1-1 với giáo viên Nhật', category: 'kaiwa', levelFrom: 'N5', levelTo: 'N3',
    format: 'private', teacherType: 'jp', mode: 'online', sessions: 20, minutesPerSession: 60, sessionsPerWeek: 2,
    fee: 450_000, branchIds: ALL, status: 'active', age: 120,
    description: 'Chọn giờ linh hoạt, học viên đặt lịch trước 24h. Phù hợp người đi làm.',
  },
  {
    id: 'CRS-09', code: 'N3-P-ON-VN', name: 'Kèm 1-1 luyện thi N3', category: 'jlpt', levelFrom: 'N4', levelTo: 'N3',
    format: 'private', teacherType: 'vn', mode: 'online', sessions: 30, minutesPerSession: 90, sessionsPerWeek: 2,
    fee: 300_000, branchIds: ALL, status: 'active', age: 90,
  },
  {
    id: 'CRS-10', code: 'N2-P-OFF-JP', name: 'Kèm 1-1 N2 với giáo viên Nhật', category: 'jlpt', levelFrom: 'N3', levelTo: 'N2',
    format: 'private', teacherType: 'jp', mode: 'offline', sessions: 24, minutesPerSession: 90, sessionsPerWeek: 2,
    fee: 600_000, branchIds: ['cg', 'q1'], status: 'active', age: 60,
  },
  {
    id: 'CRS-11', code: 'N1-G-ON', name: 'JLPT N1 Online', category: 'jlpt', levelFrom: 'N2', levelTo: 'N1',
    format: 'group', teacherType: 'mixed', mode: 'online', sessions: 80, minutesPerSession: 90, sessionsPerWeek: 2,
    fee: 9_500_000, maxStudents: 12, branchIds: ALL, status: 'draft', age: 5,
    description: 'Đang soạn lộ trình — dự kiến khai giảng tháng 2.',
  },
  {
    id: 'CRS-12', code: 'BIZ-P-ON-JP', name: 'Tiếng Nhật thương mại 1-1', category: 'business', levelFrom: 'N3', levelTo: 'N2',
    format: 'private', teacherType: 'jp', mode: 'online', sessions: 16, minutesPerSession: 90, sessionsPerWeek: 1,
    fee: 700_000, branchIds: ['q1'], status: 'paused', age: 300,
    description: 'Tạm ngưng do thiếu giáo viên Nhật có kinh nghiệm doanh nghiệp.',
  },
  {
    id: 'CRS-13', code: 'N5-G-ON', name: 'JLPT N5 Online', category: 'jlpt', levelFrom: 'beginner', levelTo: 'N5',
    format: 'group', teacherType: 'vn', mode: 'online', sessions: 48, minutesPerSession: 90, sessionsPerWeek: 3,
    fee: 3_900_000, maxStudents: 25, branchIds: ALL, status: 'active', age: 100,
  },
  {
    id: 'CRS-14', code: 'XKLD-INT-P-OFF', name: 'Luyện phỏng vấn XKLĐ 1-1', category: 'xkld', levelFrom: 'N5', levelTo: 'N4',
    format: 'private', teacherType: 'jp', mode: 'offline', sessions: 10, minutesPerSession: 60, sessionsPerWeek: 3,
    fee: 400_000, branchIds: ['hc'], status: 'active', age: 70,
  },
];

export const MOCK_COURSES: Course[] = seeds.map(({ age, ...c }) => ({ ...c, createdAt: ago(age), updatedAt: ago(Math.floor(age / 3)) }));
