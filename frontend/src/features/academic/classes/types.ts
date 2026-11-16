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
  /** 1 GV (hoặc 2 với lớp Việt + Nhật) — id nhân viên (GV) trong Nhân sự */
  teacherIds: string[];
  /** Trợ giảng (không bắt buộc) — id nhân viên (trợ giảng) trong Nhân sự */
  assistantIds?: string[];
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
  /** Lớp kế tiếp (lên lớp): mở để đón học viên từ lớp này */
  previousClassId?: string;
  cancelReason?: string;
  note?: string;
  createdAt: string;
}

export type SessionStatus = 'scheduled' | 'completed' | 'cancelled' | 'makeup';

/**
 * Nguồn gốc buổi học:
 *  - regular: sinh từ lịch của lớp (mặc định khi không có)
 *  - makeup:  học bù cho 1 buổi bị hủy (thay thế buổi đó, tính vào số buổi khóa)
 *  - extra:   học thêm để kịp tiến độ — NGOÀI số buổi của khóa, không bị xóa khi "Tạo lại lịch"
 */
export type SessionOrigin = 'regular' | 'makeup' | 'extra';

/** Điểm danh học viên: có mặt · đi muộn (vẫn tính có mặt) · vắng có phép · vắng không phép */
export type AttendanceStatus = 'present' | 'late' | 'excused' | 'absent';

/** Cách ghi nhận: HV tự quét QR · GV điểm danh trên Cổng GV · nhân viên trung tâm điểm danh hộ */
export type AttendanceMethod = 'qr' | 'teacher' | 'staff';

export interface AttendanceRecord {
  studentId: string;
  status: AttendanceStatus;
  note?: string;
  /** Giờ vào lớp (HH:mm) — giờ quét QR hoặc giờ được điểm danh */
  checkInAt?: string;
  method?: AttendanceMethod;
  /** Về sớm giữa buổi (đau bụng, việc gấp...) — HH:mm */
  leftAt?: string;
  leaveReason?: string;
  /** Vắng theo ĐƠN XIN NGHỈ đã duyệt (Cổng HV / PH) */
  leaveRequestId?: string;
  /** Buổi vắng này đã được HỌC BÙ ở lớp khác → tính như có mặt khi xét chuyên cần */
  madeUp?: { sessionId: string; classCode: string; status: 'present' | 'late' };
  /** HV lớp KHÁC đến học bù buổi này (không thuộc sĩ số lớp) */
  makeupFor?: { sessionId: string; classId: string; leaveRequestId: string };
}

/** Nhân viên trung tâm hỗ trợ trong buổi (GV / HV không tự thao tác được trên hệ thống) */
export type SupportKind = 'attendance' | 'early_leave' | 'teacher_checkin' | 'incident' | 'room' | 'other';

export interface SupportEntry {
  id: string;
  kind: SupportKind;
  note: string;
  by: string;
  at: string;
}

/** Vai trò người đứng lớp trong buổi */
export type StaffRole = 'teacher' | 'substitute' | 'assistant';

/** Chấm công giáo viên / trợ giảng cho 1 buổi (check-in / check-out) */
export interface TeacherAttendance {
  /** id nhân viên (GV / trợ giảng) trong Nhân sự */
  teacherId: string;
  /** Không có = suy theo lớp (GV lớp / trợ giảng) */
  role?: StaffRole;
  /** self = GV tự chấm trên Cổng giáo viên · staff = Giáo vụ chấm hộ / sửa */
  source?: 'self' | 'staff';
  /** Thời điểm chấm (lần cuối) */
  checkedAt?: string;
  status: 'present' | 'late' | 'absent';
  /** HH:mm */
  checkIn?: string;
  checkOut?: string;
  /** GV vắng → người dạy thay (id nhân viên (GV) trong Nhân sự) */
  substituteId?: string;
  note?: string;
}

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
  /** Điểm danh: có mặt / sĩ số (tổng hợp từ records) */
  attendance?: { present: number; total: number };
  /** Điểm danh chi tiết từng học viên */
  records?: AttendanceRecord[];
  /** Người điểm danh + thời điểm */
  attendanceBy?: { name: string; at: string };
  /** Chấm công giáo viên + trợ giảng */
  teacherAttendance?: TeacherAttendance[];
  /** Ghi đè trợ giảng của lớp cho riêng buổi này */
  assistantIds?: string[];
  /** Nhân viên xác nhận buổi đã diễn ra → căn cứ tính lương GV, trừ buổi trong gói học phí */
  confirmation?: { by: string; at: string; note?: string };
  /** Sự cố của giáo viên giữa buổi (ốm, việc gấp...) */
  incident?: SessionIncident;
  /** Nhật ký hỗ trợ của nhân viên trong buổi */
  supportLog?: SupportEntry[];
  /** Nhật ký buổi học — giáo viên ghi sau buổi dạy (Cổng giáo viên) */
  teacherLog?: SessionLog;

  /** Không có = regular */
  origin?: SessionOrigin;
  /** Buổi học bù: id buổi bị hủy được bù */
  makeupForId?: string;
  /** Ghi đè GV / phòng của lớp cho riêng buổi này (dạy thay, đổi phòng) */
  teacherIds?: string[];
  room?: string;
  /** Đã dời lịch: ngày + giờ gốc */
  rescheduledFrom?: { date: string; startTime: string; endTime: string; reason?: string };
  /** Hủy bởi ai, lúc nào */
  cancelledBy?: { name: string; at: string };
}

