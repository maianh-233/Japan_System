/**
 * Mô hình dữ liệu CRM
 *
 *  Lead (người liên hệ — có thể là người học HOẶC phụ huynh)
 *    ├─ Consultation[]   lịch sử tư vấn
 *    └─ LeadStudentLink[] ──► Student (hồ sơ học viên — bản ghi RIÊNG)
 *
 *  Lead ↔ Student là NHIỀU–NHIỀU qua LeadStudentLink (kèm quan hệ):
 *   - 1 phụ huynh (lead) có nhiều con học      → 1 lead : n học viên
 *   - bố và mẹ cùng liên hệ cho 1 con          → n lead : 1 học viên
 *   - người học tự đăng ký                      → quan hệ "self"
 *  Ẩn lead chỉ bật cờ `hidden` trên lead — học viên liên kết KHÔNG bị ảnh hưởng.
 */

import type { Gender } from '@/features/student/types';

export type LeadStatus ='new' | 'consulting' | 'test_scheduled' | 'won' | 'lost';

export type LeadSource = 'facebook' | 'website' | 'zalo' | 'tiktok' | 'referral' | 'walk_in' | 'hotline' | 'event';

export interface Lead {
  id: string;
  code: string;
  fullName: string;
  phone: string;
  email?: string;
  source: LeadSource;
  status: LeadStatus;
  branchId: string;
  /** Sale phụ trách */
  assigneeId?: string;
  /** Khóa học quan tâm */
  interest?: string;
  note?: string;
  /** Lý do mất (khi status = lost) */
  lostReason?: string;
  nextFollowUpAt?: string;
  lastContactAt?: string;
  createdAt: string;
  createdBy: string;
  hidden: boolean;
  hiddenAt?: string;
  /** Kênh nhận thông báo (khi đóng vai phụ huynh / người giám hộ) */
  notifyChannels?: NotifyChannel[];
  /** Chiến dịch marketing mang lead về */
  campaignId?: string;
  /** Đã gửi cảnh báo quá SLA (mỗi lead báo 1 lần) */
  slaEscalatedAt?: string;
  /** Đã tự chuyển Sale do quá 2×SLA (mỗi lead chuyển tự động 1 lần) */
  slaReassignedAt?: string;
}

/** Kênh gửi thông báo cho phụ huynh: điểm danh, nhắc học phí, kết quả học tập */
export type NotifyChannel = 'zalo' | 'sms' | 'email';

export type ConsultChannel = 'call' | 'zalo' | 'meeting' | 'message' | 'email';
export type ConsultResult = 'interested' | 'thinking' | 'test_booked' | 'no_answer' | 'not_interested' | 'enrolled';

export interface Consultation {
  id: string;
  leadId: string;
  channel: ConsultChannel;
  result: ConsultResult;
  content: string;
  staffName: string;
  createdAt: string;
  nextFollowUpAt?: string;
  /** Ghi chú hệ thống (tự sinh, vd: "Tạo hồ sơ học viên") */
  system?: boolean;
}

export type Relationship = 'self' | 'child' | 'sibling' | 'spouse' | 'other';

/**
 * Hồ sơ học viên. Lớp đang học, trạng thái học, học phí, tài khoản → features/student (suy ra từ ghi danh).
 */
export interface Student {
  id: string;
  code: string;
  fullName: string;
  gender?: Gender;
  dob?: string;
  phone?: string;
  email?: string;
  address?: string;
  note?: string;
  branchId: string;
  joinedAt: string;
  /** Chỉ ẩn được khi không còn học lớp nào */
  hidden: boolean;
  hiddenAt?: string;
}

export interface LeadStudentLink {
  leadId: string;
  studentId: string;
  relationship: Relationship;
  createdAt: string;
  /** Người liên hệ CHÍNH của học viên (mỗi học viên tối đa 1) — giáo vụ gọi người này trước */
  primary?: boolean;
  /** Nhận hóa đơn & nhắc học phí của học viên này */
  billing?: boolean;
  /** Nhận thông báo điểm danh / kết quả học tập */
  notify?: boolean;
}

// ======================================================================
//  CHIA LEAD & SLA · TEST XẾP LỚP · CHIẾN DỊCH
// ======================================================================

