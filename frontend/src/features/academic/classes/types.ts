/**
 * Lớp học — mở THEO một khóa học (Course), học theo một lộ trình (Curriculum) chốt lúc mở lớp.
 *
 *  ClassInfo
 *   ├─ Enrollment[] (features/student)  học viên ghi danh + hóa đơn học phí
 *   ├─ ClassSession[]                   buổi học (tự sinh từ lịch: thứ trong tuần + giờ học + tổng giờ)
 *   └─ Assignment[]                     bài tập giao cho lớp
 *  Bài thi của lớp = các MỐC THI trong lộ trình; đề tạo khi tổ chức thi (lấy từ Ngân hàng câu hỏi).
 *
 *  Mã lớp: tiền tố theo loại giáo viên của khóa — GVV_ (Việt) · GVN_ (Nhật) · GVVN_ (Việt + Nhật).
 *  Số buổi = tổng giờ ÷ số giờ mỗi buổi (giờ kết thúc − giờ bắt đầu, thường 2 tiếng).
 *
 *  Vòng đời: enrolling (chờ khai giảng) → ongoing → finished
 *                       ↘ paused ↔ ongoing      ↘ cancelled (từ mọi trạng thái chưa kết thúc)
 *  Chỉ khi "enrolling" mới sửa được toàn bộ thông tin; trạng thái khác chỉ sửa một số trường.
 */

export type ClassStatus = 'enrolling' | 'ongoing' | 'paused' | 'finished' | 'cancelled';

/** 1 = Thứ 2 ... 7 = Chủ nhật */
export type Weekday = 1 | 2 | 3 | 4 | 5 | 6 | 7;

export interface ClassInfo {
  id: string;
  courseId: string;
  curriculumId?: string;
  /** Mã đầy đủ, vd GVV_N3-K12 */
  code: string;
  name: string;
  /** Nhãn khóa (giữ để hiển thị nhanh) */
  courseLabel: string;
  branchId: string;
  /** 1 GV (hoặc 2 với lớp Việt + Nhật) — id trong TEACHERS */
  teacherIds: string[];
  room?: string;
  online?: boolean;
  meetingUrl?: string;

  plannedStartDate: string;
  plannedEndDate: string;
  /** Lùi so với dự kiến nếu chưa đủ học viên */
  actualStartDate?: string;
  actualEndDate?: string;

  weekdays: Weekday[];
  /** HH:mm */
  startTime: string;
  endTime: string;
  totalHours: number;

  minStudents: number;
  maxStudents: number;
  /** Học phí / học viên (lấy từ khóa, có thể điều chỉnh khi mở lớp) */
  fee: number;

  status: ClassStatus;
  cancelReason?: string;
  note?: string;
  createdAt: string;
}

export type SessionStatus = 'scheduled' | 'completed' | 'cancelled' | 'makeup';

export interface ClassSession {
  id: string;
  classId: string;
  /** Số thứ tự buổi (1-based) */
  no: number;
  date: string;
  startTime: string;
  endTime: string;
  kind: 'lesson' | 'exam';
  /** Bài học trong lộ trình (kind = lesson) */
  lessonId?: string;
  /** Mốc thi trong lộ trình (kind = exam) */
  examId?: string;
  status: SessionStatus;
  /** Lý do hủy / ghi chú */
  note?: string;
  /** Điểm danh: có mặt / sĩ số */
  attendance?: { present: number; total: number };
  /** Nhật ký buổi học — giáo viên ghi sau buổi dạy (Cổng giáo viên) */
  teacherLog?: SessionLog;
}

export interface SessionLog {
  /** Đã dạy gì, lớp tiếp thu thế nào */
  content: string;
  /** Bài tập về nhà đã dặn */
  homework?: string;
  /** Vấn đề cần trung tâm xử lý (HV vắng nhiều, thiết bị hỏng...) */
  issues?: string;
  author: string;
  at: string;
}

/**
 * 2 loại bài tập:
 *  - upload: học viên nộp ảnh / file / ghi âm → giáo viên chấm tay
 *  - quiz:   làm trắc nghiệm trên Cổng học viên, câu hỏi BỐC TỪ NGÂN HÀNG CÂU HỎI theo tiêu chí → hệ thống tự chấm
 */
export type AssignmentType = 'upload' | 'quiz';

export type QuizSkill = 'vocab' | 'kanji' | 'grammar' | 'reading' | 'listening';
export type QuizDifficulty = 'easy' | 'medium' | 'hard' | 'mixed';

/** Tiêu chí bốc câu hỏi từ Ngân hàng câu hỏi */
export interface QuizConfig {
  lessonIds: string[];
  skills: QuizSkill[];
  difficulty: QuizDifficulty;
  questionCount: number;
  /** Phút — bỏ trống = không giới hạn */
  timeLimitMinutes?: number;
  /** Số lần được làm (lấy điểm cao nhất) */
  attempts: number;
  /** Mỗi học viên 1 đề khác (đảo câu + đảo đáp án) */
  shuffle: boolean;
}

export interface Assignment {
  id: string;
  classId: string;
  type: AssignmentType;
  title: string;
  lessonId?: string;
  description?: string;
  assignedAt: string;
  dueDate: string;
  /** Thang điểm (mặc định 10) */
  maxScore: number;
  quiz?: QuizConfig;
}

export interface SubmissionFile {
  name: string;
  kind: 'pdf' | 'doc' | 'image' | 'audio' | 'link';
  url: string;
  size?: number;
}

/** Bài nộp của 1 học viên cho 1 bài tập (chưa nộp = không có bản ghi) */
export interface Submission {
  id: string;
  assignmentId: string;
  studentId: string;
  submittedAt: string;
  /** Bài "upload": tệp nộp. Bài "quiz": rỗng */
  files: SubmissionFile[];
  /** Bài "quiz": kết quả tự chấm (lần cao nhất) */
  quiz?: { correct: number; total: number; attempts: number; durationMinutes: number };
  /** Lời nhắn / bài làm dạng chữ */
  content?: string;
  score?: number;
  comment?: string;
  gradedAt?: string;
  gradedBy?: string;
}

/** Kết quả 1 học viên trong 1 kỳ thi (mốc thi của lộ trình) của lớp */
export interface ExamResult {
  id: string;
  classId: string;
  examId: string;
  studentId: string;
  status: 'done' | 'absent';
  /** Điểm từng phần — tổng = điểm bài thi */
  sections: { name: string; score: number; max: number }[];
  startedAt?: string;
  durationMinutes?: number;
  comment?: string;
}
