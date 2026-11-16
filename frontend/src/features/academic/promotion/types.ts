import type { ConsultChannel } from '@/features/crm/types';

/**
 * LÊN LỚP — đưa học viên lớp sắp kết thúc (lớp A) sang lớp khai giảng kỳ sau (lớp B).
 *
 *  1. Lớp A sắp kết thúc → giáo vụ chọn / tạo lớp B (Chờ khai giảng)
 *  2. Hệ thống gợi ý (nhận xét cuối khóa của GV + chuyên cần + điểm thi) → giáo vụ GIỮ CHỖ TẠM vào B
 *     (KHÔNG phải ghi danh: không tính sĩ số chính thức, không có hóa đơn)
 *  3. Lớp A kết thúc → danh sách GIAO CHO SALE (Sale phụ trách lead của người quyết định)
 *  4. Sale tư vấn NGƯỜI QUYẾT ĐỊNH: học viên lớn tự quyết; học viên nhỏ → phụ huynh (1 phụ huynh có thể nhiều con)
 *  5. Đồng ý → ghi danh thật + hóa đơn · Từ chối · Chưa liên hệ được
 *  6. Hết hạn giữ chỗ (7 ngày trước khai giảng) mà chưa chốt → tự trả chỗ
 */

export type PromotionStatus = 'pending' | 'consulting' | 'unreachable' | 'confirmed' | 'declined';

export interface PromotionNote {
  id: string;
  at: string;
  by: string;
  text: string;
  channel?: ConsultChannel;
}

export interface PromotionCandidate {
  id: string;
  studentId: string;
  /** Lớp đang học (sắp kết thúc) */
  fromClassId: string;
  /** Lớp đề xuất lên (Chờ khai giảng) — có thể đổi khi tư vấn */
  toClassId?: string;
  /** Giữ chỗ tạm trong lớp B */
  seatHeld: boolean;
  /** Hạn giữ chỗ (YYYY-MM-DD) — quá hạn chưa chốt thì tự trả chỗ */
  holdUntil?: string;
  status: PromotionStatus;
  /** Lý do vào danh sách: GV đề xuất / số liệu học tập / giáo vụ thêm tay */
  source: 'teacher' | 'stats' | 'manual';
  /** Lúc giao cho Sale (khi lớp A kết thúc) */
  handedOverAt?: string;
  /** Sale phụ trách (Employee.id) */
  saleId?: string;
  /** Học viên / phụ huynh bấm "Quan tâm" trên cổng */
  interest?: { at: string; by: 'student' | 'parent' };
  declineReason?: string;
  /** Khi chốt: ghi danh đã tạo */
  enrollmentId?: string;
  confirmedAt?: string;
  confirmedBy?: string;
  notes: PromotionNote[];
  createdAt: string;
  createdBy: string;
}
