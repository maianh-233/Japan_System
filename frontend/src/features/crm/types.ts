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
}

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
}
