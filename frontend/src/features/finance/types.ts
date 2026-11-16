/**
 * Mô hình dữ liệu Tài chính
 *
 *  Hóa đơn / thanh toán / hoàn tiền: dùng chung studentStore (Invoice sinh từ ghi danh).
 *  Module này bổ sung:
 *    - Bảng giá theo chi nhánh, ưu đãi / combo, chính sách đóng nhiều đợt
 *    - Giao dịch ngân hàng (VietQR) → đối soát với hóa đơn
 *    - Lịch sử nhắc học phí
 *    - Bảng lương GV / NV (bản chốt theo tháng) + điều chỉnh thưởng / phạt
 *    - Cấu hình hoa hồng & chỉ tiêu Sale
 */

export type PromoKind = 'percent' | 'amount' | 'combo';

export interface Promotion {
  id: string;
  code: string;
  name: string;
  kind: PromoKind;
  /** percent: %, amount / combo: số tiền giảm */
  value: number;
  /** Combo: các khóa phải đăng ký cùng lúc */
  courseIds: string[];
  branchIds: string[];
  from: string;
  to: string;
  /** Giới hạn lượt dùng (không có = không giới hạn) */
  limit?: number;
  used: number;
  active: boolean;
  note?: string;
}

export interface InstallmentPlan {
  id: string;
  name: string;
  /** Các đợt: % học phí, hạn = ngày khai giảng + offsetDays */
  parts: { percent: number; offsetDays: number }[];
  /** Áp dụng cho khóa có học phí từ */
  minFee: number;
  /** Phụ thu khi đóng nhiều đợt (%) */
  surcharge: number;
  active: boolean;
}

/** Giá riêng của khóa tại chi nhánh (không có = dùng giá niêm yết của khóa) */
export interface BranchPrice {
  courseId: string;
  branchId: string;
  fee: number;
}

export type TxnStatus = 'matched' | 'auto' | 'unmatched' | 'ignored';

export interface BankTxn {
  id: string;
  /** Mã giao dịch ngân hàng */
  ref: string;
  at: string;
  amount: number;
  /** Nội dung chuyển khoản */
  content: string;
  payer: string;
  account: string;
  branchId: string;
  status: TxnStatus;
  invoiceId?: string;
  matchedBy?: string;
  matchedAt?: string;
  note?: string;
}

export type ReminderChannel = 'zalo' | 'email' | 'sms' | 'call';

export interface Reminder {
  id: string;
  invoiceId: string;
  channel: ReminderChannel;
  at: string;
  by: string;
  note?: string;
}

/** Thưởng / phạt / phụ cấp thêm cho 1 người trong 1 tháng */
export interface PayAdjust {
  id: string;
  month: string;
  employeeId: string;
  amount: number;
  reason: string;
  by: string;
}

/** Bảng lương đã chốt (snapshot) */
export interface PayrollLock {
  kind: 'teacher' | 'staff';
  month: string;
  branchId: string;
  lockedBy: string;
  lockedAt: string;
  total: number;
  /** Đã chuyển khoản lương */
  paidAt?: string;
}

export interface CommissionTier {
  /** Doanh thu thực thu từ (đ) */
  from: number;
  percent: number;
}

export interface SaleTarget {
  employeeId: string;
  month: string;
  target: number;
}
