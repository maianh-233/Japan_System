/**
 * Dữ liệu riêng của các CỔNG (giáo viên / học viên / phụ huynh).
 * Lớp, buổi, bài tập, bài nộp, điểm danh, học phí… dùng CHUNG store với cổng nhân viên — sửa ở đâu cũng thấy ở đâu.
 */

export type RequestStatus = 'pending' | 'approved' | 'rejected';

/** GV xin hủy 1 buổi (chỉ trung tâm hủy được cả lớp) — giáo vụ duyệt, xếp người dạy thay / học bù */
export interface CancelRequest {
  id: string;
  sessionId: string;
  classId: string;
  teacherId: string;
  reasonType: string;
  reason: string;
  /** Đề xuất ngày dạy bù */
  makeupAt?: string;
  status: RequestStatus;
  createdAt: string;
  decidedBy?: string;
  decidedAt?: string;
  /** Kết quả xử lý: "Đã mời GV Phạm Đức Long dạy thay" */
  note?: string;
}

export type InviteStatus = 'pending' | 'accepted' | 'declined' | 'expired';

/** Giáo vụ mời GV khác dạy thay 1 buổi — nhận thì buổi chuyển sang GV đó, tự tính vào lương (vai trò dạy thay) */
export interface SubInvite {
  id: string;
  sessionId: string;
  classId: string;
  /** GV vắng */
  fromTeacherId: string;
  toTeacherId: string;
  reason: string;
  /** Người mời (giáo vụ) */
  invitedBy: string;
  createdAt: string;
  /** Hạn trả lời */
  respondBy: string;
  status: InviteStatus;
  respondedAt?: string;
  declineReason?: string;
}

/** Tài liệu GV tự soạn — chỉ gửi cho lớp mình dạy (khác Thư viện của trung tâm) */
export interface TeacherDoc {
  id: string;
  teacherId: string;
  title: string;
  description?: string;
  fileName: string;
  kind: 'pdf' | 'doc' | 'image' | 'audio' | 'video' | 'link';
  size?: number;
  url?: string;
  classIds: string[];
  /** Gắn với buổi học (không bắt buộc) */
  sessionId?: string;
  createdAt: string;
  /** Học viên đã xem */
  viewedBy: string[];
}

/** Đơn xin nghỉ học — học viên hoặc phụ huynh gửi; có thể kèm đăng ký học bù ở lớp khác cùng khóa */
export interface LeaveRequest {
  id: string;
  studentId: string;
  classId: string;
  sessionId: string;
  reasonType: string;
  reason: string;
  by: 'student' | 'parent';
  byName: string;
  status: RequestStatus;
  createdAt: string;
  decidedBy?: string;
  decidedAt?: string;
  /** Buổi học bù đã đăng ký (lớp khác cùng trình độ) */
  makeupSessionId?: string;
  /** Kết quả điểm danh ở buổi học bù (giáo viên lớp đó ghi) */
  makeupResult?: 'present' | 'late' | 'excused' | 'absent';
  note?: string;
}

// ---------------- Flashcard ----------------

export interface FlashCard {
  id: string;
  /** Mặt trước: từ / Kanji */
  front: string;
  reading?: string;
  meaning: string;
  example?: string;
}

export interface FlashDeck {
  id: string;
  title: string;
  level: 'N5' | 'N4' | 'N3' | 'N2' | 'N1';
  kind: 'vocab' | 'kanji';
  /** Bài học trong lộ trình */
  lesson: string;
  cards: FlashCard[];
}

/** Hộp Leitner 0–5: nhớ → lên hộp (giãn lịch ôn), quên → về hộp 0 */
export interface CardProgress {
  box: number;
  due: string;
  reviews: number;
  lapses: number;
}