/**
 * Quy tắc chia lead. Lead mới CHƯA có Sale → duyệt các quy tắc đang bật theo thứ tự ưu tiên,
 * quy tắc ĐẦU TIÊN khớp điều kiện sẽ chọn Sale. Không quy tắc nào khớp → để trống, chờ quản lý giao tay.
 *  - round_robin: xoay vòng lần lượt trong danh sách Sale
 *  - least_load:  Sale đang giữ ít lead "mở" (mới / đang tư vấn / hẹn test) nhất
 *  - fixed:       luôn giao cho Sale đầu tiên còn đủ điều kiện (vd lead Du học → chuyên viên du học)
 */
export type RouteMethod = 'round_robin' | 'least_load' | 'fixed';

export interface LeadRule {
  id: string;
  name: string;
  active: boolean;
  /** Điều kiện — bỏ trống = mọi giá trị */
  sources: LeadSource[];
  branchIds: string[];
  interests: string[];
  method: RouteMethod;
  saleIds: string[];
  /** Chỉ chia cho Sale đang trong ca làm việc (theo Xếp ca); hết người trong ca → chia cho cả nhóm */
  onlyOnShift: boolean;
  /** Thời hạn liên hệ lần đầu (phút) */
  slaMinutes: number;
  /** Quá SLA → báo cho ai (tên người / vai trò) */
  escalateTo: string[];
  /** Quá SLA × 2 → tự chuyển lead sang Sale khác trong nhóm */
  reassignOnBreach: boolean;
  /** Con trỏ xoay vòng (vị trí Sale được giao gần nhất) */
  cursor: number;
}

export interface AssignmentLog {
  id: string;
  leadId: string;
  saleId?: string;
  ruleId?: string;
  at: string;
  /** Giải thích vì sao chọn Sale này */
  reason: string;
}

// ---------------- Test xếp lớp ----------------

/** Phần của bài test: bốc N câu ĐÃ DUYỆT theo trình độ + kỹ năng từ Ngân hàng câu hỏi */
export interface PlacementSection {
  level: 'N5' | 'N4' | 'N3' | 'N2' | 'N1';
  skill: 'vocab' | 'kanji' | 'grammar' | 'reading' | 'listening';
  count: number;
}

/** Điểm (%) từ mức này trở lên → gợi ý trình độ / khóa */
export interface PlacementBand {
  minPct: number;
  level: string;
  course: string;
}

export type PlacementTestStatus = 'draft' | 'active' | 'archived';

export interface PlacementTest {
  id: string;
  code: string;
  name: string;
  description?: string;
  sections: PlacementSection[];
  /** Câu đã bốc (cố định cho bài test — đổi đề bằng "Bốc lại câu") */
  questionIds: string[];
  durationMin: number;
  bands: PlacementBand[];
  status: PlacementTestStatus;
  author: string;
  createdAt: string;
}

export type AttemptStatus = 'sent' | 'in_progress' | 'done' | 'expired';

export interface PlacementAttempt {
  id: string;
  testId: string;
  leadId: string;
  token: string;
  channel: 'zalo' | 'sms' | 'email';
  sentBy: string;
  sentAt: string;
  expiresAt: string;
  status: AttemptStatus;
  startedAt?: string;
  submittedAt?: string;
  /** Đáp án đã chọn theo questionId (-1 = bỏ trống) */
  answers?: Record<string, number>;
  correct?: number;
  total?: number;
  bySkill?: Partial<Record<PlacementSection['skill'], { correct: number; total: number }>>;
  suggestedLevel?: string;
  suggestedCourse?: string;
  /** Đã ghi kết quả vào lịch sử tư vấn của lead */
  logged?: boolean;
}

// ---------------- Chiến dịch ----------------

export type CampaignStatus = 'planned' | 'running' | 'paused' | 'ended';

export interface Campaign {
  id: string;
  code: string;
  name: string;
  /** Kênh chạy — trùng với nguồn lead */
  channel: LeadSource;
  branchIds: string[];
  startDate: string;
  endDate: string;
  budget: number;
  spent: number;
  /** Chỉ tiêu số lead */
  targetLeads?: number;
  status: CampaignStatus;
  owner: string;
  /** Mã UTM gắn vào link quảng cáo → lead từ form tự gắn chiến dịch */
  utm?: string;
  courses: string[];
  note?: string;
  createdAt: string;
}
