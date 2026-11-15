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
}

export interface Refund {
  id: string;
  amount: number;
  method: PaymentMethod;
  refundedAt: string;
  reason: string;
  by: string;
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
