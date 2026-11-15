/**
 * Khóa học = "sản phẩm" trung tâm bán. Lớp học (ClassInfo) được mở THEO khóa học.
 *
 * Mỗi khóa xác định 3 lựa chọn độc lập:
 *  - format       Lớp nhóm  | 1 kèm 1 (học riêng với giáo viên)
 *  - teacherType  GV Việt   | GV Nhật | Kết hợp Việt + Nhật
 *  - mode         Offline   | Online
 * → cùng nội dung N3 có thể bán thành nhiều khóa: "N3 nhóm offline GV Việt", "N3 1-1 online GV Nhật"...
 *
 * Học phí:
 *  - Lớp nhóm: giá TRỌN KHÓA / học viên
 *  - 1 kèm 1:  giá MỖI BUỔI (tổng = số buổi × giá buổi)
 */

export type JlptLevel = 'beginner' | 'N5' | 'N4' | 'N3' | 'N2' | 'N1';
export type CourseCategory = 'jlpt' | 'kaiwa' | 'kids' | 'business' | 'xkld';
export type CourseFormat = 'group' | 'private';
export type TeacherType = 'vn' | 'jp' | 'mixed';
export type StudyMode = 'offline' | 'online';
export type CourseStatus = 'active' | 'paused' | 'draft';

export interface Course {
  id: string;
  code: string;
  name: string;
  category: CourseCategory;
  /** Trình độ đầu vào → đầu ra */
  levelFrom: JlptLevel;
  levelTo: JlptLevel;
  format: CourseFormat;
  teacherType: TeacherType;
  mode: StudyMode;
  /** Tổng số buổi */
  sessions: number;
  minutesPerSession: number;
  sessionsPerWeek: number;
  /** Lớp nhóm: giá trọn khóa. 1 kèm 1: giá mỗi buổi */
  fee: number;
  /** Chỉ lớp nhóm */
  maxStudents?: number;
  /** Chi nhánh được bán khóa này */
  branchIds: string[];
  description?: string;
  status: CourseStatus;
  createdAt: string;
  updatedAt: string;
}