export type IncidentReason = 'faint' | 'sick' | 'family' | 'accident' | 'tech' | 'other';
/** Cách xử lý: tạm dừng lớp (chờ xử lý) · GV khác vào dạy tiếp · trợ giảng dạy tiếp · kết thúc sớm (xếp bù phần còn lại) */
export type IncidentAction = 'pause' | 'substitute' | 'assistant' | 'end_early';

export interface SessionIncident {
  /** Giờ xảy ra, HH:mm */
  at: string;
  reason: IncidentReason;
  detail?: string;
  action: IncidentAction;
  /** Người dạy tiếp phần còn lại (GV dạy thay hoặc trợ giảng) */
  takeoverId?: string;
  /** Đã báo học viên / phụ huynh */
  notified?: boolean;
  /** Tạm dừng → kết quả: lớp học tiếp (lúc nào, ai dạy) hoặc cho nghỉ luôn */
  outcome?: { kind: 'resumed' | 'ended'; at: string; teacherId?: string; by: string };
  reportedBy: string;
  reportedAt: string;
}

export interface SessionLog {
  /** Đã dạy gì, lớp tiếp thu thế nào */
  content: string;
  /** Bài tập về nhà đã dặn */
  homework?: string;
  /** Vấn đề cần trung tâm xử lý (HV vắng nhiều, thiết bị hỏng...) */
  issues?: string;
  /** Mức hoàn thành nội dung bài của buổi (báo cáo khi GV bấm "Kết thúc buổi học") */
  coverage?: LessonCoverage;
  /** Các phần (chapter) của bài đã dạy xong */
  coveredChapterIds?: string[];
  /** Lúc GV bấm "Kết thúc buổi học" lần đầu — báo cáo vẫn sửa được sau đó */
  endedAt?: string;
  author: string;
  at: string;
}

/** Tiến độ buổi: dạy xong bài · dạy được một phần (phần còn lại dời buổi sau) · chưa dạy được */
export type LessonCoverage = 'full' | 'partial' | 'none';

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
  /** Bài "upload": đề bài / tài liệu GV tự gửi (không lấy từ trung tâm) */
  attachments?: SubmissionFile[];
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

// ---------------- Thông báo cho lớp ----------------

export type AnnouncementAudience = 'student' | 'parent' | 'teacher';
export type AnnouncementChannel = 'portal' | 'zalo' | 'email';

export interface ClassAnnouncement {
  id: string;
  classId: string;
  title: string;
  content: string;
  audience: AnnouncementAudience[];
  channels: AnnouncementChannel[];
  /** Chỉ gửi học viên còn nợ học phí (mẫu nhắc học phí) */
  debtorsOnly?: boolean;
  /** Chỉ gửi các học viên này (+ phụ huynh) — gửi từ thao tác hàng loạt; bỏ trống = cả lớp */
  studentIds?: string[];
  /** Số người nhận lúc gửi */
  recipients: number;
  /** Số người đã xem (Cổng / Zalo) */
  readCount?: number;
  sentBy: string;
  sentAt: string;
  /** Hẹn giờ gửi — chưa tới giờ thì là "Chờ gửi" */
  scheduledAt?: string;
}

// ---------------- Đánh giá giáo viên ----------------

export type ReviewPeriod = 'mid' | 'final';
export type ReviewCriterion = 'method' | 'clarity' | 'enthusiasm' | 'punctuality';

/** Học viên đánh giá giáo viên (khảo sát giữa kỳ / cuối kỳ trên Cổng học viên) */
export interface TeacherReview {
  id: string;
  classId: string;
  teacherId: string;
  studentId: string;
  period: ReviewPeriod;
  /** 1–5 sao */
  overall: number;
  ratings: Record<ReviewCriterion, number>;
  comment?: string;
  /** Ẩn danh → không hiện tên học viên với giáo viên */
  anonymous: boolean;
  createdAt: string;
}

// ---------------- Chăm sóc học viên ----------------

/** Ghi nhận đã liên hệ học viên / phụ huynh (từ danh sách "Học viên cần chú ý") */
export interface CareLog {
  id: string;
  classId: string;
  studentId: string;
  channel: 'call' | 'zalo' | 'meet';
  note: string;
  by: string;
  at: string;
}

// ---------------- Nhận xét cuối khóa ----------------

export type EvalSkill = 'vocab' | 'grammar' | 'reading' | 'listening' | 'speaking';
/** Đề xuất của GV cho bước học tiếp theo */
export type EvalRecommendation = 'next_level' | 'advanced' | 'jlpt' | 'repeat' | 'other';

/**
 * Nhận xét CUỐI KHÓA của giáo viên về 1 học viên (Cổng GV › Lớp của tôi › Học viên).
 * Khác nhận xét bài tập / bài thi: đây là đánh giá tổng thể cả khóa — căn cứ để Sale tư vấn
 * học viên đăng ký khóa tiếp theo. Mỗi học viên / lớp có 1 bản, GV sửa được.
 */
export interface StudentEvaluation {
  id: string;
  classId: string;
  studentId: string;
  /** id nhân viên (GV) viết */
  teacherId: string;
  author: string;
  /** 1–5 sao từng kỹ năng */
  skills: Record<EvalSkill, number>;
  /** Thái độ, chuyên cần học tập: 1–5 */
  attitude: number;
  strengths?: string;
  weaknesses?: string;
  recommendation: EvalRecommendation;
  /** Ghi chú cho trung tâm / Sale khi tư vấn (lớp phù hợp, lịch học, lưu ý...) */
  advice?: string;
  createdAt: string;
  updatedAt: string;
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
