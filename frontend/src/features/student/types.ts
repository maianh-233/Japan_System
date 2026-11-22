/**
 * Mô hình dữ liệu Học viên
 *
 *  Student (hồ sơ — lưu ở crmStore vì được tạo từ Lead)
 *    ├─ Enrollment[]  ghi danh vào Lớp (ClassInfo) — kèm trạng thái học trong lớp đó
 *    │    └─ Invoice  hóa đơn học phí của lần ghi danh  └─ Payment[] các lần đóng
 *    ├─ PortalAccount  tài khoản Cổng học viên (0..1)
 *    └─ LeadStudentLink[] ──► Lead = người thân / người liên hệ (phụ huynh, anh chị...)
 *                              └─ PortalAccount  tài khoản Cổng phụ huynh (0..1)
 *
 *  Quy tắc ẨN: chỉ ẩn được khi KHÔNG còn ghi danh nào ở trạng thái "Đang học" hoặc "Bảo lưu".
 */

export type Gender = 'male' | 'female' | 'other';

export type { ClassInfo, ClassStatus } from '@/features/academic/classes/types';

/** Trạng thái của học viên TRONG một lớp */
export type EnrollmentStatus = 'studying' | 'reserved' | 'completed' | 'transferred' | 'dropped';

export interface Enrollment {
  id: string;
  studentId: string;
  classId: string;
  status: EnrollmentStatus;
  enrolledAt: string;
  endedAt?: string;
  note?: string;
  /** Bảo lưu đến ngày (dự kiến quay lại học) */
  reservedUntil?: string;
  /** Chuyển lớp: sang ghi danh nào / từ ghi danh nào */
  transferredTo?: string;
  transferredFrom?: string;
  /** Chứng nhận hoàn thành khóa đã cấp */
  certificate?: Certificate;
}

export interface Certificate {
  /** Mã tra cứu, vd CN-2026-N3K12-0007 */
  code: string;
  issuedAt: string;
  /** Email đã gửi (lần gần nhất) */
  sentTo: string;
  sentBy: string;
  /** Số lần gửi (gửi lại khi học viên báo chưa nhận) */
  sendCount: number;
  /** Cấp ngoại lệ dù chưa đủ điều kiện */
  exception?: string;
}

export type PaymentMethod = 'cash' | 'transfer' | 'vietqr' | 'card';

export interface Payment {
  id: string;
  amount: number;
  method: PaymentMethod;
  paidAt: string;
  collector: string;
}

export interface Invoice {
  id: string;
  code: string;
  enrollmentId: string;
  amount: number;
  discount: number;
  dueDate: string;
  issuedAt: string;
  payments: Payment[];
  /** Các lần hoàn tiền (khi học viên nghỉ / lớp hủy) */
  refunds: Refund[];
  /** Hủy phần học phí còn lại (học viên nghỉ / lớp hủy) → không còn công nợ */
  cancelled?: boolean;
  /** Hủy hóa đơn (lập sai, trùng) — qua duyệt 2 người; còn trong Thùng rác thì khôi phục được */
  voided?: { reason: string; requestedBy: string; approvedBy: string; at: string };
  /** Nội dung chuyển khoản VietQR riêng (server lập; dữ liệu giả tự ghép mã + tên) */
  transferContent?: string;
  /** Cổng HV / PH: tài khoản nhận của chi nhánh (server trả) — không có = chi nhánh chưa khai báo, đóng tại quầy */
  qr?: { bank: string; account: string; holder: string; content: string };
}

export interface Refund {
  id: string;
  amount: number;
  method: PaymentMethod;
  refundedAt: string;
  reason: string;
  by: string;
  /** Người duyệt (hoàn tiền phải qua người thứ 2) */
  approvedBy?: string;
}

export type TuitionStatus = 'paid' | 'partial' | 'unpaid' | 'overdue' | 'refunded' | 'cancelled';

export type AccountStatus = 'active' | 'inactive' | 'locked';

/** Tài khoản đăng nhập cổng (học viên hoặc phụ huynh) */
export interface PortalAccount {
  id: string;
  ownerType: 'student' | 'guardian';
  /** studentId hoặc leadId */
  ownerId: string;
  username: string;
  email?: string;
  status: AccountStatus;
  createdAt: string;
  lastLoginAt?: string;
}

/** Trạng thái học tổng hợp của học viên (suy ra từ các ghi danh) */
export type StudyStatus = 'studying' | 'reserved' | 'finished' | 'dropped' | 'none';
