import type { JlptLevel } from '../courses/types';

/**
 * Lộ trình (Curriculum) = nội dung giảng dạy.
 *
 *  Curriculum
 *   ├─ Lesson[]  (bài học, có thứ tự)
 *   │    └─ Chapter[]  (phần nhỏ trong bài: từ vựng, ngữ pháp, nghe...)
 *   │         └─ Material[]  TÀI LIỆU giảng dạy: pdf, doc, ppt, excel, audio, video, link Drive...
 *   └─ Exam[]    MỐC THI giữa kỳ / cuối kỳ — chèn SAU một lesson.
 *                Lộ trình chỉ đánh dấu "thi lúc nào, phạm vi bài nào". Đề + câu hỏi được tạo khi
 *                TỔ CHỨC KỲ THI (lấy câu hỏi từ Ngân hàng câu hỏi), không đính kèm ở đây.
 *
 *  Course ──< CourseCurriculum >── Curriculum      (NHIỀU–NHIỀU)
 *   - Mỗi khóa có đúng 1 lộ trình MẶC ĐỊNH (thường là bậc Chuẩn) cho lớp mở mới
 *   - Lớp có đầu vào tốt → chọn lộ trình bậc NÂNG CAO của cùng khóa
 *  ClassInfo.curriculumId: lớp chốt lộ trình lúc mở lớp.
 *
 *  Phiên bản: lộ trình ĐÃ PHÁT HÀNH thì khóa nội dung (lớp đang học theo nó).
 *  Muốn sửa → "Tạo phiên bản mới" (bản nháp) → sửa → phát hành.
 */

export type CurriculumStatus = 'published' | 'draft' | 'archived';

/** Bậc lộ trình trong cùng một khóa */
export type CurriculumTier = 'standard' | 'advanced';

export type MaterialType = 'pdf' | 'doc' | 'ppt' | 'xls' | 'audio' | 'video' | 'drive' | 'link';

export interface Material {
  id: string;
  name: string;
  type: MaterialType;
  /** URL file đã tải lên hoặc link ngoài (Drive, YouTube...) */
  url: string;
  /** Byte — chỉ file tải lên */
  size?: number;
  /** true = học viên xem được trên cổng; false = chỉ giáo viên (giáo án, đáp án) */
  sharedWithStudents: boolean;
}

export interface Chapter {
  id: string;
  title: string;
  materials: Material[];
}

export interface Lesson {
  id: string;
  title: string;
  chapters: Chapter[];
  /** Có bài kiểm tra nhỏ cuối lesson */
  hasQuiz?: boolean;
}

export type ExamKind = 'midterm' | 'final';

/** Mốc thi trong lộ trình. Phạm vi kiến thức = các bài từ sau mốc thi trước đến `afterLessonId`. */
export interface Exam {
  id: string;
  kind: ExamKind;
  title: string;
  /** Thi sau lesson này */
  afterLessonId: string;
  /** Ghi chú cho người tạo kỳ thi (vd: "Tập trung nghe hiểu") */
  note?: string;
}

export interface Curriculum {
  id: string;
  code: string;
  name: string;
  level: JlptLevel;
  tier: CurriculumTier;
  /** Điều kiện áp dụng (bậc nâng cao), vd "Điểm test đầu vào TB lớp ≥ 80%" */
  entryRequirement?: string;
  version: string;
  textbook?: string;
  description?: string;
  tags: string[];
  status: CurriculumStatus;
  lessons: Lesson[];
  exams: Exam[];
  author: string;
  updatedAt: string;
}

export interface CourseCurriculum {
  courseId: string;
  curriculumId: string;
  isDefault: boolean;
  linkedAt: string;
}
